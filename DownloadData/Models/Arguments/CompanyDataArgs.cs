using Cocona;

namespace Tcc.DownloadData.Models.Arguments
{
    public sealed class CompanyDataArgs : ICommandParameterSet
    {
        private IEnumerable<string> _companies_dividends = [];
        private IEnumerable<string> _companies_splits = [];
        private IEnumerable<int> _companies = [];
        private string _dividends = string.Empty;
        private string _splits = string.Empty;
        private string _company = string.Empty;
        [Option('j', Description = "Maximum parallelism for downloading company data.")]
        [HasDefaultValue]
        public int MaxParallelism { get; set; } = Environment.ProcessorCount;
        [Option('s', Description = "Tickers to download split data.")]
        [HasDefaultValue]
        public string Splits
        {
            get
            {
                return _splits;

            }
            set
            {
                _companies_splits = value.Split(',', StringSplitOptions.RemoveEmptyEntries);
                _splits = value;
            }
        }
        [Option('d', Description = "Tickers to download dividends data.")]
        [HasDefaultValue]
        public string Dividends
        {
            get
            {
                return _dividends;
            }
            set
            {
                _companies_dividends = value.Split(',', StringSplitOptions.RemoveEmptyEntries);
                _dividends = value;
            }
        }
        [Option('c', Description = "CVM Codes to download company data.")]
        [HasDefaultValue]
        public string Company
        {
            get
            {
                return _company;
            }
            set
            {
                _companies = value.Split(',', StringSplitOptions.RemoveEmptyEntries).Select(int.Parse);
                _company = value;
            }
        }
        public IEnumerable<string> CompaniesSplits => _companies_splits;
        public IEnumerable<string> CompaniesDividends => _companies_dividends;
        public IEnumerable<int> Companies => _companies;
    }
}