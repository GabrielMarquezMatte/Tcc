using System.Diagnostics;
using DownloadData.Data;
using DownloadData.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using YahooQuotesApi;

namespace DownloadData.Services
{
    public sealed class YahooService(StockContext context, YahooQuotes yahooQuotes, ILogger<YahooService> logger)
    {
        private static readonly Action<ILogger, int, Exception?> _linesChanged = LoggerMessage.Define<int>(
            LogLevel.Information,
            new EventId(1, "LinesChanged"),
            "Changed {Lines} lines in the database");
        private static readonly Action<ILogger, Exception?> _startRequest = LoggerMessage.Define(
            LogLevel.Information,
            new EventId(2, "StartRequest"),
            "Start request");
        private static readonly Action<ILogger, TimeSpan, Exception?> _endRequest = LoggerMessage.Define<TimeSpan>(
            LogLevel.Information,
            new EventId(3, "EndRequest"),
            "End request in {Elapsed}");
        private async Task<IEnumerable<History>> GetSecuritiesAsync(IEnumerable<string> tickers, CancellationToken cancellationToken)
        {
            _startRequest(logger, null);
            var sw = Stopwatch.StartNew();
            var securities = await yahooQuotes.GetHistoryAsync(tickers, ct: cancellationToken).ConfigureAwait(false);
            sw.Stop();
            _endRequest(logger, sw.Elapsed, null);
            return securities.Values.Where(x => x.HasValue).Select(x => x.Value);
        }
        public async Task ProcessAsync(CancellationToken cancellationToken)
        {
            var tickers = await context.Tickers.ToDictionaryAsync(x => $"{x.StockTicker}.SA", cancellationToken).ConfigureAwait(false);
            var dataInDb = await context.HistoricalDataYahoos.Select(x => new { x.Ticker, x.Date }).ToDictionaryAsync(x => (x.Date, x.Ticker), cancellationToken).ConfigureAwait(false);
            var securities = await GetSecuritiesAsync(tickers.Keys, cancellationToken).ConfigureAwait(false);
            foreach (var security in securities)
            {
                if (!tickers.TryGetValue(security!.Symbol.Name, out var ticker))
                {
                    continue;
                }
                var historicalData = security.Ticks.Where(x => !dataInDb.ContainsKey((DateOnly.FromDateTime(x.Date.ToDateTimeUtc()), ticker)) && x.Volume != 0).Select(h => new HistoricalDataYahoo
                {
                    Date = DateOnly.FromDateTime(h.Date.ToDateTimeUtc()),
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
            var lines = await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            _linesChanged(logger, lines, null);
        }
    }
}