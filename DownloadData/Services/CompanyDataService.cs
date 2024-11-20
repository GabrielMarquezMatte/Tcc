using System.Runtime.CompilerServices;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using DownloadData.Data;
using DownloadData.Entities;
using DownloadData.Models.Arguments;
using DownloadData.Repositories;
using DownloadData.Responses;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using System.Runtime.InteropServices;

namespace DownloadData.Services
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
        private static readonly Action<ILogger, int, Exception?> _changesSaved = LoggerMessage.Define<int>(
            LogLevel.Information,
            new EventId(3, "ChangesSaved"),
            "{Changes} changes saved"
        );
        private async IAsyncEnumerable<Industry> SaveIndustryAsync(CompanyResponse companyResponse,
                                                                   Dictionary<string, Industry> industries,
                                                                   [EnumeratorCancellation] CancellationToken cancellationToken)
        {
            foreach (var industryName in companyResponse.IndustryClassification.Split(" / ").Distinct(StringComparer.OrdinalIgnoreCase))
            {
                if (industries.TryGetValue(industryName, out var industry))
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
            if (companies.TryGetValue(companyResponse.Cnpj, out var company))
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
        private ValueTask<EntityEntry<CompanyIndustry>> SaveCompanyIndustriesAsync(Company company, Industry industry, Dictionary<(string, string), CompanyIndustry> companyIndustries, CancellationToken cancellationToken)
        {
            var companyIndustryKey = (company.Cnpj, industry.Name);
            ref var companyIndustry = ref CollectionsMarshal.GetValueRefOrAddDefault(companyIndustries, companyIndustryKey, out var exists);
            if (exists)
            {
                return ValueTask.FromResult(stockContext.CompanyIndustries.Entry(companyIndustry!));
            }
            companyIndustry = new()
            {
                Company = company,
                Industry = industry,
            };
            return stockContext.CompanyIndustries.AddAsync(companyIndustry, cancellationToken);
        }
        private async Task SaveTickersAsync(Company company, Dictionary<string, Ticker> tickers, IEnumerable<CompanyCodes> codes, CancellationToken cancellationToken)
        {
            foreach (var otherCode in codes.Where(c => !string.IsNullOrEmpty(c.Code) && !string.IsNullOrEmpty(c.Isin)).DistinctBy(x => x.Code))
            {
                if (tickers.TryGetValue(otherCode.Code, out var ticker))
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
        private Task ProcessIndustriesAsync(Company company,
                                                  CompanyResponse companyResponse,
                                                  Dictionary<string, Industry> industriesInDb,
                                                  Dictionary<(string, string), CompanyIndustry> companyIndustriesInDb,
                                                  CancellationToken cancellationToken)
        {
            return SaveIndustryAsync(companyResponse, industriesInDb, cancellationToken).ForEachAwaitAsync(async industry => await SaveCompanyIndustriesAsync(company, industry, companyIndustriesInDb, cancellationToken).ConfigureAwait(false), cancellationToken);
        }
        private async Task ProcessSingleCompanyResponseAsync(CompanyResponse companyResponse,
                                                             Dictionary<string, Company> companiesInDb,
                                                             Dictionary<string, Ticker> tickersInDb,
                                                             Dictionary<string, Industry> industriesInDb,
                                                             Dictionary<(string, string), CompanyIndustry> companyIndustriesInDb,
                                                             CancellationToken cancellationToken)
        {
            var company = await SaveCompanyAsync(companyResponse, companiesInDb, cancellationToken).ConfigureAwait(false);
            await ProcessIndustriesAsync(company, companyResponse, industriesInDb, companyIndustriesInDb, cancellationToken).ConfigureAwait(false);
            if (companyResponse.OtherCodes == null)
            {
                _companyHasNoTickers(logger, companyResponse.CompanyName, companyResponse.Cnpj, null);
                return;
            }
            await SaveTickersAsync(company, tickersInDb, companyResponse.OtherCodes, cancellationToken).ConfigureAwait(false);
            _companyHasBeenSaved(logger, companyResponse.CompanyName, companyResponse.Cnpj, null);
        }
        private Task ProcessCompanyResponsesAsync(Dictionary<string, Company> companiesInDb,
                                                        Dictionary<string, Ticker> tickersInDb,
                                                        Dictionary<string, Industry> industriesInDb,
                                                        Dictionary<(string, string), CompanyIndustry> companyIndustriesInDb,
                                                        CompanyDataArgs companyDataArgs,
                                                        CancellationToken cancellationToken)
        {
            return companyDataRepository.GetCompaniesAsync(companyDataArgs, cancellationToken)
                                        .ForEachAwaitAsync(companyResponse => ProcessSingleCompanyResponseAsync(companyResponse, companiesInDb, tickersInDb, industriesInDb, companyIndustriesInDb, cancellationToken), cancellationToken);
        }
        public async Task SaveCompaniesAsync(CompanyDataArgs companyDataArgs, CancellationToken cancellationToken)
        {
            var companiesInDb = await stockContext.Companies.ToDictionaryAsync(c => c.Cnpj, cancellationToken).ConfigureAwait(false);
            var tickersInDb = await stockContext.Tickers.ToDictionaryAsync(t => t.StockTicker, cancellationToken).ConfigureAwait(false);
            var industriesInDb = await stockContext.Industries.ToDictionaryAsync(i => i.Name, cancellationToken).ConfigureAwait(false);
            var companyIndustriesInDb = await stockContext.CompanyIndustries.ToDictionaryAsync(ci => (ci.Company!.Cnpj, ci.Industry!.Name), cancellationToken).ConfigureAwait(false);
            await ProcessCompanyResponsesAsync(companiesInDb, tickersInDb, industriesInDb, companyIndustriesInDb, companyDataArgs, cancellationToken).ConfigureAwait(false);
            var changes = await stockContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            _changesSaved(logger, changes, null);
        }
    }
}