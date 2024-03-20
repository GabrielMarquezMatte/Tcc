source("./src/libs/garch_models.r")
library(tidyverse)
MARKET_QUERY = '
WITH "HistData" AS
    (SELECT "Date",
            "TickerId",
            EXP(LN("Adjusted") - LAG(LN("Adjusted"), 1, LN("Adjusted")) OVER(PARTITION BY "TickerId"
                                                                             ORDER BY "Date"))-1 AS "Return",
            LN("Volume" * "Adjusted") AS "Volume"
     FROM "HistoricalDataYahoo"
     WHERE "Date" > $1
     AND "Adjusted" > 0)
SELECT "HistData"."Date", SUM("HistData"."Return" * "HistData"."Volume")/SUM("HistData"."Volume") AS "Return"
FROM "HistData"
WHERE ABS("HistData"."Return") < 0.5
GROUP BY "HistData"."Date"
'

ALL_SECTORS_QUERY = '
WITH "HistData" AS
    (SELECT "Date",
            "TickerId",
            EXP(LN("Adjusted") - LAG(LN("Adjusted"), 1, LN("Adjusted")) OVER(PARTITION BY "TickerId"
                                                                             ORDER BY "Date"))-1 AS "Return",
            LN("Volume" * "Adjusted") AS "Volume"
     FROM "HistoricalDataYahoo"
     WHERE "Date" > $1
     AND "Adjusted" > 0)
SELECT "HistData"."Date", "Industries"."SectorId", SUM("HistData"."Return" * "HistData"."Volume")/SUM("HistData"."Volume") AS "Return"
FROM "HistData"
INNER JOIN "Tickers" ON "HistData"."TickerId" = "Tickers"."Id"
INNER JOIN "Companies" ON "Companies"."Id" = "Tickers"."CompanyId"
INNER JOIN "CompanyIndustries" ON "CompanyIndustries"."CompanyId" = "Companies"."Id"
INNER JOIN "Industries" ON "Industries"."Id" = "CompanyIndustries"."IndustryId"
WHERE ABS("HistData"."Return") < 0.5
GROUP BY "HistData"."Date", "Industries"."SectorId"
'

CreateConnection <- function() {
    return(odbc::dbConnect(RPostgres::Postgres(),
        dbname = "stock", host = "localhost",
        port = 5432, user = "postgres", password = "postgres"
    ))
}
connection <- CreateConnection()
market <- DBI::dbGetQuery(connection, MARKET_QUERY, list(as.Date("2019-01-01")))
all_sectors_data <- DBI::dbGetQuery(connection, ALL_SECTORS_QUERY, list(as.Date("2019-01-01")))
sectors_data <- all_sectors_data %>% group_by(SectorId) %>% nest() %>% mutate(
    model = map(data, ~FindBestArchModel(.x$Return, type_models = c("eGARCH", "csGARCH", "iGARCH", "gjrGARCH"),
    dist_to_use = c("std", "sstd", "nig", "ged", "sged"), simplified = TRUE,
    max_AR = 1, max_MA = 1, max_ARCH = 2, max_GARCH = 2, min_ARCH = 1, min_GARCH = 1, min_AR = 0, min_MA = 0)$fit_bic),
)
market_model <- FindBestArchModel(market$Return, type_models = c("eGARCH", "csGARCH", "iGARCH", "gjrGARCH"),
    dist_to_use = c("std", "sstd", "nig", "ged", "sged"), simplified = TRUE,
    max_AR = 1, max_MA = 1, max_ARCH = 2, max_GARCH = 2, min_ARCH = 1, min_GARCH = 1, min_AR = 0, min_MA = 0)$fit_bic
sectors_data_columns <- sectors_data %>% select(SectorId, data) %>% unnest(cols = data) %>% pivot_wider(names_from = SectorId, values_from = Return)
sectors_xts <- xts::as.xts(sectors_data_columns)
garch_specs <- rugarch::multispec(lapply(sectors_data$model, rugarch::getspec))
garch_fits <- rugarch::multifit(garch_specs, sectors_xts)
dcc_model <- FindBestDccModel()
DBI::dbDisconnect(connection)
