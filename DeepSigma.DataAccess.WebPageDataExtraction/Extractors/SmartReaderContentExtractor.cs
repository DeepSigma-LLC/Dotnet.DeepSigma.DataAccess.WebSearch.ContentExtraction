using SmartReader;
using DeepSigma.DataAccess.WebPageDataExtraction.Interfaces;
using DeepSigma.DataAccess.WebPageDataExtraction.Models;

namespace DeepSigma.DataAccess.WebPageDataExtraction.Extractors;

/// <summary>
/// Extracts the main article content using SmartReader, a .NET port of Mozilla Readability.
/// This is the recommended extractor — it strips navigation, ads, footers, and sidebars
/// automatically before returning clean text.
/// </summary>
public sealed class SmartReaderContentExtractor : IContentExtractor
{
    /// <inheritdoc/>
    public async Task<ExtractedContent> ExtractAsync(WebPageFetchResult page, CancellationToken ct = default)
    {
        // NOTE: Reader.ParseArticleAsync (static) does not use the provided HTML string in
        // SmartReader 0.11.0 — use the instance constructor instead.
        using var reader = new Reader(page.Url, page.Html);
        var article = await reader.GetArticleAsync(ct);

        var mainText = article.IsReadable
            ? article.TextContent?.Trim() ?? string.Empty
            : string.Empty;

        DateTimeOffset? publishedAt = article.PublicationDate.HasValue
                                     && article.PublicationDate.Value != DateTime.MinValue
            ? new DateTimeOffset(article.PublicationDate.Value)
            : null;

        return new ExtractedContent(
            page.Url,
            NullIfWhiteSpace(article.Title),
            NullIfWhiteSpace(article.Byline),
            NullIfWhiteSpace(article.Excerpt),
            mainText,
            NullIfWhiteSpace(article.Language),
            publishedAt);
    }

    private static string? NullIfWhiteSpace(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value;
}
