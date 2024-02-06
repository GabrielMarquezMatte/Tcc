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
using DownloadData.Responses.Dividends;
using DownloadData.Responses.SplitSubscription;

namespace DownloadData.Repositories
{
    public sealed class CompanyDataRepository(HttpClient httpClient, IOptions<DownloadUrlsOptions> urlOptions, ILogger<CompanyDataRepository> logger)
    {
        private static readonly Action<ILogger, string, Exception?> _startFetchSplitSubscription = LoggerMessage.Define<string>(
            LogLevel.Information,
            new EventId(1, "StartFetchSplitSubscription"),
            "Start fetching split subscription for company {IssuingCompany}");
        private static readonly Action<ILogger, Uri, Exception> _fetchError = LoggerMessage.Define<Uri>(
            LogLevel.Error,
            new EventId(2, "FetchError"),
            "Error fetching data from {Uri}");
        private static readonly Action<ILogger, string, Exception?> _fetchSplitSubscription = LoggerMessage.Define<string>(
            LogLevel.Information,
            new EventId(3, "FetchSplitSubscription"),
            "Fetched split subscription for company {IssuingCompany}");
        private readonly Channel<CompanyResponse> CompanyChannel = Channel.CreateUnbounded<CompanyResponse>();
        private static Uri CreateUri<T>(Uri baseUri, T requestData)
        {
            var jsonString = JsonSerializer.Serialize(requestData);
            var base64String = Convert.ToBase64String(Encoding.UTF8.GetBytes(jsonString));
            return new(baseUri + "/" + base64String);
        }
        private async Task<T?> FetchDataAsync<T>(Uri uri, SemaphoreSlim semaphore, CancellationToken cancellationToken)
        {
            await semaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                using var response = await httpClient.GetAsync(uri, cancellationToken).ConfigureAwait(false);
                if (!response.IsSuccessStatusCode || response.Content.Headers.ContentLength <= 10)
                {
                    return default;
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
            return FetchDataAsync<CompaniesResponse>(url, semaphoreSlim, cancellationToken);
        }
        private async Task<IEnumerable<SplitSubscriptionResponse>?> FetchSplitSubscriptionAsync(string issuingCompany, SemaphoreSlim semaphore, CancellationToken cancellationToken)
        {
            SplitSubscriptionRequest request = new()
            {
                IssuingCompany = issuingCompany,
            };
            var url = CreateUri(urlOptions.Value.SplitSubscription, request);
            _startFetchSplitSubscription(logger, issuingCompany, null);
            using var cancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cancellationTokenSource.CancelAfter(5000);
            var data = await FetchDataAsync<IEnumerable<SplitSubscriptionResponse>>(url, semaphore, cancellationTokenSource.Token).ConfigureAwait(false);
            _fetchSplitSubscription(logger, issuingCompany, null);
            return data;
        }
        private Task<CompanyResponse?> FetchCompanyDetailsAsync(int codeCvm, SemaphoreSlim semaphore, CancellationToken cancellationToken)
        {
            CompanyRequest request = new() { CodeCvm = codeCvm };
            var url = CreateUri(urlOptions.Value.CompanyDetails, request);
            return FetchDataAsync<CompanyResponse>(url, semaphore, cancellationToken);
        }
        private Task<DividendsResponse?> FetchDividendsAsync(string tradingName, SemaphoreSlim semaphore, CancellationToken cancellationToken)
        {
            DividendsRequest request = new() { TradingName = tradingName };
            var url = CreateUri(urlOptions.Value.Dividends, request);
            using var cancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cancellationTokenSource.CancelAfter(5000);
            return FetchDataAsync<DividendsResponse>(url, semaphore, cancellationTokenSource.Token);
        }
        private static bool ShouldProcessCompany(CompanyResponse? company)
        {
            return company != null
                   && !string.IsNullOrEmpty(company.Cnpj)
                   && !string.IsNullOrEmpty(company.CompanyName)
                   && company.CodeCvm != 0
                   && !string.IsNullOrEmpty(company.IndustryClassification);
        }
        private async Task ProcessSplitSubscriptionAsync(CompanyResponse company, SemaphoreSlim semaphore, CancellationToken cancellationToken)
        {
            var splitResult = await FetchSplitSubscriptionAsync(company.IssuingCompany, semaphore, cancellationToken).ConfigureAwait(false);
            if (splitResult?.Any() == true)
            {
                company.SplitSubscription = splitResult.First();
            }
        }
        private async Task ProcessDividendsAsync(CompanyResponse company, SemaphoreSlim semaphore, CancellationToken cancellationToken)
        {
            if (company.OtherCodes == null)
            {
                return;
            }
            var dividends = await FetchDividendsAsync(company.TradingName, semaphore, cancellationToken).ConfigureAwait(false);
            if (dividends == null || !dividends.Dividends.Any())
            {
                return;
            }
            var codes = company.OtherCodes.Select(x => x.Code).Distinct(StringComparer.Ordinal)
                                          .Where(x => x.Length == 5).ToDictionary(x => x.Last(), x => x);
            foreach (var dividend in dividends.Dividends)
            {
                switch (dividend.StockType)
                {
                    case "ON":
                        var ticker = codes.GetValueOrDefault('3', string.Empty);
                        dividend.StockTicker = ticker;
                        break;
                    case "PN":
                        ticker = codes.GetValueOrDefault('4', string.Empty);
                        dividend.StockTicker = ticker;
                        break;
                    default:
                        break;
                }
            }
            company.Dividends = dividends.Dividends;
        }
        private async Task ProcessValidCompanyAsync(CompanyResponse company, CompanyDataArgs companyDataArgs, SemaphoreSlim semaphore, CancellationToken cancellationToken)
        {
            if (company.OtherCodes == null)
            {
                await CompanyChannel.Writer.WriteAsync(company, cancellationToken).ConfigureAwait(false);
                return;
            }
            List<Task> tasks = new(2);
            if (companyDataArgs.CompaniesSplits.Any(x => company.OtherCodes.Any(code => string.Equals(code.Code, x, StringComparison.Ordinal))))
            {
                var task = ProcessSplitSubscriptionAsync(company, semaphore, cancellationToken);
                tasks.Add(task);
            }
            if (companyDataArgs.CompaniesDividends.Any(x => company.OtherCodes.Any(code => string.Equals(code.Code, x, StringComparison.Ordinal))))
            {
                var task = ProcessDividendsAsync(company, semaphore, cancellationToken);
                tasks.Add(task);
            }
            await Task.WhenAll(tasks).ConfigureAwait(false);
            await CompanyChannel.Writer.WriteAsync(company, cancellationToken).ConfigureAwait(false);
        }
        private async Task ProcessCompanyResponseAsync(int codeCvm, SemaphoreSlim semaphore,
                                                       CompanyDataArgs companyDataArgs,
                                                       CancellationToken cancellationToken)
        {
            var company = await FetchCompanyDetailsAsync(codeCvm, semaphore, cancellationToken).ConfigureAwait(false);
            if (ShouldProcessCompany(company))
            {
                await ProcessValidCompanyAsync(company!, companyDataArgs, semaphore, cancellationToken).ConfigureAwait(false);
            }
        }
        private async Task ProcessCompaniesAsync(CompanyDataArgs companyDataArgs, CancellationToken cancellationToken)
        {
            using SemaphoreSlim semaphore = new(companyDataArgs.MaxParallelism);
            try
            {
                if (companyDataArgs.Companies.Any())
                {
                    var tasks = companyDataArgs.Companies.Select(codeCvm =>
                    {
                        return ProcessCompanyResponseAsync(codeCvm, semaphore, companyDataArgs, cancellationToken);
                    });
                    await Task.WhenAll(tasks).ConfigureAwait(false);
                    return;
                }
                var response = await FetchCompaniesAsync(semaphore, cancellationToken).ConfigureAwait(false);
                if (response == null || !response.Results.Any()) return;
                var tasks1 = response.Results.Select(result =>
                {
                    return ProcessCompanyResponseAsync(result.CodeCvm, semaphore, companyDataArgs, cancellationToken);
                });
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