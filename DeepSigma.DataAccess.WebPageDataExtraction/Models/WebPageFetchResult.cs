using System.Net;

namespace DeepSigma.DataAccess.WebPageDataExtraction.Models;

/// <summary>Represents the raw result of fetching a web page.</summary>
public sealed record WebPageFetchResult(
    string Url,
    string Html,
    string? ContentType,
    HttpStatusCode StatusCode);
