using Cocona;

namespace DownloadData.Models.Arguments
{
    public sealed class CompanyDataArgs : ICommandParameterSet
    {
        private IEnumerable<int> _companies = [];
        private string _company = string.Empty;
        [Option('j', Description = "Maximum parallelism for downloading company data.")]
        [HasDefaultValue]
        public int MaxParallelism { get; set; } = Environment.ProcessorCount;
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
        public IEnumerable<int> Companies => _companies;
    }
}