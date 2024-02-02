using System.Runtime.CompilerServices;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Tcc.DownloadData.Data;
using Tcc.DownloadData.Entities;
using Tcc.DownloadData.Models.Arguments;
using Tcc.DownloadData.Repositories;
using Tcc.DownloadData.Responses;
using Tcc.DownloadData.Responses.SplitSubscription;

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
        private async IAsyncEnumerable<Ticker> SaveTickersAsync(Company company, Dictionary<string, Ticker> tickers, IEnumerable<CompanyCodes> codes, [EnumeratorCancellation] CancellationToken cancellationToken)
        {
            foreach (var otherCode in codes.Where(c => !string.IsNullOrEmpty(c.Code) && !string.IsNullOrEmpty(c.Isin)).DistinctBy(x => x.Code))
            {
                var ticker = tickers.GetValueOrDefault(otherCode.Code);
                if (ticker != null)
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
                                              Dictionary<(Ticker, DateTime), Split> splitsInDb,
                                              CancellationToken cancellationToken)
        {
            foreach (var split in splits)
            {
                if(!string.Equals(split.AssetIssued, ticker.Isin, StringComparison.OrdinalIgnoreCase))
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
        private async Task ProcessSubscriptionsAsync(Ticker ticker, IEnumerable<SubscriptionResponse> subscriptions,
                                                    Dictionary<(Ticker, DateTime), Subscription> subscriptionsInDb,
                                                    CancellationToken cancellationToken)
        {
            foreach (var subscription in subscriptions)
            {
                if(!string.Equals(subscription.AssetIssued, ticker.Isin, StringComparison.OrdinalIgnoreCase))
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
                    LastDate = subscription.LastDate,
                    Percentage = subscription.Percentage,
                    PriceUnit = subscription.PriceUnit,
                };
                await stockContext.Subscriptions.AddAsync(subscriptionEntity, cancellationToken).ConfigureAwait(false);
                subscriptionsInDb[subscriptionKey] = subscriptionEntity;
            }
        }
        private async Task ProcessCompanyResponsesAsync(Dictionary<string, Company> companiesInDb,
                                                        Dictionary<string, Ticker> tickersInDb,
                                                        Dictionary<string, Industry> industriesInDb,
                                                        Dictionary<(string, string), CompanyIndustry> companyIndustriesInDb,
                                                        Dictionary<(Ticker, DateTime), Split> splitsInDb,
                                                        Dictionary<(Ticker, DateTime), Subscription> subscriptionsInDb,
                                                        CompanyDataArgs companyDataArgs,
                                                        CancellationToken cancellationToken)
        {
            var maxSplitDates = await stockContext.Splits
                .GroupBy(s => s.Ticker!.Company!.CvmCode)
                .Select(g => new {CvmCode = g.Key, MaxDate = g.Max(s => s.LastDate)})
                .ToListAsync(cancellationToken).ConfigureAwait(false);
            var maxSubscriptionDates = await stockContext.Subscriptions
                .GroupBy(s => s.Ticker!.Company!.CvmCode)
                .Select(g => new {CvmCode = g.Key, MaxDate = g.Max(s => s.LastDate)})
                .ToListAsync(cancellationToken).ConfigureAwait(false);
            var maxDates = maxSplitDates.Concat(maxSubscriptionDates).GroupBy(x => x.CvmCode)
                .Select(g => new {CvmCode = g.Key, MaxDate = g.Max(x => x.MaxDate)})
                .ToList();
            await foreach (var companyResponse in companyDataRepository.GetCompaniesAsync(companyDataArgs, maxDates.Select(x => (x.CvmCode, x.MaxDate)), cancellationToken).ConfigureAwait(false).WithCancellation(cancellationToken))
            {
                var company = await SaveCompanyAsync(companyResponse, companiesInDb, cancellationToken).ConfigureAwait(false);
                await ProcessIndustriesAsync(company, companyResponse, industriesInDb, companyIndustriesInDb, cancellationToken).ConfigureAwait(false);
                if (companyResponse.OtherCodes == null)
                {
                    _companyHasNoTickers(logger, companyResponse.CompanyName, companyResponse.Cnpj, arg4: null);
                    continue;
                }
                await foreach (var ticker in SaveTickersAsync(company, tickersInDb, companyResponse.OtherCodes, cancellationToken).ConfigureAwait(false).WithCancellation(cancellationToken))
                {
                    if (companyDataArgs.Splits && companyResponse.SplitSubscription != null && companyResponse.SplitSubscription.Splits != null)
                    {
                        await ProcessSplitsAsync(ticker, companyResponse.SplitSubscription.Splits, splitsInDb, cancellationToken).ConfigureAwait(false);
                    }
                    if (companyDataArgs.Splits && companyResponse.SplitSubscription != null && companyResponse.SplitSubscription.Subscriptions != null)
                    {
                        await ProcessSubscriptionsAsync(ticker, companyResponse.SplitSubscription.Subscriptions, subscriptionsInDb, cancellationToken).ConfigureAwait(false);
                    }
                }
                _companyHasBeenSaved(logger, companyResponse.CompanyName, companyResponse.Cnpj, arg4: null);
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
            await ProcessCompanyResponsesAsync(companiesInDb, tickersInDb, industriesInDb, companyIndustriesInDb, splitsInDb, subscriptionsInDb, companyDataArgs, cancellationToken).ConfigureAwait(false);
            await stockContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
    }
}