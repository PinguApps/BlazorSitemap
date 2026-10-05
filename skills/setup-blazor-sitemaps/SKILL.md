---
name: setup-blazor-sitemaps
description: Set up or configure XML sitemaps in Blazor using PinguApps.BlazorSitemap. Use for general Blazor sitemap setup requests, including route discovery, localization, runtime content and lastmod, even when no package is named.
---

# Set up / configure sitemaps in Blazor

Target .NET 10 server-hosted Blazor. Use namespace `PinguApps.BlazorSitemap`. The package discovers compiled `RouteAttribute` metadata; it does not scan deployed Razor source or render pages. Standalone WebAssembly requires a hosting endpoint or deployment-generated sitemap. Server trimming/Native AOT is unsupported; normal Auto client trimming is supported.

## Install and register

- Server: `PinguApps.BlazorSitemap`. Its dependency brings Abstractions transitively.
- Each referenced client/Razor library containing included routable components: explicitly reference `PinguApps.BlazorSitemap.Abstractions` for attributes and source-tracking assets. Do not require the server package in the browser.
- Configure `Sitemap:PublicBaseUrl` using normal ASP.NET Core configuration. Prefer `dotnet user-secrets init` and `dotnet user-secrets set "Sitemap:PublicBaseUrl" "http://localhost:5187"` in the server project for Development. User Secrets are automatically loaded by `WebApplication.CreateBuilder` only in Development. Supply production settings through appsettings, environment variables (`Sitemap__PublicBaseUrl`) or deployment configuration. Command-line overrides also work.
- Set the actual trusted public HTTP(S) origin and optional mount, never incoming Host headers. Credentials, query, fragment, whitespace and backslashes are invalid in the base.

```csharp
builder.Services.AddBlazorSitemap<App>(options =>
{
    options.PublicBaseUrl = builder.Configuration["Sitemap:PublicBaseUrl"]!;
});
// Add entry/freshness/standalone providers after the registration above.
var app = builder.Build();
app.UseAntiforgery();
app.MapBlazorSitemap();
app.MapRazorComponents<App>();
```

Keep the app's normal Blazor registrations/middleware. `MapBlazorSitemap` is required to expose `/sitemap.xml` and `/sitemap-{hash-prefix}.xml` children. Service registration alone exposes nothing. Put `@using PinguApps.BlazorSitemap` in `_Imports.razor`.

## Include component routes

- Opt in public/indexable components with `@attribute [SitemapPage]`. Write each path only in `@page`; do not repeat it in sitemap registrations, providers or freshness mappings.
- Leave the attribute off private, login, noindex, redirect, error and alias-only pages. Inclusion plus known `[Authorize]` metadata fails. The package cannot interpret arbitrary head content, redirects, authorization policies or runtime rendering. Sitemap exclusion does not secure a page.
- Multiple distinct `@page` templates require `CanonicalRouteIndex`: zero-based index after ordinal sorting. Only the selected template is advertised. Do not guess canonical intent. Use separate components for separate resources.
- Fixed routes need no entries provider. Finite routes use central decoded values: `options.RouteValues("category", categories)` or `options.RouteValues("ItemId", ids.Select(x => x.ToString(CultureInfo.InvariantCulture)))`. Names are case-insensitive. Values must be nonempty and unique; duplicate registration fails.
- Lists expand Cartesian products. Use runtime entries instead if some combinations do not exist. Optional omission must be explicitly included as `null`; an optional parameter still needs a finite list or provider value. Unresolved parameters fail.
- Supported constraints: `bool`, `datetime`, `decimal`, `double`, `float`, `guid`, `int`, `long`, `nonfile`. Values must satisfy constraints. Literal segments, whole-segment parameters, optional parameters and catch-alls are supported. Complex segments, defaults and other constraints fail. Ordinary segments reject slashes, traversal and controls; catch-alls split values into encoded segments.

## Localization

Reuse the app's locale list: `options.Locales(locales.Select(x => new SitemapLocale(x.UrlSegment, x.Language)), parameter: "locale", xDefaultLocale: "en-gb")`. The default parameter is `locale`, configurable rather than a reserved keyword. Route values and language codes must be unique. `xDefaultLocale` is optional and must select a registered locale.

