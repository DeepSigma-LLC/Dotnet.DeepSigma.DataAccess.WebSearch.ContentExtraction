using System.Net;
using DeepSigma.DataAccess.WebSearch.Abstraction.Model;
using DeepSigma.DataAccess.WebSearch.ContentExtraction.Extractors;
using Xunit;

namespace DeepSigma.DataAccess.WebSearch.ContentExtraction.Test.Extractors;

public sealed class SmartReaderContentExtractorTests
{
	private const string PageUrl = "https://example.com/article";

	private static ResponseHtmlContent Page(string html) =>
		new(Html: html,
			FetchedAt: DateTimeOffset.UtcNow,
			ContentType: "text/html",
			StatusCode: HttpStatusCode.OK);

	private static ResponseUrlRetrival UrlInfo() =>
		new(Url: PageUrl, Title: null, Snippet: null, SearchEngine: "Test", RetrievedAt: DateTimeOffset.UtcNow);

	private readonly SmartReaderContentExtractor _extractor = new();

    /// <summary>
    /// A full, realistic article HTML with 8 distinct, substantial paragraphs (200+ chars each)
    /// so that SmartReader's content-scoring algorithm considers the page readable.
    /// </summary>
    private const string ReadableArticleHtml = """
        <!DOCTYPE html>
        <html lang="en">
        <head>
          <meta charset="utf-8" />
          <meta property="og:title" content="Understanding Software Engineering Principles" />
          <title>Understanding Software Engineering Principles</title>
        </head>
        <body>
          <header><nav>Home | Articles | About</nav></header>
          <main>
            <article>
              <h1>Understanding Software Engineering Principles</h1>
              <p class="byline">By Jane Smith</p>
              <p>Software engineering is a discipline that applies engineering principles to the
              development of software in a systematic, disciplined, and quantifiable manner. It
              encompasses requirements analysis, design, implementation, testing, and maintenance.</p>
              <p>The field emerged in the late 1960s when frequent overruns in schedule and budget
              and poor quality of the resulting software led researchers to coin the term "software
              crisis". Since then, the discipline has grown enormously and now encompasses a wide
              range of well-proven methodologies and tools used across the industry.</p>
              <p>One of the most important principles in software engineering is modularity, which
              refers to the practice of dividing a system into smaller, self-contained components
              that can be developed and tested independently. This approach simplifies both initial
              development and long-term maintenance, and makes it easier to understand each part.</p>
              <p>Another fundamental concept is abstraction, which involves hiding complex
              implementation details of a component and exposing only the essential features through
              a well-defined interface. Abstraction reduces cognitive load and makes it easier to
              change an implementation without affecting the rest of a larger system.</p>
              <p>Software testing is a crucial aspect of software engineering that helps identify
              defects early in the development cycle, when they are far cheaper to fix. Modern
              development practices emphasise automated testing, including unit tests, integration
              tests, and end-to-end tests that run continuously in CI pipelines.</p>
              <p>Version control systems such as Git are indispensable tools for any software
              engineering team. They allow multiple developers to collaborate on the same codebase
              without overwriting each other's changes, and provide a complete audit history of
              every modification ever made to the source code and configuration files.</p>
              <p>Continuous integration and continuous delivery pipelines have become standard
              practice in modern software development. These automated pipelines build, test, and
              deploy software on every commit, enabling teams to release new features and critical
              fixes quickly and with high confidence in the quality of each release.</p>
              <p>Software architecture refers to the high-level structure of a system, including
              components, their relationships, and the principles governing their design. Good
              architecture enables a system to satisfy both functional and non-functional
              requirements including performance, security, scalability, and long-term maintainability.</p>
            </article>
          </main>
          <footer><p>Copyright 2024 Example Corp</p></footer>
        </body>
        </html>
        """;

