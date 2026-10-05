namespace PinguApps.BlazorSitemap;

internal sealed record SitemapSnapshot(byte[] Root, IReadOnlyDictionary<string, byte[]> Children);
