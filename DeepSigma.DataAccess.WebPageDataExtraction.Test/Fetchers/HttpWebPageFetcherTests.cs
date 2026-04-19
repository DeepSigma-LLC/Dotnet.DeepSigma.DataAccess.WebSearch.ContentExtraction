using System.Net;
using System.Net.Http.Headers;
using DeepSigma.DataAccess.WebSearch.ContentExtraction.Exceptions;
using DeepSigma.DataAccess.WebSearch.ContentExtraction.Fetchers;
using Xunit;

namespace DeepSigma.DataAccess.WebSearch.ContentExtraction.Test.Fetchers;

public sealed class HttpWebPageFetcherTests
{
    private const string SampleUrl = "https://example.com/article";
    private const string SampleHtml = "<html><body><p>Hello world</p></body></html>";

    // ---------------------------------------------------------------------------
    // Helpers
    // ---------------------------------------------------------------------------

    private static HttpWebPageFetcher BuildFetcher(
        Func<HttpRequestMessage, HttpResponseMessage> handler,
        WebPageFetcherOptions? options = null)
    {
        var http = new HttpClient(new FakeHttpMessageHandler(handler));
        return new HttpWebPageFetcher(http, options ?? new WebPageFetcherOptions());
    }

    private static HttpResponseMessage HtmlResponse(
        string html = SampleHtml,
        HttpStatusCode status = HttpStatusCode.OK,
        string? finalUrl = null)
    {
        var response = new HttpResponseMessage(status)
        {
            Content = new StringContent(html),
            RequestMessage = new HttpRequestMessage(HttpMethod.Get,
                new Uri(finalUrl ?? SampleUrl))
        };
        response.Content.Headers.ContentType = new MediaTypeHeaderValue("text/html");
        return response;
    }

    // ---------------------------------------------------------------------------
    // Happy-path tests
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task FetchAsync_ReturnsHtmlResult_WhenResponseIsOk()
    {
        var fetcher = BuildFetcher(_ => HtmlResponse());

        var result = await fetcher.FetchContentAsync(SampleUrl, CancellationToken.None);

        Assert.Equal(SampleUrl, result.SourceUrlRetrival?.Url);
        Assert.Equal(SampleHtml, result.Html);
        Assert.Equal("text/html", result.ContentType);
        Assert.Equal(HttpStatusCode.OK, result.StatusCode);
    }

    [Fact]
    public async Task FetchAsync_UsesRedirectedUrl_AsResultUrl()
    {
        const string redirectedUrl = "https://example.com/canonical-article";
        var fetcher = BuildFetcher(_ => HtmlResponse(finalUrl: redirectedUrl));

        var result = await fetcher.FetchContentAsync(SampleUrl, CancellationToken.None);

        Assert.Equal(redirectedUrl, result.Url);
    }

    [Fact]
    public async Task FetchAsync_AllowsNonHtml_WhenOptionIsSet()
    {
        var options = new WebPageFetcherOptions { AllowNonHtmlContent = true };
        var fetcher = BuildFetcher(_ =>
        {
            var response = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("plain text")
            };
            response.Content.Headers.ContentType = new MediaTypeHeaderValue("text/plain");
            return response;
        }, options);

        var result = await fetcher.FetchContentAsync(SampleUrl);

        Assert.Equal("text/plain", result.ContentType);
    }

    // ---------------------------------------------------------------------------
    // Content-type tests
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task FetchAsync_Throws_ContentTypeNotAllowedException_WhenContentTypeIsNotHtml()
    {
        var fetcher = BuildFetcher(_ =>
        {
            var response = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{\"key\":\"value\"}")
            };
            response.Content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
            return response;
        });

        var ex = await Assert.ThrowsAsync<ContentTypeNotAllowedException>(
            () => fetcher.FetchContentAsync(SampleUrl));

        Assert.Equal("application/json", ex.ContentType);
        Assert.Equal(SampleUrl, ex.Url);
    }

    // ---------------------------------------------------------------------------
    // Size-limit tests
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task FetchAsync_Throws_ResponseTooLargeException_WhenResponseExceedsMaxSize()
    {
        var options = new WebPageFetcherOptions { MaxResponseSizeBytes = 10 };
        var fetcher = BuildFetcher(_ => HtmlResponse(html: new string('x', 100)), options);

        var ex = await Assert.ThrowsAsync<ResponseTooLargeException>(
            () => fetcher.FetchContentAsync(SampleUrl, CancellationToken.None));

        Assert.Equal(SampleUrl, ex.Url);
        Assert.Equal(10, ex.Limit);
        Assert.True(ex.ActualSize > 10);
    }

    [Fact]
    public async Task FetchAsync_Throws_ResponseTooLargeException_WhenContentLengthExceedsMaxSize()
    {
        var options = new WebPageFetcherOptions { MaxResponseSizeBytes = 10 };
        var fetcher = BuildFetcher(_ =>
        {
            var response = HtmlResponse(html: new string('x', 100));
            response.Content.Headers.ContentLength = 200;
            return response;
        }, options);

        var ex = await Assert.ThrowsAsync<ResponseTooLargeException>(
            () => fetcher.FetchContentAsync(SampleUrl, CancellationToken.None));

        Assert.Equal(200, ex.ActualSize);
    }

    // ---------------------------------------------------------------------------
    // Retry tests
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task FetchAsync_RetriesOnHttpRequestException_AndSucceedsOnThirdAttempt()
    {
        int callCount = 0;
        var fetcher = BuildFetcher(_ =>
        {
            callCount++;
            if (callCount < 3)
                throw new HttpRequestException("Transient network error");
            return HtmlResponse();
        }, new WebPageFetcherOptions { MaxAttempts = 3 });

        var result = await fetcher.FetchContentAsync(SampleUrl);

        Assert.Equal(3, callCount);
        Assert.Equal(SampleHtml, result.Html);
    }

    [Fact]
    public async Task FetchAsync_ThrowsHttpRequestException_WhenAttemptsExhausted()
    {
        var fetcher = BuildFetcher(
            _ => throw new HttpRequestException("Always fails"),
            new WebPageFetcherOptions { MaxAttempts = 2 });

        await Assert.ThrowsAsync<HttpRequestException>(
            () => fetcher.FetchContentAsync(SampleUrl, CancellationToken.None));
    }

    [Fact]
    public async Task FetchAsync_DoesNotRetry_OnDomainException()
    {
        int callCount = 0;
        var fetcher = BuildFetcher(_ =>
        {
            callCount++;
            var r = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{}")
            };
            r.Content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
            return r;
        }, new WebPageFetcherOptions { MaxAttempts = 3, AllowNonHtmlContent = false });

        await Assert.ThrowsAsync<ContentTypeNotAllowedException>(
            () => fetcher.FetchContentAsync(SampleUrl));

        Assert.Equal(1, callCount);
    }

    // ---------------------------------------------------------------------------
    // Fake handler
    // ---------------------------------------------------------------------------

    private sealed class FakeHttpMessageHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> _handler;

        public FakeHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> handler)
            => _handler = handler;

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
            => Task.FromResult(_handler(request));
    }
}