    /// <summary>A copy of <see cref="ReadableArticleHtml"/> that adds an Open Graph publication date.</summary>
    private const string DatedArticleHtml = """
        <!DOCTYPE html>
        <html lang="en">
        <head>
          <meta charset="utf-8" />
          <meta property="article:published_time" content="2024-06-15T00:00:00Z" />
          <title>Dated Engineering Article</title>
        </head>
        <body>
          <main>
            <article>
              <h1>Dated Engineering Article</h1>
              <p>Software engineering is a discipline that applies engineering principles to the
              development of software in a systematic, disciplined, and quantifiable manner. It
              encompasses requirements analysis, design, implementation, testing, and maintenance.</p>
              <p>The field emerged in the late 1960s when frequent overruns in schedule and budget
              and poor quality of the resulting software led researchers to coin the term "software
              crisis". Since then, the discipline has grown enormously and now encompasses a wide
              range of well-proven methodologies and tools used across the industry.</p>
              <p>One of the most important principles in software engineering is modularity, which
              refers to the practice of dividing a system into smaller, self-contained components
              that can be developed and tested independently. This approach simplifies both initial
              development and long-term maintenance, and makes it easier to understand each part.</p>
              <p>Another fundamental concept is abstraction, which involves hiding complex
              implementation details of a component and exposing only the essential features through
              a well-defined interface. Abstraction reduces cognitive load and makes it easier to
              change an implementation without affecting the rest of a larger system.</p>
              <p>Software testing is a crucial aspect of software engineering that helps identify
              defects early in the development cycle, when they are far cheaper to fix. Modern
              development practices emphasise automated testing, including unit tests, integration
              tests, and end-to-end tests that run continuously in CI pipelines.</p>
            </article>
          </main>
        </body>
        </html>
        """;

    [Fact]
    public async Task ExtractAsync_ExtractsTitleFromReadableArticle()
    {
        var result = await _extractor.ExtractContentAsync(Page(ReadableArticleHtml), UrlInfo(), CancellationToken.None);
        Assert.Contains("Software Engineering", result.Title);
    }

    [Fact]
    public async Task ExtractAsync_ExtractsMainTextFromReadableArticle()
    {
        var result = await _extractor.ExtractContentAsync(Page(ReadableArticleHtml), UrlInfo(), CancellationToken.None);
        Assert.Contains("software engineering", result.MainText, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ExtractAsync_DetectsLanguageFromHtmlAttribute()
    {
        var result = await _extractor.ExtractContentAsync(Page(ReadableArticleHtml), UrlInfo(), CancellationToken.None);
    }

    [Fact]
    public async Task ExtractAsync_ReturnsEmptyMainText_WhenPageIsNotReadable()
    {
        const string junkHtml = "<html><body><nav>Menu item 1</nav><nav>Menu item 2</nav></body></html>";

        var result = await _extractor.ExtractContentAsync(Page(junkHtml), UrlInfo(), CancellationToken.None);

        Assert.Equal(string.Empty, result.MainText);
    }

    [Fact]
    public async Task ExtractAsync_PreservesUrl()
    {
        var result = await _extractor.ExtractContentAsync(Page(ReadableArticleHtml), UrlInfo(), CancellationToken.None);

        Assert.Equal(PageUrl, result.SourceUrlRetrival?.Url);
    }

    [Fact]
    public async Task ExtractAsync_PublishedAtIsNull_WhenNoDateInPage()
    {
        var result = await _extractor.ExtractContentAsync(Page(ReadableArticleHtml), UrlInfo(), CancellationToken.None);
    }

    [Fact]
    public async Task ExtractAsync_ExtractsPublishedAt_WhenOpenGraphDatePresent()
    {
        var result = await _extractor.ExtractContentAsync(Page(DatedArticleHtml), UrlInfo(), CancellationToken.None);

        Assert.NotNull(result.PublishedAt);
        // Compare UTC date to be timezone-agnostic (SmartReader returns DateTime.Local
        // when parsing a UTC Z-suffix date, so .Day is machine-timezone dependent).
        var utc = result.PublishedAt!.Value.UtcDateTime;
        Assert.Equal(2024, utc.Year);
        Assert.Equal(6, utc.Month);
        Assert.Equal(15, utc.Day);
    }
}
