using Microsoft.AspNetCore.Routing.Patterns;

namespace PinguApps.BlazorSitemap;

internal sealed record PageRoute(Type PageType, SitemapPageAttribute Attribute, RoutePattern Pattern, SourceRegistration? Source);
