using System.Net.Http.Json;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Threading.Channels;
using Microsoft.Extensions.Options;
using Tcc.DownloadData.Models.Options;
using Tcc.DownloadData.Requests;
using Tcc.DownloadData.Responses;

namespace Tcc.DownloadData.Repositories
{
    public sealed class CompanyDataRepository(HttpClient httpClient, IOptions<DownloadUrlsOptions> urlOptions)
    {
        private readonly Channel<CompaniesResponse> CompaniesChannel = Channel.CreateUnbounded<CompaniesResponse>();
        private readonly Channel<CompanyResponse> CompanyChannel = Channel.CreateUnbounded<CompanyResponse>();
        private static Uri CreateUri<T>(Uri baseUri, T requestData)
        {
            var jsonString = JsonSerializer.Serialize(requestData);
            var base64String = Convert.ToBase64String(Encoding.UTF8.GetBytes(jsonString));
            return new(baseUri + "/" + base64String);
        }
        private async Task<CompaniesResponse?> FetchCompaniesAsync(int pageNumber, int pageSize, CancellationToken cancellationToken)
        {
            CompaniesRequest request = new()
            {
                PageNumber = pageNumber,
                PageSize = pageSize,
            };
            var url = CreateUri(urlOptions.Value.ListCompanies, request);
            using var response = await httpClient.GetAsync(url, cancellationToken).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                return null;
            }
            return await response.Content.ReadFromJsonAsync<CompaniesResponse>(cancellationToken: cancellationToken).ConfigureAwait(false);
        }
        private async Task ProcessCompanyResponseAsync(int codeCvm, SemaphoreSlim semaphore, CancellationToken cancellationToken)
        {
            CompanyRequest request = new()
            {
                CodeCvm = codeCvm,
            };
            var url = CreateUri(urlOptions.Value.CompanyDetails, request);
            await semaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                using var response = await httpClient.GetAsync(url, cancellationToken).ConfigureAwait(false);
                if (!response.IsSuccessStatusCode || response.Content.Headers.ContentLength <= 10)
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
            finally
            {
                semaphore.Release();
            }
        }
        private async Task ProcessCompaniesAsync(int maxParallelism = 10, CancellationToken cancellationToken = default)
        {
            using SemaphoreSlim semaphore = new(maxParallelism);
            List<Task> tasks = [];
            await foreach (var companiesResponse in CompaniesChannel.Reader.ReadAllAsync(cancellationToken).ConfigureAwait(false))
            {
                if (companiesResponse.Results == null)
                {
                    continue;
                }
                foreach (var result in companiesResponse.Results)
                {
                    var task = ProcessCompanyResponseAsync(result.CodeCvm, semaphore, cancellationToken);
                    tasks.Add(task);
                }
            }
            await Task.WhenAll(tasks).ConfigureAwait(false);
            CompanyChannel.Writer.Complete();
        }
        private async Task FetchAndProcessCompaniesAsync(CancellationToken cancellationToken)
        {
            int pageNumber = 1;
            const int pageSize = 120;
            var companiesResponse = await FetchCompaniesAsync(pageNumber, pageSize, cancellationToken).ConfigureAwait(false);
            if (companiesResponse == null)
            {
                return;
            }
            await CompaniesChannel.Writer.WriteAsync(companiesResponse, cancellationToken).ConfigureAwait(false);
            while (companiesResponse != null && companiesResponse.Page.PageNumber < companiesResponse.Page.TotalPages)
            {
                pageNumber++;
                companiesResponse = await FetchCompaniesAsync(pageNumber, pageSize, cancellationToken).ConfigureAwait(false);
                if (companiesResponse == null)
                {
                    continue;
                }
                await CompaniesChannel.Writer.WriteAsync(companiesResponse, cancellationToken).ConfigureAwait(false);
            }
            CompaniesChannel.Writer.Complete();
        }
        public async IAsyncEnumerable<CompanyResponse> GetCompaniesAsync(int maxParallelism = 10, [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            var implGetCompaniesTask = ProcessCompaniesAsync(maxParallelism, cancellationToken);
            var implListCompaniesTask = FetchAndProcessCompaniesAsync(cancellationToken);
            await foreach (var company in CompanyChannel.Reader.ReadAllAsync(cancellationToken).ConfigureAwait(false))
            {
                yield return company;
            }
            await Task.WhenAll(implGetCompaniesTask, implListCompaniesTask).ConfigureAwait(false);
        }
    }
}