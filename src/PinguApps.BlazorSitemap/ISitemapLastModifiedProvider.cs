namespace PinguApps.BlazorSitemap;

/// <summary>Supplies per-URL freshness for its registered page, including collection publication events.</summary>
public interface ISitemapLastModifiedProvider
{
    /// <summary>Returns null when the significant-change date cannot be established.</summary>
    public ValueTask<SitemapLastModified?> GetLastModifiedAsync(SitemapPageContext page, CancellationToken cancellationToken = default);
}
