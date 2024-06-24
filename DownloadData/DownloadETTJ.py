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
from numba import njit
from matplotlib import pyplot as plt
from matplotlib import ticker as mticker

url_base = "https://cdn.tesouro.gov.br/sistemas-internos/apex/producao/sistemas/sistd/{year}/{title}_{year}.xls"
logger = getLogger(__name__)
basicConfig(level=INFO, format="%(asctime)s - %(name)s - %(levelname)s - %(message)s")

class OptimizeResult(NamedTuple):
    day: dt.date
    beta0: float
    beta1: float
    beta2: float
    tau0: float
    beta3: float
    tau1: float
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
        df["AnosVencimento"] = (df["Vencimento"] - df["Dia"]).dt.days/365.25
        return df[['Dia', 'Vencimento', 'Taxa Compra Manhã', 'AnosVencimento']]
    
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
    @njit
    def nelson_siegel(t: float, beta0: float, beta1: float, beta2: float, tau0: float, beta3: float, tau1: float) -> float:
        t_tau0 = t*tau0
        exp_tau0 = np.exp(-t_tau0)
        time_part0 = (1-exp_tau0)/t_tau0
        t_tau1 = t*tau1
        exp_tau1 = np.exp(-t_tau1)
        time_part1 = (1-exp_tau1)/t_tau1
        return beta0 + beta1*time_part0 + beta2*(time_part0-exp_tau0) + beta3*(time_part1-exp_tau1)
    
    @staticmethod
    def _curve_fit(func, x, y, p0):
        return curve_fit(func, x, y, p0, maxfev = 5000, bounds=(-1.5, 1.5))
    
    @staticmethod
    async def fit(data:pd.DataFrame, loop: asyncio.AbstractEventLoop, executor: ProcessPoolExecutor) -> OptimizeResult:
        x = data["AnosVencimento"].to_numpy()
        y = data["Taxa Compra Manhã"].to_numpy()
        try:
            popt, _ = await loop.run_in_executor(executor, Optimizer._curve_fit, Optimizer.nelson_siegel, x, y, (0.1, 0.01, -0.05, 1, -0.05, 1))
            return data["Dia"].iloc[0].date(), *popt, Optimizer.nelson_siegel(1, *popt)
        except Exception as e:
            logger.error("Error fitting %s - %s - %s", data['Dia'].iloc[0].date(), data, e)
            raise e

async def save(pool: asyncpg.Pool, parameters: list[OptimizeResult]):
    connection: asyncpg.Connection
    async with pool.acquire() as connection:
        prepared = await connection.prepare('INSERT INTO "NelsonSiegel" VALUES ($1, $2, $3, $4, $5, $6) ON CONFLICT DO NOTHING')
        await prepared.executemany(parameters)

async def execute_for_year(year: int, pool: asyncpg.Pool, downloader: DownloadETTJ, loop: asyncio.AbstractEventLoop, executor: ProcessPoolExecutor) -> list[OptimizeResult]:
    async with asyncio.TaskGroup() as group:
        ltn_task = group.create_task(downloader.get_bonds("LTN", year))
        ntnf_task = group.create_task(downloader.get_bonds("NTN-F", year))
    bonds = pd.concat([await ltn_task, await ntnf_task])
    if bonds.empty:
        logger.info("No data for year %d", year)
        return []
    fit_tasks: list[asyncio.Task[OptimizeResult]] = []
    parameters: list[OptimizeResult] = []
    async with asyncio.TaskGroup() as group:
        for _, data in bonds.groupby("Dia"):
            fit_task = group.create_task(Optimizer.fit(data, loop, executor))
            fit_tasks.append(fit_task)
    for fit_task in fit_tasks:
        parameters.append(await fit_task)
    # await save(pool, parameters)
    logger.info(f"Year {year} done")
    return parameters

def resolution_to_dpi(width_px: int, height_px: int) -> int:
    width_in = width_px / 100
    height_in = height_px / 100
    return int(2**12 / np.sqrt(width_in**2 + height_in**2))

def plot_today_ettj(times: np.ndarray, last_result: list[OptimizeResult]):
    plt.plot(times, Optimizer.nelson_siegel(times, *last_result[1:-1]))
    plt.title(f"Modelo de Nelson-Siegel para {last_result[0]}")
    plt.xlabel("Anos para vencimento")
    plt.ylabel("")
    plt.gca().yaxis.set_major_formatter(mticker.PercentFormatter(1))
    # 2k resolution
    plt.savefig("images/ettj_today.png", dpi=resolution_to_dpi(1000, 1000))

def plot_historical_ettj(extended: list[OptimizeResult]):
    excel = pd.read_excel("Juros.xlsx", sheet_name="Rates")
    df = pd.DataFrame(extended, columns=["Dia", "Beta0", "Beta1", "Beta2", "Tau0", "Beta3", "Tau1", "RateModel"])
    df["Dia"] = pd.to_datetime(df["Dia"])
    df = df.merge(excel, left_on="Dia", right_on="Date", how="inner")
    df = df.drop(columns=["Date"])
    df.plot(x="Dia", y=["Rate", "RateModel"])
    plt.title("ETTJ histórica - Vértice 1 ano")
    plt.xlabel("")
    plt.ylabel("")
    plt.gca().yaxis.set_major_formatter(mticker.PercentFormatter(1))
    plt.legend(["ETTJ", "Modelo de Nelson-Siegel Estimado"])
    plt.savefig("images/ettj_historical.png", dpi=resolution_to_dpi(1000, 1000))
    
async def main():
    logger.info("Starting")
    pool = asyncpg.create_pool(user='postgres', password='postgres', database='stock', host='localhost')
    client = aiohttp.ClientSession()
    group = asyncio.TaskGroup()
    start = dt.datetime.now()
    with ProcessPoolExecutor(max_workers= 4) as executor:
        loop = asyncio.get_event_loop()
        async with pool, client, group:
            downloader = DownloadETTJ(url_base, client)
            tasks = []
            for year in range(2009, dt.datetime.now().year+1):
                task = group.create_task(execute_for_year(year, pool, downloader, loop, executor))
                tasks.append(task)
    result = await asyncio.gather(*tasks)
    extended = [item for sublist in result for item in sublist]
    times = np.arange(0.001, 10, 0.001)
    last_result = extended[-1]
    plot_today_ettj(times, last_result)
    plot_historical_ettj(extended)
    end = dt.datetime.now()
    logger.info("Elapsed time: %s", end-start)

if __name__ == "__main__":
    asyncio.run(main())