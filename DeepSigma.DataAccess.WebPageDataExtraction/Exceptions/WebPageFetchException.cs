namespace DeepSigma.DataAccess.WebSearch.ContentExtraction.Exceptions;

/// <summary>Base class for exceptions thrown by web page fetching operations.</summary>
public class WebPageFetchException : Exception
{
    /// <summary>The URL that was being fetched when the exception occurred.</summary>
    public string? Url { get; }

    /// <inheritdoc cref="Exception(string)"/>
    public WebPageFetchException(string message, string? url = null)
        : base(message)
    {
        Url = url;
    }

    /// <inheritdoc cref="Exception(string, Exception)"/>
    public WebPageFetchException(string message, string? url, Exception innerException)
        : base(message, innerException)
    {
        Url = url;
    }
}

/// <summary>
/// Thrown when the response Content-Type is not <c>text/html</c> and
/// <see cref="Fetchers.WebPageFetcherOptions.AllowNonHtmlContent"/> is <c>false</c>.
/// </summary>
public sealed class ContentTypeNotAllowedException : WebPageFetchException
{
    /// <summary>The actual Content-Type that was returned.</summary>
    public string? ContentType { get; }

    /// <inheritdoc cref="WebPageFetchException(string, string)"/>
    public ContentTypeNotAllowedException(string contentType, string? url = null)
        : base($"Unexpected content type '{contentType}'. Set AllowNonHtmlContent = true to allow non-HTML responses.", url)
    {
        ContentType = contentType;
    }
}

/// <summary>
/// Thrown when the response body exceeds <see cref="Fetchers.WebPageFetcherOptions.MaxResponseSizeBytes"/>.
/// </summary>
public sealed class ResponseTooLargeException : WebPageFetchException
{
    /// <summary>The actual response size in bytes (or character count) that exceeded the limit.</summary>
    public long ActualSize { get; }

    /// <summary>The configured size limit in bytes.</summary>
    public long Limit { get; }

    /// <inheritdoc cref="WebPageFetchException(string, string)"/>
    public ResponseTooLargeException(long actualSize, long limit, string? url = null)
        : base($"Response size {actualSize} exceeds the configured limit of {limit} bytes.", url)
    {
        ActualSize = actualSize;
        Limit = limit;
    }
}
