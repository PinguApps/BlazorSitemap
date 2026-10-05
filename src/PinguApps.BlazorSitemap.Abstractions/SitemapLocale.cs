namespace PinguApps.BlazorSitemap;

/// <summary>A supported locale, separating its URL segment from its language/region annotation.</summary>
/// <param name="RouteValue">The decoded route value, for example en-gb.</param>
/// <param name="Language">A hreflang language or language-region code, for example en-GB.</param>
public sealed record SitemapLocale(string RouteValue, string Language);
