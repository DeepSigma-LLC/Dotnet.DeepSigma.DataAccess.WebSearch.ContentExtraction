namespace DeepSigma.DataAccess.WebPageDataExtraction.Models;

/// <summary>Structured content extracted from a web page.</summary>
public sealed record ExtractedContent(
    string Url,
    string? Title,
    string? Byline,
    string? Excerpt,
    string MainText,
    string? Language,
    DateTimeOffset? PublishedAt);
