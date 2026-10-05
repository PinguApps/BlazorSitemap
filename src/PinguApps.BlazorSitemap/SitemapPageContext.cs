namespace PinguApps.BlazorSitemap;

/// <summary>Identifies a canonical URL for application-owned or deployment-owned freshness lookup.</summary>
/// <param name="PageType">The component type.</param>
/// <param name="Url">The fully qualified canonical URL.</param>
/// <param name="Values">The expanded, decoded route parameters.</param>
public sealed record SitemapPageContext(Type PageType, Uri Url, IReadOnlyDictionary<string, string?> Values);
