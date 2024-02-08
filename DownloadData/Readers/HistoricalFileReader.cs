using System.Diagnostics;
using System.IO.Compression;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Channels;
using DownloadData.Entities;
using DownloadData.Responses;
using DownloadData.ValueObjects;

namespace DownloadData.Readers
{
    public sealed class HistoricalFileReader(ZipArchive zipArchive, IReadOnlyDictionary<TickerKey, Ticker> tickers)
    {
        private readonly Channel<HistoricalData> _channel = Channel.CreateUnbounded<HistoricalData>();
        public int Lines { get; private set; }
        public TimeSpan Time { get; private set; }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private unsafe static DateTime ParseDate(char* span)
        {
            return new((span[0] - '0') * 1000 + (span[1] - '0') * 100 + (span[2] - '0') * 10 + (span[3] - '0'), (span[4] - '0') * 10 + (span[5] - '0'), (span[6] - '0') * 10 + (span[7] - '0'), 0, 0, 0, DateTimeKind.Unspecified);
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private unsafe static DateTime ParseDate(ReadOnlySpan<char> span)
        {
            fixed(char* ptr = span)
            {
                return ParseDate(ptr);
            }
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static unsafe double ParseDoubleUnsafe(char* ptr)
        {
            var result = (ptr[0] - '0') * 1_000_000_000_000L +
                        (ptr[1] - '0') * 100_000_000_000L +
                        (ptr[2] - '0') * 10_000_000_000L +
                        (ptr[3] - '0') * 1_000_000_000L +
                        (ptr[4] - '0') * 100_000_000L +
                        (ptr[5] - '0') * 10_000_000L +
                        (ptr[6] - '0') * 1_000_000L +
                        (ptr[7] - '0') * 100_000L +
                        (ptr[8] - '0') * 10_000L +
                        (ptr[9] - '0') * 1_000L +
                        (ptr[10] - '0') * 100L +
                        (ptr[11] - '0') * 10L +
                        (ptr[12] - '0');
            return result * 0.01;
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private unsafe static double ParseDouble(ReadOnlySpan<char> span)
        {
            fixed(char* ptr = span)
            {
                return ParseDoubleUnsafe(ptr);
            }
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static HistoricalDataResponse ParseLine(ReadOnlySpan<char> span) => new(span.Slice(12, 12).TrimEnd(' '),
                                                                                        ParseDate(span.Slice(2, 8)),
                                                                                        ParseDouble(span.Slice(56, 13)),
                                                                                        ParseDouble(span.Slice(69, 13)),
                                                                                        ParseDouble(span.Slice(82, 13)),
                                                                                        ParseDouble(span.Slice(95, 13)),
                                                                                        ParseDouble(span.Slice(108, 13)),
                                                                                        ParseDouble(span.Slice(188, 13)),
                                                                                        ParseDate(span.Slice(202, 8)));
        private HistoricalData? ProcessLine(ReadOnlySpan<char> span)
        {
            if (span[..3] is "99C" or "00C")
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
        private async Task ProcessFileAsync(ZipArchiveEntry entry, CancellationToken cancellationToken)
        {
            var stream = entry.Open();
            await using (stream.ConfigureAwait(false))
            {
                var buffer = new char[247];
                var memory = buffer.AsMemory();
                using StreamReader reader = new(stream, Encoding.ASCII, leaveOpen: true, bufferSize: 1024 * 1024, detectEncodingFromByteOrderMarks: false);
                var read = await reader.ReadBlockAsync(memory, cancellationToken).ConfigureAwait(false);
                while (read > 0)
                {
                    Lines++;
                    var data = ProcessLine(buffer);
                    if (data is not null)
                    {
                        await _channel.Writer.WriteAsync(data, cancellationToken).ConfigureAwait(false);
                    }
                    read = await reader.ReadBlockAsync(memory, cancellationToken).ConfigureAwait(false);
                }
            }
        }
        private async Task ProcessZipAsync(CancellationToken cancellationToken)
        {
            var stopWatch = Stopwatch.StartNew();
            await ProcessFileAsync(zipArchive.Entries[0], cancellationToken).ConfigureAwait(false);
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