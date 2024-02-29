import pandas as pd
from pandas import ExcelFile
import numpy as np
from scipy.optimize import curve_fit
import asyncio
import aiohttp
import asyncpg
import datetime as dt
import io
from concurrent.futures import ProcessPoolExecutor
from logging import getLogger, basicConfig, INFO
from typing import NamedTuple

url_base = "https://cdn.tesouro.gov.br/sistemas-internos/apex/producao/sistemas/sistd/{year}/{title}_{year}.xls"
logger = getLogger(__name__)
basicConfig(level=INFO, format="%(asctime)s - %(name)s - %(levelname)s - %(message)s")

class OptimizeResult(NamedTuple):
    day: dt.date
    beta0: float
    beta1: float
    beta2: float
    tau0: float
    rate: float

class DownloadETTJ:
    def __init__(self, url_base: str, client: aiohttp.ClientSession):
        self.url_base = url_base
        self.client = client

    async def download(self, title: str, year: int):
        url = self.url_base.format(title = title, year=year)
        async with self.client.get(url) as response:
            if response.status != 200:
                return None
            return await response.read()
    
    @staticmethod
    def parse_sheet(sheet: str, file: ExcelFile) -> pd.DataFrame:
        maturity = pd.to_datetime(file.parse(sheet, usecols="B", nrows=1, header=None).iloc[0, 0], format="%d/%m/%Y")
        df = file.parse(sheet, skiprows=1)
        df = df.dropna(axis=1, how="all")
        df = df.dropna(axis=0, how="all")
        df["Vencimento"] = maturity
        df["Dia"] = pd.to_datetime(df["Dia"], format="%d/%m/%Y")
        return df[['Dia', 'Vencimento', 'Taxa Compra Manhã']]
    
    async def get_bonds(self, title: str, year: int) -> pd.DataFrame:
        data = await self.download(title, year)
        if data is None:
            return pd.DataFrame()
        with io.BytesIO(data) as data_io:
            with pd.ExcelFile(data_io) as file:
                sheets = file.sheet_names
                return pd.concat([self.parse_sheet(sheet, file) for sheet in sheets])

class Optimizer:
    @staticmethod
    def nelson_siegel(t: float, beta0: float, beta1: float, beta2: float, tau0: float) -> float:
        return beta0 + beta1*((1-np.exp(-t*tau0))/(t*tau0)) + beta2*(((1-np.exp(-t*tau0))/(t*tau0))-np.exp(-t*tau0))
    
    @staticmethod
    def _curve_fit(func, x, y, p0):
        return curve_fit(func, x, y, p0, maxfev = 5000, bounds=(-1.5, 1.5))
    
    @staticmethod
    async def fit(data:pd.DataFrame, loop: asyncio.AbstractEventLoop, executor: ProcessPoolExecutor) -> OptimizeResult:
        x = (data["Vencimento"] - data["Dia"]).dt.days/365.25
        y = data["Taxa Compra Manhã"]
        try:
            popt, _ = await loop.run_in_executor(executor, Optimizer._curve_fit, Optimizer.nelson_siegel, x, y, (0.1, 0.01, -0.05, 1))
            return data["Dia"].iloc[0].date(), *popt, Optimizer.nelson_siegel(1, *popt)
        except Exception as e:
            logger.error(f"Error fitting {data['Dia'].iloc[0].date()} - {data} - {e}")
            raise e

async def save(pool: asyncpg.Pool, parameters: list[OptimizeResult]):
    connection: asyncpg.Connection
    async with pool.acquire() as connection:
        prepared = await connection.prepare('INSERT INTO "NelsonSiegel" VALUES ($1, $2, $3, $4, $5, $6) ON CONFLICT DO NOTHING')
        await prepared.executemany(parameters)

async def execute_for_year(year: int, pool: asyncpg.Pool, downloader: DownloadETTJ, loop: asyncio.AbstractEventLoop, executor: ProcessPoolExecutor):
    async with asyncio.TaskGroup() as group:
        ltn_task = group.create_task(downloader.get_bonds("LTN", year))
        ntnf_task = group.create_task(downloader.get_bonds("NTN-F", year))
    bonds = pd.concat([await ltn_task, await ntnf_task])
    if bonds.empty:
        logger.info(f"No data for year {year}")
        return
    fit_tasks: list[asyncio.Task[OptimizeResult]] = []
    parameters: list[OptimizeResult] = []
    async with asyncio.TaskGroup() as group:
        for _, data in bonds.groupby("Dia"):
            fit_task = group.create_task(Optimizer.fit(data, loop, executor))
            fit_tasks.append(fit_task)
    for fit_task in fit_tasks:
        parameters.append(await fit_task)
    await save(pool, parameters)
    logger.info(f"Year {year} done")
    return
    
async def main():
    logger.info("Starting")
    with ProcessPoolExecutor(max_workers=8) as executor:
        loop = asyncio.get_event_loop()
        async with asyncpg.create_pool(user='postgres', password='postgres', database='stock', host='localhost') as pool:
            async with aiohttp.ClientSession() as client:
                downloader = DownloadETTJ(url_base, client)
                async with asyncio.TaskGroup() as group:
                    for year in range(2003, dt.datetime.now().year+1):
                        group.create_task(execute_for_year(year, pool, downloader, loop, executor))

if __name__ == "__main__":
    asyncio.run(main())