using DeepSigma.DataAccess.WebSearch.ContentExtraction.Models;

namespace DeepSigma.DataAccess.WebSearch.ContentExtraction.Interfaces;

/// <summary>Parses and extracts structured content from a fetched web page.</summary>
public interface IContentExtractor
{
    /// <summary>Extracts structured content from the provided <paramref name="page"/>.</summary>
    Task<ExtractedContent> ExtractAsync(WebPageFetchResult page, CancellationToken ct = default);
}
