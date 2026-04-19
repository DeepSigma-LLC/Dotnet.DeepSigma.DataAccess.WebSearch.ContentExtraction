using System.Net;
using Microsoft.Playwright;
using DeepSigma.DataAccess.WebSearch.Abstraction;
using DeepSigma.DataAccess.WebSearch.Abstraction.Model;
using System.Diagnostics.CodeAnalysis;

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
    private readonly string _userAgent;
    private readonly SemaphoreSlim _initLock = new(1, 1);
    private IPlaywright? _playwright;
    private IBrowser? _browser;

    /// <param name="userAgent">
    /// Optional User-Agent header. Defaults to the DeepSigmaBot identifier.
    /// </param>
    public PlaywrightWebPageFetcher(string? userAgent = null)
    {
        _userAgent = userAgent ?? "DefaultUserAgent/1.0";
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
			RetrievedAt: DateTimeOffset.UtcNow
        );
        return FetchContentAsync(response, cancellationToken);
	}

	/// <inheritdoc/>
	public async Task<ResponseHtmlContent> FetchContentAsync(ResponseUrlRetrival response, CancellationToken cancellationToken = default)
    {
        var browser = await EnsureBrowserAsync(cancellationToken);
        var page = await browser.NewPageAsync();
        try
        {
            await page.SetExtraHTTPHeadersAsync(
                new Dictionary<string, string> { ["User-Agent"] = _userAgent });

            await page.GotoAsync(response.Url, new PageGotoOptions
            {
                WaitUntil = WaitUntilState.NetworkIdle
            });

            var html = await page.ContentAsync();
            return new ResponseHtmlContent(
                Url: response.Url, 
                Html: html, 
                FetchedAt: DateTimeOffset.UtcNow, 
                ContentType:"text/html", StatusCode: 
                HttpStatusCode.OK,
                SourceUrlRetrival: response
                );
        }
        finally
        {
            await page.CloseAsync();
        }
    }

    private async Task<IBrowser> EnsureBrowserAsync(CancellationToken ct)
    {
        if (_browser is not null) return _browser;

        await _initLock.WaitAsync(ct);
        try
        {
            if (_browser is not null) return _browser;
            _playwright = await Playwright.CreateAsync();
            _browser = await _playwright.Chromium.LaunchAsync(
                new BrowserTypeLaunchOptions { Headless = true });
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
            await _browser.DisposeAsync();

        _playwright?.Dispose();
        _initLock.Dispose();
    }


}
