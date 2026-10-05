namespace PinguApps.BlazorSitemap;

/// <summary>One published instance of a component route. Providers return only canonical, indexable instances.</summary>
/// <param name="Values">Decoded parameter values. Missing finite parameters are expanded centrally.</param>
public sealed record SitemapEntry(IReadOnlyDictionary<string, string?> Values)
{
    /// <summary>Gets the reliable modification date of this variant. Runtime entries must supply it here or through their page-specific freshness provider.</summary>
    public SitemapLastModified? LastModified { get; init; }

    /// <summary>Gets an identity shared by equivalent translations of this instance within this page type.</summary>
    public string? AlternateGroup { get; init; }
}
