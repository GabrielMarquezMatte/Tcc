using System.Runtime.CompilerServices;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using DownloadData.Data;
using DownloadData.Entities;
using DownloadData.Models.Arguments;
using DownloadData.Repositories;
using DownloadData.Responses;
using DownloadData.Responses.Dividends;
using DownloadData.Responses.SplitSubscription;

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
                if(industries.TryGetValue(industryName, out var industry))
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
            if(companies.TryGetValue(companyResponse.Cnpj, out var company))
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
            var companyIndustryKey = (company.Cnpj, industry.Name);
            if(companyIndustries.ContainsKey(companyIndustryKey))
            {
                return;
            }
            CompanyIndustry companyIndustry = new()
            {
                Company = company,
                Industry = industry,
            };
            await stockContext.CompanyIndustries.AddAsync(companyIndustry, cancellationToken).ConfigureAwait(false);
            companyIndustries[(company.Cnpj, industry.Name)] = companyIndustry;
        }
        private async IAsyncEnumerable<Ticker> SaveTickersAsync(Company company, Dictionary<string, Ticker> tickers, IEnumerable<CompanyCodes> codes, [EnumeratorCancellation] CancellationToken cancellationToken)
        {
            foreach (var otherCode in codes.Where(c => !string.IsNullOrEmpty(c.Code) && !string.IsNullOrEmpty(c.Isin)).DistinctBy(x => x.Code))
            {
                if(tickers.TryGetValue(otherCode.Code, out var ticker))
                {
                    yield return ticker;
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
                yield return ticker;
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
        private async Task ProcessSplitsAsync(Ticker ticker, IEnumerable<SplitsResponse> splits,
                                              Dictionary<(Ticker, DateOnly), Split> splitsInDb,
                                              CancellationToken cancellationToken)
        {
            foreach (var split in splits)
            {
                if (!string.Equals(split.AssetIssued, ticker.Isin, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }
                var splitKey = (ticker, split.LastDate);
                if (splitsInDb.ContainsKey(splitKey))
                {
                    continue;
                }
                Split splitEntity = new()
                {
                    Ticker = ticker,
                    LastDate = split.LastDate,
                    SplitFactor = split.SplitFactor,
                    ApprovalDate = split.ApprovalDate,
                    Type = split.Type,
                };
                await stockContext.Splits.AddAsync(splitEntity, cancellationToken).ConfigureAwait(false);
                splitsInDb[splitKey] = splitEntity;
            }
        }
        private async Task ProcessDividendsAsync(Ticker ticker, IEnumerable<DividendsResult> dividends,
                                                 Dictionary<(Ticker, DateOnly), Dividend> dividendsInDb,
                                                 CancellationToken cancellationToken)
        {
            foreach (var dividend in dividends)
            {
                if (!string.Equals(dividend.StockTicker, ticker.StockTicker, StringComparison.OrdinalIgnoreCase) || dividend.ApprovalDate == null || dividend.PriorExDate == null)
                {
                    continue;
                }
                var dividendKey = (ticker, dividend.ApprovalDate.Value);
                if (dividendsInDb.ContainsKey(dividendKey))
                {
                    continue;
                }
                Dividend dividendEntity = new()
                {
                    Ticker = ticker,
                    ApprovalDate = dividend.ApprovalDate.Value,
                    PriorExDate = dividend.PriorExDate.Value,
                    ClosePrice = dividend.ClosePrice,
                    DividendType = dividend.DividendType,
                    DividendsPercentage = dividend.DividendsPercentage,
                    DividendsValue = dividend.DividendsValue,
                };
                await stockContext.Dividends.AddAsync(dividendEntity, cancellationToken).ConfigureAwait(false);
                dividendsInDb[dividendKey] = dividendEntity;
            }
        }
        private async Task ProcessSubscriptionsAsync(Ticker ticker, IEnumerable<SubscriptionResponse> subscriptions,
                                                    Dictionary<(Ticker, DateOnly), Subscription> subscriptionsInDb,
                                                    CancellationToken cancellationToken)
        {
            foreach (var subscription in subscriptions)
            {
                if (!string.Equals(subscription.AssetIssued, ticker.Isin, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }
                var subscriptionKey = (ticker, subscription.LastDate);
                if (subscriptionsInDb.ContainsKey(subscriptionKey))
                {
                    continue;
                }
                Subscription subscriptionEntity = new()
                {
                    Ticker = ticker,
                    ApprovalDate = subscription.ApprovalDate,
                    LastDate = subscriptionKey.Item2,
                    Percentage = subscription.Percentage,
                    PriceUnit = subscription.PriceUnit,
                };
                await stockContext.Subscriptions.AddAsync(subscriptionEntity, cancellationToken).ConfigureAwait(false);
                subscriptionsInDb[subscriptionKey] = subscriptionEntity;
            }
        }
        private async Task ProcessFinancialEventsAsync(Ticker ticker,
                                                       CompanyResponse companyResponse,
                                                       Dictionary<(Ticker, DateOnly), Split> splitsInDb,
                                                       Dictionary<(Ticker, DateOnly), Subscription> subscriptionsInDb,
                                                       Dictionary<(Ticker, DateOnly), Dividend> dividendsInDb,
                                                       CancellationToken cancellationToken)
        {
            if (companyResponse.SplitSubscription != null)
            {
                await ProcessSplitsAsync(ticker, companyResponse.SplitSubscription.Splits, splitsInDb, cancellationToken).ConfigureAwait(false);
                await ProcessSubscriptionsAsync(ticker, companyResponse.SplitSubscription.Subscriptions, subscriptionsInDb, cancellationToken).ConfigureAwait(false);
            }

            if (companyResponse.Dividends != null)
            {
                await ProcessDividendsAsync(ticker, companyResponse.Dividends, dividendsInDb, cancellationToken).ConfigureAwait(false);
            }
        }
        private async Task ProcessSingleCompanyResponseAsync(CompanyResponse companyResponse,
                                                             Dictionary<string, Company> companiesInDb,
                                                             Dictionary<string, Ticker> tickersInDb,
                                                             Dictionary<string, Industry> industriesInDb,
                                                             Dictionary<(string, string), CompanyIndustry> companyIndustriesInDb,
                                                             Dictionary<(Ticker, DateOnly), Split> splitsInDb,
                                                             Dictionary<(Ticker, DateOnly), Subscription> subscriptionsInDb,
                                                             Dictionary<(Ticker, DateOnly), Dividend> dividendsInDb,
                                                             CancellationToken cancellationToken)
        {
            var company = await SaveCompanyAsync(companyResponse, companiesInDb, cancellationToken).ConfigureAwait(false);
            await ProcessIndustriesAsync(company, companyResponse, industriesInDb, companyIndustriesInDb, cancellationToken).ConfigureAwait(false);
            if (companyResponse.OtherCodes == null)
            {
                _companyHasNoTickers(logger, companyResponse.CompanyName, companyResponse.Cnpj, null);
                return;
            }
            await foreach (var ticker in SaveTickersAsync(company, tickersInDb, companyResponse.OtherCodes, cancellationToken).ConfigureAwait(false))
            {
                await ProcessFinancialEventsAsync(ticker, companyResponse, splitsInDb, subscriptionsInDb, dividendsInDb, cancellationToken).ConfigureAwait(false);
            }
            _companyHasBeenSaved(logger, companyResponse.CompanyName, companyResponse.Cnpj, null);
        }
        private async Task ProcessCompanyResponsesAsync(Dictionary<string, Company> companiesInDb,
                                                        Dictionary<string, Ticker> tickersInDb,
                                                        Dictionary<string, Industry> industriesInDb,
                                                        Dictionary<(string, string), CompanyIndustry> companyIndustriesInDb,
                                                        Dictionary<(Ticker, DateOnly), Split> splitsInDb,
                                                        Dictionary<(Ticker, DateOnly), Subscription> subscriptionsInDb,
                                                        Dictionary<(Ticker, DateOnly), Dividend> dividendsInDb,
                                                        CompanyDataArgs companyDataArgs,
                                                        CancellationToken cancellationToken)
        {
            await foreach (var companyResponse in companyDataRepository.GetCompaniesAsync(companyDataArgs, cancellationToken).ConfigureAwait(false))
            {
                await ProcessSingleCompanyResponseAsync(companyResponse, companiesInDb, tickersInDb, industriesInDb,
                                                        companyIndustriesInDb, splitsInDb, subscriptionsInDb,
                                                        dividendsInDb, cancellationToken).ConfigureAwait(false);
            }
        }
        public async Task SaveCompaniesAsync(CompanyDataArgs companyDataArgs, CancellationToken cancellationToken)
        {
            var companiesInDb = await stockContext.Companies.ToDictionaryAsync(c => c.Cnpj, cancellationToken).ConfigureAwait(false);
            var tickersInDb = await stockContext.Tickers.ToDictionaryAsync(t => t.StockTicker, cancellationToken).ConfigureAwait(false);
            var industriesInDb = await stockContext.Industries.ToDictionaryAsync(i => i.Name, cancellationToken).ConfigureAwait(false);
            var companyIndustriesInDb = await stockContext.CompanyIndustries.ToDictionaryAsync(ci => (ci.Company!.Cnpj, ci.Industry!.Name), cancellationToken).ConfigureAwait(false);
            var splitsInDb = await stockContext.Splits.ToDictionaryAsync(s => (s.Ticker!, s.LastDate), cancellationToken).ConfigureAwait(false);
            var subscriptionsInDb = await stockContext.Subscriptions.ToDictionaryAsync(s => (s.Ticker!, s.LastDate), cancellationToken).ConfigureAwait(false);
            var dividendsInDb = await stockContext.Dividends.ToDictionaryAsync(d => (d.Ticker!, d.ApprovalDate), cancellationToken).ConfigureAwait(false);
            await ProcessCompanyResponsesAsync(companiesInDb, tickersInDb, industriesInDb, companyIndustriesInDb,
                                               splitsInDb, subscriptionsInDb, dividendsInDb, companyDataArgs,
                                               cancellationToken).ConfigureAwait(false);
            var changes = await stockContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            _changesSaved(logger, changes, null);
        }
    }
}