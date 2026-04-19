using System.Net;
using DeepSigma.DataAccess.WebSearch.Abstraction.Model;
using DeepSigma.DataAccess.WebSearch.ContentExtraction.Extractors;
using Xunit;

namespace DeepSigma.DataAccess.WebSearch.ContentExtraction.Test.Extractors;

public sealed class AngleSharpContentExtractorTests
{
    private const string PageUrl = "https://example.com/page";

    private static ResponseHtmlContent Page(string html) =>
        new(Url: PageUrl, 
            Html: html, 
            ContentType: "text/html", 
            StatusCode: HttpStatusCode.OK, 
            FetchedAt: DateTimeOffset.UtcNow);

    private readonly AngleSharpContentExtractor _extractor = new();

    [Fact]
    public async Task ExtractAsync_ExtractsTitleFromTitleTag()
    {
        var html = "<html><head><title>My Article Title</title></head><body><p>Content</p></body></html>";

        var result = await _extractor.ExtractContentAsync(Page(html), CancellationToken.None);

        Assert.Equal("My Article Title", result.Title);
    }

    [Fact]
    public async Task ExtractAsync_FallsBackToH1_WhenNoTitleTag()
    {
        var html = "<html><body><h1>Fallback Heading</h1><p>Content</p></body></html>";

        var result = await _extractor.ExtractContentAsync(Page(html), CancellationToken.None);

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

        var result = await _extractor.ExtractContentAsync(Page(html), CancellationToken.None);

        Assert.Equal("A short summary.", result.Snippet);
    }

    [Fact]
    public async Task ExtractAsync_ExtractsLangAttribute()
    {
        var html = "<html lang=\"en\"><body><p>Hello</p></body></html>";

        var result = await _extractor.ExtractContentAsync(Page(html), CancellationToken.None);

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

        var result = await _extractor.ExtractContentAsync(Page(html), CancellationToken.None);

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

        var result = await _extractor.ExtractContentAsync(Page(html), CancellationToken.None);

        Assert.Contains("Real article content.", result.MainText);
        Assert.DoesNotContain("Site navigation noise", result.MainText);
        Assert.DoesNotContain("alert", result.MainText);
    }

    [Fact]
    public async Task ExtractAsync_ReturnsEmptyMainText_WhenNoParagraphsExist()
    {
        var html = "<html><body><div>No paragraphs here.</div></body></html>";

        var result = await _extractor.ExtractContentAsync(Page(html), CancellationToken.None);

        Assert.Equal(string.Empty, result.MainText);
    }

    [Fact]
    public async Task ExtractAsync_BylineIsAlwaysNull()
    {
        var html = "<html><body><p>Content</p></body></html>";

        var result = await _extractor.ExtractContentAsync(Page(html), CancellationToken.None);

        Assert.Null(result.Byline);
    }

    [Fact]
    public async Task ExtractAsync_PublishedAtIsAlwaysNull()
    {
        var html = "<html><body><p>Content</p></body></html>";

        var result = await _extractor.ExtractContentAsync(Page(html), CancellationToken.None);

        Assert.Null(result.PublishedAt);
    }

    [Fact]
    public async Task ExtractAsync_PreservesUrl()
    {
        var html = "<html><body><p>Content</p></body></html>";

        var result = await _extractor.ExtractContentAsync(Page(html), CancellationToken.None);

        Assert.Equal(PageUrl, result.SourceHtmlContent?.Url);
    }
}
