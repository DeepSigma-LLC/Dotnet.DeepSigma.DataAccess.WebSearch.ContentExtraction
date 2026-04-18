using System.Net;
using DeepSigma.DataAccess.WebSearch.ContentExtraction.Interfaces;
using DeepSigma.DataAccess.WebSearch.ContentExtraction.Models;

namespace DeepSigma.DataAccess.WebSearch.ContentExtraction.Fetchers;

/// <summary>
/// Fetches web pages using <see cref="HttpClient"/> with automatic decompression,
/// redirect following, content-type validation, size limiting, and exponential-backoff retries.
/// </summary>
public sealed class HttpWebPageFetcher : IWebPageFetcher
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

    /// <inheritdoc/>
    public async Task<WebPageFetchResult> FetchAsync(string url, CancellationToken ct = default)
    {
        int attempt = 0;
        while (true)
        {
            attempt++;
            try
            {
                using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
                cts.CancelAfter(_options.Timeout);

                using var response = await _httpClient.GetAsync(
                    url, HttpCompletionOption.ResponseHeadersRead, cts.Token);
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

                var finalUrl = response.RequestMessage?.RequestUri?.ToString() ?? url;
                return new WebPageFetchResult(finalUrl, html, contentType, response.StatusCode);
            }
            catch (Exception ex) when (attempt < _options.MaxRetries && IsRetryable(ex, ct))
            {
                await Task.Delay(TimeSpan.FromSeconds(Math.Pow(2, attempt - 1)), ct);
            }
        }
    }

    private static bool IsRetryable(Exception ex, CancellationToken ct) =>
        !ct.IsCancellationRequested
        && ex is HttpRequestException or TaskCanceledException;
}
