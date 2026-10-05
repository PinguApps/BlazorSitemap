namespace PinguApps.BlazorSitemap;

internal sealed record FreshnessRegistration(Type PageType, Func<IServiceProvider, ISitemapLastModifiedProvider> Resolve);
