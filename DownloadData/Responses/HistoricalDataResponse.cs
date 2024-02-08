using System.Runtime.InteropServices;

namespace DownloadData.Responses
{
    [StructLayout(LayoutKind.Auto, CharSet = CharSet.Ansi)]
    public readonly ref struct HistoricalDataResponse(ReadOnlySpan<char> ticker, DateTime date, double open, double high, double low, double average, double close, double strike, DateTime expiration)
    {
        public readonly ReadOnlySpan<char> Ticker = ticker;
        public readonly DateTime Date = date;
        public readonly double Open = open;
        public readonly double High = high;
        public readonly double Low = low;
        public readonly double Average = average;
        public readonly double Close = close;
        public readonly double Strike = strike;
        public readonly DateTime Expiration = expiration;
    }
}