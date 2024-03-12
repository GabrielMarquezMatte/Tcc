import asyncpg
import datetime as dt
from asyncpg import Record
import pandas as pd
import numpy as np
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

async def get_returns(connection: asyncpg.Connection, start_date:dt.date, remove_industry: list[str]):
    if remove_industry:
        val = await connection.fetch(removal_query, start_date, remove_industry)
        return [Returns(*i) for i in val]
    val = await connection.fetch(initial_query, start_date)
    return [Returns(*i) for i in val]

def calculate_index(values: list[Returns]):
    df = pd.DataFrame(values, columns=['Date', 'TickerId', 'Return', 'Volume'])
    df['Date'] = pd.to_datetime(df['Date'])
    df = df.set_index('Date')
    return df.groupby('Date').agg({'Return': 'mean'}), df.groupby('Date').apply(lambda x: np.average(x['Return'], weights=x['Volume'])).to_frame('Return')

async def execute_for_industry(connection: asyncpg.Connection, industries: tuple[int], loop: asyncio.AbstractEventLoop, executor: ProcessPoolExecutor):
    values = await get_returns(connection, dt.date(2020, 1, 1), industries)
    mean, volume = await loop.run_in_executor(executor, calculate_index, values)
    return mean["Return"].std(), volume["Return"].std()

async def execute_for_permutation(pool: asyncpg.Pool, industries: tuple[int], loop: asyncio.AbstractEventLoop, executor: ProcessPoolExecutor):
    async with pool.acquire() as connection:
        start = dt.datetime.now()
        val = await execute_for_industry(connection, industries, loop, executor)
        print(f"Results for {industries} achieved in {dt.datetime.now() - start}")

async def execute(pool: asyncpg.Pool, industries: permutations, loop: asyncio.AbstractEventLoop, executor: ProcessPoolExecutor):
    tasks: list[asyncio.Task[tuple[float, float]]] = []
    async with asyncio.TaskGroup() as group:
        for industry in industries:
            task = group.create_task(execute_for_permutation(pool, industry, loop, executor))
            tasks.append(task)
            if len(tasks) == 10:
                return await asyncio.gather(*tasks)
    return await asyncio.gather(*tasks)

async def main():
    async with asyncpg.create_pool(user='postgres', password='postgres', database='stock', host='localhost') as pool:
        async with pool.acquire() as connection:
            industries = [i["Id"] for i in await get_industries(connection)]
        executor = ProcessPoolExecutor(max_workers=4)
        with executor:
            loop = asyncio.get_event_loop()
            for i in range(2, len(industries)):
                permut = permutations(industries, i)
                results = await execute(pool, permut, loop, executor)
                print(f"Results for {i} industries")
                print(results)

if __name__ == "__main__":
    import asyncio
    asyncio.run(main())