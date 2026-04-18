# DeepSigma.DataAccess.WebSearch.ContentExtraction

[![NuGet Version](https://img.shields.io/nuget/v/DeepSigma.DataAccess.WebSearch.ContentExtraction)](https://www.nuget.org/packages/DeepSigma.DataAccess.WebSearch.ContentExtraction)
[![NuGet Downloads](https://img.shields.io/nuget/dt/DeepSigma.DataAccess.WebSearch.ContentExtraction)](https://www.nuget.org/packages/DeepSigma.DataAccess.WebSearch.ContentExtraction)
[![Build](https://github.com/DeepSigma-LLC/Dotnet.DeepSigma.DataAccess.WebSearch.ContentExtraction/actions/workflows/build.yml/badge.svg)](https://github.com/DeepSigma-LLC/Dotnet.DeepSigma.DataAccess.WebSearch.ContentExtraction/actions)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](LICENSE)

A .NET library for fetching and extracting clean, structured content from web pages. It separates the concerns of **fetching** HTML (over HTTP or via a headless browser) from **extracting** the meaningful article content, and returns structured data ready for indexing, ranking, or downstream processing.

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
- [Return types](#return-types)
  - [WebPageFetchResult](#webpagefetchresult)
  - [ExtractedContent](#extractedcontent)
- [Playwright setup](#playwright-setup)
- [Architecture](#architecture)
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
  - Exponential-backoff retry on transient failures (default 3 attempts)
  - Captures the final URL after redirects
- **`PlaywrightWebPageFetcher`** — renders pages in headless Chromium and captures the post-JavaScript HTML; lazy browser initialisation with thread-safe setup
- **`SmartReaderContentExtractor`** — extracts article title, byline, excerpt, publication date, language, and clean plain text using [SmartReader](https://github.com/strumenta/SmartReader) (a .NET port of Mozilla Readability); automatically strips navigation, ads, footers, and sidebars
- **`AngleSharpContentExtractor`** — CSS-selector-based fallback extractor powered by [AngleSharp](https://anglesharp.github.io/); useful for non-article pages or when Readability returns no readable content
- Clean interfaces (`IWebPageFetcher`, `IContentExtractor`) for easy substitution and testing
- Structured `ExtractedContent` record — drop-in for ranking, search indexing, or storage pipelines
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

// Create instances directly — no DI required
var fetcher   = HttpWebPageFetcher.Create();
var extractor = new SmartReaderContentExtractor();

WebPageFetchResult page    = await fetcher.FetchAsync("https://example.com/article");
ExtractedContent   content = await extractor.ExtractAsync(page);

Console.WriteLine(content.Title);
Console.WriteLine(content.MainText);
```

---

## Usage

### With ASP.NET Core / Generic Host (DI)

Register all services in one call, then inject `IWebPageFetcher` and `IContentExtractor`
wherever you need them.

```csharp
// Program.cs
using DeepSigma.DataAccess.WebSearch.ContentExtraction.Extensions;

builder.Services.AddWebPageDataExtraction(options =>
{
    options.UserAgent            = "MyBot/1.0 (+https://mysite.example/bot)";
    options.Timeout              = TimeSpan.FromSeconds(60);
    options.MaxRetries           = 3;
    options.MaxResponseSizeBytes = 5 * 1024 * 1024; // 5 MB
});
```

```csharp
// ArticleService.cs
using DeepSigma.DataAccess.WebSearch.ContentExtraction.Interfaces;
using DeepSigma.DataAccess.WebSearch.ContentExtraction.Models;

public class ArticleService(IWebPageFetcher fetcher, IContentExtractor extractor)
{
    public async Task<ExtractedContent> GetArticleAsync(string url, CancellationToken ct = default)
    {
        var page = await fetcher.FetchAsync(url, ct);
        return await extractor.ExtractAsync(page, ct);
    }
}
```

---

### Standalone (no DI)

```csharp
using DeepSigma.DataAccess.WebSearch.ContentExtraction.Fetchers;
using DeepSigma.DataAccess.WebSearch.ContentExtraction.Extractors;

var options = new WebPageFetcherOptions
{
    UserAgent  = "MyBot/1.0 (+https://mysite.example/bot)",
    Timeout    = TimeSpan.FromSeconds(45),
    MaxRetries = 2
};

var fetcher   = HttpWebPageFetcher.Create(options);
var extractor = new SmartReaderContentExtractor();

var page    = await fetcher.FetchAsync("https://example.com/article");
var content = await extractor.ExtractAsync(page);

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

builder.Services.AddWebPageDataExtraction();
builder.Services.AddPlaywrightFetcher(userAgent: "MyBot/1.0 (+https://mysite.example/bot)");
```

**Without DI:**

```csharp
using DeepSigma.DataAccess.WebSearch.ContentExtraction.Fetchers;
using DeepSigma.DataAccess.WebSearch.ContentExtraction.Extractors;

await using var fetcher = new PlaywrightWebPageFetcher();
var extractor           = new SmartReaderContentExtractor();

var page    = await fetcher.FetchAsync("https://example.com/spa-page");
var content = await extractor.ExtractAsync(page);
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

var fetcher   = HttpWebPageFetcher.Create();
var extractor = new AngleSharpContentExtractor();

var page    = await fetcher.FetchAsync("https://example.com/product");
var content = await extractor.ExtractAsync(page);

Console.WriteLine(content.MainText);
```

**Tip:** combine both extractors — try `SmartReaderContentExtractor` first and fall back to
`AngleSharpContentExtractor` when `MainText` is empty:

```csharp
var page = await fetcher.FetchAsync(url, ct);

var smart   = await smartExtractor.ExtractAsync(page, ct);
var content = string.IsNullOrWhiteSpace(smart.MainText)
    ? await angleSharpExtractor.ExtractAsync(page, ct)
    : smart;
```

---

## Configuration

`WebPageFetcherOptions` controls the behaviour of `HttpWebPageFetcher`.

| Property | Type | Default | Description |
|---|---|---|---|
| `UserAgent` | `string` | `"DeepSigmaBot/1.0 (…)"` | Value of the `User-Agent` request header |
| `Timeout` | `TimeSpan` | `00:00:30` | Per-request timeout |
| `MaxRetries` | `int` | `3` | Maximum attempts (includes the first try); retries use exponential backoff |
| `MaxResponseSizeBytes` | `long` | `10485760` (10 MB) | Requests exceeding this size throw `InvalidOperationException` |
| `AllowNonHtmlContent` | `bool` | `false` | When `false`, non-`text/html` responses throw `InvalidOperationException` |

---

## Return types

### `WebPageFetchResult`

Returned by `IWebPageFetcher.FetchAsync`. Contains the raw fetch output.

| Property | Type | Description |
|---|---|---|
| `Url` | `string` | Final URL after any redirects |
| `Html` | `string` | Full HTML source of the page |
| `ContentType` | `string?` | Value of the `Content-Type` media type header (e.g. `"text/html"`) |
| `StatusCode` | `HttpStatusCode` | HTTP response status code |

---

### `ExtractedContent`

Returned by `IContentExtractor.ExtractAsync`. Contains the structured, cleaned content.

| Property | Type | Description |
|---|---|---|
| `Url` | `string` | Source URL (passed through from `WebPageFetchResult`) |
| `Title` | `string?` | Page or article title |
| `Byline` | `string?` | Author / byline (populated by `SmartReaderContentExtractor`) |
| `Excerpt` | `string?` | Short summary or meta description |
| `MainText` | `string` | Clean, plain-text body of the article (empty string when not readable) |
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
│          IWebPageFetcher         │
│                                  │
│  HttpWebPageFetcher              │  ← HTTP with retry (default)
│  PlaywrightWebPageFetcher        │  ← headless Chromium (JS pages)
└────────────────┬─────────────────┘
                 │ WebPageFetchResult
                 │  (Url, Html, ContentType, StatusCode)
                 ▼
┌──────────────────────────────────┐
│         IContentExtractor        │
│                                  │
│  SmartReaderContentExtractor     │  ← Mozilla Readability (primary)
│  AngleSharpContentExtractor      │  ← CSS selectors (fallback)
└────────────────┬─────────────────┘
                 │ ExtractedContent
                 │  (Title, Byline, Excerpt, MainText, Language, PublishedAt)
                 ▼
          downstream use
    (indexing, ranking, storage)
```

The interfaces are intentionally thin so implementations can be swapped in tests or replaced
with custom fetchers and extractors without changing any downstream code.

---

## License

This project is licensed under the [MIT License](LICENSE).  

