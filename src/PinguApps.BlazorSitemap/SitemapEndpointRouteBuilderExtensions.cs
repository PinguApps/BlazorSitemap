using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace PinguApps.BlazorSitemap;

/// <summary>Maps public sitemap HTTP endpoints.</summary>
public static class SitemapEndpointRouteBuilderExtensions
{
    /// <summary>Maps /sitemap.xml and /sitemap-{partition}.xml relative to the application's path base.</summary>
    public static IEndpointConventionBuilder MapBlazorSitemap(this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);
        // Validate discovery at startup; do not query application data until a snapshot is requested.
        _ = endpoints.ServiceProvider.GetRequiredService<RouteCatalog>();
        RouteGroupBuilder group = endpoints.MapGroup("");
        group.MapGet("/sitemap.xml", async (HttpContext context, SitemapCache cache) =>
        {
            SitemapSnapshot snapshot = await cache.GetAsync(context.RequestAborted);
            return Xml(snapshot.Root);
        });
        group.MapGet("/sitemap-{partition}.xml", async (string partition, HttpContext context, SitemapCache cache) =>
        {
            SitemapSnapshot snapshot = await cache.GetAsync(context.RequestAborted);
            return snapshot.Children.TryGetValue(partition, out byte[]? xml) ? Xml(xml) : Results.NotFound();
        });
        group.AllowAnonymous();
        return group;
    }

    private static IResult Xml(byte[] xml) => Results.Bytes(xml, "application/xml; charset=utf-8");
}
