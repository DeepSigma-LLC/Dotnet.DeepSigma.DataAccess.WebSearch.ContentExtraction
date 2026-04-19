using System.Net;
using Microsoft.Playwright;
using DeepSigma.DataAccess.WebSearch.Abstraction;
using DeepSigma.DataAccess.WebSearch.Abstraction.Model;

namespace DeepSigma.DataAccess.WebSearch.ContentExtraction.Fetchers;

/// <summary>
/// Fetches JavaScript-heavy pages by launching a headless Chromium browser via Playwright
/// and waiting for network activity to settle before capturing the final HTML.
/// <para>
/// <b>Prerequisites:</b> call <c>playwright install chromium</c> on the host before use.
/// See https://playwright.dev/dotnet/docs/intro for setup instructions.
/// </para>
/// </summary>
public sealed class PlaywrightWebPageFetcher : IHtmlRetriever, IAsyncDisposable
{
    private readonly WebPageFetcherOptions _options;
    private readonly SemaphoreSlim _initLock = new(1, 1);
    private IPlaywright? _playwright;
    private IBrowser? _browser;

    /// <param name="options">
    /// Optional fetcher options. When omitted a default instance is used.
    /// </param>
    public PlaywrightWebPageFetcher(WebPageFetcherOptions? options = null)
    {
        _options = options ?? new WebPageFetcherOptions();
    }

    /// <summary>
    /// Asynchronously retrieves the HTML content from the specified URL.
    /// </summary>
    /// <param name="url">The URL of the web page to fetch. Cannot be null or empty.</param>
    /// <param name="cancellationToken">An optional token to monitor for cancellation requests.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains a ResponseHtmlContent object with
    /// the retrieved HTML content.</returns>
    public Task<ResponseHtmlContent> FetchContentAsync(string url, CancellationToken cancellationToken = default)
    {
        ResponseUrlRetrival response = new(
            Url: url,
            Title: null,
            Snippet: null,
            SearchEngine: "Manual",
            RetrievedAt: DateTimeOffset.UtcNow);
        return FetchContentAsync(response, cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<ResponseHtmlContent> FetchContentAsync(ResponseUrlRetrival response, CancellationToken cancellationToken = default)
    {
        var browser = await EnsureBrowserAsync(cancellationToken).ConfigureAwait(false);
        var page = await browser.NewPageAsync().ConfigureAwait(false);
        try
        {
            await page.SetExtraHTTPHeadersAsync(
                new Dictionary<string, string> { ["User-Agent"] = _options.UserAgent }).ConfigureAwait(false);

            await page.GotoAsync(response.Url, new PageGotoOptions
            {
                WaitUntil = WaitUntilState.NetworkIdle,
                Timeout = (float)_options.Timeout.TotalMilliseconds
            }).ConfigureAwait(false);

            var html = await page.ContentAsync().ConfigureAwait(false);
            return new ResponseHtmlContent(
                Url: response.Url,
                Html: html,
                FetchedAt: DateTimeOffset.UtcNow,
                ContentType: "text/html",
                StatusCode: HttpStatusCode.OK,
                SourceUrlRetrival: response);
        }
        finally
        {
            await page.CloseAsync().ConfigureAwait(false);
        }
    }

    private async Task<IBrowser> EnsureBrowserAsync(CancellationToken ct)
    {
        if (_browser is not null) return _browser;

        await _initLock.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            if (_browser is not null) return _browser;
            _playwright = await Playwright.CreateAsync().ConfigureAwait(false);
            _browser = await _playwright.Chromium.LaunchAsync(
                new BrowserTypeLaunchOptions { Headless = true }).ConfigureAwait(false);
            return _browser;
        }
        finally
        {
            _initLock.Release();
        }
    }

    /// <inheritdoc/>
    public async ValueTask DisposeAsync()
    {
        if (_browser is not null)
            await _browser.DisposeAsync().ConfigureAwait(false);

        _playwright?.Dispose();
        _initLock.Dispose();
    }
}
