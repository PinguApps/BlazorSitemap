using System.Globalization;
using System.Reflection;

namespace PinguApps.BlazorSitemap;

internal static class SourceFreshness
{
    private const string Prefix = "BlazorSitemap.Source/";
    internal static SitemapLastModified? Read(Type page, SitemapPageAttribute attribute)
    {
        string source = attribute.SourceFile.Replace('\\', '/');
        AssemblyMetadataAttribute? stamp = page.Assembly.GetCustomAttributes<AssemblyMetadataAttribute>()
            .Where(x => x.Key.StartsWith(Prefix, StringComparison.Ordinal))
            .Where(x => source.EndsWith('/' + x.Key[Prefix.Length..], StringComparison.Ordinal) || source == x.Key[Prefix.Length..])
            .OrderByDescending(x => x.Key.Length).FirstOrDefault();
        return stamp?.Value is { } timestamp
            ? new SitemapLastModified(DateTimeOffset.ParseExact(timestamp, "O", CultureInfo.InvariantCulture)) : null;
    }
}
