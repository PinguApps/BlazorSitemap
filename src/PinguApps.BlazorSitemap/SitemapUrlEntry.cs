namespace PinguApps.BlazorSitemap;

/// <summary>A published URL served outside component routing, with application-supplied freshness.</summary>
/// <param name="Location">An application-relative path beginning with /, or an absolute HTTP(S) URL under PublicBaseUrl. Supply escaped URLs, not route templates.</param>
/// <param name="LastModified">The date or timestamp of a significant published change. Always required.</param>
public sealed record SitemapUrlEntry(string Location, SitemapLastModified LastModified)
{
    internal Uri Resolve(Uri baseUrl)
    {
        if (string.IsNullOrWhiteSpace(Location) || Location.Any(char.IsWhiteSpace) || Location.Any(char.IsControl) ||
            Location.Contains('\\') || Location.Contains('#') || Location.IndexOfAny(['{', '}', '<', '>', '"', '`', '|', '^']) >= 0 ||
            Location.StartsWith("//", StringComparison.Ordinal))
        {
            throw new InvalidOperationException($"Invalid standalone sitemap URL '{Location}'. Use an escaped application-relative path or an absolute HTTP(S) URL without credentials or fragment.");
        }

        for (int i = 0; i < Location.Length; i++)
        {
            if (Location[i] == '%' && (i + 2 >= Location.Length || !Uri.IsHexDigit(Location[i + 1]) || !Uri.IsHexDigit(Location[i + 2])))
            {
                throw new InvalidOperationException($"Invalid percent encoding in standalone sitemap URL '{Location}'.");
            }
        }

        string candidate = Location.StartsWith('/') ? baseUrl.AbsoluteUri + Location[1..] : Location;
        if (!Uri.TryCreate(candidate, UriKind.Absolute, out Uri? url) ||
            (url.Scheme != Uri.UriSchemeHttp && url.Scheme != Uri.UriSchemeHttps) || url.UserInfo.Length != 0 ||
            url.Fragment.Length != 0 || !url.AbsoluteUri.StartsWith(baseUrl.AbsoluteUri, StringComparison.Ordinal) || url.AbsoluteUri.Length >= 2048)
        {
            throw new InvalidOperationException($"Standalone sitemap URL '{Location}' must be a well-formed escaped URL under PublicBaseUrl and be shorter than 2048 characters.");
        }

        if (LastModified is null)
        {
            throw new InvalidOperationException($"Missing supplied lastmod for standalone sitemap URL '{Location}'. Source estimates and RequireLastModified do not apply to standalone URLs.");
        }

        return url;
    }
}