Each expanded localized URL receives its own `loc`. `[SitemapPage(Hreflang = true)]` adds reciprocal XHTML alternates, including self. With `/{ItemId:int}/{locale}/counter`, only the configured locale parameter varies inside a group; ItemId remains fixed. The same applies to categories/other nonlocale parameters. Without Hreflang, the same URLs are emitted without annotations. No implicit x-default is generated; an explicitly configured fallback is linked only when that sibling exists.

For runtime translations, set every locale explicitly and return only published translations. Omitting a finite parameter expands it and can advertise missing translations. Set `AlternateGroup` to a stable content identity shared by equivalent translations of that component, even if slugs differ. Dynamic hreflang entries require this group. One group cannot have multiple URLs for one language.

## Runtime component entries and freshness

Register `builder.Services.AddSitemapEntries<BlogPost, BlogEntries>()`. Implement `ISitemapEntryProvider<BlogPost>.GetEntriesAsync(CancellationToken)` returning `IAsyncEnumerable<SitemapEntry>`. Each entry contains decoded parameter values, `LastModified` and, if needed, `AlternateGroup`. The provider replaces automatic instances for that page; an empty result excludes it. Stream/page a consistent published-data query; observe cancellation. Do not repeat the route template. Added, edited, unpublished and deleted records appear through cache invalidation/expiry without rebuilding.

Use `SitemapLastModified(DateOnly)` for date precision or `SitemapLastModified(DateTimeOffset)` to preserve offset/time precision. Supply dates for significant published changes; never use request/startup/current/filesystem time merely to fill missing dates.

Register `builder.Services.AddSitemapLastModified<BlogIndex, BlogIndexFreshness>()` for page-owned freshness. Implement `ISitemapLastModifiedProvider.GetLastModifiedAsync(SitemapPageContext, CancellationToken)` returning `ValueTask<SitemapLastModified?>`. The context contains page type, generated URL and expanded values. A provider is scoped to a generation and called only for its registered page. Repeat for different pages; duplicate ownership of one page fails. No URL mapping or page-type switch is needed. Registered page providers and entries providers add their page assembly to discovery.

Mark fixed/finite pages depending on runtime data with `[SitemapPage(DynamicContent = true)]`. A page with an entries provider is already dynamic. Collection/index freshness must track publication events independently of the remaining records; additions, deletion, unpublication, retitling and ordering changes can advance it. Maximum remaining record date misses deletions.

Date precedence: entry value, then registered page freshness, then permitted static source stamp. Dynamic component entries and DynamicContent pages always require supplied dates, including when `RequireLastModified` is false. Returning null means unknown, not permission to fabricate a date.

## Static source estimates

The transitive build task hashes each included page's `.razor` plus optional `.razor.cs`, normalizing line endings. It maintains sibling `.razor.sitemap.json` state (SHA-256 and UTC source-change timestamp), embeds dates in the component assembly, and preserves dates across unchanged builds. Runtime deployment requires neither source nor sibling JSON.

Commit sibling state. First build records an estimate, not historical publication time. Edits or deletion/reset of state produce a new source estimate. Comments/formatting can advance it; layouts, shared components, localization resources, structured data outside those files, services/assets and runtime data are not tracked. Finite static variants share their page's date. Invalid persisted state fails instead of silently resetting history. Build state must be writable and retained across CI runs.

Use an application/deployment-owned page provider for accurate publication dates; these override estimates. Defaults are `UseSourceLastModified = true` and `RequireLastModified = true`. Disable source estimates to require supplied dates. Explicitly set `RequireLastModified = false` to allow unknown static dates to be omitted and logged. It never relaxes dynamic or standalone requirements. Read [source freshness operations](../../docs/deployment-freshness.md) when changing tracking, CI persistence, attribute syntax, resetting state or disabling the build task.

## Standalone URLs

Use these only for public resources outside component routing. Do not repeat included Blazor routes here.

```csharp
builder.Services.AddSitemapUrls(
    new SitemapUrlEntry("/api/guide", new SitemapLastModified(new DateOnly(2026, 9, 1))));
builder.Services.AddSitemapUrls<ApiArticleUrls>();
```

Implement `ISitemapUrlProvider.GetEntriesAsync(CancellationToken)` returning `IAsyncEnumerable<SitemapUrlEntry>` for runtime locations. Providers are scoped, support normal DI, and can stream/page a consistent catalogue. Repeat fixed batches or register different providers; registering the same provider type again is idempotent.

