namespace PinguApps.BlazorSitemap;

internal sealed record SourceRegistration(Type PageType, Func<IServiceProvider, CancellationToken, IAsyncEnumerable<SitemapEntry>> Read);
