import asyncpg
import datetime as dt
from itertools import permutations
import asyncio
from concurrent.futures import ProcessPoolExecutor
from typing import NamedTuple
from logging import getLogger, basicConfig, INFO
import numpy as np
import polars as pl

logger = getLogger(__name__)
basicConfig(level=INFO, format="%(asctime)s - %(name)s - %(levelname)s - %(message)s")

class Returns(NamedTuple):
    date: dt.date
    ticker_id: int
    return_: float
    volume: float

initial_query = """
WITH "HistData" AS (
    SELECT "Date",
    "TickerId",
    EXP(LN(
        CASE
            WHEN "Adjusted" < 0 THEN "Close"
            ELSE "Adjusted"
        END
    ) - LAG(
        LN(
            CASE
                WHEN "Adjusted" < 0 THEN "Close"
                ELSE "Adjusted"
            END
        ),
        1,
        LN(
            CASE
                WHEN "Adjusted" < 0 THEN "Close"
                ELSE "Adjusted"
            END
        )
    ) OVER(
        PARTITION BY "TickerId"
        ORDER BY "Date"
    ))-1 AS "Return",
    LN("Volume") AS "Volume"
FROM "HistoricalDataYahoo"
WHERE "Date" > $1
)
SELECT "HistData"."Date", "HistData"."TickerId", "Industries"."SectorId", "HistData"."Return", "HistData"."Volume"
FROM "HistData"
INNER JOIN "Tickers" ON "Tickers"."Id" = "HistData"."TickerId"
INNER JOIN "Companies" ON "Companies"."Id" = "Tickers"."CompanyId"
INNER JOIN "CompanyIndustries" ON "CompanyIndustries"."CompanyId" = "Companies"."Id"
INNER JOIN "Industries" ON "Industries"."Id" = "CompanyIndustries"."IndustryId"
"""

aggregate_expression = [
    pl.col('Return').mean().alias('mean_return'),
    pl.col('Volume').sum().alias('sum_volume'),
    pl.col('WeightedReturn').sum().alias('weighted_sum')
]

def get_sectors(connection: asyncpg.Connection):
    return connection.fetch('SELECT * FROM "Sector"')

def get_all_returns(start_date:dt.date):
    connection_string = "postgresql://postgres:postgres@localhost/stock"
    return pl.read_database_uri(initial_query.replace("$1", f"'{start_date.strftime('%Y-%m-%d')}'"), connection_string, engine="adbc")

def get_returns(base_returns: pl.DataFrame, remove_sector: set[int]):
    return base_returns.filter(~pl.col('SectorId').is_in(remove_sector))

def calculate_index(values: pl.DataFrame) -> tuple[float, float]:
    values = values.with_columns((pl.col('Return') * pl.col('Volume')).alias('WeightedReturn'))
    # Group by 'Date' and aggregate
    aggregated = values.group_by('Date').agg(aggregate_expression)
    aggregated = aggregated.with_columns((pl.col('weighted_sum') / pl.col('sum_volume')).alias('WeightedAvgReturn'))
    mean_return_std = aggregated.select(pl.col('mean_return').std()).get_column('mean_return').to_numpy()[0]
    weighted_avg_return_std = aggregated.select(pl.col('WeightedAvgReturn').std()).get_column('WeightedAvgReturn').to_numpy()[0]
    return mean_return_std, weighted_avg_return_std

async def execute_for_sector(base_returns: pl.DataFrame, industries: tuple[int], loop: asyncio.AbstractEventLoop, executor: ProcessPoolExecutor):
    values = get_returns(base_returns, industries)
    if values.shape[0] == base_returns.shape[0]:
        std_mean = values.groupby('Date').agg(pl.col('Return').std()).get_column('Return_std').to_numpy()[0]
        return std_mean, std_mean
    values = values.unique(subset=['Date', 'TickerId'])
    values = values.drop('SectorId', 'TickerId')
    return await loop.run_in_executor(executor, calculate_index, values)

