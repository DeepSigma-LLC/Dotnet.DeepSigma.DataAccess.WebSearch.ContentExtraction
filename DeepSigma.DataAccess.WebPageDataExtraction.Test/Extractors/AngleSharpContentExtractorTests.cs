using System.Net;
using DeepSigma.DataAccess.WebSearch.Abstraction.Model;
using DeepSigma.DataAccess.WebSearch.ContentExtraction.Extractors;
using DeepSigma.DataAccess.WebSearch.ContentExtraction.Fetchers;
using Xunit;

namespace DeepSigma.DataAccess.WebSearch.ContentExtraction.Test.Extractors;

public sealed class AngleSharpContentExtractorTests
{
    private const string PageUrl = "https://example.com/page";

    private static ResponseHtmlContent Page(string html) =>
        new(Html: html,
            FetchedAt: DateTimeOffset.UtcNow,
            ContentType: "text/html",
            StatusCode: HttpStatusCode.OK);

    private static ResponseUrlRetrival UrlInfo() =>
        new(Url: PageUrl, Title: null, Snippet: null, SearchEngine: "Test", RetrievedAt: DateTimeOffset.UtcNow);

    private readonly AngleSharpContentExtractor _extractor = new();

    [Fact]
    public async Task ExtractAsync_ExtractsTitleFromTitleTag()
    {
        var html = "<html><head><title>My Article Title</title></head><body><p>Content</p></body></html>";

        var result = await _extractor.ExtractContentAsync(Page(html), UrlInfo(), CancellationToken.None);

        Assert.Equal("My Article Title", result.Title);
    }

    [Fact]
    public async Task ExtractAsync_FallsBackToH1_WhenNoTitleTag()
    {
        var html = "<html><body><h1>Fallback Heading</h1><p>Content</p></body></html>";

        var result = await _extractor.ExtractContentAsync(Page(html), UrlInfo(), CancellationToken.None);

        Assert.Equal("Fallback Heading", result.Title);
    }

    [Fact]
    public async Task ExtractAsync_ExtractsExcerptFromMetaDescription()
    {
        var html = """
            <html>
              <head>
                <meta name="description" content="A short summary." />
              </head>
              <body><p>Content</p></body>
            </html>
            """;

        var result = await _extractor.ExtractContentAsync(Page(html), UrlInfo(), CancellationToken.None);

        Assert.Equal("A short summary.", result.Snippet);
    }

    [Fact]
    public async Task ExtractAsync_ExtractsLangAttribute()
    {
        var html = "<html lang=\"en\"><body><p>Hello</p></body></html>";

        var result = await _extractor.ExtractContentAsync(Page(html), UrlInfo(), CancellationToken.None);

        Assert.Equal("en", result.Language);
    }

    [Fact]
    public async Task ExtractAsync_ExtractsParagraphsIntoMainText()
    {
        var html = """
            <html><body>
              <p>First paragraph.</p>
              <p>Second paragraph.</p>
            </body></html>
            """;

        var result = await _extractor.ExtractContentAsync(Page(html), UrlInfo(), CancellationToken.None);

        Assert.Contains("First paragraph.", result.MainText);
        Assert.Contains("Second paragraph.", result.MainText);
    }

    [Fact]
    public async Task ExtractAsync_RemovesNavAndScriptContent()
    {
        var html = """
            <html><body>
              <nav><p>Site navigation noise</p></nav>
              <script>alert('injected');</script>
              <main><p>Real article content.</p></main>
            </body></html>
            """;

        var result = await _extractor.ExtractContentAsync(Page(html), UrlInfo(), CancellationToken.None);

        Assert.Contains("Real article content.", result.MainText);
        Assert.DoesNotContain("Site navigation noise", result.MainText);
        Assert.DoesNotContain("alert", result.MainText);
    }

    [Fact]
    public async Task ExtractAsync_ReturnsEmptyMainText_WhenNoParagraphsExist()
    {
        var html = "<html><body><div>No paragraphs here.</div></body></html>";

        var result = await _extractor.ExtractContentAsync(Page(html), UrlInfo(), CancellationToken.None);

        Assert.Equal(string.Empty, result.MainText);
    }

    [Fact]
    public async Task ExtractAsync_BylineIsAlwaysNull()
    {
        var html = "<html><body><p>Content</p></body></html>";

        var result = await _extractor.ExtractContentAsync(Page(html), UrlInfo(), CancellationToken.None);

        Assert.Null(result.Byline);
    }

    [Fact]
    public async Task ExtractAsync_PublishedAtIsAlwaysNull()
    {
        var html = "<html><body><p>Content</p></body></html>";

        var result = await _extractor.ExtractContentAsync(Page(html), UrlInfo(), CancellationToken.None);

        Assert.Null(result.PublishedAt);
    }

    [Fact]
    public async Task ExtractAsync_PreservesUrl()
    {
        var html = "<html><body><p>Content</p></body></html>";

        var result = await _extractor.ExtractContentAsync(Page(html), UrlInfo(), CancellationToken.None);

        Assert.Equal(PageUrl, result.SourceUrlRetrival?.Url);
    }

    [Fact]
    public async Task ExtractAsync_CustomNoisySelector_RemovesMatchingElements()
    {
        var html = """
            <html><body>
              <div class="cookie-banner"><p>Accept cookies</p></div>
              <main><p>Real content.</p></main>
            </body></html>
            """;

        var options = new AngleSharpExtractorOptions
        {
            NoisySelectors = [.. AngleSharpExtractorOptions.DefaultNoisySelectors, ".cookie-banner"]
        };
        var extractor = new AngleSharpContentExtractor(options);

        var result = await extractor.ExtractContentAsync(Page(html), UrlInfo(), CancellationToken.None);

        Assert.Contains("Real content.", result.MainText);
        Assert.DoesNotContain("Accept cookies", result.MainText);
    }

    [Fact]
    public async Task FallbackExtractor_UsedWhenSmartReaderReturnsEmpty()
    {
        const string nonArticleHtml = """
            <html><body>
              <p>Product feature one.</p>
              <p>Product feature two.</p>
            </body></html>
            """;

        var smartReader = new SmartReaderContentExtractor();
        var angleSharp = new AngleSharpContentExtractor();

        var page = new ResponseHtmlContent(
            Html: nonArticleHtml,
            FetchedAt: DateTimeOffset.UtcNow,
            ContentType: "text/html",
            StatusCode: HttpStatusCode.OK);
        var urlInfo = UrlInfo();

        var smart = await smartReader.ExtractContentAsync(page, urlInfo, CancellationToken.None);
        var result = string.IsNullOrWhiteSpace(smart.MainText)
            ? await angleSharp.ExtractContentAsync(page, urlInfo, CancellationToken.None)
            : smart;

        Assert.Contains("Product feature one.", result.MainText);
        Assert.Contains("Product feature two.", result.MainText);
    }
}
