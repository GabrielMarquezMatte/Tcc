using System.Net.Http.Json;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Threading.Channels;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using DownloadData.Models.Arguments;
using DownloadData.Models.Options;
using DownloadData.Requests;
using DownloadData.Responses;
using DownloadData.Responses.Companies;

namespace DownloadData.Repositories
{
    public sealed class CompanyDataRepository(HttpClient httpClient, IOptions<DownloadUrlsOptions> urlOptions, ILogger<CompanyDataRepository> logger)
    {
        private static readonly Action<ILogger, Uri, Exception> _fetchError = LoggerMessage.Define<Uri>(
            LogLevel.Error,
            new EventId(2, "FetchError"),
            "Error fetching data from {Uri}");
        private readonly Channel<CompanyResponse> CompanyChannel = Channel.CreateUnbounded<CompanyResponse>();
        private static Uri CreateUri<T>(Uri baseUri, T requestData)
        {
            var jsonString = JsonSerializer.Serialize(requestData);
            var base64String = Convert.ToBase64String(Encoding.UTF8.GetBytes(jsonString));
            return new(baseUri + "/" + base64String);
        }
        private async Task<T?> FetchDataAsync<T>(Uri uri, SemaphoreSlim semaphore, JsonSerializerOptions? jsonSerializer, CancellationToken cancellationToken, CancellationToken timeCancellation)
        {
            await semaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                using var response = await httpClient.GetAsync(uri, timeCancellation).ConfigureAwait(false);
                if (!response.IsSuccessStatusCode || response.Content.Headers.ContentLength <= 10)
                {
                    return default;
                }
                if (jsonSerializer != null)
                {
                    return await response.Content.ReadFromJsonAsync<T>(jsonSerializer, cancellationToken).ConfigureAwait(false);
                }
                return await response.Content.ReadFromJsonAsync<T>(cancellationToken: cancellationToken).ConfigureAwait(false);
            }
            catch (InvalidOperationException ex)
            {
                _fetchError(logger, uri, ex);
                return default;
            }
            catch (HttpRequestException ex)
            {
                _fetchError(logger, uri, ex);
                return default;
            }
            catch (JsonException ex)
            {
                _fetchError(logger, uri, ex);
                return default;
            }
            catch (TaskCanceledException ex)
            {
                _fetchError(logger, uri, ex);
                return default;
            }
            finally
            {
                semaphore.Release();
            }
        }
        private Task<CompaniesResponse?> FetchCompaniesAsync(SemaphoreSlim semaphoreSlim, CancellationToken cancellationToken)
        {
            CompaniesRequest request = new();
            var url = CreateUri(urlOptions.Value.ListCompanies, request);
            return FetchDataAsync<CompaniesResponse>(url, semaphoreSlim, null, cancellationToken, CancellationToken.None);
        }
        private Task<CompanyResponse?> FetchCompanyDetailsAsync(int codeCvm, SemaphoreSlim semaphore, CancellationToken cancellationToken)
        {
            CompanyRequest request = new() { CodeCvm = codeCvm };
            var url = CreateUri(urlOptions.Value.CompanyDetails, request);
            return FetchDataAsync<CompanyResponse>(url, semaphore, null, cancellationToken, CancellationToken.None);
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool ShouldProcessCompany(CompanyResponse? company)
        {
            return company != null
                   && !string.IsNullOrEmpty(company.Cnpj)
                   && !string.IsNullOrEmpty(company.CompanyName)
                   && company.CodeCvm != 0
                   && !string.IsNullOrEmpty(company.IndustryClassification);
        }
        private async Task ProcessCompanyResponseAsync(int codeCvm, SemaphoreSlim semaphore,
                                                       CancellationToken cancellationToken)
        {
            var company = await FetchCompanyDetailsAsync(codeCvm, semaphore, cancellationToken).ConfigureAwait(false);
            if (ShouldProcessCompany(company))
            {
                await CompanyChannel.Writer.WriteAsync(company!, cancellationToken).ConfigureAwait(false);
            }
        }
        private async Task ProcessCompaniesAsync(CompanyDataArgs companyDataArgs, CancellationToken cancellationToken)
        {
            using SemaphoreSlim semaphore = new(companyDataArgs.MaxParallelism);
            try
            {
                if (companyDataArgs.Companies.Any())
                {
                    var tasks = companyDataArgs.Companies.Select(async codeCvm => await ProcessCompanyResponseAsync(codeCvm, semaphore, cancellationToken).ConfigureAwait(false));
                    await Task.WhenAll(tasks).ConfigureAwait(false);
                    return;
                }
                var response = await FetchCompaniesAsync(semaphore, cancellationToken).ConfigureAwait(false);
                if (response?.Results.Any() != true) return;
                var tasks1 = response.Results.Select(async result => await ProcessCompanyResponseAsync(result.CodeCvm, semaphore, cancellationToken).ConfigureAwait(false));
                await Task.WhenAll(tasks1).ConfigureAwait(false);
            }
            finally
            {
                CompanyChannel.Writer.Complete();
            }
        }
        public async IAsyncEnumerable<CompanyResponse> GetCompaniesAsync(CompanyDataArgs companyDataArgs, [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            var task = ProcessCompaniesAsync(companyDataArgs, cancellationToken);
            await foreach (var company in CompanyChannel.Reader.ReadAllAsync(cancellationToken).ConfigureAwait(false))
            {
                yield return company;
            }
            await task.ConfigureAwait(false);
        }
    }
}