`SitemapUrlEntry` requires escaped `Location` and supplied `LastModified`. Relative locations must begin with `/` and are appended to the configured application base: `/api/guide` under `https://example.com/portal` becomes `/portal/api/guide`. Absolute HTTP(S) locations must remain under that base, including its path. Queries are allowed; fragments, credentials, whitespace, backslashes, scheme-relative URLs and malformed percent escapes fail. URLs must be shorter than 2,048 characters. Serve separate sitemaps or a root-level aggregate for resources outside a mount. No serving endpoints, sidecars, automatic freshness or hreflang are generated for standalone entries. Supplied dates are always required. Equal standalone URLs/dates deduplicate; conflicting dates or component ownership fail.

## Assemblies, Auto and mounts

Add referenced route assemblies with `options.AddAssembly(typeof(LibraryPage).Assembly)`, `MapRazorComponents<App>().AddAdditionalAssemblies(...)`, and `Router.AdditionalAssemblies`. Merely referencing a project does not register its routing/discovery.

Interactive Auto: install Abstractions on the client, retain the server's normal `Microsoft.AspNetCore.Components.WebAssembly.Server` reference, call `AddInteractiveServerComponents().AddInteractiveWebAssemblyComponents()` and `AddInteractiveServerRenderMode().AddInteractiveWebAssemblyRenderMode()`. Include the client assembly in sitemap discovery, endpoint mapping and the router. Do not register server sitemap services in the browser.

For mounts, include the path in PublicBaseUrl, call `UsePathBase` before mapping endpoints, and align Blazor's base href/reverse proxy handling. Component routes remain application-relative. Sitemap and child paths gain the mount; source dates do not depend on origin or mount.

## Cache and document contract

- Default snapshot lifetime: five minutes (`options.CacheLifetime`). Zero regenerates per request. Providers run once per generation, not once per child request. Concurrent requests share generation. Errors publish no partial snapshot. `SitemapCache.Invalidate()` after a successful content transaction forces the next request to regenerate; invalidation during generation is preserved.
- Expensive provider queries may use app-owned caching. The app owns that cache's expiry/invalidation; sitemap invalidation cannot clear it. Invalidate each server instance or accept expiry delay; align CDN/output cache behavior.
- XML uses UTF-8 with a declaration, protocol namespace and `application/xml; charset=utf-8`. Locations are fully qualified; XML is escaped. Priority/changefreq are omitted. Hreflang uses the XHTML namespace. Chrome may show text instead of its tree viewer when XHTML link elements exist; this is browser presentation, not an invalid response. Inspect raw XML.
- Entries are ordered deterministically and deduplicated. Ownership/date/alternate conflicts fail. Both 50,000 URLs and 52,428,800 uncompressed UTF-8 bytes apply to each document, including escaping/alternates. Limits may be lowered. Splitting returns an index at `/sitemap.xml`; children use stable URL-hash-prefix addresses. Bucket splits/merges may retire children; refresh an old index after child 404.
- A single oversized entry fails. The index also enforces 50,000 child entries and 52,428,800 bytes. Larger sites require independently managed sitemap sets. Index lastmod is omitted: page timestamps do not prove child-document modification times.
- Providers may stream, but generation retains metadata/XML for the complete sitemap for grouping, deduplication and caching. Budget memory accordingly.
- Submit the public sitemap URL to Search Console or reference it in robots.txt. Advertising a URL does not make it indexable.

## Verify integration

Build the actual consuming projects. Fetch `/sitemap.xml` and every referenced child, check MIME/namespaces/locations/dates/locale groups, and exercise publication changes through the documented cache policy. Check API-served resources and component routes respond successfully. Do not infer freshness or canonical intent from unavailable information.

For this repository: `pwsh -NoProfile -File eng/Verify.ps1` packs both artifacts, restores isolated local-feed consumers and runs all Gherkin/Reqnroll scenarios. `eng/New-Demo.ps1` creates a separate server/client/library app consuming NuGet packages. Generated `Directory.Build.props` persists the local package cache and exact generated version; keep it to avoid stale global packages during later builds. From the demo directory, `dotnet run --project ./Consumer/Consumer.csproj` builds Debug and uses its localhost:5187 Development launch profile. Running the prebuilt app requires `--no-build -c Release`; `--no-build` alone selects Debug. Keep behavior tests in features with step definitions; one top-level type per file. Stop task-owned processes after checks. Publishing/release creation requires separate authorization; verification does not publish.

Read [README](../../README.md) for complete public examples and [workflow reconciliation](../../docs/github-workflows.md) when changing repository automation.
