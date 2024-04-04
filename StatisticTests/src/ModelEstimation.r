source("./src/libs/garch_models.r")
QUERY_MARKET <- '
SELECT "Date",
        EXP(LN("Adjusted") - LAG(LN("Adjusted"), 1, LN("Adjusted")) OVER(PARTITION BY "TickerId"
                                                                            ORDER BY "Date"))-1 AS "Return",
        LN("Volume" * "Adjusted") AS "Volume"
FROM "HistoricalDataYahoo"
WHERE "Date" > $1
AND "Adjusted" > 0
'

ALL_SECTORS_QUERY <- '
SELECT
    "HistoricalDataYahoo"."Date"
    ,"Industries"."SectorId"
    ,EXP(LN("HistoricalDataYahoo"."Adjusted") - LAG(LN("HistoricalDataYahoo"."Adjusted"), 1, LN("HistoricalDataYahoo"."Adjusted")) OVER(PARTITION BY "HistoricalDataYahoo"."TickerId" ORDER BY "HistoricalDataYahoo"."Date"))-1 AS "Return"
    ,LN("HistoricalDataYahoo"."Volume") AS "Volume"
FROM "HistoricalDataYahoo"
INNER JOIN "Tickers" ON "HistoricalDataYahoo"."TickerId" = "Tickers"."Id"
INNER JOIN "Companies" ON "Companies"."Id" = "Tickers"."CompanyId"
INNER JOIN "CompanyIndustries" ON "CompanyIndustries"."CompanyId" = "Companies"."Id"
INNER JOIN "Industries" ON "Industries"."Id" = "CompanyIndustries"."IndustryId"
WHERE "Date" > $1
AND "Adjusted" > 0
AND "SectorId" = $2
'

RATES_QUERY <- '
SELECT "Date", "Rate"
FROM "NelsonSiegel"
WHERE "Date" > $1
ORDER BY "Date"
'

VOLATILITY_MODELS <- c("eGARCH", "iGARCH", "gjrGARCH")
DISTRIBUTIONS <- c("std", "sstd")

DCC_MODEL <- c("DCC", "aDCC")
DCC_DISTRIBUTION <- c("mvnorm", "mvt", "mvlaplace")

CreateConnection <- function() {
    return(odbc::dbConnect(RPostgres::Postgres(),
        dbname = "stock", host = "localhost",
        port = 5432, user = "postgres", password = "postgres"
    ))
}

GetMarketValues <- function(conn, date) {
    `%>%` <- magrittr::`%>%`
    data <- odbc::dbGetQuery(conn, QUERY_MARKET, params = list(date)) %>%
        dplyr::group_by(Date) %>%
        dplyr::summarize(MarketReturn = log(1 + sum(Return * Volume) / sum(Volume)))
    garch_model <- FindBestArchModel(data$MarketReturn, type_models = VOLATILITY_MODELS, dist_to_use = DISTRIBUTIONS)
    data <- data %>%
        dplyr::mutate(MarketVolatility = garch_model$fit_bic@fit$sigma)
    result <- list(data = data, garch_model = garch_model$fit_bic, garch_spec = garch_model$ugspec_b)
    return(result)
}

GetSectorValues <- function(date, sector) {
    conn <- CreateConnection()
    val <- NULL
    try(val <- odbc::dbGetQuery(conn, ALL_SECTORS_QUERY, params = list(date, sector)), silent = TRUE)
    odbc::dbDisconnect(conn)
    if (is.null(val)) {
        stop("Error querying data")
    }
    return(val)
}

GetSectors <- function(conn) {
    return(odbc::dbGetQuery(conn, 'SELECT * FROM "Sector"'))
}

GetRates <- function(conn, date) {
    return(odbc::dbGetQuery(conn, RATES_QUERY, params = list(date)))
}

GetPtax <- function(date) {
    `%>%` <- magrittr::`%>%`
    return(GetBCBData::gbcbd_get_series(1, first.date = date, do.parallel = FALSE, use.memoise = FALSE) %>%
        dplyr::select(Date = ref.date, Ptax = value) %>%
        dplyr::mutate(Ptax = c(0, diff(log(Ptax)))))
}

