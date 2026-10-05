using System.Reflection;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Routing.Patterns;

namespace PinguApps.BlazorSitemap;

internal sealed class RouteCatalog
{
    private readonly SitemapOptions _options;
    private readonly IInlineConstraintResolver _constraints;
    internal Uri BaseUrl { get; }
    internal IReadOnlyList<PageRoute> Pages { get; }

    public RouteCatalog(SitemapOptions options, IEnumerable<SourceRegistration> sources, IInlineConstraintResolver constraints)
    {
        _options = options;
        _constraints = constraints;
        BaseUrl = options.Validate();
        Dictionary<Type, SourceRegistration> providers = [];
        foreach (SourceRegistration source in sources)
        {
            if (!providers.TryAdd(source.PageType, source))
            {
                throw new InvalidOperationException($"Multiple sitemap providers were registered for {source.PageType.FullName}.");
            }
        }

        List<PageRoute> pages = [];
        foreach (Type type in options.Assemblies.SelectMany(x => x.GetTypes()).Where(x => x.IsVisible).Distinct().OrderBy(x => x.FullName, StringComparer.Ordinal))
        {
            SitemapPageAttribute? attribute = type.GetCustomAttribute<SitemapPageAttribute>();
            if (attribute is null)
            {
                continue;
            }

            if (!typeof(IComponent).IsAssignableFrom(type) || type.IsAbstract || type.ContainsGenericParameters)
            {
                throw new InvalidOperationException($"{type.FullName}: SitemapPage requires a concrete Blazor component.");
            }

            if (type.GetCustomAttributes(inherit: true).OfType<IAuthorizeData>().Any())
            {
                throw new InvalidOperationException($"{type.FullName}: an authorized component cannot also be a SitemapPage. Remove its sitemap inclusion.");
            }

            string[] routes = type.GetCustomAttributes<RouteAttribute>().Select(x => x.Template)
                .Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray();
            int index = attribute.CanonicalRouteIndex;
            if (index == -1 && routes.Length == 1)
            {
                index = 0;
            }

            if (index < 0 || index >= routes.Length)
            {
                throw new InvalidOperationException($"{type.Name}: choose CanonicalRouteIndex from its ordinally sorted @page templates when there are multiple routes; aliases are excluded.");
            }

            string template = routes[index];

            if (!template.StartsWith('/') || template.Contains('?') && !template.Contains("?}", StringComparison.Ordinal) || template.Contains('#') || template.Contains('\\'))
            {
                throw new InvalidOperationException($"{type.FullName}: invalid Blazor canonical route '{template}'.");
            }

            RoutePattern pattern = RoutePatternFactory.Parse(template);
            foreach (RoutePatternPathSegment segment in pattern.PathSegments)
            {
                if (segment.Parts.Count != 1 || segment.Parts[0] is RoutePatternSeparatorPart)
                {
                    throw new InvalidOperationException($"{type.FullName}: complex route segments are unsupported; use literal segments or whole-segment parameters.");
                }
            }

            providers.TryGetValue(type, out SourceRegistration? source);
            foreach (RoutePatternParameterPart parameter in pattern.Parameters)
            {
                if (parameter.Default is not null)
                {
                    throw new InvalidOperationException($"{type.FullName}: default route values are unsupported. Supply an explicit finite value or provider value.");
                }

                foreach (RoutePatternParameterPolicyReference policy in parameter.ParameterPolicies)
                {
                    _ = Resolve(policy.Content!);
                }

                if (source is null && !options.FiniteValues.ContainsKey(parameter.Name))
                {
                    throw new InvalidOperationException($"{type.FullName}: unresolved parameter '{parameter.Name}'. Register RouteValues/Locales or AddSitemapEntries for this page. Optional omission also requires an explicit null choice.");
                }
            }

            if (attribute.Hreflang && (options.LocaleValues.Count == 0 || !pattern.Parameters.Any(x => x.Name.Equals(options.LocaleParameter, StringComparison.OrdinalIgnoreCase))))
            {
                throw new InvalidOperationException($"{type.FullName}: Hreflang requires Locales and the configured locale parameter in the canonical route.");
            }

            pages.Add(new PageRoute(type, attribute, pattern, source));
        }

        foreach (Type page in providers.Keys)
        {
            if (!pages.Any(x => x.PageType == page))
            {
                throw new InvalidOperationException($"{page.FullName}: provider targets a page without SitemapPage inclusion in a registered assembly.");
            }
        }

        Pages = pages;
    }

