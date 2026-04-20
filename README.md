# DeepSigma.DataAccess.WebSearch.ContentExtraction

[![NuGet Version](https://img.shields.io/nuget/v/DeepSigma.DataAccess.WebSearch.ContentExtraction)](https://www.nuget.org/packages/DeepSigma.DataAccess.WebSearch.ContentExtraction)
[![NuGet Downloads](https://img.shields.io/nuget/dt/DeepSigma.DataAccess.WebSearch.ContentExtraction)](https://www.nuget.org/packages/DeepSigma.DataAccess.WebSearch.ContentExtraction)
[![Build](https://github.com/DeepSigma-LLC/Dotnet.DeepSigma.DataAccess.WebSearch.ContentExtraction/actions/workflows/build.yml/badge.svg)](https://github.com/DeepSigma-LLC/Dotnet.DeepSigma.DataAccess.WebSearch.ContentExtraction/actions)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](LICENSE)

A .NET 10 library for fetching and extracting clean, structured content from web pages. It separates the concerns of **fetching** HTML (over HTTP or via a headless browser) from **extracting** the meaningful article content, and returns structured data ready for indexing, ranking, or downstream processing.

---

## Table of Contents

- [Features](#features)
- [Requirements](#requirements)
- [Installation](#installation)
- [Quick Start](#quick-start)
- [Usage](#usage)
  - [With ASP.NET Core / Generic Host (DI)](#with-aspnet-core--generic-host-di)
  - [Standalone (no DI)](#standalone-no-di)
  - [JavaScript-rendered pages (Playwright)](#javascript-rendered-pages-playwright)
  - [Fallback extractor (AngleSharp)](#fallback-extractor-anglesharp)
- [Configuration](#configuration)
- [Error handling](#error-handling)
- [Return types](#return-types)
  - [ResponseHtmlContent](#responsehtmlcontent)
  - [ResponseExtractedContent](#responseextractedcontent)
- [Playwright setup](#playwright-setup)
- [Architecture](#architecture)
- [Project Structure](#project-structure)
- [Dependencies](#dependencies)
- [Testing](#testing)
- [Contributing](#contributing)
- [License](#license)

---

## Features

- **`HttpWebPageFetcher`** — downloads HTML over HTTP with:
  - Automatic gzip / brotli / deflate decompression
  - Redirect following (up to 10 hops)
  - Per-request timeout (default 30 s)
  - Configurable response-size guard (default 10 MB)
  - Content-type validation (rejects non-HTML by default)
  - Exponential-backoff retry on transient failures (default 3 attempts); throws `WebPageFetchTimeoutException` when all attempts are exhausted so callers can distinguish timeouts from user-initiated cancellations
- **`PlaywrightWebPageFetcher`** — renders pages in headless Chromium and captures the post-JavaScript HTML; lazy browser initialisation with thread-safe setup
- **`SmartReaderContentExtractor`** — extracts article title, byline, excerpt, publication date, language, and clean plain text using [SmartReader](https://github.com/strumenta/SmartReader) (a .NET port of Mozilla Readability); automatically strips navigation, ads, footers, and sidebars
- **`AngleSharpContentExtractor`** — CSS-selector-based fallback extractor powered by [AngleSharp](https://anglesharp.github.io/); useful for non-article pages or when Readability returns no readable content
- Clean interfaces (`IHtmlRetriever`, `IContentExtractor`) from [`DeepSigma.DataAccess.WebSearch.Abstraction`](https://www.nuget.org/packages/DeepSigma.DataAccess.WebSearch.Abstraction) for easy substitution and testing
- Structured `ResponseExtractedContent` record — drop-in for ranking, search indexing, or storage pipelines
- First-class ASP.NET Core DI support via `AddWebPageDataExtraction()`

---

## Requirements

| Requirement | Version |
|---|---|
| .NET | 10.0 or later |
| Playwright browsers *(optional)* | Chromium — see [Playwright setup](#playwright-setup) |

---

## Installation

```shell
dotnet add package DeepSigma.DataAccess.WebSearch.ContentExtraction
```

---

## Quick Start

```csharp
using DeepSigma.DataAccess.WebSearch.ContentExtraction.Fetchers;
using DeepSigma.DataAccess.WebSearch.ContentExtraction.Extractors;
using DeepSigma.DataAccess.WebSearch.Abstraction.Model;

// Create instances directly — no DI required
var fetcher   = HttpWebPageFetcher.Create();
var extractor = new SmartReaderContentExtractor();

// Wrap the target URL in a ResponseUrlRetrival — the abstraction's URL carrier
var urlInfo = new ResponseUrlRetrival(
    Url: "https://example.com/article",
    Title: null, Snippet: null,
    SearchEngine: "Direct",
    RetrievedAt: DateTimeOffset.UtcNow);

var page    = await fetcher.FetchContentAsync(urlInfo);
var content = await extractor.ExtractContentAsync(page, urlInfo);

Console.WriteLine(content.Title);
Console.WriteLine(content.MainText);
```

---

## Usage

### With ASP.NET Core / Generic Host (DI)

Register all services in one call, then inject `IHtmlRetriever` and `IContentExtractor`
wherever you need them.

```csharp
// Program.cs
using DeepSigma.DataAccess.WebSearch.ContentExtraction.Extensions;

builder.Services.AddWebPageDataExtraction(options =>
{
    options.UserAgent            = "MyBot/1.0 (+https://mysite.example/bot)";
    options.Timeout              = TimeSpan.FromSeconds(60);
    options.MaxAttempts          = 3;
    options.MaxResponseSizeBytes = 5 * 1024 * 1024; // 5 MB
});
```

```csharp
// ArticleService.cs
using DeepSigma.DataAccess.WebSearch.Abstraction;
using DeepSigma.DataAccess.WebSearch.Abstraction.Model;

public class ArticleService(IHtmlRetriever fetcher, IContentExtractor extractor)
{
    public async Task<ResponseExtractedContent> GetArticleAsync(string url, CancellationToken ct = default)
    {
        var urlInfo = new ResponseUrlRetrival(
            Url: url, Title: null, Snippet: null,
            SearchEngine: "Direct", RetrievedAt: DateTimeOffset.UtcNow);

        var page = await fetcher.FetchContentAsync(urlInfo, ct);
        return await extractor.ExtractContentAsync(page, urlInfo, ct);
    }
}
```

---

### Standalone (no DI)

```csharp
using DeepSigma.DataAccess.WebSearch.ContentExtraction.Fetchers;
using DeepSigma.DataAccess.WebSearch.ContentExtraction.Extractors;
using DeepSigma.DataAccess.WebSearch.Abstraction.Model;

var options = new WebPageFetcherOptions
{
    UserAgent   = "MyBot/1.0 (+https://mysite.example/bot)",
    Timeout     = TimeSpan.FromSeconds(45),
    MaxAttempts = 2
};

var fetcher   = HttpWebPageFetcher.Create(options);
var extractor = new SmartReaderContentExtractor();

var urlInfo = new ResponseUrlRetrival(
    Url: "https://example.com/article",
    Title: null, Snippet: null,
    SearchEngine: "Direct",
    RetrievedAt: DateTimeOffset.UtcNow);

var page    = await fetcher.FetchContentAsync(urlInfo);
var content = await extractor.ExtractContentAsync(page, urlInfo);

Console.WriteLine($"Title   : {content.Title}");
Console.WriteLine($"Byline  : {content.Byline}");
Console.WriteLine($"Language: {content.Language}");
Console.WriteLine($"Date    : {content.PublishedAt}");
Console.WriteLine();
Console.WriteLine(content.MainText);
```

---

### JavaScript-rendered pages (Playwright)

Use `PlaywrightWebPageFetcher` when the target page requires JavaScript to render its
content. See [Playwright setup](#playwright-setup) for the one-time browser installation step.

**With DI — call `AddPlaywrightFetcher` after `AddWebPageDataExtraction`:**

```csharp
// Program.cs
using DeepSigma.DataAccess.WebSearch.ContentExtraction.Extensions;

builder.Services.AddWebPageDataExtraction(options =>
{
    options.UserAgent = "MyBot/1.0 (+https://mysite.example/bot)";
});
builder.Services.AddPlaywrightFetcher();
```

**Without DI:**

```csharp
using DeepSigma.DataAccess.WebSearch.ContentExtraction.Fetchers;
using DeepSigma.DataAccess.WebSearch.ContentExtraction.Extractors;
using DeepSigma.DataAccess.WebSearch.Abstraction.Model;

await using var fetcher = new PlaywrightWebPageFetcher(new WebPageFetcherOptions
{
    UserAgent = "MyBot/1.0 (+https://mysite.example/bot)"
});
var extractor = new SmartReaderContentExtractor();

var urlInfo = new ResponseUrlRetrival(
    Url: "https://example.com/spa-page",
    Title: null, Snippet: null,
    SearchEngine: "Direct",
    RetrievedAt: DateTimeOffset.UtcNow);

var page    = await fetcher.FetchContentAsync(urlInfo);
var content = await extractor.ExtractContentAsync(page, urlInfo);
```

> `PlaywrightWebPageFetcher` implements `IAsyncDisposable`. Always dispose it (`await using`)
> to cleanly shut down the headless browser process.

---

### Fallback extractor (AngleSharp)

`AngleSharpContentExtractor` uses CSS selectors to gather paragraphs directly. It is a
good choice for non-article pages (product pages, landing pages, etc.) where the Mozilla
Readability algorithm returns no readable content.

```csharp
using DeepSigma.DataAccess.WebSearch.ContentExtraction.Fetchers;
using DeepSigma.DataAccess.WebSearch.ContentExtraction.Extractors;
using DeepSigma.DataAccess.WebSearch.Abstraction.Model;

var fetcher   = HttpWebPageFetcher.Create();
var extractor = new AngleSharpContentExtractor();

var urlInfo = new ResponseUrlRetrival(
    Url: "https://example.com/product",
    Title: null, Snippet: null,
    SearchEngine: "Direct",
    RetrievedAt: DateTimeOffset.UtcNow);

var page    = await fetcher.FetchContentAsync(urlInfo);
var content = await extractor.ExtractContentAsync(page, urlInfo);

Console.WriteLine(content.MainText);
```

**Tip:** combine both extractors — try `SmartReaderContentExtractor` first and fall back to
`AngleSharpContentExtractor` when `MainText` is empty:

```csharp
var page = await fetcher.FetchContentAsync(urlInfo, ct);

var smart   = await smartExtractor.ExtractContentAsync(page, urlInfo, ct);
var content = string.IsNullOrWhiteSpace(smart.MainText)
    ? await angleSharpExtractor.ExtractContentAsync(page, urlInfo, ct)
    : smart;
```

---

## Configuration

`WebPageFetcherOptions` controls the behaviour of `HttpWebPageFetcher`.

| Property | Type | Default | Description |
|---|---|---|---|
| `UserAgent` | `string` | `"DefaultUserAgent/1.0"` | Value of the `User-Agent` request header |
| `Timeout` | `TimeSpan` | `00:00:30` | Per-request timeout |
| `MaxAttempts` | `int` | `3` | Total number of attempts (first try + retries); uses exponential backoff with jitter |
| `MaxResponseSizeBytes` | `long` | `10485760` (10 MB) | Requests exceeding this size throw `ResponseTooLargeException` |
| `AllowNonHtmlContent` | `bool` | `false` | When `false`, non-`text/html` responses throw `ContentTypeNotAllowedException` |

---

## Error handling

All fetch-related exceptions derive from `WebPageFetchException` (namespace
`DeepSigma.DataAccess.WebSearch.ContentExtraction.Exceptions`), making it easy to catch
fetch failures as a group or handle specific cases individually.

| Exception | When thrown |
|---|---|
| `WebPageFetchTimeoutException` | All retry attempts timed out (i.e. the per-request `Timeout` expired on every try). Wraps the original `TaskCanceledException`. Does **not** inherit from `OperationCanceledException`, so it is never confused with a user-initiated cancellation. |
| `ResponseTooLargeException` | The response body exceeds `MaxResponseSizeBytes`. |
| `ContentTypeNotAllowedException` | The server returned a non-`text/html` content type and `AllowNonHtmlContent` is `false`. |

> A true user cancellation (via `CancellationToken`) still propagates as `OperationCanceledException`
> and is never caught or wrapped by the fetcher.

**Example — distinguishing timeouts from cancellations:**

```csharp
using DeepSigma.DataAccess.WebSearch.ContentExtraction.Exceptions;
using DeepSigma.DataAccess.WebSearch.Abstraction.Model;

var urlInfo = new ResponseUrlRetrival(
    Url: url, Title: null, Snippet: null,
    SearchEngine: "Direct", RetrievedAt: DateTimeOffset.UtcNow);

try
{
    var page = await fetcher.FetchContentAsync(urlInfo, cancellationToken);
    var content = await extractor.ExtractContentAsync(page, urlInfo, cancellationToken);
}
catch (WebPageFetchTimeoutException ex)
{
    // All retry attempts timed out — log and skip or enqueue for later
    logger.LogWarning(ex, "Timed out fetching {Url} after {Attempts} attempt(s)", ex.Url, ex.Attempts);
}
catch (OperationCanceledException)
{
    // User or host cancelled — rethrow so the caller knows
    throw;
}
catch (WebPageFetchException ex)
{
    // ContentTypeNotAllowedException, ResponseTooLargeException, etc.
    logger.LogWarning(ex, "Fetch failed for {Url}: {Message}", ex.Url, ex.Message);
}
```

---

## Return types

### `ResponseHtmlContent`

Returned by `IHtmlRetriever.FetchContentAsync`. Contains the raw fetch output.

| Property | Type | Description |
|---|---|---|
| `Html` | `string` | Full HTML source of the page |
| `FetchedAt` | `DateTimeOffset` | Timestamp when the content was fetched |
| `ContentType` | `string?` | Value of the `Content-Type` media type header (e.g. `"text/html"`) |
| `StatusCode` | `HttpStatusCode` | HTTP response status code |

---

### `ResponseExtractedContent`

Returned by `IContentExtractor.ExtractContentAsync`. Contains the structured, cleaned content.

| Property | Type | Description |
|---|---|---|
| `SourceUrlRetrival` | `ResponseUrlRetrival` | The URL retrieval record passed to `ExtractContentAsync` |
| `SourceHtmlContent` | `ResponseHtmlContent?` | Reference back to the source HTML |
| `MainText` | `string` | Clean, plain-text body of the article (empty string when not readable) |
| `Title` | `string?` | Page or article title |
| `Byline` | `string?` | Author / byline (populated by `SmartReaderContentExtractor`) |
| `Snippet` | `string?` | Short summary or meta description |
| `Language` | `string?` | BCP 47 language tag (e.g. `"en"`, `"fr"`) |
| `PublishedAt` | `DateTimeOffset?` | Publication date parsed from page metadata; `null` if not found |

---

## Playwright setup

`PlaywrightWebPageFetcher` requires Chromium to be installed on the host machine before use.
Run the following command once — locally or as part of your CI/CD pipeline.

**Local / development machine:**

```shell
pwsh bin/Debug/net10.0/playwright.ps1 install chromium
```

**GitHub Actions:**

```yaml
- name: Install Playwright browsers
  run: pwsh bin/Release/net10.0/playwright.ps1 install chromium
```

**Docker:**

```dockerfile
RUN pwsh /app/playwright.ps1 install chromium --with-deps
```

See the [Playwright for .NET documentation](https://playwright.dev/dotnet/docs/intro) for
full platform-specific setup guidance.

---

## Architecture

The library is split into two independent concerns connected by a single intermediate model.

```
┌──────────────────────────────────┐
│           IHtmlRetriever         │
│                                  │
│  HttpWebPageFetcher              │  ← HTTP with retry (default)
│  PlaywrightWebPageFetcher        │  ← headless Chromium (JS pages)
└────────────────┬─────────────────┘
                 │ ResponseHtmlContent
                 │  (Html, FetchedAt, ContentType, StatusCode)
                 │
                 │ + ResponseUrlRetrival  (passed through from caller)
                 ▼
┌──────────────────────────────────┐
│         IContentExtractor        │
│                                  │
│  SmartReaderContentExtractor     │  ← Mozilla Readability (primary)
│  AngleSharpContentExtractor      │  ← CSS selectors (fallback)
└────────────────┬─────────────────┘
                 │ ResponseExtractedContent
                 │  (SourceUrlRetrival, Title, Byline, Snippet, MainText, Language, PublishedAt)
                 ▼
          downstream use
    (indexing, ranking, storage)
```

The interfaces are intentionally thin so implementations can be swapped in tests or replaced
with custom fetchers and extractors without changing any downstream code.

---

## Project Structure

```
DeepSigma.DataAccess.WebPageDataExtraction/
├── Exceptions/
│   └── WebPageFetchException.cs             # Base exception + timeout, size, content-type variants
├── Extractors/
│   ├── AngleSharpContentExtractor.cs        # AngleSharp-based fallback extractor
│   └── SmartReaderContentExtractor.cs       # Mozilla Readability extractor (recommended)
├── Extensions/
│   └── ServiceCollectionExtensions.cs       # DI registration helpers
├── Fetchers/
│   ├── HttpWebPageFetcher.cs                # HTTP-based page fetcher
│   ├── PlaywrightWebPageFetcher.cs          # Headless Chromium fetcher
│   └── WebPageFetcherOptions.cs             # Fetcher configuration
└── DeepSigma.DataAccess.WebSearch.ContentExtraction.csproj

DeepSigma.DataAccess.WebPageDataExtraction.Test/
├── Extractors/
│   ├── AngleSharpContentExtractorTests.cs
│   └── SmartReaderContentExtractorTests.cs
├── Fetchers/
│   └── HttpWebPageFetcherTests.cs
└── DeepSigma.DataAccess.WebSearch.ContentExtraction.Test.csproj
```

---

## Dependencies

| Package | Purpose |
|---|---|
| [AngleSharp](https://www.nuget.org/packages/AngleSharp) | HTML parsing for the AngleSharp extractor |
| [SmartReader](https://www.nuget.org/packages/SmartReader) | Mozilla Readability port for article extraction |
| [Microsoft.Playwright](https://www.nuget.org/packages/Microsoft.Playwright) | Headless browser automation |
| [DeepSigma.DataAccess.WebSearch.Abstraction](https://www.nuget.org/packages/DeepSigma.DataAccess.WebSearch.Abstraction) | Shared interfaces (`IHtmlRetriever`, `IContentExtractor`) and models |
| [Microsoft.Extensions.DependencyInjection.Abstractions](https://www.nuget.org/packages/Microsoft.Extensions.DependencyInjection.Abstractions) | DI integration |
| [Microsoft.Extensions.Http](https://www.nuget.org/packages/Microsoft.Extensions.Http) | `IHttpClientFactory` / typed client support |

---

## Testing

The solution includes an xUnit v3 test project. Run all tests with:

```shell
dotnet test
```

Test classes cover:
- `HttpWebPageFetcherTests` — integration tests for the HTTP fetcher
- `AngleSharpContentExtractorTests` — unit tests for AngleSharp extraction
- `SmartReaderContentExtractorTests` — unit tests for SmartReader extraction

---

## License

This project is licensed under the [MIT License](LICENSE).  

