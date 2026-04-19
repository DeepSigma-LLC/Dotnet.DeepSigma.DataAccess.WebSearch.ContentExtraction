using AngleSharp;
using DeepSigma.DataAccess.WebSearch.Abstraction;
using DeepSigma.DataAccess.WebSearch.Abstraction.Model;

namespace DeepSigma.DataAccess.WebSearch.ContentExtraction.Extractors;

/// <summary>
/// Fallback extractor that uses AngleSharp to parse HTML and extract paragraphs.
/// Prefer <see cref="SmartReaderContentExtractor"/> for better article fidelity.
/// Use this extractor when the page is not article-style or SmartReader returns no readable content.
/// </summary>
public sealed class AngleSharpContentExtractor : IContentExtractor
{
    private readonly AngleSharpExtractorOptions _options;

    /// <param name="options">Optional configuration. When omitted a default instance is used.</param>
    public AngleSharpContentExtractor(AngleSharpExtractorOptions? options = null)
    {
        _options = options ?? new AngleSharpExtractorOptions();
    }

    /// <inheritdoc/>
    public async Task<ResponseExtractedContent> ExtractContentAsync(ResponseHtmlContent pageResponseContent, ResponseUrlRetrival urlRetrival, CancellationToken cancellationToken = default)
    {
        var config = Configuration.Default;
        var context = BrowsingContext.New(config);
        var document = await context.OpenAsync(req => req.Content(pageResponseContent.Html), cancellationToken).ConfigureAwait(false);
        var title = document.QuerySelector("title")?.TextContent?.Trim()
                    ?? document.QuerySelector("h1")?.TextContent?.Trim();

        var excerpt = document.QuerySelector("meta[name='description']")
                              ?.GetAttribute("content")?.Trim();

        var publishedAtMeta = document.QuerySelector("meta[property='article:published_time']")
                    ?? document.QuerySelector("meta[name='pubdate']")
                    ?? document.QuerySelector("meta[name='publication_date']");

        DateTimeOffset? publishedAt = publishedAtMeta != null && DateTime.TryParse(publishedAtMeta.GetAttribute("content"), out var dt)
                        ? new DateTimeOffset(dt)
                        : null;

        var lang = document.DocumentElement.GetAttribute("lang");

        foreach (var selector in _options.NoisySelectors)
        {
            foreach (var el in document.QuerySelectorAll(selector))
                el.Remove();
        }

        var paragraphs = document.QuerySelectorAll("article p, main p, p");

        var mainText = string.Join("\n\n",
            paragraphs
                .Select(p => p.TextContent.Trim())
                .Where(t => !string.IsNullOrWhiteSpace(t)));

        return new ResponseExtractedContent(
            SourceUrlRetrival: urlRetrival,
            SourceHtmlContent: pageResponseContent,
            MainText: mainText,
            Title: string.IsNullOrWhiteSpace(title) ? null : title,
            Snippet: string.IsNullOrWhiteSpace(excerpt) ? null : excerpt,
            Language: string.IsNullOrWhiteSpace(lang) ? null : lang,
            PublishedAt: publishedAt);
    }
}
