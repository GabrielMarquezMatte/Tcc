using System.Diagnostics;
using System.Runtime.CompilerServices;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Tcc.DownloadData.Data;
using Tcc.DownloadData.Entities;
using Tcc.DownloadData.Repositories;
using Tcc.DownloadData.Responses;

namespace Tcc.DownloadData.Services
{
    public sealed class CompanyDataService(StockContext stockContext, CompanyDataRepository companyDataRepository, ILogger<CompanyDataService> logger)
    {
        private static readonly Action<ILogger, string, string, Exception?> _companyHasNoTickers = LoggerMessage.Define<string, string>(
            LogLevel.Warning,
            new EventId(1, "CompanyHasNoTickers"),
            "Company {Name}({Cnpj}) has no tickers"
        );
        private static readonly Action<ILogger, string, string, Exception?> _companyHasBeenSaved = LoggerMessage.Define<string, string>(
            LogLevel.Information,
            new EventId(2, "CompanyHasBeenSaved"),
            "Company {Name}({Cnpj}) has been saved"
        );
        private static readonly Action<ILogger, string, Exception?> _logTimeSpent = LoggerMessage.Define<string>(
            LogLevel.Information,
            new EventId(3, "LogTimeSpent"),
            "SaveCompaniesAsync took {Elapsed}"
        );
        private async IAsyncEnumerable<Industry> SaveIndustryAsync(CompanyResponse companyResponse,
                                                                   Dictionary<string, Industry> industries,
                                                                   [EnumeratorCancellation] CancellationToken cancellationToken)
        {
            var industrySplit = companyResponse.IndustryClassification.Split(" / ").Distinct(StringComparer.OrdinalIgnoreCase);
            foreach (var industryName in industrySplit)
            {
                var industry = industries.GetValueOrDefault(industryName);
                if (industry != null)
                {
                    yield return industry;
                    continue;
                }
                industry = new()
                {
                    Name = industryName,
                };
                await stockContext.Industries.AddAsync(industry, cancellationToken).ConfigureAwait(false);
                industries[industry.Name] = industry;
                yield return industry;
            }
        }
        private async Task<Company> SaveCompanyAsync(CompanyResponse companyResponse, Dictionary<string, Company> companies, CancellationToken cancellationToken)
        {
            var company = companies.GetValueOrDefault(companyResponse.Cnpj);
            if (company != null)
            {
                return company;
            }
            company = new()
            {
                Cnpj = companyResponse.Cnpj,
                Name = companyResponse.CompanyName,
                CvmCode = companyResponse.CodeCvm,
                HasBdrs = companyResponse.HasBdrs,
                HasEmissions = companyResponse.HasEmissions,
            };
            await stockContext.Companies.AddAsync(company, cancellationToken).ConfigureAwait(false);
            companies[company.Cnpj] = company;
            return company;
        }
        private async Task SaveCompanyIndustriesAsync(Company company, Industry industry, Dictionary<(string, string), CompanyIndustry> companyIndustries, CancellationToken cancellationToken)
        {
            var companyIndustry = companyIndustries.GetValueOrDefault((company.Cnpj, industry.Name));
            if (companyIndustry != null)
            {
                return;
            }
            companyIndustry = new()
            {
                Company = company,
                Industry = industry,
            };
            await stockContext.CompanyIndustries.AddAsync(companyIndustry, cancellationToken).ConfigureAwait(false);
            companyIndustries[(company.Cnpj, industry.Name)] = companyIndustry;
        }
        private async Task SaveTickersAsync(Company company, Dictionary<string, Ticker> tickers, IEnumerable<CompanyCodes> codes, CancellationToken cancellationToken)
        {
            foreach (var otherCode in codes.Where(c => !string.IsNullOrEmpty(c.Code) && !string.IsNullOrEmpty(c.Isin)).DistinctBy(x => x.Code))
            {
                var ticker = tickers.GetValueOrDefault(otherCode.Code);
                if (ticker != null)
                {
                    continue;
                }
                ticker = new()
                {
                    Isin = otherCode.Isin,
                    StockTicker = otherCode.Code,
                    Company = company,
                };
                await stockContext.Tickers.AddAsync(ticker, cancellationToken).ConfigureAwait(false);
                tickers[otherCode.Code] = ticker;
            }
        }
        private async Task ProcessIndustriesAsync(Company company,
                                                  CompanyResponse companyResponse,
                                                  Dictionary<string, Industry> industriesInDb,
                                                  Dictionary<(string, string), CompanyIndustry> companyIndustriesInDb,
                                                  CancellationToken cancellationToken)
        {
            await foreach (var industry in SaveIndustryAsync(companyResponse, industriesInDb, cancellationToken).ConfigureAwait(false))
            {
                await SaveCompanyIndustriesAsync(company, industry, companyIndustriesInDb, cancellationToken).ConfigureAwait(false);
            }
        }
        private async Task ProcessCompanyResponsesAsync(Dictionary<string, Company> companiesInDb,
                                                        Dictionary<string, Ticker> tickersInDb,
                                                        Dictionary<string, Industry> industriesInDb,
                                                        Dictionary<(string, string), CompanyIndustry> companyIndustriesInDb,
                                                        int maxParallelism = 10,
                                                        CancellationToken cancellationToken = default)
        {
            await foreach (var companyResponse in companyDataRepository.GetCompaniesAsync(maxParallelism, cancellationToken).ConfigureAwait(false).WithCancellation(cancellationToken))
            {
                var company = await SaveCompanyAsync(companyResponse, companiesInDb, cancellationToken).ConfigureAwait(false);
                await ProcessIndustriesAsync(company, companyResponse, industriesInDb, companyIndustriesInDb, cancellationToken).ConfigureAwait(false);

                if (companyResponse.OtherCodes == null)
                {
                    _companyHasNoTickers(logger, companyResponse.CompanyName, companyResponse.Cnpj, arg4: null);
                    continue;
                }

                await SaveTickersAsync(company, tickersInDb, companyResponse.OtherCodes, cancellationToken).ConfigureAwait(false);
                _companyHasBeenSaved(logger, companyResponse.CompanyName, companyResponse.Cnpj, arg4: null);
            }
        }
        public async Task SaveCompaniesAsync(int maxParallelism = 10, CancellationToken cancellationToken = default)
        {
            var stopwatch = Stopwatch.StartNew();
            var companiesInDb = await stockContext.Companies.ToDictionaryAsync(c => c.Cnpj, cancellationToken).ConfigureAwait(false);
            var tickersInDb = await stockContext.Tickers.ToDictionaryAsync(t => t.StockTicker, cancellationToken).ConfigureAwait(false);
            var industriesInDb = await stockContext.Industries.ToDictionaryAsync(i => i.Name, cancellationToken).ConfigureAwait(false);
            var companyIndustriesInDb = await stockContext.CompanyIndustries.ToDictionaryAsync(ci => (ci.Company!.Cnpj, ci.Industry!.Name), cancellationToken).ConfigureAwait(false);
            await ProcessCompanyResponsesAsync(companiesInDb, tickersInDb, industriesInDb, companyIndustriesInDb, maxParallelism, cancellationToken).ConfigureAwait(false);
            await stockContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            stopwatch.Stop();
            _logTimeSpent(logger, stopwatch.Elapsed.ToString(), arg3: null);
        }
    }
}