def execute_for_sector_sync(base_returns: pl.DataFrame, industries: tuple[int]):
    values = get_returns(base_returns, industries)
    if values.shape[0] == base_returns.shape[0]:
        std_mean = values.groupby('Date').agg(pl.col('Return').std()).get_column('Return_std').to_numpy()[0]
        return std_mean, std_mean
    values = values.unique(subset=['Date', 'TickerId'])
    values = values.drop('SectorId', 'TickerId')
    return calculate_index(values)

async def execute(base_returns: pl.DataFrame, sectors: permutations, n_permutations: int, loop: asyncio.AbstractEventLoop, executor: ProcessPoolExecutor, group: asyncio.TaskGroup):
    tasks: list[asyncio.Task[tuple[float, float]]] = []
    for sector in sectors:
        if np.random.random() > 0.7:
            continue
        task = group.create_task(execute_for_sector(base_returns, set(sector), loop, executor))
        tasks.append(task)
        if len(tasks) >= 250:
            break
    logger.info(f"Created {len(tasks)} tasks for {n_permutations} sectors permutation")
    return await asyncio.gather(*tasks)

def execute_sync(base_returns: pl.DataFrame, sectors: permutations):
    results = []
    for sector in sectors:
        results.append(execute_for_sector_sync(base_returns, set(sector)))
    return results

async def execute_permutation(base_returns: pl.DataFrame, sectors: set[int], n_sectors: int, loop: asyncio.AbstractEventLoop, executor: ProcessPoolExecutor, group: asyncio.TaskGroup):
    permutation = permutations(sectors, n_sectors)
    start = dt.datetime.now()
    result = await execute(base_returns, permutation, n_sectors, loop, executor, group)
    end = dt.datetime.now()
    logger.info(f"Results for {n_sectors} sectors. Time taken: {end - start}. Length: {len(result)}")
    return result

async def main(executor: ProcessPoolExecutor):
    async with asyncpg.create_pool(user='postgres', password='postgres', database='stock', host='localhost') as pool, pool.acquire() as connection:
        sectors: set[int] = {i["Id"] for i in await get_sectors(connection)}
        logger.info("Retrieved sectors")
        base_returns = get_all_returns(dt.date(2020, 1, 1))
        logger.info("Retrieved base returns")
        loop = asyncio.get_event_loop()
        tasks: list[asyncio.Task[list[tuple[float, float]]]] = []
        start = dt.datetime.now()
        async with asyncio.TaskGroup() as group:
            for i in range(1, len(sectors) - 1):
                task = group.create_task(execute_permutation(base_returns, sectors, i, loop, executor, group))
                tasks.append(task)
            logger.info(f"Created {len(tasks)} tasks")
        end = dt.datetime.now()
        logger.info(f"Time taken: {end - start}")
        return await asyncio.gather(*tasks)

async def main_sync():
    async with asyncpg.create_pool(user='postgres', password='postgres', database='stock', host='localhost') as pool, pool.acquire() as connection:
        industries: set[int] = {i["Id"] for i in await get_sectors(connection)}
        logger.info("Retrieved sectors")
        base_returns = get_all_returns(dt.date(2020, 1, 1))
        logger.info("Retrieved base returns")
        results = []
        start = dt.datetime.now()
        for i in range(2, len(industries) -1 ):
            permut = permutations(industries, i)
            start_execution = dt.datetime.now()
            result = execute_sync(base_returns, permut)
            end_execution = dt.datetime.now()
            logger.info(f"Results for {i} sectors. Time taken: {end_execution - start_execution}")
            results.append(result)
        end = dt.datetime.now()
        logger.info(f"Time taken: {end - start}")
        return results

if __name__ == "__main__":
    import asyncio
    with ProcessPoolExecutor() as executor:
        asyncio.run(main(executor))