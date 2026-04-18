namespace DeepSigma.DataAccess.WebSearch.ContentExtraction.Fetchers;

/// <summary>Configuration options for <see cref="HttpWebPageFetcher"/>.</summary>
public sealed class WebPageFetcherOptions
{
    /// <summary>User-Agent header sent with every request. Defaults to the DeepSigmaBot identifier.</summary>
    public string UserAgent { get; set; } = "DeepSigmaBot/1.0 (+https://github.com/DeepSigma-LLC)";

    /// <summary>Per-request timeout. Defaults to 30 seconds.</summary>
    public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>Maximum number of attempts before propagating the last exception. Defaults to 3.</summary>
    public int MaxRetries { get; set; } = 3;

    /// <summary>Maximum allowed response size in bytes. Defaults to 10 MB.</summary>
    public long MaxResponseSizeBytes { get; set; } = 10 * 1024 * 1024;

    /// <summary>
    /// When <c>false</c> (default), responses whose Content-Type is not <c>text/html</c>
    /// will cause <see cref="HttpWebPageFetcher.FetchAsync"/> to throw.
    /// </summary>
    public bool AllowNonHtmlContent { get; set; } = false;
}
