using System.Runtime.CompilerServices;

namespace PinguApps.BlazorSitemap.Tests.Support;

public sealed class MemoryUrlProvider(TestContent content) : ISitemapUrlProvider
{
    public async IAsyncEnumerable<SitemapUrlEntry> GetEntriesAsync([EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        content.UrlReads++;
        foreach (SitemapUrlEntry entry in content.UrlRows)
        {
            cancellationToken.ThrowIfCancellationRequested();
            yield return entry;
        }

        if (content.FailDuringRead) { throw new InvalidOperationException("Standalone publication query failed."); }
        await Task.CompletedTask;
    }
}
