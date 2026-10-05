using System.Runtime.CompilerServices;

namespace PinguApps.BlazorSitemap.Tests.Support;

public sealed class OtherUrlProvider(TestContent content) : ISitemapUrlProvider
{
    public async IAsyncEnumerable<SitemapUrlEntry> GetEntriesAsync([EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        foreach (SitemapUrlEntry entry in content.OtherUrlRows)
        {
            cancellationToken.ThrowIfCancellationRequested();
            yield return entry;
        }
        await Task.CompletedTask;
    }
}
