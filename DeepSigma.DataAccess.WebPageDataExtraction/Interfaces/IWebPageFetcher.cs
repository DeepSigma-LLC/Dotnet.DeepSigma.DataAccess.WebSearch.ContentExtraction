using DeepSigma.DataAccess.WebSearch.ContentExtraction.Models;

namespace DeepSigma.DataAccess.WebSearch.ContentExtraction.Interfaces;

/// <summary>Downloads the raw HTML of a web page.</summary>
public interface IWebPageFetcher
{
    /// <summary>Fetches the page at <paramref name="url"/> and returns the raw HTML result.</summary>
    Task<WebPageFetchResult> FetchAsync(string url, CancellationToken ct = default);
}
