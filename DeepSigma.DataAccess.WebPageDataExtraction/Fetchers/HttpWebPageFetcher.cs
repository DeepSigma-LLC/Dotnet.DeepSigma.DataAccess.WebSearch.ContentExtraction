using System.Net;
using DeepSigma.DataAccess.WebSearch.Abstraction;
using DeepSigma.DataAccess.WebSearch.Abstraction.Model;

namespace DeepSigma.DataAccess.WebSearch.ContentExtraction.Fetchers;

/// <summary>
/// Fetches web pages using <see cref="HttpClient"/> with automatic decompression,
/// redirect following, content-type validation, size limiting, and exponential-backoff retries.
/// </summary>
public sealed class HttpWebPageFetcher : IHtmlRetriever
{
    private readonly HttpClient _httpClient;
    private readonly WebPageFetcherOptions _options;

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

    /// <summary>
    /// Asynchronously retrieves the HTML content from the specified URL.
    /// </summary>
    /// <param name="URL">The URL of the web page to fetch content from. Must be a valid, absolute URL.</param>
    /// <param name="cancellationToken">An optional cancellation token that can be used to cancel the operation.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains a ResponseHtmlContent object with
    /// the retrieved HTML content.</returns>
	public async Task<ResponseHtmlContent> FetchContentAsync(string URL, CancellationToken cancellationToken = default)
	{
		ResponseUrlRetrival response = new(
            Url: URL,
            Title: null,
            Snippet: null,
            SearchEngine: "Manual",
			RetrievedAt: DateTimeOffset.UtcNow
        );
        return await FetchContentAsync(response, cancellationToken);
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
                    responseUrl.Url, HttpCompletionOption.ResponseHeadersRead, cts.Token);
                response.EnsureSuccessStatusCode();

                var contentType = response.Content.Headers.ContentType?.MediaType;
                if (!_options.AllowNonHtmlContent
                    && contentType != null
                    && !contentType.Contains("text/html", StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidOperationException($"Unexpected content type: {contentType}");
                }

                var contentLength = response.Content.Headers.ContentLength;
                if (contentLength.HasValue && contentLength.Value > _options.MaxResponseSizeBytes)
                    throw new InvalidOperationException(
                        $"Response size exceeds limit: {contentLength.Value} bytes");

                var html = await response.Content.ReadAsStringAsync(cts.Token);
                if (html.Length > _options.MaxResponseSizeBytes)
                    throw new InvalidOperationException(
                        $"Response size exceeds limit: {html.Length} chars");

                var finalUrl = response.RequestMessage?.RequestUri?.ToString() ?? responseUrl.Url;
                return new ResponseHtmlContent(
                    Url: finalUrl, 
                    Html: html,
                    FetchedAt: DateTimeOffset.UtcNow,
                    StatusCode: response.StatusCode,
                    ContentType: contentType,
                    SourceUrlRetrival: responseUrl);
            }
            catch (Exception ex) when (attempt < _options.MaxRetries && IsRetryable(ex, cancellationToken))
            {
                await Task.Delay(TimeSpan.FromSeconds(Math.Pow(2, attempt - 1)), cancellationToken);
            }
        }
    }

    private static bool IsRetryable(Exception ex, CancellationToken ct) =>
        !ct.IsCancellationRequested
        && ex is HttpRequestException or TaskCanceledException;
}
