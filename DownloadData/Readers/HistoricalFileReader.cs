using System.Buffers;
using System.Diagnostics;
using System.IO.Compression;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Channels;
using CommunityToolkit.HighPerformance.Buffers;
using DownloadData.Entities;
using DownloadData.Responses;
using DownloadData.ValueObjects;

namespace DownloadData.Readers
{
    public sealed class HistoricalFileReader(ZipArchive zipArchive, IReadOnlyDictionary<TickerKey, Ticker> tickers,
                                             IDictionary<(Ticker ticker, DateOnly Date), HistoricalData> historicalData,
                                             ChannelWriter<HistoricalData> channel)
    {
        private readonly Channel<Memory<char>> fileChannel = Channel.CreateUnbounded<Memory<char>>(new UnboundedChannelOptions
        {
            SingleReader = true,
            SingleWriter = true
        });
        public int Lines { get; private set; }
        public TimeSpan Time { get; private set; }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private unsafe static DateOnly ParseDate(char* span)
        {
            return new((span[0] - '0') * 1000 + (span[1] - '0') * 100 + (span[2] - '0') * 10 + (span[3] - '0'), (span[4] - '0') * 10 + (span[5] - '0'), (span[6] - '0') * 10 + (span[7] - '0'));
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private unsafe static DateOnly ParseDate(ReadOnlySpan<char> span)
        {
            fixed (char* ptr = span)
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
            fixed (char* ptr = span)
            {
                return ParseDoubleUnsafe(ptr);
            }
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static HistoricalDataResponse ParseLine(ReadOnlySpan<char> ticker, ReadOnlySpan<char> span)
        {
            return new(ticker, ParseDate(span.Slice(2, 8)), ParseDouble(span.Slice(56, 13)),
                       ParseDouble(span.Slice(69, 13)), ParseDouble(span.Slice(82, 13)), ParseDouble(span.Slice(95, 13)),
                       ParseDouble(span.Slice(108, 13)), ParseDouble(span.Slice(188, 13)), ParseDate(span.Slice(202, 8)));
        }

        private HistoricalData? ProcessLine(ReadOnlySpan<char> span)
        {
            if (span[..3] is "99C" or "00C")
            {
                return null;
            }
            var tickerSpan = span.Slice(12, 12).TrimEnd(' ');
            if (!tickers.TryGetValue(tickerSpan, out var ticker))
            {
                return null;
            }
            var response = ParseLine(tickerSpan, span);
            if (historicalData.ContainsKey((ticker, response.Date)))
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
        private async Task ReadFileAsync(CancellationToken cancellationToken)
        {
            var stream = zipArchive.Entries[0].Open();
            await using (stream.ConfigureAwait(false))
            {
                using MemoryOwner<char> buffer = MemoryOwner<char>.Allocate(247, ArrayPool<char>.Shared, AllocationMode.Default);
                var memory = buffer.Memory;
                using StreamReader reader = new(stream, Encoding.ASCII, leaveOpen: true, bufferSize: 1024 * 1024, detectEncodingFromByteOrderMarks: false);
                var read = await reader.ReadBlockAsync(memory, cancellationToken).ConfigureAwait(false);
                while (read > 0)
                {
                    await fileChannel.Writer.WriteAsync(memory, cancellationToken).ConfigureAwait(false);
                    read = await reader.ReadBlockAsync(memory, cancellationToken).ConfigureAwait(false);
                }
                fileChannel.Writer.Complete();
            }
        }
        public async Task ProcessZipAsync(CancellationToken cancellationToken)
        {
            var stopWatch = Stopwatch.StartNew();
            var readTask = ReadFileAsync(cancellationToken);
            await foreach(var buffer in fileChannel.Reader.ReadAllAsync(cancellationToken).ConfigureAwait(false))
            {
                Lines++;
                var data = ProcessLine(buffer.Span);
                if (data is not null)
                {
                    await channel.WriteAsync(data, cancellationToken).ConfigureAwait(false);
                }
            }
            await readTask.ConfigureAwait(false);
            stopWatch.Stop();
            Time = stopWatch.Elapsed;
        }
    }
}