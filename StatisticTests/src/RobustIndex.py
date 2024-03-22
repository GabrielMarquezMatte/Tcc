from concurrent.futures import ProcessPoolExecutor
import asyncpg
import datetime as dt
from itertools import permutations
import asyncio
from typing import NamedTuple
from logging import getLogger, basicConfig, INFO
import numpy as np
import polars as pl
from matplotlib import pyplot as plt

logger = getLogger(__name__)
basicConfig(level=INFO, format="%(asctime)s - %(name)s - %(levelname)s - %(message)s")

class Returns(NamedTuple):
    date: dt.date
    ticker_id: int
    return_: float
    volume: float

initial_query = """
WITH "HistData" AS
    (SELECT "Date",
            "TickerId",
            EXP(LN("Adjusted") - LAG(LN("Adjusted"), 1, LN("Adjusted")) OVER(PARTITION BY "TickerId"
                                                                             ORDER BY "Date"))-1 AS "Return",
            LN("Volume" * "Adjusted") AS "Volume"
     FROM "HistoricalDataYahoo"
     WHERE "Date" > $1
     AND "Adjusted" > 0)
SELECT "HistData"."Date",
       "HistData"."TickerId",
       "Industries"."SectorId",
       "HistData"."Return",
       "HistData"."Volume"
FROM "HistData"
INNER JOIN "Tickers" ON "Tickers"."Id" = "HistData"."TickerId"
INNER JOIN "Companies" ON "Companies"."Id" = "Tickers"."CompanyId"
INNER JOIN "CompanyIndustries" ON "CompanyIndustries"."CompanyId" = "Companies"."Id"
INNER JOIN "Industries" ON "Industries"."Id" = "CompanyIndustries"."IndustryId"
WHERE ABS("HistData"."Return") < 0.5
"""

aggregate_expression = [
    pl.col('Return').mean().alias('mean_return'),
    pl.col('Volume').sum().alias('sum_volume'),
    pl.col('WeightedReturn').sum().alias('weighted_sum')
]

weight_expression = (pl.col('Return') * pl.col('Volume')).alias('WeightedReturn')
filter_expression = pl.col('weighted_sum') > 0
weighted_avg_return_expression = (pl.col('weighted_sum') / pl.col('sum_volume')).alias('WeightedAvgReturn')
std_expressions = [
    pl.col('mean_return').std().alias('Return_std'),
    pl.col('WeightedAvgReturn').std().alias('WeightedAvgReturn_std')
]

def get_sectors(connection: asyncpg.Connection):
    return connection.fetch('SELECT * FROM "Sector"')

def get_all_returns(start_date:dt.date):
    connection_string = "postgresql://postgres:postgres@localhost/stock"
    data_frame = pl.read_database_uri(initial_query.replace("$1", f"'{start_date.strftime('%Y-%m-%d')}'"), connection_string, engine="adbc")
    data_frame = data_frame.with_columns(weight_expression)
    return data_frame, *calculate_index(data_frame.unique(subset=['Date', 'TickerId']).drop('SectorId', 'TickerId'))

def calculate_index(values: pl.DataFrame) -> tuple[float, float]:
    aggregated = values.group_by('Date').agg(aggregate_expression).filter(filter_expression)
    aggregated = aggregated.with_columns(weighted_avg_return_expression)
    stds = aggregated.select(std_expressions)
    mean_return_std = stds.get_column('Return_std').to_numpy()[0]
    weighted_avg_return_std = stds.get_column('WeightedAvgReturn_std').to_numpy()[0]
    return mean_return_std, weighted_avg_return_std

def execute_for_sector(base_returns: pl.DataFrame, industries: set[int]):
    values = base_returns.filter(~pl.col('SectorId').is_in(industries))
    values = values.unique(subset=['Date', 'TickerId'])
    values = values.drop('SectorId', 'TickerId')
    return calculate_index(values)

def execute(base_returns: pl.DataFrame, sectors: permutations, n_permutations: int):
    results: list[tuple[float, float]] = []
    for sector in sectors:
        if np.random.random() > 0.7:
            continue
        result = execute_for_sector(base_returns, set(sector))
        results.append(result)
        if len(results) >= 800:
            break
    logger.info("Created %d tasks for %d sectors permutation", len(results), n_permutations)
    return results

def calculate_jackknife_result(base_mean_std: float, base_log_std: float, results: list[tuple[float, float]]):
    n = len(results)
    mean_jackknife = np.mean([i[0] for i in results])
    log_jackknife = np.mean([i[1] for i in results])
    bias_mean = (n - 1) * (mean_jackknife - base_mean_std)
    bias_log = (n - 1) * (log_jackknife - base_log_std)
    se_mean = np.sqrt((n - 1) / n * np.sum([(i[0] - mean_jackknife) ** 2 for i in results]))
    se_log = np.sqrt((n - 1) / n * np.sum([(i[1] - log_jackknife) ** 2 for i in results]))
    return (bias_mean, se_mean), (bias_log, se_log)

async def execute_permutation(base_returns: pl.DataFrame, sectors: set[int], n_sectors: int,
                        base_mean_std: float, base_log_std: float, loop: asyncio.AbstractEventLoop,
                        executor: ProcessPoolExecutor):
    permutation = permutations(sectors, n_sectors)
    start = dt.datetime.now()
    result = await loop.run_in_executor(executor, execute, base_returns, permutation, n_sectors)
    end = dt.datetime.now()
    logger.info("Results for %d sectors. Time taken: %s. Length: %d", n_sectors, end - start, len(result))
    return calculate_jackknife_result(base_mean_std, base_log_std, result)

async def main(executor: ProcessPoolExecutor, loop: asyncio.AbstractEventLoop):
    async with asyncpg.create_pool(user='postgres', password='postgres', database='stock', host='localhost') as pool, pool.acquire() as connection:
        sectors: set[int] = {i["Id"] for i in await get_sectors(connection)}
        logger.info("Retrieved %d sectors", len(sectors))
        base_returns, base_mean_std, base_log_std = get_all_returns(dt.date(2014, 1, 1))
        logger.info("Retrieved base returns with %d rows", base_returns.shape[0])
        start = dt.datetime.now()
        tasks: list[asyncio.Task] = []
        async with asyncio.TaskGroup() as group:
            for i in range(1, len(sectors)):
                task = group.create_task(execute_permutation(base_returns, sectors, i, base_mean_std, base_log_std, loop, executor))
                tasks.append(task)
        results = await asyncio.gather(*tasks)
        logger.info("Created %d tasks", len(results))
        end = dt.datetime.now()
        logger.info("Time taken: %s", end - start)
        bias_mean = [i[0][0] for i in results]
        se_mean = [i[0][1] for i in results]
        bias_log = [i[1][0] for i in results]
        se_log = [i[1][1] for i in results]
        fig = plt.figure()
        # Create two subplots to be shown one below the other
        ax1 = fig.add_subplot(211)
        ax2 = fig.add_subplot(212)
        ax1.plot(bias_mean, label="Bias Mean")
        ax1.plot(bias_log, label="Bias Log")
        ax1.legend()
        ax2.plot(se_mean, label="SE Mean")
        ax2.plot(se_log, label="SE Log")
        ax2.legend()
        plt.show()
        return results

if __name__ == "__main__":
    import asyncio
    executor = ProcessPoolExecutor(2)
    loop = asyncio.new_event_loop()
    try:
        with executor:
            loop.run_until_complete(main(executor, loop))
    finally:
        loop.close()