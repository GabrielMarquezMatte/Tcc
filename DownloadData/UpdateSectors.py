import pandas as pd
import asyncpg
import asyncio
from logging import getLogger, basicConfig, INFO

logger = getLogger(__name__)
basicConfig(level=INFO, format="%(asctime)s - %(name)s - %(levelname)s - %(message)s")

async def update_industry(pool: asyncpg.pool.Pool, industry_id: int, sector_id: int):
    async with pool.acquire() as connection:
        await connection.execute('UPDATE "Industries" SET "SectorId" = $1 WHERE "Id" = $2', sector_id, industry_id)
        logger.info(f"Updated industry {industry_id} with sector {sector_id}")

async def main():
    logger.info("Starting")
    pool = asyncpg.create_pool(user='postgres', password='postgres', database='stock', host='localhost')
    group = asyncio.TaskGroup()
    async with pool, group:
        industries = pd.read_excel("Sectors.xlsx")
        for industry_id, _, sector_id in industries.values:
            group.create_task(update_industry(pool, industry_id, sector_id))

if __name__ == "__main__":
    asyncio.run(main())