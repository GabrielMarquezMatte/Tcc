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
DISTRIBUTIONS <- c("std", "sstd", "ged", "snorm", "sged")

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

GetSingleRate <- function(URL, year) {
    data <- read.csv(URL)
    print(sprintf("Finished getting rates for %d", year))
    return(data)
}

GetRatesUSAYear <- function(year) {
    URL <- sprintf("https://home.treasury.gov/resource-center/data-chart-center/interest-rates/daily-treasury-rates.csv/%d/all?type=daily_treasury_yield_curve&field_tdr_date_value=%d&page&_format=csv", year, year)
    print(sprintf("Getting rates for %d", year))
    return(future::future(GetSingleRate(URL, year)))
}

ExecutePromise <- function(promise) {
    `%>%` <- magrittr::`%>%`
    return(promise %>%
        dplyr::mutate(Date = as.Date(Date, format = "%m/%d/%Y")) %>%
        dplyr::select(Date, UsaRate = X1.Yr) %>%
        dplyr::mutate(UsaRate = UsaRate / 100))
}

GetRatesUSA <- function(start) {
    `%>%` <- magrittr::`%>%`
    dates <- lubridate::year(seq(start, Sys.Date(), by = "year"))
    list_promises <- lapply(dates, GetRatesUSAYear)
    values <- lapply(list_promises, future::value)
    promises <- lapply(values, ExecutePromise)
    return(do.call(rbind, promises) %>% dplyr::filter(Date >= start) %>% dplyr::arrange(Date) %>% na.omit)
}

GetCPI <- function(start) {
    `%>%` <- magrittr::`%>%`
    URL <- "https://fred.stlouisfed.org/graph/fredgraph.csv?bgcolor=%23e1e9f0&chart_type=line&drp=0&fo=open%20sans&graph_bgcolor=%23ffffff&height=450&mode=fred&recession_bars=on&txtcolor=%23444444&ts=12&tts=12&width=1318&nt=0&thu=0&trc=0&show_legend=yes&show_axis_titles=yes&show_tooltip=yes&id=CPIAUCSL&scale=left&cosd=1947-01-01&coed=2024-03-01&line_color=%234572a7&link_values=false&line_style=solid&mark_type=none&mw=3&lw=2&ost=-99999&oet=99999&mma=0&fml=a&fq=Monthly&fam=avg&fgst=lin&fgsnd=2020-02-01&line_index=1&transformation=lin&vintage_date=2024-05-08&revision_date=2024-05-08&nd=1947-01-01"
    data <- read.csv(URL) %>%
        dplyr::mutate(Date = as.Date(DATE, format = "%Y-%m-%d")) %>%
        dplyr::filter(Date >= start) %>%
        dplyr::mutate(CPI = c(0, diff(log(CPIAUCSL)))) %>%
        dplyr::mutate(CPI = exp(cumsum(CPI))) %>%
        dplyr::select(Date, CPI)
    return(data)
}

GetIpca <- function(start) {
    `%>%` <- magrittr::`%>%`
    data <- GetBCBData::gbcbd_get_series(433, first.date = start, do.parallel = FALSE, use.memoise = FALSE) %>%
        dplyr::select(Date = ref.date, IPCA = value) %>%
        dplyr::mutate(IPCA = cumprod(1 + IPCA / 100))
    return(data)
}

GetPtax <- function(date) {
    `%>%` <- magrittr::`%>%`
    return(GetBCBData::gbcbd_get_series(1, first.date = date, do.parallel = FALSE, use.memoise = FALSE) %>%
        dplyr::select(Date = ref.date, Ptax = value))
}

GetRealExchangeRate <- function(start) {
    `%>%` <- magrittr::`%>%`
    cpi <- GetCPI(start)
    ipca <- GetIpca(start)
    ptax <- GetPtax(start)
    data <- dplyr::left_join(ptax, ipca, by = "Date") %>%
        dplyr::left_join(cpi, by = "Date") %>%
        dplyr::mutate(
            IPCA = c(ifelse(is.na(IPCA[1]), 1, IPCA[1]), IPCA[-1]),
            CPI = c(ifelse(is.na(CPI[1]), 1, CPI[1]), CPI[-1])
        ) %>%
        dplyr::mutate(IPCA = zoo::na.locf(IPCA), CPI = zoo::na.locf(CPI)) %>%
        dplyr::mutate(RealExchangeRate = Ptax * CPI / IPCA) %>%
        dplyr::mutate(RealExchangeRate = c(0, diff(log(RealExchangeRate))))
    return(data)
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
        dist_to_use = DCC_DISTRIBUTION, fit = multifit,
    )
    dcc_fit <- rmgarch::dccfit(dcc_model$dccspec_b, data = only_returns)
    sector_volatility <- garch_model$fit_bic@fit$sigma
    market_volatility <- joined$MarketVolatility
    covariance <- rmgarch::rcov(dcc_fit)
    betas <- covariance[1, 2, ] / covariance[2, 2, ]
    adjusted_variance <- sector_volatility^2 - (betas * market_volatility)^2
    final_df <- joined %>%
        dplyr::select(Date, SectorReturn, MarketReturn) %>%
        dplyr::mutate(
            SectorVolatility = zoo::na.locf(sqrt(adjusted_variance)),
            SectorVariance = adjusted_variance,
            NonAdjusted = sector_volatility,
            MarketVolatility = joined$MarketVolatility,
            MarketImpact = sqrt(betas^2 * market_volatility^2)
        )
    result <- list(data = final_df, garch_model = garch_model$fit_bic, dcc_model = dcc_fit)
    return(result)
}

