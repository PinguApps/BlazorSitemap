namespace PinguApps.BlazorSitemap;

/// <summary>Streams published URLs that are not backed by discovered component routes.</summary>
public interface ISitemapUrlProvider
{
    /// <summary>Enumerates a consistent publication view, observing cancellation. Every URL requires a supplied date.</summary>
    public IAsyncEnumerable<SitemapUrlEntry> GetEntriesAsync(CancellationToken cancellationToken = default);
}
