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
    /// <summary>
    /// Extracts structured content from the specified HTML string asynchronously.
    /// </summary>
    /// <param name="html">The HTML content to extract information from. Cannot be null or empty.</param>
    /// <param name="url">The original URL of the HTML content. Used for context in extraction. Optional.</param>
    /// <param name="cancellationToken">A cancellation token that can be used to cancel the asynchronous operation. Optional.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains a ResponseExtractedContent object
    /// with the extracted content.</returns>
	public async Task<ResponseExtractedContent> ExtractedContentAsync(string html, string? url = null, CancellationToken? cancellationToken = null)
	{
        ResponseHtmlContent pageResponseContent = new(
            URL: url ?? string.Empty,
            HTML: html,
            FetchedAt: DateTimeOffset.UtcNow,
            ContentType: "text/html",
            StatusCode: System.Net.HttpStatusCode.OK
        );
        return await ExtractedContentAsync(pageResponseContent, cancellationToken);
	}

	/// <inheritdoc/>
	public async Task<ResponseExtractedContent> ExtractedContentAsync(ResponseHtmlContent htmlContent, CancellationToken? cancellationToken = default)
    {
        // NOTE: Reader.ParseArticleAsync (static) does not use the provided HTML string in
        // SmartReader 0.11.0 — use the instance constructor instead.
        using var reader = new Reader(htmlContent.URL, htmlContent.HTML);
        var article = await reader.GetArticleAsync(cancellationToken ?? CancellationToken.None);

        var mainText = article.IsReadable
            ? article.TextContent?.Trim() ?? string.Empty
            : string.Empty;

        DateTimeOffset? publishedAt = article.PublicationDate.HasValue
                                     && article.PublicationDate.Value != DateTime.MinValue
            ? new DateTimeOffset(article.PublicationDate.Value)
            : null;

        return new ResponseExtractedContent(
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
