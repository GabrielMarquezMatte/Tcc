import polars as pl
import datetime as dt
import numpy as np
from numba import jit
from libs.garch_fit import FindBestGarch, vol_models, mean_types, distribution_types
from libs.dcc_fit import FitDcc
from concurrent.futures import ProcessPoolExecutor
from arch.univariate.base import ARCHModelResult

QUERY_MARKET = '''
SELECT "Date",
        EXP(LN("Adjusted") - LAG(LN("Adjusted"), 1, LN("Adjusted")) OVER(PARTITION BY "TickerId"
                                                                            ORDER BY "Date"))-1 AS "Return",
        LN("Volume" * "Adjusted") AS "Volume"
FROM "HistoricalDataYahoo"
WHERE "Date" > $1
AND "Adjusted" > 0
'''

ALL_SECTORS_QUERY = '''
SELECT "HistoricalDataYahoo"."Date", "Industries"."SectorId", "HistoricalDataYahoo"."Adjusted", LN("HistoricalDataYahoo"."Volume") AS "Volume"
FROM "HistoricalDataYahoo"
INNER JOIN "Tickers" ON "HistoricalDataYahoo"."TickerId" = "Tickers"."Id"
INNER JOIN "Companies" ON "Companies"."Id" = "Tickers"."CompanyId"
INNER JOIN "CompanyIndustries" ON "CompanyIndustries"."CompanyId" = "Companies"."Id"
INNER JOIN "Industries" ON "Industries"."Id" = "CompanyIndustries"."IndustryId"
WHERE "Date" > $1
AND "Adjusted" > 0
'''

CONNECTION_STRING = 'postgresql://postgres:postgres@localhost:5432/stock'

VOL_MODELS: list[vol_models] = ['EGARCH', 'HARCH']
MEAN_MODELS: list[mean_types] = ['ARX', 'HARX']
DISTRIBUTIONS: list[distribution_types] = ['studentst', 'skewstudent', 'skewt', 't']

def GetMarketValues(executor: ProcessPoolExecutor,start_date: dt.date):
    df = pl.read_database_uri(QUERY_MARKET.replace("$1", f"'{start_date:%Y-%m-%d}'"), CONNECTION_STRING, engine="adbc")
    df = df.with_columns((pl.col('Return') * pl.col('Volume')).alias('MarketReturn'))
    df = df.group_by('Date').agg((pl.col('MarketReturn') / pl.col('Volume').sum()).sum().alias('MarketReturn'))
    _, _, _, result = FindBestGarch(executor, (df.get_column('MarketReturn') * 100).to_numpy(), verbose=True,
                                max_p=2, max_q=2, max_o=2,
                                volatility_models=VOL_MODELS,mean_models=MEAN_MODELS,
                                distributions=DISTRIBUTIONS)
    df = df.with_columns(pl.lit(result.conditional_volatility / 100).alias('MarketVolatility'))
    return df, result

def GetSectorValues(start_date: dt.date) -> pl.DataFrame:
    return pl.read_database_uri(ALL_SECTORS_QUERY.replace("$1", f"'{start_date:%Y-%m-%d}'"), CONNECTION_STRING, engine="adbc")

def GetSectors():
    return pl.read_database_uri('SELECT * FROM "Sector"', CONNECTION_STRING, engine="adbc")

def CalculateVolatilityForSector(executor: ProcessPoolExecutor, all_values: pl.DataFrame, market_values: tuple[pl.DataFrame, ARCHModelResult], sector: int):
    sector_df = all_values.filter(pl.col('SectorId') == sector)
    sector_df = sector_df.with_columns(pl.col('Adjusted').pct_change().alias('Return'))
    sector_df = sector_df.filter(pl.col('Return').abs() < 0.5)
    sector_df = sector_df.group_by('Date').agg((pl.col('Return') * pl.col('Volume') / pl.col('Volume').sum()).sum().alias('SectorReturn'))
    joined = sector_df.join(market_values[0], on='Date', how='inner')
    sector_return = (joined.get_column('SectorReturn') * 100).to_numpy()
    _, _, _, result = FindBestGarch(executor, sector_return, verbose=True,
                                    max_p=2, max_q=2, max_o=2,
                                    volatility_models=VOL_MODELS,mean_models=MEAN_MODELS,
                                    distributions=DISTRIBUTIONS)
    return_market = (joined.get_column('MarketReturn') * 100).to_numpy()
    dcc = FitDcc(np.array([return_market, sector_return]), [result, market_values[1]])
    volatility = result.conditional_volatility / 100
    market_volatility = joined.get_column('MarketVolatility').to_numpy()
    betas = dcc[1, 0, :] / dcc[1, 1, :]
    adjusted_volatility = np.sqrt((volatility ** 2) - (betas * market_volatility) ** 2)
    joined = joined.with_columns(pl.lit(adjusted_volatility).alias('AdjustedVolatility'))
    return joined

def main(executor: ProcessPoolExecutor):
    start_date = dt.date(2022, 1, 1)
    market_values = GetMarketValues(executor, start_date)
    all_values = GetSectorValues(start_date)
    sectors = GetSectors()
    for sector in sectors['Id']:
        CalculateVolatilityForSector(executor, all_values, market_values, sector)

if __name__ == '__main__':
    with ProcessPoolExecutor(4) as executor:
        main(executor)