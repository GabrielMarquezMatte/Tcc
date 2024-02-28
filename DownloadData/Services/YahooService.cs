using DownloadData.Data;
using DownloadData.Entities;
using DownloadData.Repositories;
using Microsoft.EntityFrameworkCore;
using YahooQuotesApi;

namespace DownloadData.Services
{
    public sealed class YahooService(StockContext context, YahooQuotes yahooQuotes)
    {
        public async Task ProcessAsync(CancellationToken cancellationToken)
        {
            var tickers = await context.Tickers.ToDictionaryAsync(x => $"{x.StockTicker}.SA",cancellationToken).ConfigureAwait(false);
            var securities = await yahooQuotes.GetAsync(tickers.Select(t => t.Key), Histories.PriceHistory, ct: cancellationToken).ConfigureAwait(false);
            foreach(var security in securities.Values.Where(s => s?.PriceHistory.HasValue == true))
            {
                var ticker = tickers.GetValueOrDefault(security!.Symbol.Name);
                if(ticker == null)
                {
                    continue;
                }
                var historicalData = security!.PriceHistory.Value.Select(h => new HistoricalDataYahoo
                {
                    Date = h.Date.ToDateOnly(),
                    Open = h.Open,
                    High = h.High,
                    Low = h.Low,
                    Close = h.Close,
                    Adjusted = h.AdjustedClose,
                    Volume = h.Volume,
                    Ticker = ticker,
                });
                await context.HistoricalDataYahoos.AddRangeAsync(historicalData, cancellationToken).ConfigureAwait(false);
            }
            await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
    }
}