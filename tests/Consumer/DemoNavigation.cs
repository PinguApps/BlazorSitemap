using System.Reflection;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Routing.Patterns;
using PinguApps.BlazorSitemap;

namespace Consumer;

// App navigation for the demo's literal and required-parameter routes.
// Keeping it here lets the example declare each page path only in its Razor file.
internal static class DemoNavigation
{
    internal static Uri To(Type page, string baseUrl, IReadOnlyDictionary<string, string?>? values = null)
    {
        string[] templates = page.GetCustomAttributes<RouteAttribute>().Select(x => x.Template)
            .Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray();
        int index = page.GetCustomAttribute<SitemapPageAttribute>()!.CanonicalRouteIndex;
        RoutePattern route = RoutePatternFactory.Parse(templates[index < 0 ? 0 : index]);
        string path = string.Join('/', route.PathSegments.Select(segment => segment.Parts[0] switch
        {
            RoutePatternLiteralPart literal => Uri.EscapeDataString(literal.Content),
            RoutePatternParameterPart parameter => Uri.EscapeDataString(values![parameter.Name]!),
            _ => throw new InvalidOperationException("Unexpected demo navigation segment.")
        }));
        return new Uri(new Uri(baseUrl.TrimEnd('/') + "/"), path);
    }
}
