from aiohttp import ClientSession
import datetime as dt
import pandas as pd
import io

class YahooDataReader:
    def __init__(self, client_session: ClientSession):
        self.client_session = client_session
        self.base_url = "https://query1.finance.yahoo.com/v7/finance/download/"
    
    async def fetch(self, ticker: str, start: dt.datetime, end: dt.datetime, interval: str = "1d"):
        url = f"{self.base_url}{ticker}?period1={int(start.timestamp())}&period2={int(end.timestamp())}&interval={interval}&events=history&includeAdjustedClose=true"
        async with self.client_session.get(url) as response:
            data = await response.text()
        return data
    
    @staticmethod
    def read_data(csv_data: str) -> pd.DataFrame:
        with io.StringIO(csv_data) as f:
            df = pd.read_csv(f, parse_dates=['Date'])
        return df
    
    async def get_data(self, ticker: str, start: dt.datetime, end: dt.datetime, interval: str = "1d"):
        csv_data = await self.fetch(ticker, start, end, interval)
        df = self.read_data(csv_data)
        return df