using System.Runtime.InteropServices;

namespace DownloadData.Responses
{
    [StructLayout(LayoutKind.Auto, CharSet = CharSet.Ansi)]
    public ref struct HistoricalDataResponse
    {
        public DateTime Date;
        public ReadOnlySpan<char> Ticker;
        public double Open;
        public double High;
        public double Low;
        public double Average;
        public double Close;
        public double Strike;
        public DateTime Expiration;
    }
}