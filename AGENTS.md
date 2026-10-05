# Verification

Run `pwsh -NoProfile -File eng/Verify.ps1` after product changes. It packs first, restores the consumer and tests from an isolated local feed, and executes every Gherkin scenario. Direct solution restore cannot resolve the unpublished package until it has been packed.

Keep product behavior tests in `.feature` files with Reqnroll step definitions. `tests/Consumer`, `tests/Consumer.Client` and `tests/Consumer.Library` consume packed NuGet artifacts; preserve that boundary when changing examples. The client uses the browser-compatible Abstractions package.

# Product rules

Use `PinguApps.BlazorSitemap` for public APIs. Discovery is opt-in from compiled component route metadata. Multiple routes require an explicit canonical choice. Advertise only published canonical instances; localized providers must identify equivalent translations.

Keep one top-level class, record, struct, enum or interface per file, including test support and examples. Nested types are allowed for a genuine ownership reason.

Standalone URL registrations and `ISitemapUrlProvider` supplement component discovery. Require supplied dates, validate locations under PublicBaseUrl, and share deduplication, caching and partitioning. Do not repeat component routes as standalone URLs.

Dynamic entries and DynamicContent pages require supplied dates. Static pages default to embedded source-change estimates from persistent `.razor.sitemap.json` files, hashing the page and optional `.razor.cs`. Preserve unchanged timestamps and commit the sibling state. Clearly distinguish source estimates from exact rendered/publication freshness. Supplied page-specific dates take precedence. RequireLastModified defaults to true; omission requires explicit configuration. Keep both XML document limits and the index limits enforced on serialized UTF-8 bytes.

For API or behavior changes, update the README and executable consumer examples together. For source freshness changes, also read `docs/deployment-freshness.md` and update its Gherkin scenarios. Stop all task-owned app/build processes after verification.

# Release boundary

Local verification produces ignored artifacts. Publishing requires an intentional release/tag workflow and configured NuGet trusted publishing. The scheduled draft workflow is Sunday 09:39 UTC; local work must not invoke it. Implementation and verification alone do not authorize publishing or creating a release.
