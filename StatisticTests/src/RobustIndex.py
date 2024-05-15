from concurrent.futures import ProcessPoolExecutor
import asyncpg
import datetime as dt
from itertools import permutations
import asyncio
from logging import getLogger, basicConfig, INFO
import numpy as np
import polars as pl
from matplotlib import pyplot as plt
from numba import njit

logger = getLogger(__name__)
basicConfig(level=INFO, format="%(asctime)s - %(name)s - %(levelname)s - %(message)s")

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
    mean_results: list[float] = []
    log_results: list[float] = []
    for sector in sectors:
        if np.random.random() > 0.7:
            continue
        mean_result, log_result = execute_for_sector(base_returns, set(sector))
        mean_results.append(mean_result)
        log_results.append(log_result)
        if len(mean_results) == 800:
            break
    logger.info("Created %d tasks for %d sectors permutation", len(mean_results), n_permutations)
    return np.array(mean_results), np.array(log_results)

@njit
def calculate_jackknife_result(base_mean_std: float, base_log_std: float, mean_results: np.ndarray, log_results: np.ndarray):
    n = mean_results.shape[0]
    mean_jackknife = np.mean(mean_results)
    log_jackknife = np.mean(log_results)
    bias_mean = (n - 1) * (mean_jackknife - base_mean_std)
    bias_log = (n - 1) * (log_jackknife - base_log_std)
    se_mean = np.sqrt((n-1)/n * np.sum((mean_results - mean_jackknife) ** 2))
    se_log = np.sqrt((n-1)/n * np.sum((log_results - log_jackknife) ** 2))
    return (bias_mean, se_mean), (bias_log, se_log)

async def execute_permutation(base_returns: pl.DataFrame, sectors: set[int], n_sectors: int,
                        base_mean_std: float, base_log_std: float, loop: asyncio.AbstractEventLoop,
                        executor: ProcessPoolExecutor):
    permutation = permutations(sectors, n_sectors)
    start = dt.datetime.now()
    mean_results, log_results = await loop.run_in_executor(executor, execute, base_returns, permutation, n_sectors)
    end = dt.datetime.now()
    logger.info("Results for %d sectors. Time taken: %s. Length: %d", n_sectors, end - start, len(mean_results))
    return calculate_jackknife_result(base_mean_std, base_log_std, mean_results, log_results)

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
        fig.suptitle("Teste Jackknife")
        ax1 = fig.add_subplot(211)
        ax2 = fig.add_subplot(212)
        ax1.plot(bias_mean, label="Viés (Pesos Iguais)", color="#377eb8")
        ax1.plot(bias_log, label="Viés (Pesos Log)", color="#ff7f00")
        ax1.legend()
        ax2.plot(se_mean, label="Erro Padrão (Pesos Iguais)", color="#377eb8")
        ax2.plot(se_log, label="Erro Padrão (Pesos Log)", color="#ff7f00")
        ax2.legend()
        fig.savefig("images/jackknife.png")
        return results

if __name__ == "__main__":
    import asyncio
    executor = ProcessPoolExecutor(3)
    loop = asyncio.new_event_loop()
    try:
        with executor:
            loop.run_until_complete(main(executor, loop))
    finally:
        loop.close()