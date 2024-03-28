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

GetSectorValues <- function(conn, date, sector) {
    return(odbc::dbGetQuery(conn, ALL_SECTORS_QUERY, params = list(date, sector)))
}

GetSectors <- function(conn) {
    return(odbc::dbGetQuery(conn, 'SELECT * FROM "Sector" WHERE "Id" > 4'))
}

CalculateVolatilityForSector <- function(all_values, market_result) {
    `%>%` <- magrittr::`%>%`
    sector_df <- all_values %>%
        dplyr::filter(abs(Return) < 0.5) %>%
        dplyr::group_by(Date) %>%
        dplyr::summarize(SectorReturn = log(1 + sum(Return * Volume) / sum(Volume)))
    joined <- dplyr::inner_join(sector_df, market_result$data, by = "Date")
    garch_model <- FindBestArchModel(joined$SectorReturn, type_models = VOLATILITY_MODELS, dist_to_use = DISTRIBUTIONS)
    multispec <- rugarch::multispec(c(garch_model$ugspec_b, market_result$garch_spec))
    only_returns <- joined %>% dplyr::select(SectorReturn, MarketReturn) %>% as.matrix()
    multifit <- rugarch::multifit(multispec = multispec, data = only_returns)
    dcc_model <- FindBestDccModel(only_returns, uspec = multispec,
                                  type_models = DCC_MODEL,
                                  dist_to_use = DCC_DISTRIBUTION, fit = multifit)
    sector_volatility <- garch_model$fit_bic@fit$sigma
    market_volatility <- joined$MarketVolatility
    covariance <- rmgarch::rcov(dcc_model$fit_bic)
    betas <- covariance[1, 2, ] / covariance[2, 2, ]
    adjusted_volatility <- sqrt(sector_volatility^2 - (betas * market_volatility)^2)
    only_returns <- joined %>%
      dplyr::select(Date, SectorReturn, MarketReturn) %>%
      dplyr::mutate(SectorVolatility = adjusted_volatility,
                    NonAdjusted = sector_volatility,
                    MarketVolatility = joined$MarketVolatility,
                    MarketImpact = sqrt(betas^2*market_volatility^2))
    return(only_returns)
}

RemoveMarketImpact <- coro::generator(function(connection) {
    start_date <- as.Date("2010-01-01")
    market_values <- GetMarketValues(connection, start_date)
    sectors <- GetSectors(connection)
    index <- 0
    for(sector in sectors$Id) {
        all_values <- GetSectorValues(connection, start_date, sector)
        coro::yield(CalculateVolatilityForSector(all_values, market_values))
    }
})

connection <- CreateConnection()
iter <- RemoveMarketImpact(connection)
?coro::async
odbc::dbDisconnect(connection)
