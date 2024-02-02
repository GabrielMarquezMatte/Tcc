using System.Net.Http.Json;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Threading.Channels;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Tcc.DownloadData.Converters;
using Tcc.DownloadData.Models.Arguments;
using Tcc.DownloadData.Models.Options;
using Tcc.DownloadData.Requests;
using Tcc.DownloadData.Responses;
using Tcc.DownloadData.Responses.Companies;
using Tcc.DownloadData.Responses.SplitSubscription;

namespace Tcc.DownloadData.Repositories
{
    public sealed class CompanyDataRepository(HttpClient httpClient, IOptions<DownloadUrlsOptions> urlOptions, ILogger<CompanyDataRepository> logger)
    {
        private static readonly Action<ILogger, string, Exception?> _startFetchSplitSubscription = LoggerMessage.Define<string>(
            LogLevel.Information,
            new EventId(1, "StartFetchSplitSubscription"),
            "Start fetching split subscription for company {IssuingCompany}");
        private static readonly Action<ILogger, string, Exception> _fetchSplitSubscriptionError = LoggerMessage.Define<string>(
            LogLevel.Error,
            new EventId(2, "FetchSplitSubscriptionError"),
            "Error fetching split subscription for company {IssuingCompany}");
        private static readonly Action<ILogger, string, Exception?> _fetchSplitSubscription = LoggerMessage.Define<string>(
            LogLevel.Information,
            new EventId(3, "FetchSplitSubscription"),
            "Fetched split subscription for company {IssuingCompany}");
        private static readonly JsonSerializerOptions JsonSerializerOptions = new()
        {
            Converters = { new DoubleConverter(), new DateConverter() },
        };
        private readonly Channel<CompanyResponse> CompanyChannel = Channel.CreateUnbounded<CompanyResponse>();
        private int NumberSplits;
        private static Uri CreateUri<T>(Uri baseUri, T requestData)
        {
            var jsonString = JsonSerializer.Serialize(requestData);
            var base64String = Convert.ToBase64String(Encoding.UTF8.GetBytes(jsonString));
            return new(baseUri + "/" + base64String);
        }
        private async Task<CompaniesResponse?> FetchCompaniesAsync(CancellationToken cancellationToken)
        {
            CompaniesRequest request = new()
            {
                PageNumber = -1,
                PageSize = 120,
            };
            var url = CreateUri(urlOptions.Value.ListCompanies, request);
            using var response = await httpClient.GetAsync(url, cancellationToken).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                return null;
            }
            return await response.Content.ReadFromJsonAsync<CompaniesResponse>(cancellationToken: cancellationToken).ConfigureAwait(false);
        }
        private async Task<IEnumerable<SplitSubscriptionResponse>?> FetchSplitSubscriptionAsync(string issuingCompany, CancellationToken cancellationToken)
        {
            SplitSubscriptionRequest request = new()
            {
                IssuingCompany = issuingCompany,
            };
            var url = CreateUri(urlOptions.Value.SplitSubscription, request);
            try
            {
                _startFetchSplitSubscription(logger, issuingCompany, null);
                using CancellationTokenSource cancellationTokenSource = new(TimeSpan.FromSeconds(20));
                using var response = await httpClient.GetAsync(url, cancellationTokenSource.Token).ConfigureAwait(false);
                if (!response.IsSuccessStatusCode || response.Content.Headers.ContentLength <= 10)
                {
                    return null;
                }
                _fetchSplitSubscription(logger, issuingCompany, null);
                NumberSplits++;
                return await response.Content.ReadFromJsonAsync<IEnumerable<SplitSubscriptionResponse>>(JsonSerializerOptions, cancellationToken: cancellationToken).ConfigureAwait(false);
            }
            catch (InvalidOperationException ex)
            {
                _fetchSplitSubscriptionError(logger, issuingCompany, ex);
                return null;
            }
            catch (HttpRequestException ex)
            {
                _fetchSplitSubscriptionError(logger, issuingCompany, ex);
                return null;
            }
            catch (JsonException ex)
            {
                _fetchSplitSubscriptionError(logger, issuingCompany, ex);
                return null;
            }
        }
        private async Task ProcessCompanyResponseAsync(int codeCvm, SemaphoreSlim semaphore,
                                                       CompanyDataArgs companyDataArgs, DateTime lastSplitDate,
                                                       CancellationToken cancellationToken)
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
                if (lastSplitDate - DateTime.Now > TimeSpan.FromDays(-2) || company.OtherCodes?.Count() <= 0)
                {
                    await CompanyChannel.Writer.WriteAsync(company, cancellationToken).ConfigureAwait(false);
                    return;
                }
                if (companyDataArgs.Splits && NumberSplits < companyDataArgs.MaxSplits || companyDataArgs.Companies.Contains(company.IssuingCompany, StringComparer.Ordinal))
                {
                    var result = await FetchSplitSubscriptionAsync(company.IssuingCompany, cancellationToken).ConfigureAwait(false);
                    if (result != null)
                    {
                        company.SplitSubscription = result.First();
                    }
                }
                await CompanyChannel.Writer.WriteAsync(company, cancellationToken).ConfigureAwait(false);
            }
            finally
            {
                semaphore.Release();
            }
        }
        private async Task ProcessCompaniesAsync(CompanyDataArgs companyDataArgs, IEnumerable<(int, DateTime)> lastSplitsDates, CancellationToken cancellationToken)
        {
            var response = await FetchCompaniesAsync(cancellationToken).ConfigureAwait(false);
            if (response == null || response.Results == null)
            {
                return;
            }
            using SemaphoreSlim semaphore = new(companyDataArgs.MaxParallelism);
            List<Task> tasks = new(response.Results.Count);
            foreach (var result in response.Results.Select(x => x.CodeCvm))
            {
                DateTime lastSplitDate = lastSplitsDates.FirstOrDefault(x => x.Item1 == result, (0, DateTime.Now.AddDays(-5))).Item2;
                var task = ProcessCompanyResponseAsync(result, semaphore, companyDataArgs, lastSplitDate, cancellationToken);
                tasks.Add(task);
            }
            await Task.WhenAll(tasks).ConfigureAwait(false);
            CompanyChannel.Writer.Complete();
        }
        public async IAsyncEnumerable<CompanyResponse> GetCompaniesAsync(CompanyDataArgs companyDataArgs, IEnumerable<(int, DateTime)> lastSplitsDates, [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            var task = ProcessCompaniesAsync(companyDataArgs, lastSplitsDates, cancellationToken);
            await foreach (var company in CompanyChannel.Reader.ReadAllAsync(cancellationToken).ConfigureAwait(false))
            {
                yield return company;
            }
            await task.ConfigureAwait(false);
        }
    }
}