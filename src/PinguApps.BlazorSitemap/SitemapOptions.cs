using System.Reflection;
using System.Text.RegularExpressions;

namespace PinguApps.BlazorSitemap;

/// <summary>Configures discovery, canonical origin, finite route values, freshness and caching.</summary>
public sealed partial class SitemapOptions
{
    internal HashSet<Assembly> Assemblies { get; } = [];
    internal Dictionary<string, string?[]> FiniteValues { get; } = new(StringComparer.OrdinalIgnoreCase);
    internal Dictionary<string, SitemapLocale> LocaleValues { get; } = new(StringComparer.Ordinal);
    internal string LocaleParameter { get; private set; } = "locale";
    internal string? XDefaultLocale { get; private set; }

    /// <summary>Gets or sets the public HTTP(S) base URL, including any application path base. Never taken from request headers.</summary>
    public string PublicBaseUrl { get; set; } = "";

    /// <summary>Gets or sets whether static pages use embedded source-change estimates. Default: true. Supplied dates take precedence.</summary>
    public bool UseSourceLastModified { get; set; } = true;

    /// <summary>Gets or sets the snapshot lifetime. Zero regenerates on each request. Default: five minutes.</summary>
    public TimeSpan CacheLifetime { get; set; } = TimeSpan.FromMinutes(5);

    /// <summary>Gets or sets whether generation fails if any canonical URL lacks a date. Default: true. Dynamic dates are always required.</summary>
    public bool RequireLastModified { get; set; } = true;

    /// <summary>Gets or sets the maximum URLs per document; may lower but cannot exceed the protocol's 50,000 limit.</summary>
    public int MaxUrlsPerSitemap { get; set; } = 50_000;

    /// <summary>Gets or sets the maximum uncompressed UTF-8 bytes per document; cannot exceed 52,428,800.</summary>
    public int MaxBytesPerSitemap { get; set; } = 52_428_800;

    /// <summary>Includes a referenced component assembly. Also register it with Blazor's router/endpoints.</summary>
    public void AddAssembly(Assembly assembly)
    {
        ArgumentNullException.ThrowIfNull(assembly);
        Assemblies.Add(assembly);
    }

    /// <summary>Registers finite decoded values. Use null explicitly to include an optional parameter's omitted form.</summary>
    public void RouteValues(string parameter, IEnumerable<string?> values)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(parameter);
        ArgumentNullException.ThrowIfNull(values);
        string?[] items = values.ToArray();
        if (items.Length == 0 || items.Any(x => x is not null && string.IsNullOrWhiteSpace(x)) ||
            items.Distinct(StringComparer.OrdinalIgnoreCase).Count() != items.Length)
        {
            throw new ArgumentException($"Finite parameter '{parameter}' needs nonempty, unique values (null is allowed for optional omission).", nameof(values));
        }

        if (!FiniteValues.TryAdd(parameter, items))
        {
            throw new ArgumentException($"Finite parameter '{parameter}' was registered twice.", nameof(parameter));
        }
    }

    /// <summary>Reuses the app's supported locales, registering finite values and hreflang mapping together.</summary>
    /// <param name="locales">Supported URL segments and language codes.</param>
    /// <param name="parameter">The route parameter name.</param>
    /// <param name="xDefaultLocale">An explicitly chosen fallback locale. Linked only in groups where that variant exists.</param>
    public void Locales(IEnumerable<SitemapLocale> locales, string parameter = "locale", string? xDefaultLocale = null)
    {
        ArgumentNullException.ThrowIfNull(locales);
        SitemapLocale[] items = locales.ToArray();
        if (LocaleValues.Count != 0 || items.Any(x => !LanguagePattern().IsMatch(x.Language)) ||
            items.Select(x => x.Language).Distinct(StringComparer.OrdinalIgnoreCase).Count() != items.Length)
        {
            throw new ArgumentException("Locales need unique language or language-region codes and may be registered only once.", nameof(locales));
        }

        RouteValues(parameter, items.Select(x => x.RouteValue));
        foreach (SitemapLocale locale in items)
        {
            LocaleValues.Add(locale.RouteValue, locale);
        }

        if (xDefaultLocale is not null && !LocaleValues.ContainsKey(xDefaultLocale))
        {
            throw new ArgumentException("x-default must select a registered locale.", nameof(xDefaultLocale));
        }

        LocaleParameter = parameter;
        XDefaultLocale = xDefaultLocale;
    }

    internal Uri Validate()
    {
        if (!Uri.TryCreate(PublicBaseUrl, UriKind.Absolute, out Uri? url) ||
            (url.Scheme != Uri.UriSchemeHttps && url.Scheme != Uri.UriSchemeHttp) ||
            url.Host.Length == 0 || url.UserInfo.Length != 0 || url.Query.Length != 0 || url.Fragment.Length != 0 ||
            PublicBaseUrl.Contains('\\', StringComparison.Ordinal) || PublicBaseUrl.Any(char.IsWhiteSpace))
        {
            throw new InvalidOperationException("Sitemap PublicBaseUrl must be an absolute HTTP(S) URL without credentials, query, fragment or whitespace.");
        }

        if (CacheLifetime < TimeSpan.Zero || MaxUrlsPerSitemap is < 1 or > 50_000 || MaxBytesPerSitemap is < 256 or > 52_428_800)
        {
            throw new InvalidOperationException("Sitemap limits must be 1..50,000 URLs and 256..52,428,800 bytes; cache lifetime cannot be negative.");
        }

        return new Uri(url.AbsoluteUri.TrimEnd('/') + "/", UriKind.Absolute);
    }

    [GeneratedRegex("^[a-zA-Z]{2,3}(-([a-zA-Z]{2}|[0-9]{3}))?$", RegexOptions.CultureInvariant)]
    private static partial Regex LanguagePattern();
}
