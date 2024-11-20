using Microsoft.EntityFrameworkCore;
using DownloadData.Data;
using DownloadData.Enums;
using DownloadData.Repositories;
using DownloadData.Models.Arguments;

namespace DownloadData.Services
{
    public sealed class HistoricalDataService(StockContext stockContext, HistoricalDataRepository historicalDataRepository)
    {
        private static IEnumerable<DateOnly> GetDates(DateOnly startDate, DateOnly endDate, HistoricalType historicalType)
        {
            return historicalType switch
            {
                HistoricalType.Day => Enumerable.Range(0, endDate.DayNumber - startDate.DayNumber + 1).Select(startDate.AddDays).Where(date => date.DayOfWeek is not DayOfWeek.Saturday and not DayOfWeek.Sunday),
                HistoricalType.Month => Enumerable.Range(0, (endDate.Year - startDate.Year) * 12 + endDate.Month - startDate.Month + 1)
                    .Select(startDate.AddMonths),
                HistoricalType.Year => Enumerable.Range(0, endDate.Year - startDate.Year + 1).Select(startDate.AddYears),
                _ => throw new ArgumentOutOfRangeException(nameof(historicalType), historicalType, message: null)
            };
        }
        public async Task ProcessFilesAsync(HistoricalDataArgs historicalDataArgs, CancellationToken cancellationToken)
        {
            var tickers = await stockContext.Tickers.ToDictionaryAsync(ticker => ticker.StockTicker, cancellationToken).ConfigureAwait(false);
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
            await historicalDataRepository.GetHistoricalDataAsync(tickers.GetAlternateLookup<ReadOnlySpan<char>>(), datesReadonlyCollection, historicalDataArgs.HistoricalType, historicalDataArgs.MaxParallelism, cancellationToken).ConfigureAwait(false);
            await stockContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
    }
}