using PinguApps.BlazorSitemap;

namespace Consumer;

public sealed class BlogIndexFreshness(BlogStore store) : ISitemapLastModifiedProvider
{
    public ValueTask<SitemapLastModified?> GetLastModifiedAsync(SitemapPageContext page, CancellationToken cancellationToken = default)
        => ValueTask.FromResult<SitemapLastModified?>(new SitemapLastModified(store.CollectionPublishedAtUtc));
}
