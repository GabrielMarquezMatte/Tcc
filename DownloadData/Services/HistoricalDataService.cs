using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using DownloadData.Data;
using DownloadData.Enums;
using DownloadData.Repositories;
using DownloadData.ValueObjects;
using DownloadData.Models.Arguments;

namespace DownloadData.Services
{
    public sealed class HistoricalDataService(StockContext stockContext, HistoricalDataRepository historicalDataRepository, ILogger<HistoricalDataService> logger)
    {
        private static readonly Action<ILogger, string, Exception?> _linesChanged = LoggerMessage.Define<string>(
            LogLevel.Information,
            new EventId(1, "LinesChanged"),
            "Changed {Lines} lines in the database");
        private static IEnumerable<DateOnly> GetDates(DateOnly startDate, DateOnly endDate, HistoricalType historicalType)
        {
            return historicalType switch
            {
                HistoricalType.Day => Enumerable.Range(0, endDate.DayNumber - startDate.DayNumber + 1).Select(startDate.AddDays).Where(date => date.DayOfWeek != DayOfWeek.Saturday && date.DayOfWeek != DayOfWeek.Sunday),
                HistoricalType.Month => Enumerable.Range(0, (endDate.Year - startDate.Year) * 12 + endDate.Month - startDate.Month + 1)
                    .Select(startDate.AddMonths),
                HistoricalType.Year => Enumerable.Range(0, endDate.Year - startDate.Year + 1).Select(startDate.AddYears),
                _ => throw new ArgumentOutOfRangeException(nameof(historicalType), historicalType, message: null)
            };
        }
        public async Task ProcessFilesAsync(HistoricalDataArgs historicalDataArgs, CancellationToken cancellationToken)
        {
            var tickers = await stockContext.Tickers.ToDictionaryAsync(ticker => new TickerKey(ticker.StockTicker), cancellationToken).ConfigureAwait(false);
            var hasData = await stockContext.HistoricalData.AnyAsync(cancellationToken: cancellationToken).ConfigureAwait(false);
            if (historicalDataArgs.StartDate == null)
            {
                historicalDataArgs.StartDate = hasData ? (await stockContext.HistoricalData.MaxAsync(historicalData => historicalData.Date, cancellationToken).ConfigureAwait(false)).AddDays(1) : new(2000, 1, 1);
            }
            var dates = GetDates(historicalDataArgs.StartDate.Value, historicalDataArgs.EndDate, historicalDataArgs.HistoricalType);
            var datesReadonlyCollection = dates.ToList().AsReadOnly();
            if (datesReadonlyCollection.Count == 0)
            {
                return;
            }
            await historicalDataRepository.GetHistoricalDataAsync(tickers, datesReadonlyCollection, historicalDataArgs.HistoricalType, historicalDataArgs.MaxParallelism, cancellationToken).ConfigureAwait(false);
            var lines = await stockContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            _linesChanged(logger, lines.ToString(CultureInfo.InvariantCulture), arg3: null);
        }
    }
}