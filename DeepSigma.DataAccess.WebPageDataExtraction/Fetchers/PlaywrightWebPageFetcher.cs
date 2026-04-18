using System.Net;
using Microsoft.Playwright;
using DeepSigma.DataAccess.WebSearch.ContentExtraction.Interfaces;
using DeepSigma.DataAccess.WebSearch.ContentExtraction.Models;

namespace DeepSigma.DataAccess.WebSearch.ContentExtraction.Fetchers;

/// <summary>
/// Fetches JavaScript-heavy pages by launching a headless Chromium browser via Playwright
/// and waiting for network activity to settle before capturing the final HTML.
/// <para>
/// <b>Prerequisites:</b> call <c>playwright install chromium</c> on the host before use.
/// See https://playwright.dev/dotnet/docs/intro for setup instructions.
/// </para>
/// </summary>
public sealed class PlaywrightWebPageFetcher : IWebPageFetcher, IAsyncDisposable
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
        _userAgent = userAgent ?? "DeepSigmaBot/1.0 (+https://github.com/DeepSigma-LLC)";
    }

    /// <inheritdoc/>
    public async Task<WebPageFetchResult> FetchAsync(string url, CancellationToken ct = default)
    {
        var browser = await EnsureBrowserAsync(ct);
        var page = await browser.NewPageAsync();
        try
        {
            await page.SetExtraHTTPHeadersAsync(
                new Dictionary<string, string> { ["User-Agent"] = _userAgent });

            await page.GotoAsync(url, new PageGotoOptions
            {
                WaitUntil = WaitUntilState.NetworkIdle
            });

            var html = await page.ContentAsync();
            return new WebPageFetchResult(url, html, "text/html", HttpStatusCode.OK);
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
