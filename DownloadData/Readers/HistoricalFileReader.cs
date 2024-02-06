using System.Diagnostics;
using System.IO.Compression;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Channels;
using Tcc.DownloadData.Entities;
using Tcc.DownloadData.Responses;
using Tcc.DownloadData.ValueObjects;

namespace Tcc.DownloadData.Readers
{
    public sealed class HistoricalFileReader(ZipArchive zipArchive, IReadOnlyDictionary<TickerKey, Ticker> tickers)
    {
        private readonly Channel<HistoricalData> _channel = Channel.CreateUnbounded<HistoricalData>();
        public int Lines { get; private set; }
        public TimeSpan Time { get; private set; }
        private static DateTime ParseDate(ReadOnlySpan<char> span)
        {
            return new((span[0] - '0') * 1000 + (span[1] - '0') * 100 + (span[2] - '0') * 10 + (span[3] - '0'), (span[4] - '0') * 10 + (span[5] - '0'), (span[6] - '0') * 10 + (span[7] - '0'), 0, 0, 0, DateTimeKind.Unspecified);
        }
        private static double ParseDouble(ReadOnlySpan<char> span)
        {
            long result = (span[0] - '0') * 1_000_000_000_000L +
                          (span[1] - '0') * 100_000_000_000L +
                          (span[2] - '0') * 10_000_000_000L +
                          (span[3] - '0') * 1_000_000_000L +
                          (span[4] - '0') * 100_000_000L +
                          (span[5] - '0') * 10_000_000L +
                          (span[6] - '0') * 1_000_000L +
                          (span[7] - '0') * 100_000L +
                          (span[8] - '0') * 10_000L +
                          (span[9] - '0') * 1_000L +
                          (span[10] - '0') * 100L +
                          (span[11] - '0') * 10L +
                          (span[12] - '0');
            return result * 0.01;
        }
        private static HistoricalDataResponse ParseLine(ReadOnlySpan<char> span) => new()
        {
            Ticker = span.Slice(12, 12).TrimEnd(),
            Date = ParseDate(span.Slice(2, 8)),
            Open = ParseDouble(span.Slice(56, 13)),
            High = ParseDouble(span.Slice(69, 13)),
            Low = ParseDouble(span.Slice(82, 13)),
            Average = ParseDouble(span.Slice(95, 13)),
            Close = ParseDouble(span.Slice(108, 13)),
            Strike = ParseDouble(span.Slice(188, 13)),
            Expiration = ParseDate(span.Slice(202, 8)),
        };
        private static bool CheckLine(ReadOnlySpan<char> span)
        {
            return span.Length == 245 && !span.StartsWith("99C", StringComparison.Ordinal) && !span.StartsWith("00C", StringComparison.Ordinal);
        }
        private HistoricalData? ProcessLine(ReadOnlySpan<char> span)
        {
            if (!CheckLine(span))
            {
                return null;
            }
            var response = ParseLine(span);
            if (!tickers.TryGetValue(response.Ticker, out var ticker))
            {
                return null;
            }
            return new()
            {
                Ticker = ticker,
                Date = response.Date,
                Open = response.Open,
                High = response.High,
                Low = response.Low,
                Average = response.Average,
                Close = response.Close,
                Strike = response.Strike,
                Expiration = response.Expiration,
            };
        }
        private async IAsyncEnumerable<HistoricalData> ProcessFileAsync(ZipArchiveEntry entry, [EnumeratorCancellation] CancellationToken cancellationToken)
        {
            var stream = entry.Open();
            await using (stream.ConfigureAwait(false))
            {
                using StreamReader reader = new(stream, Encoding.UTF8, leaveOpen: true, bufferSize: 1024 * 1024, detectEncodingFromByteOrderMarks: false);
                while (!reader.EndOfStream)
                {
                    var line = await reader.ReadLineAsync(cancellationToken).ConfigureAwait(false);
                    if (line is null)
                    {
                        continue;
                    }
                    Lines++;
                    var data = ProcessLine(line);
                    if (data is not null)
                    {
                        yield return data;
                    }
                }
            }
        }
        private async Task ProcessZipAsync(CancellationToken cancellationToken)
        {
            var stopWatch = Stopwatch.StartNew();
            foreach (var entry in zipArchive.Entries)
            {
                await foreach (var data in ProcessFileAsync(entry, cancellationToken).ConfigureAwait(false).WithCancellation(cancellationToken))
                {
                    await _channel.Writer.WriteAsync(data, cancellationToken).ConfigureAwait(false);
                }
            }
            stopWatch.Stop();
            Time = stopWatch.Elapsed;
            _channel.Writer.Complete();
        }
        public async IAsyncEnumerable<HistoricalData> ReadAsync([EnumeratorCancellation] CancellationToken cancellationToken)
        {
            var task = ProcessZipAsync(cancellationToken);
            await foreach (var data in _channel.Reader.ReadAllAsync(cancellationToken).ConfigureAwait(false))
            {
                yield return data;
            }
            await task.ConfigureAwait(false);
        }
    }
}