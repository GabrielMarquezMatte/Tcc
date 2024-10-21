using System.Buffers;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.IO.Compression;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Channels;
using CommunityToolkit.HighPerformance.Buffers;
using DownloadData.Entities;
using DownloadData.Responses;

namespace DownloadData.Readers
{
    public sealed class HistoricalFileReader(ZipArchive zipArchive, SpanLookup tickers,
                                             Dictionary<(Ticker ticker, DateOnly Date), HistoricalData> historicalData,
                                             ChannelWriter<HistoricalData> channel)
    {
        public int Lines { get; private set; }
        public TimeSpan Time { get; private set; }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static DateOnly ParseDate(ReadOnlySpan<char> span)
        {
            return new((span[0] - '0') * 1000 + (span[1] - '0') * 100 + (span[2] - '0') * 10 + (span[3] - '0'), (span[4] - '0') * 10 + (span[5] - '0'), (span[6] - '0') * 10 + (span[7] - '0'));
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static double ParseDouble(ReadOnlySpan<char> span)
        {
            var result = (span[0] - '0') * 1_000_000_000_000L +
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
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static HistoricalDataResponse ParseLine(ReadOnlySpan<char> ticker, ReadOnlySpan<char> span)
        {
            return new(ticker, ParseDate(span.Slice(2, 8)), ParseDouble(span.Slice(56, 13)),
                       ParseDouble(span.Slice(69, 13)), ParseDouble(span.Slice(82, 13)), ParseDouble(span.Slice(95, 13)),
                       ParseDouble(span.Slice(108, 13)), ParseDouble(span.Slice(188, 13)), ParseDate(span.Slice(202, 8)));
        }

        private bool TryProcessLine(ReadOnlySpan<char> span, [NotNullWhen(true)] out HistoricalData? data)
        {
            data = null;
            if (span[..3] is "99C" or "00C")
            {
                return false;
            }
            var tickerSpan = span.Slice(12, 12).TrimEnd(' ');
            if (!tickers.TryGetValue(tickerSpan, out var ticker))
            {
                return false;
            }
            var response = ParseLine(tickerSpan, span);
            var key = (ticker, response.Date);
            ref var newData = ref CollectionsMarshal.GetValueRefOrAddDefault(historicalData, key, out var exists);
            if (exists)
            {
                return false;
            }
            newData = new()
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
            data = newData;
            return true;
        }
        public async ValueTask ProcessZipAsync(CancellationToken cancellationToken)
        {
            var stopWatch = Stopwatch.StartNew();
            var stream = zipArchive.Entries[0].Open();
            await using (stream.ConfigureAwait(false))
            {
                using MemoryOwner<char> buffer = MemoryOwner<char>.Allocate(247, ArrayPool<char>.Shared, AllocationMode.Default);
                var memory = buffer.Memory;
                using StreamReader reader = new(stream, Encoding.ASCII, detectEncodingFromByteOrderMarks: false, bufferSize: 1024 * 1024, leaveOpen: true);
                var read = await reader.ReadBlockAsync(memory, cancellationToken).ConfigureAwait(false);
                while (read > 0)
                {
                    Lines++;
                    if (TryProcessLine(buffer.Span, out var data))
                    {
                        await channel.WriteAsync(data, cancellationToken).ConfigureAwait(false);
                    }
                    read = await reader.ReadBlockAsync(memory, cancellationToken).ConfigureAwait(false);
                }
            }
            stopWatch.Stop();
            Time = stopWatch.Elapsed;
        }
    }
}