    internal IEnumerable<Dictionary<string, string?>> Expand(PageRoute page, SitemapEntry entry)
    {
        Dictionary<string, string?> values = new(entry.Values, StringComparer.OrdinalIgnoreCase);
        foreach (string name in values.Keys)
        {
            if (!page.Pattern.Parameters.Any(x => x.Name.Equals(name, StringComparison.OrdinalIgnoreCase)))
            {
                throw new InvalidOperationException($"{page.PageType.Name}: provider supplied unknown route parameter '{name}'.");
            }
        }

        return ExpandParameter(0, values);

        IEnumerable<Dictionary<string, string?>> ExpandParameter(int index, Dictionary<string, string?> current)
        {
            if (index == page.Pattern.Parameters.Count)
            {
                yield return current;
                yield break;
            }

            RoutePatternParameterPart parameter = page.Pattern.Parameters[index];
            if (current.TryGetValue(parameter.Name, out string? value))
            {
                if (_options.FiniteValues.TryGetValue(parameter.Name, out string?[]? permitted) && !permitted.Contains(value, StringComparer.Ordinal))
                {
                    throw new InvalidOperationException($"{page.PageType.Name}: '{value}' is not a registered value of '{parameter.Name}'.");
                }

                foreach (Dictionary<string, string?> result in ExpandParameter(index + 1, current))
                {
                    yield return result;
                }
            }
            else if (_options.FiniteValues.TryGetValue(parameter.Name, out string?[]? finite))
            {
                foreach (string? item in finite)
                {
                    Dictionary<string, string?> expanded = new(current, StringComparer.OrdinalIgnoreCase) { [parameter.Name] = item };
                    foreach (Dictionary<string, string?> result in ExpandParameter(index + 1, expanded))
                    {
                        yield return result;
                    }
                }
            }
            else
            {
                throw new InvalidOperationException($"{page.PageType.Name}: missing parameter '{parameter.Name}' in a provider entry. Use explicit null for optional omission.");
            }
        }
    }

    internal Uri Bind(PageRoute page, Dictionary<string, string?> values)
    {
        List<string> segments = [];
        bool omitted = false;
        foreach (RoutePatternPathSegment segment in page.Pattern.PathSegments)
        {
            if (segment.Parts[0] is RoutePatternLiteralPart literal)
            {
                segments.Add(EncodeSegment(literal.Content));
                continue;
            }

            RoutePatternParameterPart parameter = (RoutePatternParameterPart)segment.Parts[0];
            string? value = values[parameter.Name];
            if (value is null)
            {
                if (!parameter.IsOptional)
                {
                    throw new InvalidOperationException($"{page.PageType.Name}: missing required parameter '{parameter.Name}'.");
                }

                omitted = true;
                continue;
            }

            if (omitted)
            {
                throw new InvalidOperationException($"{page.PageType.Name}: a populated parameter cannot follow an omitted optional segment.");
            }

            RouteValueDictionary routeValues = new(values.Select(x => new KeyValuePair<string, object?>(x.Key, x.Value)));
            foreach (RoutePatternParameterPolicyReference policy in parameter.ParameterPolicies)
            {
                if (!Resolve(policy.Content!).Match(null, null, parameter.Name, routeValues, RouteDirection.UrlGeneration))
                {
                    throw new InvalidOperationException($"{page.PageType.Name}: '{value}' violates constraint '{policy.Content}' on '{parameter.Name}'.");
                }
            }

            segments.Add(parameter.IsCatchAll ? string.Join('/', value.Split('/').Select(EncodeSegment)) : EncodeSegment(value));
        }

        Uri url = new(BaseUrl, string.Join('/', segments));
        if (url.AbsoluteUri.Length >= 2048 || !url.AbsoluteUri.StartsWith(BaseUrl.AbsoluteUri, StringComparison.Ordinal))
        {
            throw new InvalidOperationException($"{page.PageType.Name}: generated canonical URL must remain under PublicBaseUrl and be shorter than 2048 characters.");
        }

        return url;
    }

    private IRouteConstraint Resolve(string policy)
    {
        // Match the built-in constraints documented for Blazor, rather than accepting MVC-only policies.
        if (policy is not ("bool" or "datetime" or "decimal" or "double" or "float" or "guid" or "int" or "long" or "nonfile"))
        {
            throw new InvalidOperationException($"Unsupported Blazor sitemap constraint '{policy}'. Use a documented built-in Blazor constraint.");
        }

        return _constraints.ResolveConstraint(policy) ?? throw new InvalidOperationException($"Cannot resolve route constraint '{policy}'.");
    }

    private static string EncodeSegment(string segment)
    {
        if (string.IsNullOrWhiteSpace(segment) || segment is "." or ".." || segment.Contains('/') || segment.Contains('\\') || segment.Any(char.IsControl))
        {
            throw new InvalidOperationException("A canonical route segment must be nonempty and cannot contain slashes, dot traversal or control characters. Use a catch-all for multiple segments.");
        }

        return Uri.EscapeDataString(segment);
    }
}
