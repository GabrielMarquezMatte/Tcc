using System.Diagnostics;
using System.Globalization;
using System.IO.Compression;
using System.Text;
using System.Threading.Channels;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Tcc.DownloadData.Data;
using Tcc.DownloadData.Entities;
using Tcc.DownloadData.Enums;
using Tcc.DownloadData.Models.Options;
using Tcc.DownloadData.Readers;

namespace Tcc.DownloadData.Repositories
{
    public sealed class HistoricalDataRepository(StockContext stockContext, HttpClient httpClient, IOptions<DownloadUrlsOptions> urlOptions)
    {
        private readonly Channel<HistoricalData> channel = Channel.CreateUnbounded<HistoricalData>();
        private static string TypeToString(HistoricalType historicalType) => historicalType switch
        {
            HistoricalType.Day => "D",
            HistoricalType.Month => "M",
            HistoricalType.Year => "A",
            _ => throw new ArgumentOutOfRangeException(nameof(historicalType), historicalType, message: null)
        };
        private static string DateToString(HistoricalType historicalType, DateTime date) => historicalType switch
        {
            HistoricalType.Day => date.ToString("ddMMyyyy", CultureInfo.InvariantCulture),
            HistoricalType.Month => date.ToString("MMyyyy", CultureInfo.InvariantCulture),
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
            using var zipArchive = await DownloadAsync(historicalType, date, cancellationToken).ConfigureAwait(false);
            if (zipArchive is null)
            {
                return;
            }
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
                                                 IEnumerable<DateTime> dates, HistoricalType historicalType,
                                                 int maxDegreeOfParallelism = 10,
                                                 CancellationToken cancellationToken = default)
        {
            var historicalData = await stockContext.HistoricalData.Where(historicalData => dates.Contains(historicalData.Date)).ToDictionaryAsync(historicalData => (historicalData.Ticker!.StockTicker, historicalData.Date), cancellationToken).ConfigureAwait(false);
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
        }
    }
}