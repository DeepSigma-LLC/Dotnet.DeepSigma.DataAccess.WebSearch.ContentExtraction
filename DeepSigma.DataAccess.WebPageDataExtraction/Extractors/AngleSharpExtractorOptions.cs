namespace DeepSigma.DataAccess.WebSearch.ContentExtraction.Extractors;

/// <summary>Configuration options for <see cref="AngleSharpContentExtractor"/>.</summary>
public sealed class AngleSharpExtractorOptions
{
    /// <summary>
    /// The built-in list of noisy CSS selectors removed before paragraph extraction.
    /// Use this as a base when extending <see cref="NoisySelectors"/>.
    /// </summary>
    public static readonly IReadOnlyList<string> DefaultNoisySelectors =
    [
        "script", "style", "nav", "footer", "header", "aside",
        "[role='navigation']", "[role='banner']", "[role='complementary']"
    ];

    /// <summary>
    /// CSS selectors for elements that are removed from the document before paragraph extraction.
    /// Defaults to <see cref="DefaultNoisySelectors"/>.
    /// Add selectors to extend the list, e.g. <c>".cookie-banner"</c>.
    /// </summary>
    public IReadOnlyList<string> NoisySelectors { get; set; } = DefaultNoisySelectors;
}
