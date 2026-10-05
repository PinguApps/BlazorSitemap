using System.Runtime.CompilerServices;
using Microsoft.AspNetCore.Components;

namespace PinguApps.BlazorSitemap.Tests.Support;

public sealed class MemoryProvider<TPage>(TestContent content, SitemapCache cache) : ISitemapEntryProvider<TPage> where TPage : IComponent
{
    public async IAsyncEnumerable<SitemapEntry> GetEntriesAsync([EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        content.Reads++;
        if (content.InvalidateDuringRead)
        {
            content.InvalidateDuringRead = false;
            cache.Invalidate();
        }

        foreach (SitemapEntry row in content.Rows)
        {
            cancellationToken.ThrowIfCancellationRequested();
            yield return row;
        }

        if (content.FailDuringRead)
        {
            throw new InvalidOperationException("Publication query failed.");
        }

        await Task.CompletedTask;
    }
}
