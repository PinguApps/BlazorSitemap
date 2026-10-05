# PinguApps.BlazorSitemap

[![PinguApps.BlazorSitemap version](https://img.shields.io/nuget/v/PinguApps.BlazorSitemap?style=for-the-badge&label=PinguApps.BlazorSitemap)](https://www.nuget.org/packages/PinguApps.BlazorSitemap/) [![PinguApps.BlazorSitemap downloads](https://img.shields.io/nuget/dt/PinguApps.BlazorSitemap?style=for-the-badge&label=downloads)](https://www.nuget.org/packages/PinguApps.BlazorSitemap/)

[![PinguApps.BlazorSitemap.Abstractions version](https://img.shields.io/nuget/v/PinguApps.BlazorSitemap.Abstractions?style=for-the-badge&label=PinguApps.BlazorSitemap.Abstractions)](https://www.nuget.org/packages/PinguApps.BlazorSitemap.Abstractions/) [![PinguApps.BlazorSitemap.Abstractions downloads](https://img.shields.io/nuget/dt/PinguApps.BlazorSitemap.Abstractions?style=for-the-badge&label=downloads)](https://www.nuget.org/packages/PinguApps.BlazorSitemap.Abstractions/)

Write your route once, on the Razor page. This package finds it, fills in the values you provide, and serves an XML sitemap. It handles translations, database-backed pages, modification dates and splitting large sitemaps into smaller files.

For **.NET 10, server-hosted Blazor**, including Interactive Auto. The public namespace is `PinguApps.BlazorSitemap`.

For AI-assisted integration, use the [repository skill](skills/setup-blazor-sitemaps/SKILL.md). Workflow maintainers can refer to the [automation reconciliation](docs/github-workflows.md).

## Get a sitemap running

Install the package in your server project:

```sh
dotnet add package PinguApps.BlazorSitemap
```

**Install `PinguApps.BlazorSitemap.Abstractions` in each referenced project that contains included routable components**, including WebAssembly/client projects and Razor component libraries:

```sh
dotnet add path/to/YourApp.Client.csproj package PinguApps.BlazorSitemap.Abstractions
dotnet add path/to/YourApp.Library.csproj package PinguApps.BlazorSitemap.Abstractions
```

The server gets Abstractions through the main package. Referenced projects need their own reference for attributes and source tracking; install it only where those components live. Assembly discovery is configured below.

For local development, start with **User Secrets** in the server project directory:

```sh
dotnet user-secrets init
dotnet user-secrets set "Sitemap:PublicBaseUrl" "https://www.example.com"
```

`WebApplication.CreateBuilder` loads User Secrets automatically in the **Development** environment. For a local site, replace the sample address with its actual origin, such as `http://localhost:5187`. Keep your public production address in production configuration; User Secrets are a development-only store. [ASP.NET Core User Secrets](https://learn.microsoft.com/en-us/aspnet/core/security/app-secrets?view=aspnetcore-10.0)

You can also use `appsettings.json`:

```json
{ "Sitemap": { "PublicBaseUrl": "https://www.example.com" } }
```

Normal configuration works: User Secrets, `appsettings.Production.json`, environment variables such as `Sitemap__PublicBaseUrl`, and command-line overrides. Use your application's public address, including a path base if it has one.

In `Program.cs`, use your own namespace for `App`:

```csharp
using PinguApps.BlazorSitemap;
using YourApp.Components;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddRazorComponents();
builder.Services.AddBlazorSitemap<App>(options =>
{
    options.PublicBaseUrl = builder.Configuration["Sitemap:PublicBaseUrl"]!;
});

var app = builder.Build();
app.UseAntiforgery();
app.MapBlazorSitemap(); // This creates the sitemap endpoints.
app.MapRazorComponents<App>();
app.Run();
```

> [!IMPORTANT]
> **`app.MapBlazorSitemap();` is the line that serves the sitemap.** Service registration alone does not create an endpoint.
> **`PublicBaseUrl` is required.** It must be an absolute HTTP(S) address without credentials, a query or a fragment. Incoming Host headers never decide your sitemap URLs.

Add `@using PinguApps.BlazorSitemap` to `_Imports.razor`, then opt in a public page:

```razor
@page "/about"
@attribute [SitemapPage]
<h1>About us</h1>
```

That's your first entry. `GET /sitemap.xml` returns `application/xml; charset=utf-8`:

```xml
<?xml version="1.0" encoding="UTF-8"?>
<urlset xmlns="http://www.sitemaps.org/schemas/sitemap/0.9">
  <url>
    <loc>https://www.example.com/about</loc>
    <lastmod>2026-09-01T09:00:00.0000000+00:00</lastmod>
  </url>
</urlset>
```

The build also creates `About.razor.sitemap.json`, which tracks source changes. Dates in this README are illustrative; your output uses your recorded source or content dates. XML below shows entries inside the same `urlset`, with whitespace added for readability. No `priority` or `changefreq` fields are emitted.

> [!NOTE]
> **Chrome may display a sitemap with hreflang as plain text.** Its built-in XML tree viewer is disabled by the XHTML `link` elements used for translations. The response is still valid XML with the correct content type. Changing to `text/xml` does not restore the tree viewer; use View Source or an XML viewer to inspect it. Sitemaps without hreflang display normally. [Chromium's XML viewer logic](https://chromium.googlesource.com/chromium/src/+/bf31f32181416cfe913c38eee2ba43abcbee1281/third_party/blink/renderer/core/xml/parser/xml_document_parser_rs.cc)

Submit the public sitemap URL to Search Console, or add this to your site's `robots.txt`:

```text
Sitemap: https://www.example.com/sitemap.xml
```

## Static dates: build it and keep the history

For a page marked `[SitemapPage]`, the build hashes its `.razor` file and optional `.razor.cs`. It saves the hash and UTC date beside the page, in a file such as `About.razor.sitemap.json`:

```json
{
  "sha256": "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef",
  "lastChangedUtc": "2026-09-01T09:00:00.0000000+00:00"
}
```

The hash above just illustrates the format. The real hash is calculated for you. The date is embedded in the compiled assembly, so deployed apps need neither source files nor these JSON files.

| What happens | Resulting sitemap date |
| --- | --- |
| First tracked build | Time that build observes the source |
| Another build, same source | Previous date stays |
| Edit the page or its code-behind | New source version gets a new date |
| Add or remove its code-behind | New date |
| Delete the sibling JSON and rebuild | New date, intentionally resetting its history |
| Change the public base URL | URL changes; source date stays |

**Commit the sibling JSON files with your pages.** CI then preserves dates on unchanged builds. CI can generate them, but must save that state for the next build. There is no deployment API to invoke and no URL manifest to maintain. Home, pages in component libraries and client-project pages work the same way.

> [!WARNING]
> **Source dates are estimates, not exact publication or rendered-content dates.** Comments and formatting can advance them. Changes to layouts, localization resources, shared components, services or assets do not. All finite variants of a static page share its source date. First builds cannot recover historical dates.
> Google recommends dates reflecting significant changes to the served page. Use a supplied freshness provider when you need that accuracy. [Google's guidance](https://developers.google.com/search/docs/crawling-indexing/sitemaps/build-sitemap)

If a shared change should advance a page's estimate, delete its sibling JSON and rebuild. For exact application-owned dates, use the page-specific provider shown below. Supplied dates take precedence over source estimates.

**Every entry needs a date by default.** A missing stamp or provider produces an error naming the page and URL. Runtime data never falls back to a Razor source date. The package does not fill gaps with request time, startup time or filesystem dates.

See [source tracking and CI details](docs/deployment-freshness.md), including first builds, branching, resetting state, supported attribute declarations and disabling the build task.

## Locales: one list, separate canonical URLs

Reuse the locale list your app already has. Inside the sitemap options callback:

```csharp
options.Locales(SupportedLocales.All.Select(x =>
    new SitemapLocale(x.UrlSegment, x.Language)), xDefaultLocale: "en-gb");
```

Here, your list contains English (`en-gb`, `en-GB`) and Welsh (`cy-gb`, `cy-GB`). The first value is the route segment; the second is the language annotation.

```razor
@page "/{locale}/counter"
@attribute [SitemapPage]
<h1>@(Locale == "cy-gb" ? "Rhifydd" : "Counter")</h1>
@code {
    [Parameter] public string Locale { get; set; } = "";
}
```

Without hreflang, you get two independent canonical entries:

```xml
<url>
  <loc>https://www.example.com/cy-gb/counter</loc>
  <lastmod>2026-09-01T09:00:00.0000000+00:00</lastmod>
</url>
<url>
  <loc>https://www.example.com/en-gb/counter</loc>
  <lastmod>2026-09-01T09:00:00.0000000+00:00</lastmod>
</url>
```

If these are translations of the same page, change the attribute to `[SitemapPage(Hreflang = true)]`. The URLs stay the same, and each entry gains reciprocal alternate links:

```xml
<url>
  <loc>https://www.example.com/cy-gb/counter</loc>
  <lastmod>2026-09-01T09:00:00.0000000+00:00</lastmod>
  <xhtml:link xmlns:xhtml="http://www.w3.org/1999/xhtml" rel="alternate" hreflang="cy-GB" href="https://www.example.com/cy-gb/counter" />
  <xhtml:link xmlns:xhtml="http://www.w3.org/1999/xhtml" rel="alternate" hreflang="en-GB" href="https://www.example.com/en-gb/counter" />
  <xhtml:link xmlns:xhtml="http://www.w3.org/1999/xhtml" rel="alternate" hreflang="x-default" href="https://www.example.com/en-gb/counter" />
</url>
<url>
  <loc>https://www.example.com/en-gb/counter</loc>
  <lastmod>2026-09-01T09:00:00.0000000+00:00</lastmod>
  <xhtml:link xmlns:xhtml="http://www.w3.org/1999/xhtml" rel="alternate" hreflang="cy-GB" href="https://www.example.com/cy-gb/counter" />
  <xhtml:link xmlns:xhtml="http://www.w3.org/1999/xhtml" rel="alternate" hreflang="en-GB" href="https://www.example.com/en-gb/counter" />
  <xhtml:link xmlns:xhtml="http://www.w3.org/1999/xhtml" rel="alternate" hreflang="x-default" href="https://www.example.com/en-gb/counter" />
</url>
```

`x-default` appears only because you explicitly selected it. Leave out `xDefaultLocale` to omit it. Both variants remain self-canonical. [Google's localized-version guidance](https://developers.google.com/search/docs/specialty/international/localized-versions)

> [!IMPORTANT]
> **`locale` is the default parameter name, not a reserved keyword.** The name passed to `Locales` selects which parameter represents language. For `{culture}`, use `options.Locales(locales, parameter: "culture")`.
> **Hreflang is for equivalent translations.** It does not make aliases, categories or unrelated pages into translations.

## More than one parameter: items and their translations

Suppose a counter belongs to an item:

```razor
@page "/{ItemId:int}/{locale}/counter"
@attribute [SitemapPage(Hreflang = true)]
<h1>Counter for item @ItemId (@Locale)</h1>
@code {
    [Parameter] public int ItemId { get; set; }
    [Parameter] public string Locale { get; set; } = "";
}
```

Keep the locale registration above and register the finite item IDs:

```csharp
options.RouteValues("ItemId", new[] { "42", "43" });
```

This generates four entries. Language changes only `{locale}`; it never changes `{ItemId}`:

| Canonical URL | Its alternate siblings |
| --- | --- |
| `https://www.example.com/42/cy-gb/counter` | Item 42 in Welsh, English and English x-default |
| `https://www.example.com/42/en-gb/counter` | Item 42 in Welsh, English and English x-default |
| `https://www.example.com/43/cy-gb/counter` | Item 43 in Welsh, English and English x-default |
| `https://www.example.com/43/en-gb/counter` | Item 43 in Welsh, English and English x-default |

For example, the first entry is:

```xml
<url>
  <loc>https://www.example.com/42/cy-gb/counter</loc>
  <lastmod>2026-09-01T09:00:00.0000000+00:00</lastmod>
  <xhtml:link xmlns:xhtml="http://www.w3.org/1999/xhtml" rel="alternate" hreflang="cy-GB" href="https://www.example.com/42/cy-gb/counter" />
  <xhtml:link xmlns:xhtml="http://www.w3.org/1999/xhtml" rel="alternate" hreflang="en-GB" href="https://www.example.com/42/en-gb/counter" />
  <xhtml:link xmlns:xhtml="http://www.w3.org/1999/xhtml" rel="alternate" hreflang="x-default" href="https://www.example.com/42/en-gb/counter" />
</url>
```

The other entries have the same shape, with their own `loc` and the siblings listed in the table. Values such as `"abc"` fail the `int` constraint. For database-backed IDs or items with different translation availability, use an entries provider instead of claiming every combination exists. Those entries require content dates.

## Categories and other finite lists

Finite values are useful beyond languages. For a small, fixed category list:

```razor
@page "/{category}/guides"
@attribute [SitemapPage]
<h1>@Category guides</h1>
@code {
    [Parameter] public string Category { get; set; } = "";
}
```

Register your existing category list, not a set of complete paths:

```csharp
options.RouteValues("category", new[] { "books", "gardening", "C# & .NET" });
```

Output, with the same source timestamp for these static variants:

```xml
<url><loc>https://www.example.com/C%23%20%26%20.NET/guides</loc><lastmod>2026-09-01T09:00:00.0000000+00:00</lastmod></url>
<url><loc>https://www.example.com/books/guides</loc><lastmod>2026-09-01T09:00:00.0000000+00:00</lastmod></url>
<url><loc>https://www.example.com/gardening/guides</loc><lastmod>2026-09-01T09:00:00.0000000+00:00</lastmod></url>
```

Decoded values are URL-encoded by segment, then XML-escaped. If the category page also has `{locale}`, the two lists produce every category/locale combination: three categories × two languages = six URLs. Hreflang groups retain the category, just as they retain ItemId above. If Welsh gardening guides don't exist, use a provider returning only the existing combinations.

Optional values must be explicit. For a page declared with `@page "/archive/{year:int?}"`, configure:

```csharp
options.RouteValues("year", new string?[] { null, "2026" });
```

With `[SitemapPage]`, the result is:

```xml
<url><loc>https://www.example.com/archive</loc><lastmod>2026-09-01T09:00:00.0000000+00:00</lastmod></url>
<url><loc>https://www.example.com/archive/2026</loc><lastmod>2026-09-01T09:00:00.0000000+00:00</lastmod></url>
```

A missing list, an empty list, duplicate values or an unresolved parameter fails with an explanation.

## A page with an old alias

Choose the canonical route by index, without repeating its path:

```razor
@page "/about"
@page "/old-about"
@attribute [SitemapPage(CanonicalRouteIndex = 0)]
```

The index starts at zero after sorting distinct templates using ordinal string order. Here, `0` selects the first template. Output:

```xml
<url>
  <loc>https://www.example.com/about</loc>
  <lastmod>2026-09-01T09:00:00.0000000+00:00</lastmod>
</url>
```

There is no `/old-about` entry.

Multiple routes need this explicit choice: the package cannot infer your canonical intent. If they really represent separate canonical resources, use separate page components. [Google's canonical guidance](https://developers.google.com/search/docs/crawling-indexing/consolidate-duplicate-urls)

## Blog posts and database records

Declare the route once:

```razor
@page "/{locale}/blog/{slug}"
@attribute [SitemapPage(Hreflang = true)]
@code {
    [Parameter] public string Locale { get; set; } = "";
    [Parameter] public string Slug { get; set; } = "";
}
```

Register an entries provider after `AddBlazorSitemap`:

```csharp
builder.Services.AddSitemapEntries<BlogPost, BlogEntries>();

public sealed class BlogEntries(BlogStore store) : ISitemapEntryProvider<BlogPost>
{
    public async IAsyncEnumerable<SitemapEntry> GetEntriesAsync(
        [System.Runtime.CompilerServices.EnumeratorCancellation]
        CancellationToken cancellationToken = default)
    {
        foreach (var article in store.Published())
        {
            cancellationToken.ThrowIfCancellationRequested();
            yield return new SitemapEntry(new Dictionary<string, string?>
            {
                ["locale"] = article.Locale,
                ["slug"] = article.Slug
            })
            {
                AlternateGroup = article.Id,
                LastModified = new SitemapLastModified(article.UpdatedAtUtc)
            };
        }
        await Task.CompletedTask;
    }
}
```

`BlogStore` is your application service. This mirrors the [complete demo provider](tests/Consumer/BlogEntries.cs). For a real database, stream or page a consistent query of published records. The provider replaces automatic instances for that page; an empty result creates no entries.

Suppose your published translations are:

| Article ID | Locale | Slug | UpdatedAtUtc |
| --- | --- | --- | --- |
| welcome | en-gb | welcome | 2026-09-01 10:00 UTC |
| welcome | cy-gb | croeso | 2026-09-02 11:00 UTC |
| coast | en-gb | the-coast | 2026-09-03 12:00 UTC |

A draft is not returned. The coast article has no published Welsh translation. Output:

```xml
<url>
  <loc>https://www.example.com/cy-gb/blog/croeso</loc>
  <lastmod>2026-09-02T11:00:00.0000000+00:00</lastmod>
  <xhtml:link xmlns:xhtml="http://www.w3.org/1999/xhtml" rel="alternate" hreflang="cy-GB" href="https://www.example.com/cy-gb/blog/croeso" />
  <xhtml:link xmlns:xhtml="http://www.w3.org/1999/xhtml" rel="alternate" hreflang="en-GB" href="https://www.example.com/en-gb/blog/welcome" />
  <xhtml:link xmlns:xhtml="http://www.w3.org/1999/xhtml" rel="alternate" hreflang="x-default" href="https://www.example.com/en-gb/blog/welcome" />
</url>
<url>
  <loc>https://www.example.com/en-gb/blog/the-coast</loc>
  <lastmod>2026-09-03T12:00:00.0000000+00:00</lastmod>
  <xhtml:link xmlns:xhtml="http://www.w3.org/1999/xhtml" rel="alternate" hreflang="en-GB" href="https://www.example.com/en-gb/blog/the-coast" />
  <xhtml:link xmlns:xhtml="http://www.w3.org/1999/xhtml" rel="alternate" hreflang="x-default" href="https://www.example.com/en-gb/blog/the-coast" />
</url>
<url>
  <loc>https://www.example.com/en-gb/blog/welcome</loc>
  <lastmod>2026-09-01T10:00:00.0000000+00:00</lastmod>
  <xhtml:link xmlns:xhtml="http://www.w3.org/1999/xhtml" rel="alternate" hreflang="cy-GB" href="https://www.example.com/cy-gb/blog/croeso" />
  <xhtml:link xmlns:xhtml="http://www.w3.org/1999/xhtml" rel="alternate" hreflang="en-GB" href="https://www.example.com/en-gb/blog/welcome" />
  <xhtml:link xmlns:xhtml="http://www.w3.org/1999/xhtml" rel="alternate" hreflang="x-default" href="https://www.example.com/en-gb/blog/welcome" />
</url>
```

`AlternateGroup` joins translations of one article even when slugs differ. It is required for localized provider entries. Only returned siblings get annotations; if English is missing, its configured `x-default` is omitted too. Set `Hreflang = false` to keep the same three `loc`/`lastmod` entries without any `xhtml:link` elements.

**Supply the locale on each translation.** Omitting a finite parameter expands its central list, which would advertise translations that might not exist. Return only published canonical variants, removing records when they are unpublished or deleted.

> [!IMPORTANT]
> **Dynamic entries require supplied dates.** Set `LastModified` on each entry or register a freshness provider for that page. Source stamps are never used for runtime content.

Use `new SitemapLastModified(DateOnly)` if only a date is known: output is `2026-09-01`. `DateTimeOffset` preserves the time and offset, as above. Each translation can have its own date. Advance dates for significant published changes, including relevant shared content.

## A blog index, and page-specific freshness providers

An index can have a fixed route and still change when its contents change:

```razor
@page "/blog"
@attribute [SitemapPage(DynamicContent = true)]
```

Give that page its own provider:

```csharp
builder.Services.AddSitemapLastModified<BlogIndex, BlogIndexFreshness>();

public sealed class BlogIndexFreshness(BlogStore store) : ISitemapLastModifiedProvider
{
    public ValueTask<SitemapLastModified?> GetLastModifiedAsync(
        SitemapPageContext page, CancellationToken cancellationToken = default)
        => ValueTask.FromResult<SitemapLastModified?>(
            new SitemapLastModified(store.CollectionPublishedAtUtc));
}
```

With a collection timestamp of 4 September at 13:00 UTC, output is:

```xml
<url>
  <loc>https://www.example.com/blog</loc>
  <lastmod>2026-09-04T13:00:00.0000000+00:00</lastmod>
</url>
```

**Call `AddSitemapLastModified<TPage, TProvider>()` once for each page that needs its own freshness provider:**

```csharp
builder.Services.AddSitemapLastModified<BlogIndex, BlogIndexFreshness>();
builder.Services.AddSitemapLastModified<CatalogueIndex, CatalogueIndexFreshness>();
```

These are two independent registrations; the second doesn't replace the first. Each provider implements the same interface and receives only instances of its registered page. There is no type switch or URL mapping to maintain. Assuming the catalogue component declares its route and its provider returns 5 September, its additional output is:

```xml
<url><loc>https://www.example.com/catalogue</loc><lastmod>2026-09-05</lastmod></url>
```

Register after `AddBlazorSitemap`. Only one freshness provider can own a given page; duplicate registrations fail clearly. Providers are scoped to a sitemap generation and can use scoped database services. They receive expanded route values and the generated canonical URL in `SitemapPageContext`. For a localized index, use `page.Values` to choose its locale's collection timestamp.

The date precedence is: **entry date → registered page provider → static source stamp**. A provider may return `null` if no date is known; strict validation then fails if no permitted static stamp is available. A page with an entries provider is already dynamic. `DynamicContent = true` marks fixed or finite pages that depend on runtime data.

Keep a **collection publication timestamp** in your database. Advance it on additions, deletions, unpublication, retitling and ordering changes. Taking the maximum remaining article timestamp misses deletions. After a deletion at 14:00, the index still exists and becomes:

```xml
<url><loc>https://www.example.com/blog</loc><lastmod>2026-09-04T14:00:00.0000000+00:00</lastmod></url>
```

The deleted post's entry disappears. Neither change needs an application rebuild.

## URLs served by APIs or other projects

If a public page doesn't come from a Blazor component, add its URL directly. These entries join the discovered pages in the same sitemap. **Do not repeat a Blazor page's route here**; use its attribute or component provider instead.

For fixed URLs, register one or more batches after `AddBlazorSitemap`:

```csharp
builder.Services.AddSitemapUrls(
    new SitemapUrlEntry("/api/guide", new SitemapLastModified(new DateOnly(2026, 9, 1))),
    new SitemapUrlEntry("https://www.example.com/other-project/help",
        new SitemapLastModified(new DateTimeOffset(2026, 9, 2, 10, 0, 0, TimeSpan.Zero))));
```

Additional output:

```xml
<url><loc>https://www.example.com/api/guide</loc><lastmod>2026-09-01</lastmod></url>
<url><loc>https://www.example.com/other-project/help</loc><lastmod>2026-09-02T10:00:00.0000000+00:00</lastmod></url>
```

For runtime URLs, register a scoped `ISitemapUrlProvider`. It supplies actual locations and dates, without being tied to a component type:

```csharp
builder.Services.AddSitemapUrls<ApiArticleUrls>();

public sealed class ApiArticleUrls(BlogStore store) : ISitemapUrlProvider
{
    public async IAsyncEnumerable<SitemapUrlEntry> GetEntriesAsync(
        [System.Runtime.CompilerServices.EnumeratorCancellation]
        CancellationToken cancellationToken = default)
    {
        foreach (var article in store.Published().Where(x => x.Locale == "en-gb"))
        {
            cancellationToken.ThrowIfCancellationRequested();
            yield return new SitemapUrlEntry(
                "/api/articles/" + Uri.EscapeDataString(article.Slug),
                new SitemapLastModified(article.UpdatedAtUtc));
        }
        await Task.CompletedTask;
    }
}
```

For the published English articles above, additional output is:

```xml
<url><loc>https://www.example.com/api/articles/the-coast</loc><lastmod>2026-09-03T12:00:00.0000000+00:00</lastmod></url>
<url><loc>https://www.example.com/api/articles/welcome</loc><lastmod>2026-09-01T10:00:00.0000000+00:00</lastmod></url>
```

Replace `BlogStore` with your service; register its dependencies normally. Stream or page a consistent query of published URLs. Edits change supplied dates; removing an entry unpublishes it after cache expiry or invalidation. The [working demo](tests/Consumer/DemoHost.cs) maps these endpoints and registers both sources.

**Every standalone URL requires a supplied lastmod**, even if `RequireLastModified` is false. There is no component source to track. Static registrations don't generate sidecar files or serving endpoints: you must serve those pages yourself. These entries support `loc` and `lastmod`; hreflang remains available through component entries.

Locations are either escaped application-relative paths starting with `/`, or fully qualified HTTP(S) URLs under `PublicBaseUrl`. With a base of `https://www.example.com/portal`, `/api/guide` becomes `https://www.example.com/portal/api/guide`. Absolute URLs must stay within that base, including its path. Queries are allowed and XML-escaped; fragments, credentials, whitespace, malformed percent escapes and URLs of 2,048 characters or more are rejected. If another project lives outside your mount, serve a separate sitemap there or arrange a domain-root sitemap rather than advertising out-of-scope URLs. [Sitemap location rules](https://www.sitemaps.org/protocol.html#location)

Call the fixed overload repeatedly for more batches, and the generic overload for each provider. Re-registering the same provider type has no effect. Equal standalone URLs and dates deduplicate; conflicting dates or ownership by an included component fail. All entries share the cache, deterministic order and partition limits described below.

## Prefer accurate dates, or explicitly allow missing ones

To accept only application-supplied dates:

```csharp
options.UseSourceLastModified = false;
```

A page-specific freshness provider can supply a CMS/deployment publication date for a static page too. Its `loc` stays the same; its supplied date replaces the estimate. Without a supplied date, generation fails by default with `Missing trustworthy lastmod` and the page URL.

If you genuinely have no reliable date and prefer to omit it, also set:

```csharp
options.RequireLastModified = false;
```

Unknown **static** freshness then produces:

```xml
<url><loc>https://www.example.com/about</loc></url>
```

A log reports the number of missing static dates. Dynamic dates remain required under either policy. Leave the defaults alone if you want a value on every entry and accept the documented static source estimates.

## Keep private and noindex pages out

Inclusion is opt-in. For login, private, noindex, redirects or error pages, leave off `[SitemapPage]`:

```razor
@page "/private"
<HeadContent><meta name="robots" content="noindex" /></HeadContent>
<h1>Private page</h1>
```

This creates **no sitemap entry**. Its head includes the noindex tag shown above. Known `[Authorize]` metadata combined with sitemap inclusion is rejected.

The package cannot inspect every authorization policy, arbitrary head content or runtime redirect. Choose included pages to match the site's actual public/indexable behavior. Exclusion from a sitemap does not secure a page.

## Component libraries and Interactive Auto

For a referenced Razor component library, add its assembly in the sitemap options and in Blazor's endpoint/router configuration:

```csharp
// Inside the sitemap options callback:
options.AddAssembly(typeof(LibraryPage).Assembly);

// When mapping the app:
app.MapRazorComponents<App>()
    .AddAdditionalAssemblies(typeof(LibraryPage).Assembly);
```

If `LibraryPage` declares `@page "/library"` and `[SitemapPage]`, output gains:

```xml
<url><loc>https://www.example.com/library</loc><lastmod>2026-09-01T09:00:00.0000000+00:00</lastmod></url>
```

The library carries its own compiled routes and source stamps. Also set `Router.AdditionalAssemblies` if your router needs them. Registering a provider automatically adds that page's assembly to sitemap discovery.

**There are two NuGet packages.** The server package depends on the lightweight Abstractions package, so a server app only needs to install `PinguApps.BlazorSitemap`. For client projects, install `PinguApps.BlazorSitemap.Abstractions` directly to avoid a server framework dependency.

**Interactive Auto works with server and client projects.** Install the main package on the server and `PinguApps.BlazorSitemap.Abstractions` on the client. Both use the same namespace; the small package contains the page attribute, locale record and source-tracking build assets, without requiring the server-only shared framework in the browser.

Use Blazor's normal interactive registrations, plus client-assembly discovery:

```csharp
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents()
    .AddInteractiveWebAssemblyComponents();

// Inside the sitemap options callback:
options.AddAssembly(typeof(Client.AutoPage).Assembly);

app.MapBlazorSitemap();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode()
    .AddInteractiveWebAssemblyRenderMode()
    .AddAdditionalAssemblies(typeof(Client.AutoPage).Assembly);
```

Replace `Client.AutoPage` with your own client page/marker. The server needs its normal `Microsoft.AspNetCore.Components.WebAssembly.Server` reference. Add the client assembly to the router too. With the [demo Auto page](tests/Consumer.Client/AutoPage.razor), output gains:

```xml
<url><loc>https://www.example.com/auto</loc><lastmod>2026-09-01T09:00:00.0000000+00:00</lastmod></url>
```

The [working host](tests/Consumer/DemoHost.cs) shows the full setup. [Official render-mode guidance](https://learn.microsoft.com/en-us/aspnet/core/blazor/components/render-modes?view=aspnetcore-10.0)

A standalone WebAssembly app needs a hosting endpoint or deployment process to serve a dynamic sitemap. The server's reflection discovery requires an untrimmed deployment; trimmed/Native AOT server publishing is not supported. The Auto client's normal trimming is supported.

## Mount the app under a path base

Set `PublicBaseUrl` to `https://www.example.com/portal`. Before mapping endpoints:

```csharp
app.UsePathBase(new Uri(builder.Configuration["Sitemap:PublicBaseUrl"]!)
    .AbsolutePath.TrimEnd('/'));
app.MapBlazorSitemap();
```

Your Razor routes stay application-relative. The endpoint is now `/portal/sitemap.xml`, and an About entry becomes:

```xml
<url><loc>https://www.example.com/portal/about</loc><lastmod>2026-09-01T09:00:00.0000000+00:00</lastmod></url>
```

Child URLs become `/portal/sitemap-{hash-prefix}.xml`. Set Blazor's `<base href>` consistently and match your reverse proxy's mount behavior. Source dates don't depend on the domain or path base.

## Cache expensive queries

The package caches a complete sitemap snapshot for **five minutes** by default. Providers run once per generation, not once per child request. Set `options.CacheLifetime` to another `TimeSpan`; zero regenerates every request.

After a content transaction commits, inject `SitemapCache` and call:

```csharp
sitemapCache.Invalidate();
```

The next request regenerates the sitemap. Without invalidation, changes appear after expiry. For example, an edited post changes from:

```xml
<url><loc>https://www.example.com/en-gb/blog/welcome</loc><lastmod>2026-09-01T10:00:00.0000000+00:00</lastmod></url>
```

To its new published date:

```xml
<url><loc>https://www.example.com/en-gb/blog/welcome</loc><lastmod>2026-09-06T15:00:00.0000000+00:00</lastmod></url>
```

Concurrent requests share generation; a failed query never publishes a partial snapshot. **You can also cache queries inside your providers.** If enumerating all your blog articles and dates is expensive, use your application's normal cache there. You own its lifetime and invalidation: sitemap invalidation cannot clear a cache owned by your app. This applies to component entries, freshness queries and standalone URL providers. For multiple server instances, invalidate each or accept the expiry delay. Align CDN/output caching with that policy.

## Large sitemaps and validation

Every child document enforces both **50,000 URLs** and **52,428,800 uncompressed UTF-8 bytes**, including XML escaping and hreflang markup. When either limit requires splitting, `/sitemap.xml` becomes an index:

```xml
<?xml version="1.0" encoding="UTF-8"?>
<sitemapindex xmlns="http://www.sitemaps.org/schemas/sitemap/0.9">
  <sitemap><loc>https://www.example.com/sitemap-0.xml</loc></sitemap>
  <sitemap><loc>https://www.example.com/sitemap-1.xml</loc></sitemap>
</sitemapindex>
```

This is a shortened illustration; real child names and count come from URL hash-prefix buckets. Each listed child is a real endpoint containing a normal `urlset`, with the XML declaration and entries shown above. You can lower `MaxUrlsPerSitemap` and `MaxBytesPerSitemap` for your app.

URLs have deterministic order. Hash partitioning keeps unaffected child addresses stable when a bucket grows; shrinking buckets may merge. An old index can reference a retired child, so refresh the index after a child 404. A single entry that cannot fit fails clearly. The index has its own 50,000-child and 52,428,800-byte limits; larger sites need separately managed sitemap sets. Index lastmod is omitted because page dates do not establish a child document's modification date. [Sitemaps protocol](https://www.sitemaps.org/protocol.html)

Identical URLs and metadata are deduplicated. Conflicting ownership, dates or alternate groups fail. Parameter names are case-insensitive. Supported routes use literal segments, whole-segment parameters, optional parameters or catch-alls. Supported Blazor constraints are `bool`, `datetime`, `decimal`, `double`, `float`, `guid`, `int`, `long`, `nonfile`. Complex segments, defaults and other constraints are rejected. Ordinary values cannot contain slashes, traversal or control characters. URLs must stay under the configured public base and be shorter than 2,048 characters. [Blazor routing guidance](https://learn.microsoft.com/en-us/aspnet/core/blazor/fundamentals/routing?view=aspnetcore-10.0)

Providers can stream records, but generation retains URL metadata and XML for grouping, deduplication and caching. Allow memory for the site's complete sitemap.

## Try the working example or contribute

From a checkout, using PowerShell 7 and the SDK in `global.json`:

```powershell
pwsh -NoProfile -File eng/Verify.ps1
pwsh -NoProfile -File eng/New-Demo.ps1
```

Verification packs the packages and symbols, restores the example from an isolated local NuGet feed, and runs the Gherkin/Reqnroll suite. Results go to `TestResults`. `New-Demo.ps1` creates an independent app outside the repository and prints its path. Its server, client and Razor library consume the packed packages. Generated `Directory.Build.props` keeps the package version and isolated cache in place for subsequent restores, builds and runs, so an older same-version package in your global cache cannot replace the local artifact.

From the generated demo directory, run:

```powershell
dotnet run --project ./Consumer/Consumer.csproj
```

This builds Debug if needed and uses the launch profile's `http://localhost:5187` address and Development environment. To use the prebuilt Release app, run `dotnet run --project ./Consumer/Consumer.csproj --no-build -c Release`. `--no-build` alone selects Debug, which may not have been built yet.

Visit `/blog` and `/sitemap.xml`. The example includes Home, About with an old alias, English/Welsh counters, a library page, an Auto page, a blog index, three published article translations, a fixed API-served guide and two provider-backed API article pages: **13 sitemap entries**, all with dates. Static component dates use source stamps; article and collection dates use publication data. Standalone pages supply their dates.

The edit/add/unpublish/delete controls update the XML without rebuilding. Addition gives 15 entries; unpublishing the coast article returns to 13; deleting the added article leaves 11. Both component and API article URLs update together. The in-memory content resets on restart. The private page stays excluded. Controls are local Development examples; protect real publishing endpoints. Stop the app with Ctrl+C when finished.
