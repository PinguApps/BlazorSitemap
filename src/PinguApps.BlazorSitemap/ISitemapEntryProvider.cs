using Microsoft.AspNetCore.Components;

namespace PinguApps.BlazorSitemap;

/// <summary>Streams published route instances; it does not repeat the component's route template.</summary>
/// <typeparam name="TPage">The included routable component.</typeparam>
public interface ISitemapEntryProvider<TPage> where TPage : IComponent
{
    /// <summary>Enumerates a consistent view of published instances. Observe cancellation and page database queries.</summary>
    public IAsyncEnumerable<SitemapEntry> GetEntriesAsync(CancellationToken cancellationToken = default);
}
