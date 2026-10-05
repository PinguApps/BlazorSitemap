namespace PinguApps.BlazorSitemap;

internal sealed record CanonicalEntry(Type? Page, Uri Url, SitemapLastModified? LastModified, string? Group, SitemapLocale? Locale);
