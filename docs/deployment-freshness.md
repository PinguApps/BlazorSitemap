# Static source-change timestamps

Static pages get a date automatically during a normal `dotnet build` or `dotnet publish`. There is no HTTP crawl, base URL, deployment callback or runtime state file involved in freshness tracking. The public base URL is still needed to turn routes into absolute sitemap URLs.

## What the build does

The Abstractions package contains cross-platform MSBuild `buildTransitive` assets. These flow to server, client and Razor component library projects consuming the package. The task finds standard `@attribute [SitemapPage]` declarations, including fully qualified names and the `Attribute` suffix. Put the attribute on the Razor page itself; aliases or attributes supplied only through `_Imports.razor` or code-behind are not detected by this source task. Runtime discovery still reads compiled metadata; use a page-specific freshness provider for such declarations.

For `Counter.razor`, it hashes:

- The text of `Counter.razor`.
- The text of `Counter.razor.cs`, if present, including whether that file exists.

Line endings are normalized, and UTF-8 BOM differences are ignored. Other edits, including whitespace and comments, change the hash. A SHA-256 hash and UTC timestamp are written to `Counter.razor.sitemap.json`. This file is data, not generated page code. It is not deployed as a loose content file.

An unchanged hash keeps the timestamp. A changed hash, a new page, or intentionally deleted state gets the time the build observes that version. A malformed state file fails the build with its filename; restore it or delete it to intentionally reset the estimate. Builds must be serialized when writing the same checkout.

The build also emits assembly metadata under `obj`. Razor's compiler supplies the source identity on `[SitemapPage]`, so runtime lookup needs no URL or component namespace mapping. The resulting DLL carries the timestamp. Deployment needs neither the Razor source nor the sibling JSON. Referenced component libraries carry their own stamps; Interactive Auto pages carry theirs in the client assembly that the server discovers.

## Keep history across CI builds

**Commit the `.razor.sitemap.json` files alongside your pages.** Do not add them to `.gitignore`, delete them during checkout, or store them only in `obj`.

A normal workflow is:

1. Edit a page or its code-behind.
2. Build. The changed page's sibling JSON updates.
3. Commit the source and JSON together.
4. CI checks out both and builds. Unchanged source retains the recorded date.

CI can generate missing or changed state too, but it must retain that result for the next build, through a reviewed commit or durable artifact retrieval. The package does not push commits. Throwaway CI caches are not a reliable history store. A fresh checkout without the JSON starts with a new build-time estimate for every discovered page.

For an existing site, the first stamp cannot recover its original publication date. It starts at the first tracked build. Failed builds can have updated state, and abandoned branches can have stamps for content that was never published: these are source observations, not publication events. Rebuilding on an unchanged branch preserves dates; switching between branches with their committed state restores each branch's estimates.

## Force an update

Delete the relevant sibling JSON and rebuild. For example, after changing a shared resource affecting Counter:

```powershell
Remove-Item -LiteralPath 'Components/Pages/Counter.razor.sitemap.json'
dotnet build
```

The same route receives a new source-change timestamp. No URL needs to be written again. Renaming a page creates a new sibling state; remove the old sibling file when you remove or rename the source page.

## What this estimate means

The timestamp means **when this source version was first observed by a tracked build**. It is a practical approximation, not an exact date of a significant change to the served page. It does not read filesystem modification dates, Git dates, request time or startup time.

Layout, `_Imports.razor`, localization resources, separate services, shared components, images, linked assets and database changes are not hashed. A change to one translation resource will not automatically advance that locale's date. All finite variants of a static component use its same source stamp. Edits that do not change meaningful rendered content can still advance it. Deleting the JSON deliberately loses history.

Google recommends accurate lastmod values tied to significant page changes. Choose these source estimates only when their limitations suit your application. For accurate per-variant or publication dates, register `AddSitemapLastModified<TPage, TProvider>()`. Provider dates override estimates. Set `UseSourceLastModified = false` to disallow estimates; completeness remains enforced by default. Dynamic content always needs supplied dates, even if its Razor file has a source stamp. [Google sitemap guidance](https://developers.google.com/search/docs/crawling-indexing/sitemaps/build-sitemap)

To disable source-state generation in a project, use:

```xml
<PropertyGroup>
  <BlazorSitemapTrackSourceChanges>false</BlazorSitemapTrackSourceChanges>
</PropertyGroup>
```

An opted-in static page in that assembly then needs a freshness provider, unless omission is explicitly allowed. This property controls build assets; `UseSourceLastModified` controls runtime acceptance of existing embedded estimates.
