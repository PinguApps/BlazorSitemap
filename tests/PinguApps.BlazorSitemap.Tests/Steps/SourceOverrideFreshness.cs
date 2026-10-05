namespace PinguApps.BlazorSitemap.Tests.Steps;

public sealed class SourceOverrideFreshness : ISitemapLastModifiedProvider
{
    public ValueTask<SitemapLastModified?> GetLastModifiedAsync(SitemapPageContext page, CancellationToken cancellationToken = default)
        => ValueTask.FromResult<SitemapLastModified?>(new SitemapLastModified(new DateOnly(2020, 1, 2)));
}
