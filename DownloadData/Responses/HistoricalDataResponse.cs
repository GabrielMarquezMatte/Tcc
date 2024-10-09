using System.Runtime.InteropServices;

namespace DownloadData.Responses
{
    [StructLayout(LayoutKind.Auto, CharSet = CharSet.Ansi)]
#pragma warning disable SS017 // Structs should implement Equals(), GetHashCode(), and ToString().
    public readonly ref struct HistoricalDataResponse(ReadOnlySpan<char> ticker, DateOnly date, double open, double high, double low, double average, double close, double strike, DateOnly expiration)
#pragma warning restore SS017 // Structs should implement Equals(), GetHashCode(), and ToString().
    {
        public readonly ReadOnlySpan<char> Ticker = ticker;
        public readonly DateOnly Date = date;
        public readonly double Open = open;
        public readonly double High = high;
        public readonly double Low = low;
        public readonly double Average = average;
        public readonly double Close = close;
        public readonly double Strike = strike;
        public readonly DateOnly Expiration = expiration;
    }
}