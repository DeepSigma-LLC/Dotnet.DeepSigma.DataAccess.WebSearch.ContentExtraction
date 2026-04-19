using System.Net;
using Microsoft.Extensions.DependencyInjection;
using DeepSigma.DataAccess.WebSearch.ContentExtraction.Extractors;
using DeepSigma.DataAccess.WebSearch.ContentExtraction.Fetchers;
using DeepSigma.DataAccess.WebSearch.Abstraction;

namespace DeepSigma.DataAccess.WebSearch.ContentExtraction.Extensions;

/// <summary>Extension methods for registering web page data extraction services.</summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers <see cref="IHtmlRetriever"/> (HTTP-based with gzip/brotli/deflate decompression)
    /// and <see cref="IContentExtractor"/> (SmartReader) into the DI container.
    /// </summary>
    /// <param name="services">The service collection to add to.</param>
    /// <param name="configureOptions">Optional delegate to customise fetcher options.</param>
    public static IServiceCollection AddWebPageDataExtraction(
        this IServiceCollection services,
        Action<WebPageFetcherOptions>? configureOptions = null)
    {
        var options = new WebPageFetcherOptions();
        configureOptions?.Invoke(options);
        services.AddSingleton(options);

        services.AddHttpClient<IHtmlRetriever, HttpWebPageFetcher>((httpClient, sp) =>
            new HttpWebPageFetcher(httpClient, sp.GetRequiredService<WebPageFetcherOptions>()))
        .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
        {
            AutomaticDecompression =
                DecompressionMethods.GZip | DecompressionMethods.Brotli | DecompressionMethods.Deflate,
            AllowAutoRedirect = true,
            MaxAutomaticRedirections = 10
        });

        services.AddTransient<IContentExtractor, SmartReaderContentExtractor>();

        return services;
    }

    /// <summary>
    /// Replaces the <see cref="IHtmlRetriever"/> registration with <see cref="PlaywrightWebPageFetcher"/>
    /// for pages that require JavaScript rendering. Shares the <see cref="WebPageFetcherOptions"/>
    /// registered by <see cref="AddWebPageDataExtraction"/>.
    /// <para>
    /// <b>Prerequisites:</b> run <c>playwright install chromium</c> on the host before use.
    /// </para>
    /// </summary>
    public static IServiceCollection AddPlaywrightFetcher(this IServiceCollection services)
    {
        services.AddSingleton<IHtmlRetriever>(sp =>
            new PlaywrightWebPageFetcher(
                sp.GetService<WebPageFetcherOptions>() ?? new WebPageFetcherOptions()));
        return services;
    }
}
