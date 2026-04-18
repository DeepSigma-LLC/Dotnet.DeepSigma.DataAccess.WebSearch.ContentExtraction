using AngleSharp;
using DeepSigma.DataAccess.WebSearch.ContentExtraction.Interfaces;
using DeepSigma.DataAccess.WebSearch.ContentExtraction.Models;

namespace DeepSigma.DataAccess.WebSearch.ContentExtraction.Extractors;

/// <summary>
/// Fallback extractor that uses AngleSharp to parse HTML and extract paragraphs.
/// Prefer <see cref="SmartReaderContentExtractor"/> for better article fidelity.
/// Use this extractor when the page is not article-style or SmartReader returns no readable content.
/// </summary>
public sealed class AngleSharpContentExtractor : IContentExtractor
{
    private static readonly string[] NoisySelectors =
    [
        "script", "style", "nav", "footer", "header", "aside",
        "[role='navigation']", "[role='banner']", "[role='complementary']"
    ];

    /// <inheritdoc/>
    public async Task<ExtractedContent> ExtractAsync(WebPageFetchResult page, CancellationToken ct = default)
    {
        var config = Configuration.Default;
        var context = BrowsingContext.New(config);
        var document = await context.OpenAsync(req => req.Content(page.Html), ct);

        var title = document.QuerySelector("title")?.TextContent?.Trim()
                    ?? document.QuerySelector("h1")?.TextContent?.Trim();

        var excerpt = document.QuerySelector("meta[name='description']")
                              ?.GetAttribute("content")?.Trim();

        var lang = document.DocumentElement.GetAttribute("lang");

        foreach (var selector in NoisySelectors)
        {
            foreach (var el in document.QuerySelectorAll(selector))
                el.Remove();
        }

        var paragraphs = document.QuerySelectorAll("article p, main p, p");
        var mainText = string.Join("\n\n",
            paragraphs
                .Select(p => p.TextContent.Trim())
                .Where(t => !string.IsNullOrWhiteSpace(t)));

        return new ExtractedContent(
            page.Url,
            string.IsNullOrWhiteSpace(title) ? null : title,
            null,
            string.IsNullOrWhiteSpace(excerpt) ? null : excerpt,
            mainText,
            string.IsNullOrWhiteSpace(lang) ? null : lang,
            null);
    }
}
