using SmartReader;
using DeepSigma.DataAccess.WebSearch.Abstraction;
using DeepSigma.DataAccess.WebSearch.Abstraction.Model;

namespace DeepSigma.DataAccess.WebSearch.ContentExtraction.Extractors;

/// <summary>
/// Extracts the main article content using SmartReader, a .NET port of Mozilla Readability.
/// This is the recommended extractor — it strips navigation, ads, footers, and sidebars
/// automatically before returning clean text.
/// </summary>
public sealed class SmartReaderContentExtractor : IContentExtractor
{
    /// <inheritdoc/>
    public async Task<ResponseExtractedContent> ExtractContentAsync(ResponseHtmlContent htmlContent, ResponseUrlRetrival urlContent, CancellationToken cancellationToken = default)
    {
        // NOTE: Reader.ParseArticleAsync (static) does not use the provided HTML string in
        // SmartReader 0.11.0 — use the instance constructor instead.
        using var reader = new Reader(urlContent.Url, htmlContent.Html);
        var article = await reader.GetArticleAsync(cancellationToken).ConfigureAwait(false);

        var mainText = article.IsReadable
            ? article.TextContent?.Trim() ?? string.Empty
            : string.Empty;

        DateTimeOffset? publishedAt = article.PublicationDate.HasValue
                                     && article.PublicationDate.Value != DateTime.MinValue
            ? new DateTimeOffset(article.PublicationDate.Value)
            : null;

        return new ResponseExtractedContent(
            SourceUrlRetrival: urlContent,
            SourceHtmlContent: htmlContent,
            MainText: mainText,
            Title: NullIfWhiteSpace(article.Title),
            Byline: NullIfWhiteSpace(article.Byline),
            Language: NullIfWhiteSpace(article.Language),
            Snippet: NullIfWhiteSpace(article.Excerpt),
            PublishedAt: publishedAt);
    }

    private static string? NullIfWhiteSpace(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value;
}
