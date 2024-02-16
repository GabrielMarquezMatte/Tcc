using System.Collections.ObjectModel;
using System.Globalization;
using System.IO.Compression;
using System.Text;
using System.Threading.Channels;
using FastEnumUtility;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using DownloadData.Data;
using DownloadData.Entities;
using DownloadData.Enums;
using DownloadData.Models.Options;
using DownloadData.Readers;
using DownloadData.ValueObjects;

namespace DownloadData.Repositories
{
    public sealed class HistoricalDataRepository(StockContext stockContext, HttpClient httpClient,
                                                 IOptions<DownloadUrlsOptions> urlOptions,
                                                 ILogger<HistoricalDataRepository> logger)
    {
        private readonly Channel<HistoricalData> channel = Channel.CreateUnbounded<HistoricalData>();
        private static readonly Action<ILogger, string, Exception?> _downloadStarted = LoggerMessage.Define<string>(
            LogLevel.Information,
            new EventId(0, "DownloadStarted"),
            "Started downloading historical data on {Date}");
        private static readonly Action<ILogger, string, Exception?> _downloadFailed = LoggerMessage.Define<string>(
            LogLevel.Warning,
            new EventId(1, "DownloadFailed"),
            "Failed to download historical data for on {Date}");
        private static readonly Action<ILogger, string, Exception?> _downloadSucceeded = LoggerMessage.Define<string>(
            LogLevel.Information,
            new EventId(2, "DownloadSucceeded"),
            "Downloaded historical data on {Date}");
        private static readonly Action<ILogger, string, int, TimeSpan, Exception?> _finishedProcessing = LoggerMessage.Define<string, int, TimeSpan>(
            LogLevel.Information,
            new EventId(3, "FinishedProcessing"),
            "Finished processing historical data on {Date}. Processed {Lines} lines in {Time}");
        private static readonly Action<ILogger, int, TimeSpan, Exception?> _finishedProcessingAll = LoggerMessage.Define<int, TimeSpan>(
            LogLevel.Information,
            new EventId(4, "FinishedProcessingAll"),
            "Finished processing all historical data. Processed {Lines} lines in {Time}");
        private int _lines;
        private TimeSpan _time;
        private static void LogMessage(Action<ILogger, string, Exception?> action, ILogger logger, DateOnly date, HistoricalType historicalType, Exception? exception = null)
        {
            action(logger, DateToStringLog(historicalType, date), exception);
        }
        private static string TypeToString(HistoricalType historicalType) => historicalType.GetLabel() ?? throw new ArgumentOutOfRangeException(nameof(historicalType), historicalType, message: null);
        private static string DateToString(HistoricalType historicalType, DateOnly date) => historicalType switch
        {
            HistoricalType.Day => date.ToString("ddMMyyyy", CultureInfo.InvariantCulture),
            HistoricalType.Month => date.ToString("MMyyyy", CultureInfo.InvariantCulture),
            HistoricalType.Year => date.ToString("yyyy", CultureInfo.InvariantCulture),
            _ => throw new ArgumentOutOfRangeException(nameof(historicalType), historicalType, message: null)
        };
        private static string DateToStringLog(HistoricalType historicalType, DateOnly date) => historicalType switch
        {
            HistoricalType.Day => date.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture),
            HistoricalType.Month => date.ToString("MM/yyyy", CultureInfo.InvariantCulture),
            HistoricalType.Year => date.ToString("yyyy", CultureInfo.InvariantCulture),
            _ => throw new ArgumentOutOfRangeException(nameof(historicalType), historicalType, message: null)
        };
        private Uri BuildUri(HistoricalType historicalType, DateOnly date)
        {
            var url = urlOptions.Value.HistoricalData;
            StringBuilder builder = new(url.ToString());
            builder.Append(TypeToString(historicalType));
            builder.Append(DateToString(historicalType, date));
            builder.Append(".ZIP");
            return new(builder.ToString());
        }
        private async Task<ZipArchive?> DownloadAsync(SemaphoreSlim semaphore, HistoricalType historicalType, DateOnly date, CancellationToken cancellationToken)
        {
            var uri = BuildUri(historicalType, date);
            await semaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                LogMessage(_downloadStarted, logger, date, historicalType);
#pragma warning disable IDISP001 // Dispose created
                var response = await httpClient.GetAsync(uri, cancellationToken).ConfigureAwait(false);
#pragma warning restore IDISP001 // Dispose created
                if (!response.IsSuccessStatusCode)
                {
                    LogMessage(_downloadFailed, logger, date, historicalType);
                    return null;
                }
                LogMessage(_downloadSucceeded, logger, date, historicalType);
#pragma warning disable IDISP001 // Dispose created
                var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
#pragma warning restore IDISP001 // Dispose created
                return new(stream, ZipArchiveMode.Read, leaveOpen: false);
            }
            finally
            {
                semaphore.Release();
            }
        }
        private async Task ProcessSingleFileAsync(SemaphoreSlim semaphore,
                                                  IReadOnlyDictionary<TickerKey, Ticker> tickers,
                                                  Dictionary<(Ticker ticker, DateOnly Date), HistoricalData> historicalDataDict,
                                                  HistoricalType historicalType, DateOnly date,
                                                  CancellationToken cancellationToken)
        {
            using var zipArchive = await DownloadAsync(semaphore, historicalType, date, cancellationToken).ConfigureAwait(false);
            if (zipArchive is null)
            {
                return;
            }
            HistoricalFileReader reader = new(zipArchive, tickers, historicalDataDict, channel.Writer);
            await reader.ProcessZipAsync(cancellationToken).ConfigureAwait(false);
            _lines += reader.Lines;
            _time += reader.Time;
            _finishedProcessing(logger, DateToStringLog(historicalType, date), reader.Lines, reader.Time, null);
        }
        private async Task ProcessAllFilesAsync(IReadOnlyDictionary<TickerKey, Ticker> tickers,
                                                Dictionary<(Ticker ticker, DateOnly Date), HistoricalData> historicalDataDict,
                                                HistoricalType historicalType,
                                                IEnumerable<DateOnly> dates, int maxDegreeOfParallelism,
                                                CancellationToken cancellationToken)
        {
            List<Task> tasks = [];
            using SemaphoreSlim semaphore = new(maxDegreeOfParallelism);
            try
            {
                foreach (var date in dates)
                {
                    tasks.Add(ProcessSingleFileAsync(semaphore, tickers, historicalDataDict, historicalType, date, cancellationToken));
                }
                await Task.WhenAll(tasks).ConfigureAwait(false);
            }
            finally
            {
                channel.Writer.Complete();
            }
        }
        public async Task GetHistoricalDataAsync(IReadOnlyDictionary<TickerKey, Ticker> tickers,
                                                 ReadOnlyCollection<DateOnly> dates, HistoricalType historicalType,
                                                 int maxDegreeOfParallelism, CancellationToken cancellationToken)
        {
            var startDate = dates.Min();
            var endDate = dates.Max();
            switch (historicalType)
            {
                case HistoricalType.Month:
                    startDate = startDate.AddDays(-startDate.Day + 1);
                    endDate = endDate.AddDays(-endDate.Day + 1).AddMonths(1).AddDays(-1);
                    break;
                case HistoricalType.Year:
                    startDate = startDate.AddDays(-startDate.DayOfYear + 1);
                    endDate = endDate.AddDays(-endDate.DayOfYear + 1).AddYears(1).AddDays(-1);
                    break;
            }
            var historicalData = await stockContext.HistoricalData.Where(x => x.Date >= startDate && x.Date <= endDate)
                                                                  .ToDictionaryAsync(historicalData => (historicalData.Ticker!, historicalData.Date), cancellationToken)
                                                                  .ConfigureAwait(false);
            var task = ProcessAllFilesAsync(tickers, historicalData, historicalType, dates, maxDegreeOfParallelism, cancellationToken);
            await foreach (var data in channel.Reader.ReadAllAsync(cancellationToken).ConfigureAwait(false))
            {
                await stockContext.HistoricalData.AddAsync(data, cancellationToken).ConfigureAwait(false);
            }
            await task.ConfigureAwait(false);
            _finishedProcessingAll(logger, _lines, _time, null);
        }
    }
}