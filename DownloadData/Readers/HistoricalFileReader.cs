using System.Diagnostics;
using System.IO.Compression;
using System.Runtime.CompilerServices;
using System.Threading.Channels;
using Tcc.DownloadData.Responses;

namespace Tcc.DownloadData.Readers
{
    public sealed class HistoricalFileReader(ZipArchive zipArchive)
    {
        private readonly Channel<HistoricalDataResponse> _channel = Channel.CreateUnbounded<HistoricalDataResponse>();
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
        private static HistoricalDataResponse ProcessLine(ReadOnlySpan<char> span) => new()
        {
            Ticker = span.Slice(12, 12).TrimEnd().ToString(),
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
        private static async IAsyncEnumerable<HistoricalDataResponse> ProcessFileAsync(ZipArchiveEntry entry, [EnumeratorCancellation] CancellationToken cancellationToken)
        {
            var stream = entry.Open();
            await using (stream.ConfigureAwait(false))
            {
                using StreamReader reader = new(stream);
                while (!reader.EndOfStream)
                {
                    var line = await reader.ReadLineAsync(cancellationToken).ConfigureAwait(false);
                    if (CheckLine(line))
                    {
                        yield return ProcessLine(line);
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
                    Lines++;
                    await _channel.Writer.WriteAsync(data, cancellationToken).ConfigureAwait(false);
                }
            }
            stopWatch.Stop();
            Time = stopWatch.Elapsed;
            _channel.Writer.Complete();
        }
        public async IAsyncEnumerable<HistoricalDataResponse> ReadAsync([EnumeratorCancellation] CancellationToken cancellationToken)
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