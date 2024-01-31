using System.Globalization;
using System.IO.Compression;
using System.Runtime.CompilerServices;
using System.Threading.Channels;
using Tcc.DownloadData.Responses;

namespace Tcc.DownloadData.Readers
{
    public sealed class HistoricalFileReader(ZipArchive zipArchive)
    {
        private readonly Channel<HistoricalDataResponse> _channel = Channel.CreateUnbounded<HistoricalDataResponse>();
        private static HistoricalDataResponse ProcessLine(string line) => new()
        {
            Ticker = line.Substring(12, 12).Trim(),
            Date = DateTime.ParseExact(line.AsSpan(2, 8), "yyyyMMdd", CultureInfo.InvariantCulture),
            Open = double.Parse(line.AsSpan(56, 13), CultureInfo.InvariantCulture) / 100,
            High = double.Parse(line.AsSpan(69, 13), CultureInfo.InvariantCulture) / 100,
            Low = double.Parse(line.AsSpan(82, 13), CultureInfo.InvariantCulture) / 100,
            Average = double.Parse(line.AsSpan(95, 13), CultureInfo.InvariantCulture) / 100,
            Close = double.Parse(line.AsSpan(108, 13), CultureInfo.InvariantCulture) / 100,
            Strike = double.Parse(line.AsSpan(188, 13), CultureInfo.InvariantCulture) / 100,
            Expiration = DateTime.ParseExact(line.Substring(202, 8), "yyyyMMdd", CultureInfo.InvariantCulture),
        };
        private static bool CheckLine(string? line)
        {
            var startSpan = line.AsSpan();
            return line?.Length == 245 && !startSpan.StartsWith("99COTAHIST", StringComparison.Ordinal) && !startSpan.StartsWith("00COTAHIST", StringComparison.Ordinal);
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
                        yield return ProcessLine(line!);
                    }
                }
            }
        }
        private async Task ProcessZipAsync(CancellationToken cancellationToken)
        {
            foreach (var entry in zipArchive.Entries)
            {
                await foreach (var data in ProcessFileAsync(entry, cancellationToken).ConfigureAwait(false))
                {
                    await _channel.Writer.WriteAsync(data, cancellationToken).ConfigureAwait(false);
                }
            }
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