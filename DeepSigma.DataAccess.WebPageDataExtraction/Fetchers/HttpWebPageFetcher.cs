using System.Net;
using DeepSigma.DataAccess.WebSearch.Abstraction;
using DeepSigma.DataAccess.WebSearch.Abstraction.Model;
using DeepSigma.DataAccess.WebSearch.ContentExtraction.Exceptions;


namespace DeepSigma.DataAccess.WebSearch.ContentExtraction.Fetchers;

/// <summary>
/// Fetches web pages using <see cref="HttpClient"/> with automatic decompression,
/// redirect following, content-type validation, size limiting, and exponential-backoff retries.
/// </summary>
public sealed class HttpWebPageFetcher : IHtmlRetriever
{
    private readonly HttpClient _httpClient;
    private readonly WebPageFetcherOptions _options;
    private static readonly Random _jitter = new();

    /// <summary>
    /// Initialises the fetcher with a pre-configured <see cref="HttpClient"/> and optional options.
    /// Suitable for direct instantiation without a DI container.
    /// </summary>
    public HttpWebPageFetcher(HttpClient httpClient)
        : this(httpClient, new WebPageFetcherOptions()) { }

    /// <summary>
    /// Initialises the fetcher with a pre-configured <see cref="HttpClient"/> and explicit options.
    /// This constructor is used by the DI typed-client registration.
    /// </summary>
    public HttpWebPageFetcher(HttpClient httpClient, WebPageFetcherOptions options)
    {
        _options = options;
        _httpClient = httpClient;
        _httpClient.DefaultRequestHeaders.UserAgent.TryParseAdd(_options.UserAgent);
    }

    /// <summary>
    /// Creates a standalone <see cref="HttpWebPageFetcher"/> with gzip/brotli/deflate
    /// decompression and auto-redirect enabled — no DI required.
    /// </summary>
    public static HttpWebPageFetcher Create(WebPageFetcherOptions? options = null)
    {
        var handler = new HttpClientHandler
        {
            AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Brotli | DecompressionMethods.Deflate,
            AllowAutoRedirect = true,
            MaxAutomaticRedirections = 10
        };
        return new HttpWebPageFetcher(new HttpClient(handler), options ?? new WebPageFetcherOptions());
    }

    /// <inheritdoc/>
    public async Task<ResponseHtmlContent> FetchContentAsync(ResponseUrlRetrival responseUrl, CancellationToken cancellationToken = default)
    {
        int attempt = 0;
        while (true)
        {
            attempt++;
            try
            {
                using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                cts.CancelAfter(_options.Timeout);

                using var response = await _httpClient.GetAsync(
                    responseUrl.Url, HttpCompletionOption.ResponseHeadersRead, cts.Token).ConfigureAwait(false);
                response.EnsureSuccessStatusCode();

                var contentType = response.Content.Headers.ContentType?.MediaType;
                if (!_options.AllowNonHtmlContent
                    && contentType != null
                    && !contentType.Contains("text/html", StringComparison.OrdinalIgnoreCase))
                {
                    throw new ContentTypeNotAllowedException(contentType, responseUrl.Url);
                }

                var contentLength = response.Content.Headers.ContentLength;
                if (contentLength.HasValue && contentLength.Value > _options.MaxResponseSizeBytes)
                    throw new ResponseTooLargeException(contentLength.Value, _options.MaxResponseSizeBytes, responseUrl.Url);

                var html = await response.Content.ReadAsStringAsync(cts.Token).ConfigureAwait(false);
                if (html.Length > _options.MaxResponseSizeBytes)
                    throw new ResponseTooLargeException(html.Length, _options.MaxResponseSizeBytes, responseUrl.Url);

                var finalUrl = response.RequestMessage?.RequestUri?.ToString() ?? responseUrl.Url;
                return new ResponseHtmlContent(
                    Html: html,
                    FetchedAt: DateTimeOffset.UtcNow,
                    StatusCode: response.StatusCode,
                    ContentType: contentType);
            }
            catch (HttpRequestException ex) when (!cancellationToken.IsCancellationRequested)
            {
                if (attempt >= _options.MaxAttempts)
                    throw new WebPageFetchTimeoutException(attempt, responseUrl.Url, ex);
                await DelayWithJitterAsync(attempt, cancellationToken).ConfigureAwait(false);
            }
            catch (TaskCanceledException ex) when (!cancellationToken.IsCancellationRequested)
            {
                if (attempt >= _options.MaxAttempts)
                    throw new WebPageFetchTimeoutException(attempt, responseUrl.Url, ex);
                await DelayWithJitterAsync(attempt, cancellationToken).ConfigureAwait(false);
            }
        }
    }

    /// <summary>
    /// Creates a delay with exponential backoff and a random jitter, suitable for retry scenarios.
    /// </summary>
    /// <remarks>The delay duration increases exponentially with each attempt and includes a random jitter to
    /// reduce contention in concurrent retry scenarios.</remarks>
    /// <param name="attempt">The current retry attempt number. Must be greater than or equal to 1.</param>
    /// <param name="ct">A cancellation token that can be used to cancel the delay operation.</param>
    /// <returns>A task that completes after the calculated delay interval, or earlier if the operation is canceled.</returns>
    private static Task DelayWithJitterAsync(int attempt, CancellationToken ct)
    {
        var baseMs = (int)Math.Pow(2, attempt - 1) * 1000;
        var jitterMs = _jitter.Next(0, 500);
        return Task.Delay(baseMs + jitterMs, ct);
    }
}
