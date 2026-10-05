using Microsoft.AspNetCore.Components;

namespace PinguApps.BlazorSitemap.Tests.Support;

public sealed class TestFreshness<TPage>(TestContent content) : ISitemapLastModifiedProvider where TPage : IComponent
{
    public ValueTask<SitemapLastModified?> GetLastModifiedAsync(SitemapPageContext page, CancellationToken cancellationToken = default)
        => ValueTask.FromResult(content.Freshness.GetValueOrDefault(page.Url.AbsolutePath));
}
