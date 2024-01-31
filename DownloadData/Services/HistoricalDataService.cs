using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Tcc.DownloadData.Data;
using Tcc.DownloadData.Enums;
using Tcc.DownloadData.Repositories;

namespace Tcc.DownloadData.Services
{
    public sealed class HistoricalDataService(StockContext stockContext, HistoricalDataRepository historicalDataRepository, ILogger<HistoricalDataService> logger)
    {
        private static readonly Action<ILogger, string, Exception?> _linesChanged = LoggerMessage.Define<string>(
            LogLevel.Information,
            new EventId(1, "LinesChanged"),
            "Changed {Lines} lines in the database");
        private static IEnumerable<DateTime> GetDates(DateTime startDate, DateTime endDate, HistoricalType historicalType)
        {
            return historicalType switch
            {
                HistoricalType.Day => Enumerable.Range(0, (endDate - startDate).Days + 1).Select(offset => startDate.AddDays(offset)),
                HistoricalType.Month => Enumerable.Range(0, (endDate.Year - startDate.Year) * 12 + endDate.Month - startDate.Month + 1)
                    .Select(startDate.AddMonths),
                HistoricalType.Year => Enumerable.Range(0, endDate.Year - startDate.Year + 1).Select(startDate.AddYears),
                _ => throw new ArgumentOutOfRangeException(nameof(historicalType), historicalType, message: null)
            };
        }
        public async Task ProcessFilesAsync(DateTime? startDate, DateTime? endDate, HistoricalType historicalType,
                                            int maxParallelism = 10, CancellationToken cancellationToken = default)
        {
            var tickers = await stockContext.Tickers.ToDictionaryAsync(ticker => ticker.StockTicker, cancellationToken).ConfigureAwait(false);
            var hasData = await stockContext.HistoricalData.AnyAsync(cancellationToken: cancellationToken).ConfigureAwait(false);
            if (startDate == null)
            {
                startDate = hasData ? (await stockContext.HistoricalData.MaxAsync(historicalData => historicalData.Date, cancellationToken).ConfigureAwait(false)).AddDays(1) : new(2000, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            }
            if (endDate == null)
            {
                endDate = DateTime.Today;
            }
            var dates = GetDates(startDate.Value, endDate.Value, historicalType);
            var datesReadonlyCollection = dates.ToList().AsReadOnly();
            await historicalDataRepository.GetHistoricalDataAsync(tickers, datesReadonlyCollection, historicalType, maxParallelism, cancellationToken).ConfigureAwait(false);
            var lines = await stockContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            _linesChanged(logger, lines.ToString(CultureInfo.InvariantCulture), arg3: null);
        }
    }
}