TestRatesForStationarity <- function(rates_data) {
    adf_test <- tseries::adf.test(rates_data$Rate)
    kpss_test <- tseries::kpss.test(rates_data$Rate)
    return(list(adf_test = adf_test, kpss_test = kpss_test))
}

ExecuteVARForSector <- function(sector_volatility, rates_data, real_exchange, usa_rates) {
    `%>%` <- magrittr::`%>%`
    joined <- dplyr::inner_join(sector_volatility, rates_data, by = "Date") %>%
        dplyr::inner_join(real_exchange, by = "Date") %>%
        dplyr::inner_join(usa_rates, by = "Date") %>%
        dplyr::select(SectorVariance, Rate, RealExchangeRate, UsaRate)
    optimal_lag <- vars::VARselect(joined, lag.max = 3, type = "const")$selection["AIC(n)"]
    model <- vars::VAR(joined, p = optimal_lag, type = "const")
    return(list(model = model, optimal_lag = optimal_lag))
}

ExecuteForSector <- function(start_date, sector_id, rates, real_exchange, market_values, usa_rates) {
    all_values <- GetSectorValues(start_date, sector_id)
    sector_volatility <- CalculateVolatilityForSector(all_values, market_values)
    var_model <- ExecuteVARForSector(sector_volatility$data, rates, real_exchange, usa_rates)
    message(paste("Modelo calculado para o setor", sector_id))
    value <- list(sector = sector_id, var_model = var_model, data = sector_volatility, market_values = market_values, rates = rates, real_exchange = real_exchange)
    saveRDS(value, file = paste0("models/sector_", sector_id, ".rds"))
    return(value)
}

ExecuteForMarket <- function(market_values, rates, real_exchange, usa_rates) {
    `%>%` <- magrittr::`%>%`
    var_model <- ExecuteVARForSector(market_values$data %>% dplyr::select(Date, SectorVariance = MarketVolatility), rates, real_exchange, usa_rates)
    message("Modelo calculado para o mercado")
    value <- list(sector = "market", var_model = var_model, data = market_values, market_values = market_values, rates = rates, real_exchange = real_exchange)
    saveRDS(value, file = "models/market.rds")
    return(value)
}

SingleExecution <- function(market_values, rates, real_exchange, usa_rates) {
    start <- Sys.time()
    ran <- FALSE
    try(
        {
            ExecuteForMarket(market_values, rates, real_exchange, usa_rates)
            ran <- TRUE
        },
        silent = F
    )
    end <- Sys.time()
    if (ran) {
        message(paste("Executed for market in", end - start))
    } else {
        message("Error for market")
    }
}

SingleExecutionSector <- function(start_date, sector, rates, real_exchange, market_values, usa_rates) {
    start <- Sys.time()
    ran <- FALSE
    try(
        {
            ExecuteForSector(start_date, sector, rates, real_exchange, market_values, usa_rates)
            ran <- TRUE
        },
        silent = F
    )
    end <- Sys.time()
    if (ran) {
        message(paste("Executed for sector", sector, "in", end - start))
    } else {
        message(paste("Error for sector", sector))
    }
}

RemoveMarketImpact <- function(connection) {
    start_date <- as.Date("2009-01-01")
    rates <- GetRates(connection, start_date)
    test_results <- TestRatesForStationarity(rates)
    if (test_results$adf_test$p.value < 0.05 || test_results$kpss_test$p.value > 0.05) {
        stop("Rates are not stationary")
    }
    real_exchange <- GetRealExchangeRate(start_date)
    usa_rates <- GetRatesUSA(start_date)
    saveRDS(rates, file = "models/rates.rds")
    saveRDS(real_exchange, file = "models/real_exchange.rds")
    saveRDS(usa_rates, file = "models/usa_rates.rds")
    market_values <- GetMarketValues(connection, start_date)
    sectors <- GetSectors(connection)
    lista <- list()
    index <- 1
    lista[[index]] <- future::future(SingleExecution(market_values, rates, real_exchange, usa_rates))
    index <- index + 1
    for (sector in sectors$Id) {
        lista[[index]] <- future::future(SingleExecutionSector(start_date, sector, rates, real_exchange, market_values, usa_rates))
        index <- index + 1
    }
    return(lista)
}

RemoveMarketImpactSync <- function(connection) {
    start_date <- as.Date("2009-01-01")
    rates <- GetRates(connection, start_date)
    test_results <- TestRatesForStationarity(rates)
    if (test_results$adf_test$p.value < 0.05 || test_results$kpss_test$p.value > 0.05) {
        stop("Rates are not stationary")
    }
    real_exchange <- GetRealExchangeRate(start_date)
    usa_rates <- GetRatesUSA(start_date)
    saveRDS(rates, file = "models/rates.rds")
    saveRDS(real_exchange, file = "models/real_exchange.rds")
    saveRDS(usa_rates, file = "models/usa_rates.rds")
    market_values <- GetMarketValues(connection, start_date)
    SingleExecution(market_values, rates, real_exchange, usa_rates)
    sectors <- GetSectors(connection)
    for (sector in sectors$Id) {
        SingleExecutionSector(start_date, sector, rates, real_exchange, market_values, usa_rates)
    }
}

future::plan(future::multisession, workers = 3)
if (!dir.exists("models")) {
    dir.create("models")
}
connection <- CreateConnection()
# RemoveMarketImpactSync(connection)
futures <- RemoveMarketImpact(connection)
for (future_value in futures) {
    value <- future::value(future_value)
}
odbc::dbDisconnect(connection)