CalculateVolatilityForSector <- function(all_values, market_result) {
    `%>%` <- magrittr::`%>%`
    sector_df <- all_values %>%
        dplyr::filter(abs(Return) < 0.5) %>%
        dplyr::group_by(Date) %>%
        dplyr::summarize(SectorReturn = log(1 + sum(Return * Volume) / sum(Volume))) %>%
        dplyr::filter(!is.na(SectorReturn))
    joined <- dplyr::inner_join(sector_df, market_result$data, by = "Date")
    garch_model <- FindBestArchModel(joined$SectorReturn, type_models = VOLATILITY_MODELS, dist_to_use = DISTRIBUTIONS)
    multispec <- rugarch::multispec(c(garch_model$ugspec_b, market_result$garch_spec))
    only_returns <- joined %>%
        dplyr::select(SectorReturn, MarketReturn) %>%
        as.matrix()
    multifit <- rugarch::multifit(multispec = multispec, data = only_returns)
    dcc_model <- FindBestDccModel(only_returns,
        uspec = multispec,
        type_models = DCC_MODEL,
        dist_to_use = DCC_DISTRIBUTION, fit = multifit
    )
    sector_volatility <- garch_model$fit_bic@fit$sigma
    market_volatility <- joined$MarketVolatility
    covariance <- rmgarch::rcov(dcc_model$fit_bic)
    betas <- covariance[1, 2, ] / covariance[2, 2, ]
    adjusted_volatility <- sqrt(sector_volatility^2 - (betas * market_volatility)^2)
    final_df <- joined %>%
        dplyr::select(Date, SectorReturn, MarketReturn) %>%
        dplyr::mutate(
            SectorVolatility = adjusted_volatility,
            SectorVariance = adjusted_volatility^2,
            NonAdjusted = sector_volatility,
            MarketVolatility = joined$MarketVolatility,
            MarketImpact = sqrt(betas^2 * market_volatility^2)
        )
    result <- list(data = final_df, garch_model = garch_model$fit_bic, dcc_model = dcc_model$fit_bic)
    return(result)
}

TestRatesForStationarity <- function(rates_data) {
    adf_test <- tseries::adf.test(rates_data$Rate)
    kpss_test <- tseries::kpss.test(rates_data$Rate)
    return(list(adf_test = adf_test, kpss_test = kpss_test))
}

ExecuteVARForSector <- function(sector_volatility, rates_data, ptax) {
    `%>%` <- magrittr::`%>%`
    joined <- dplyr::inner_join(sector_volatility, rates_data, by = "Date") %>%
        dplyr::inner_join(ptax, by = "Date") %>%
        dplyr::select(SectorVariance, Rate, Ptax)
    var_length <- vars::VARselect(joined, lag.max = 3, type = "const")$selection[1]
    return(vars::VAR(joined, p = var_length, type = "const"))
}

ExecuteForSector <- function(start_date, sector_id, rates, ptax, market_values) {
    all_values <- GetSectorValues(start_date, sector_id)
    sector_volatility <- CalculateVolatilityForSector(all_values, market_values)
    var_model <- ExecuteVARForSector(sector_volatility$data, rates, ptax)
    message(paste("Modelo calculado para o setor", sector_id))
    value <- list(sector = sector_id, var_model = var_model, data = sector_volatility, market_values = market_values)
    saveRDS(value, file = paste0("models/sector_", sector_id, ".rds"))
    return(value)
}

RemoveMarketImpact <- function(connection) {
    start_date <- as.Date("2010-01-01")
    rates <- GetRates(connection, start_date)
    test_results <- TestRatesForStationarity(rates)
    if (test_results$adf_test$p.value < 0.05 || test_results$kpss_test$p.value > 0.05) {
        stop("Rates are not stationary")
    }
    ptax <- GetPtax(start_date)
    market_values <- GetMarketValues(connection, start_date)
    sectors <- GetSectors(connection)
    lista <- list()
    index <- 1
    for (sector in sectors$Id) {
        lista[[index]] <- future::future({
            ExecuteForSector(start_date, sector, rates, ptax, market_values)
        })
        index <- index + 1
    }
    return(lista)
}

future::plan(future::multisession, workers = 3)
if (!dir.exists("models")) {
    dir.create("models")
}
connection <- CreateConnection()
futures <- RemoveMarketImpact(connection)
for (future_value in futures) {
    value <- future::value(future_value)
}
odbc::dbDisconnect(connection)
