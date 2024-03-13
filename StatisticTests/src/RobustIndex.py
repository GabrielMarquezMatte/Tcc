import asyncpg
import datetime as dt
import pandas as pd
from itertools import permutations
import asyncio
from concurrent.futures import ProcessPoolExecutor
from typing import NamedTuple

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
SELECT "HistData"."Date", "HistData"."TickerId", "CompanyIndustries"."IndustryId", "HistData"."Return", "HistData"."Volume"
FROM "HistData"
INNER JOIN "Tickers" ON "Tickers"."Id" = "HistData"."TickerId"
INNER JOIN "Companies" ON "Companies"."Id" = "Tickers"."CompanyId"
INNER JOIN "CompanyIndustries" ON "CompanyIndustries"."CompanyId" = "Companies"."Id"
"""

removal_query = """
SELECT DISTINCT "Date",
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
INNER JOIN "Tickers" ON "Tickers"."Id" = "HistoricalDataYahoo"."TickerId"
INNER JOIN "Companies" ON "Companies"."Id" = "Tickers"."CompanyId"
INNER JOIN "CompanyIndustries" ON "CompanyIndustries"."CompanyId" = "Companies"."Id"
WHERE "HistoricalDataYahoo"."Date" > $1
AND "CompanyIndustries"."IndustryId" <> ALL($2::int[])
"""

def get_industries(connection: asyncpg.Connection):
    return connection.fetch('SELECT * FROM "Industries"')

async def get_all_returns(connection: asyncpg.Connection, start_date:dt.date):
    val = await connection.fetch(initial_query, start_date)
    df = pd.DataFrame(val, columns=['Date', 'TickerId', 'IndustryId', 'Return', 'Volume'])
    df['Date'] = pd.to_datetime(df['Date'])
    return df

def get_returns(base_returns: pd.DataFrame, remove_industry: set[int]):
    if remove_industry:
        return base_returns[~base_returns['IndustryId'].isin(remove_industry)]
    return base_returns

def average_return(values: pd.DataFrame) -> float:
    return (values['Return'] * values['Volume']).sum()

def calculate_index(values: pd.DataFrame) -> tuple[float, float]:
    grouped = values.groupby('Date')
    # Calculate the mean return for each date directly
    mean_return = grouped['Return'].mean().std()
    # Calculate the weighted average return for each date
    # Ensure this operation is vectorized
    sum_weights = grouped['Volume'].sum()
    weighted_sum = grouped.apply(average_return, include_groups=False)
    weighted_avg_return = (weighted_sum / sum_weights).std()
    return mean_return, weighted_avg_return

async def execute_for_industry(base_returns: pd.DataFrame, industries: tuple[int], loop: asyncio.AbstractEventLoop, executor: ProcessPoolExecutor):
    values = get_returns(base_returns, industries)
    if values.shape[0] == base_returns.shape[0]:
        return 0, 0
    values = values.drop_duplicates(subset=['Date', 'TickerId'])
    values = values.drop(columns=['IndustryId', 'TickerId'])
    return await loop.run_in_executor(executor, calculate_index, values)

def execute_for_industry_sync(base_returns: pd.DataFrame, industries: tuple[int]):
    values = get_returns(base_returns, industries)
    if values.shape[0] == base_returns.shape[0]:
        return 0, 0
    values = values.drop_duplicates(subset=['Date', 'TickerId'])
    values = values.drop(columns=['IndustryId', 'TickerId'])
    return calculate_index(values)

async def execute(base_returns: pd.DataFrame, industries: permutations, loop: asyncio.AbstractEventLoop, executor: ProcessPoolExecutor):
    tasks: list[asyncio.Task[tuple[float, float]]] = []
    async with asyncio.TaskGroup() as group:
        for industry in industries:
            task = group.create_task(execute_for_industry(base_returns, set(industry), loop, executor))
            tasks.append(task)
            if len(tasks) == 100:
                return await asyncio.gather(*tasks)
    return await asyncio.gather(*tasks)

def execute_sync(base_returns: pd.DataFrame, industries: permutations):
    results = []
    for industry in industries:
        results.append(execute_for_industry_sync(base_returns, set(industry)))
        if len(results) == 100:
            return results
    return results

async def main(executor: ProcessPoolExecutor):
    async with asyncpg.create_pool(user='postgres', password='postgres', database='stock', host='localhost') as pool, pool.acquire() as connection:
        industries: set[int] = {i["Id"] for i in await get_industries(connection)}
        base_returns = await get_all_returns(connection, dt.date(2020, 1, 1))
        loop = asyncio.get_event_loop()
        for i in range(2, len(industries)):
            permut = permutations(industries, i)
            results = await execute(base_returns, permut, loop, executor)
            print(f"Results for {i} industries")
            print(results)

async def main_sync():
    async with asyncpg.create_pool(user='postgres', password='postgres', database='stock', host='localhost') as pool, pool.acquire() as connection:
        industries: set[int] = {i["Id"] for i in await get_industries(connection)}
        base_returns = await get_all_returns(connection, dt.date(2020, 1, 1))
        for i in range(2, len(industries)):
            permut = permutations(industries, i)
            results = execute_sync(base_returns, permut)
            print(f"Results for {i} industries")
            print(results)

if __name__ == "__main__":
    import asyncio
    with ProcessPoolExecutor() as executor:
        asyncio.run(main(executor))