using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Threading.Channels;
using Cocona;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Tcc.DownloadData.Data;
using Tcc.DownloadData.Options;
using Tcc.DownloadData.Requests;
using Tcc.DownloadData.Responses;

namespace Tcc.DownloadData.Commands
{
    public sealed class DownloadTickers(StockContext stockContext, HttpClient httpClient, IOptions<DownloadUrlsOptions> urlOptions)
    {
        private readonly Channel<CompaniesResponse> CompaniesChannel = Channel.CreateUnbounded<CompaniesResponse>();
        private readonly Channel<CompanyResponse> CompanyChannel = Channel.CreateUnbounded<CompanyResponse>();
        private async Task<CompaniesResponse?> GetCompaniesAsync(int pageNumber, CancellationToken cancellationToken)
        {
            CompaniesRequest request = new()
            {
                PageNumber = pageNumber,
            };
            var jsonString = JsonSerializer.Serialize(request);
            var base64String = Convert.ToBase64String(Encoding.UTF8.GetBytes(jsonString));
            Uri url = new(urlOptions.Value.CompaniesUrl + "/" + base64String);
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
        private async Task<CompanyResponse?> GetCompanyResponseAsync(int codeCvm, CancellationToken cancellationToken)
        {
            CompanyRequest request = new()
            {
                CodeCvm = codeCvm,
            };
            var jsonString = JsonSerializer.Serialize(request);
            var base64String = Convert.ToBase64String(Encoding.UTF8.GetBytes(jsonString));
            Uri url = new(urlOptions.Value.CompanyUrl + "/" + base64String);
            using var response = await httpClient.GetAsync(url, cancellationToken).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();
            if(response.Content.Headers.ContentLength <= 10)
            {
                return null;
            }
            return await response.Content.ReadFromJsonAsync<CompanyResponse>(cancellationToken: cancellationToken).ConfigureAwait(false);
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
                    var companyResponse = await GetCompanyResponseAsync(result.CodeCvm, cancellationToken).ConfigureAwait(false);
                    if(companyResponse == null
                        || string.IsNullOrEmpty(companyResponse.Cnpj)
                        || string.IsNullOrEmpty(companyResponse.CompanyName)
                        || companyResponse.CodeCvm == 0
                        || string.IsNullOrEmpty(companyResponse.IndustryClassification))
                    {
                        continue;
                    }
                    await CompanyChannel.Writer.WriteAsync(companyResponse, cancellationToken).ConfigureAwait(false);
                }
            }
            CompanyChannel.Writer.Complete();
        }
        private async Task SaveCompaniesAsync(CancellationToken cancellationToken)
        {
            await foreach (var companyResponse in CompanyChannel.Reader.ReadAllAsync(cancellationToken).ConfigureAwait(false))
            {
                if (companyResponse == null)
                {
                    continue;
                }
                var company = await stockContext.Companies.FirstOrDefaultAsync(c => c.Cnpj == companyResponse.Cnpj, cancellationToken).ConfigureAwait(false);
                if (company == null)
                {
                    company = new()
                    {
                        Cnpj = companyResponse.Cnpj,
                        Industry = companyResponse.IndustryClassification,
                        Name = companyResponse.CompanyName,
                        CvmCode = companyResponse.CodeCvm,
                        HasBdrs = companyResponse.HasBdrs,
                        HasEmissions = companyResponse.HasEmissions,
                    };
                    await stockContext.Companies.AddAsync(company, cancellationToken).ConfigureAwait(false);
                }
                if (companyResponse.OtherCodes == null)
                {
                    continue;
                }
                company.Tickers ??= [];
                foreach (var otherCode in companyResponse.OtherCodes.Where(c => !string.IsNullOrEmpty(c.Code) && !string.IsNullOrEmpty(c.Isin)).DistinctBy(x => x.Code))
                {
                    var ticker = await stockContext.Tickers.FirstOrDefaultAsync(t => t.StockTicker == otherCode.Code, cancellationToken).ConfigureAwait(false);
                    if (ticker != null)
                    {
                        continue;
                    }
                    ticker = new()
                    {
                        Isin = otherCode.Isin,
                        StockTicker = otherCode.Code,
                    };
                    company.Tickers.Add(ticker);
                }
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