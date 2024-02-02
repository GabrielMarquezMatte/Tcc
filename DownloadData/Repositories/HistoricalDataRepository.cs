using System.Collections.ObjectModel;
using System.Globalization;
using System.IO.Compression;
using System.Text;
using System.Threading.Channels;
using FastEnumUtility;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Tcc.DownloadData.Data;
using Tcc.DownloadData.Entities;
using Tcc.DownloadData.Enums;
using Tcc.DownloadData.Models.Options;
using Tcc.DownloadData.Readers;

namespace Tcc.DownloadData.Repositories
{
    public sealed class HistoricalDataRepository(StockContext stockContext, HttpClient httpClient, IOptions<DownloadUrlsOptions> urlOptions, ILogger<HistoricalDataRepository> logger)
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
        private static void LogMessage(Action<ILogger, string, Exception?> action, ILogger logger, DateTime date, HistoricalType historicalType, Exception? exception = null)
        {
            action(logger, DateToStringLog(historicalType, date), exception);
        }
        private static string TypeToString(HistoricalType historicalType) => historicalType.GetLabel() ?? throw new ArgumentOutOfRangeException(nameof(historicalType), historicalType, message: null);
        private static string DateToString(HistoricalType historicalType, DateTime date) => historicalType switch
        {
            HistoricalType.Day => date.ToString("ddMMyyyy", CultureInfo.InvariantCulture),
            HistoricalType.Month => date.ToString("MMyyyy", CultureInfo.InvariantCulture),
            HistoricalType.Year => date.ToString("yyyy", CultureInfo.InvariantCulture),
            _ => throw new ArgumentOutOfRangeException(nameof(historicalType), historicalType, message: null)
        };
        private static string DateToStringLog(HistoricalType historicalType, DateTime date) => historicalType switch
        {
            HistoricalType.Day => date.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture),
            HistoricalType.Month => date.ToString("MM/yyyy", CultureInfo.InvariantCulture),
            HistoricalType.Year => date.ToString("yyyy", CultureInfo.InvariantCulture),
            _ => throw new ArgumentOutOfRangeException(nameof(historicalType), historicalType, message: null)
        };
        private Uri BuildUri(HistoricalType historicalType, DateTime date)
        {
            var url = urlOptions.Value.HistoricalData;
            StringBuilder builder = new(url.ToString());
            builder.Append(TypeToString(historicalType));
            builder.Append(DateToString(historicalType, date));
            builder.Append(".ZIP");
            return new(builder.ToString());
        }
        private async Task<ZipArchive?> DownloadAsync(HistoricalType historicalType, DateTime date, CancellationToken cancellationToken)
        {
            var uri = BuildUri(historicalType, date);
            var response = await httpClient.GetAsync(uri, cancellationToken).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                return null;
            }
            var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
            return new ZipArchive(stream, ZipArchiveMode.Read, leaveOpen: false);
        }
        private async Task ProcessSingleFileAsync(IReadOnlyDictionary<string, Ticker> tickers, HistoricalType historicalType,
                                                  DateTime date, CancellationToken cancellationToken)
        {
            LogMessage(_downloadStarted, logger, date, historicalType);
            using var zipArchive = await DownloadAsync(historicalType, date, cancellationToken).ConfigureAwait(false);
            if (zipArchive is null)
            {
                LogMessage(_downloadFailed, logger, date, historicalType);
                return;
            }
            LogMessage(_downloadSucceeded, logger, date, historicalType);
            HistoricalFileReader reader = new(zipArchive);
            var historicalData = reader.ReadAsync(cancellationToken);
            await foreach (var data in historicalData.ConfigureAwait(false).WithCancellation(cancellationToken))
            {
                if (!tickers.TryGetValue(data.Ticker, out var ticker))
                {
                    continue;
                }
                HistoricalData entity = new()
                {
                    Ticker = ticker,
                    Date = data.Date,
                    Open = data.Open,
                    High = data.High,
                    Low = data.Low,
                    Average = data.Average,
                    Close = data.Close,
                    Strike = data.Strike,
                    Expiration = data.Expiration,
                };
                await channel.Writer.WriteAsync(entity, cancellationToken).ConfigureAwait(false);
            }
            _lines += reader.Lines;
            _time += reader.Time;
            _finishedProcessing(logger, DateToStringLog(historicalType, date), reader.Lines, reader.Time, null);
        }
        private async Task ProcessFileSemaphoreAsync(SemaphoreSlim semaphore,
                                                     IReadOnlyDictionary<string, Ticker> tickers,
                                                     HistoricalType historicalType, DateTime date,
                                                     CancellationToken cancellationToken)
        {
            await semaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                await ProcessSingleFileAsync(tickers, historicalType, date, cancellationToken).ConfigureAwait(false);
            }
            finally
            {
                semaphore.Release();
            }
        }
        private async Task ProcessAllFilesAsync(IReadOnlyDictionary<string, Ticker> tickers, HistoricalType historicalType,
                                                IEnumerable<DateTime> dates, int maxDegreeOfParallelism, CancellationToken cancellationToken)
        {
            List<Task> tasks = [];
            using SemaphoreSlim semaphore = new(maxDegreeOfParallelism);
            foreach (var date in dates)
            {
                tasks.Add(ProcessFileSemaphoreAsync(semaphore, tickers, historicalType, date, cancellationToken));
            }
            await Task.WhenAll(tasks).ConfigureAwait(false);
            channel.Writer.Complete();
        }
        public async Task GetHistoricalDataAsync(IReadOnlyDictionary<string, Ticker> tickers,
                                                 ReadOnlyCollection<DateTime> dates, HistoricalType historicalType,
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
            var historicalData = await stockContext.HistoricalData.Where(x => x.Date >= startDate && x.Date <= endDate).ToDictionaryAsync(historicalData => (historicalData.Ticker!.StockTicker, historicalData.Date), cancellationToken).ConfigureAwait(false);
            var task = ProcessAllFilesAsync(tickers, historicalType, dates, maxDegreeOfParallelism, cancellationToken);
            await foreach (var data in channel.Reader.ReadAllAsync(cancellationToken).ConfigureAwait(false))
            {
                var historical = historicalData.GetValueOrDefault((data.Ticker!.StockTicker, data.Date));
                if (historical is not null)
                {
                    historical.Open = data.Open;
                    historical.High = data.High;
                    historical.Low = data.Low;
                    historical.Average = data.Average;
                    historical.Close = data.Close;
                    historical.Strike = data.Strike;
                    historical.Expiration = data.Expiration;
                    continue;
                }
                await stockContext.HistoricalData.AddAsync(data, cancellationToken).ConfigureAwait(false);
                historicalData.Add((data.Ticker!.StockTicker, data.Date), data);
            }
            await task.ConfigureAwait(false);
            _finishedProcessingAll(logger, _lines, _time, null);
        }
    }
}