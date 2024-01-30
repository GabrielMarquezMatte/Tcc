using System.Net.Http.Json;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Threading.Channels;
using Cocona;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Tcc.DownloadData.Data;
using Tcc.DownloadData.Entities;
using Tcc.DownloadData.Options;
using Tcc.DownloadData.Requests;
using Tcc.DownloadData.Responses;

namespace Tcc.DownloadData.Commands
{
    public sealed class DownloadTickers(StockContext stockContext, HttpClient httpClient, IOptions<DownloadUrlsOptions> urlOptions)
    {
        private readonly Channel<CompaniesResponse> CompaniesChannel = Channel.CreateUnbounded<CompaniesResponse>();
        private readonly Channel<CompanyResponse> CompanyChannel = Channel.CreateUnbounded<CompanyResponse>();
        private static Uri CreateUri<T>(Uri baseUri, T requestData)
        {
            var jsonString = JsonSerializer.Serialize(requestData);
            var base64String = Convert.ToBase64String(Encoding.UTF8.GetBytes(jsonString));
            return new(baseUri + "/" + base64String);
        }
        private async Task<CompaniesResponse?> GetCompaniesAsync(int pageNumber, CancellationToken cancellationToken)
        {
            CompaniesRequest request = new()
            {
                PageNumber = pageNumber,
            };
            var url = CreateUri(urlOptions.Value.CompaniesUrl, request);
            using var response = await httpClient.GetAsync(url, cancellationToken).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadFromJsonAsync<CompaniesResponse>(cancellationToken: cancellationToken).ConfigureAwait(false);
        }
        private async Task GetCompaniesWriterAsync(CancellationToken cancellationToken)
        {
            int pageNumber = 1;
            var companiesResponse = await GetCompaniesAsync(pageNumber, cancellationToken).ConfigureAwait(false);
            if (companiesResponse == null)
            {
                return;
            }
            await CompaniesChannel.Writer.WriteAsync(companiesResponse, cancellationToken).ConfigureAwait(false);
            while (companiesResponse != null && companiesResponse.Page.PageNumber < companiesResponse.Page.TotalPages)
            {
                pageNumber++;
                companiesResponse = await GetCompaniesAsync(pageNumber, cancellationToken).ConfigureAwait(false);
                if (companiesResponse == null)
                {
                    continue;
                }
                await CompaniesChannel.Writer.WriteAsync(companiesResponse, cancellationToken).ConfigureAwait(false);
            }
            CompaniesChannel.Writer.Complete();
        }
        private async Task GetCompanyResponseAsync(int codeCvm, CancellationToken cancellationToken)
        {
            CompanyRequest request = new()
            {
                CodeCvm = codeCvm,
            };
            var url = CreateUri(urlOptions.Value.CompanyUrl, request);
            using var response = await httpClient.GetAsync(url, cancellationToken).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();
            if (response.Content.Headers.ContentLength <= 10)
            {
                return;
            }
            var company = await response.Content.ReadFromJsonAsync<CompanyResponse>(cancellationToken: cancellationToken).ConfigureAwait(false);
            if (company == null
                || string.IsNullOrEmpty(company.Cnpj)
                || string.IsNullOrEmpty(company.CompanyName)
                || company.CodeCvm == 0
                || string.IsNullOrEmpty(company.IndustryClassification))
            {
                return;
            }
            await CompanyChannel.Writer.WriteAsync(company, cancellationToken).ConfigureAwait(false);
        }
        private async Task GetCompaniesReaderAsync(CancellationToken cancellationToken)
        {
            await foreach (var companiesResponse in CompaniesChannel.Reader.ReadAllAsync(cancellationToken).ConfigureAwait(false))
            {
                if (companiesResponse.Results == null)
                {
                    continue;
                }
                foreach (var result in companiesResponse.Results)
                {
                    await GetCompanyResponseAsync(result.CodeCvm, cancellationToken).ConfigureAwait(false);
                }
            }
            CompanyChannel.Writer.Complete();
        }
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
        private async Task SaveCompaniesAsync(CancellationToken cancellationToken)
        {
            var companiesInDb = await stockContext.Companies.ToDictionaryAsync(c => c.Cnpj, cancellationToken).ConfigureAwait(false);
            var tickersInDb = await stockContext.Tickers.ToDictionaryAsync(t => t.StockTicker, cancellationToken).ConfigureAwait(false);
            var industriesInDb = await stockContext.Industries.ToDictionaryAsync(i => i.Name, cancellationToken).ConfigureAwait(false);
            var companyIndustriesInDb = await stockContext.CompanyIndustries.ToDictionaryAsync(ci => (ci.Company!.Cnpj, ci.Industry!.Name), cancellationToken).ConfigureAwait(false);
            await foreach (var companyResponse in CompanyChannel.Reader.ReadAllAsync(cancellationToken).ConfigureAwait(false))
            {
                var company = await SaveCompanyAsync(companyResponse, companiesInDb, cancellationToken).ConfigureAwait(false);
                await foreach (var industry in SaveIndustryAsync(companyResponse, industriesInDb, cancellationToken).ConfigureAwait(false))
                {
                    await SaveCompanyIndustriesAsync(company, industry, companyIndustriesInDb, cancellationToken).ConfigureAwait(false);
                }
                if (companyResponse.OtherCodes == null)
                {
                    continue;
                }
                await SaveTickersAsync(company, tickersInDb, companyResponse.OtherCodes, cancellationToken).ConfigureAwait(false);
            }
            await stockContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
        [Command("download-tickers")]
        public Task ExecuteAsync([Ignore] CancellationToken cancellationToken)
        {
            Task companiesWriterTask = GetCompaniesWriterAsync(cancellationToken);
            Task companiesReaderTask = GetCompaniesReaderAsync(cancellationToken);
            Task saveCompaniesTask = SaveCompaniesAsync(cancellationToken);
            return Task.WhenAll(companiesWriterTask, companiesReaderTask, saveCompaniesTask);
        }
    }
}