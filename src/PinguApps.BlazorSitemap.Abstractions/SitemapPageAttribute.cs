using System.Runtime.CompilerServices;

namespace PinguApps.BlazorSitemap;

/// <summary>Opts a public, indexable Blazor page into the sitemap.</summary>
[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public sealed class SitemapPageAttribute : Attribute
{
    /// <summary>Creates page metadata. The compiler supplies the Razor source identity automatically.</summary>
    public SitemapPageAttribute([CallerFilePath] string sourceFile = "") => SourceFile = sourceFile;

    /// <summary>The compiler's source identity, used to find embedded source-change timestamps.</summary>
    public string SourceFile { get; }

    /// <summary>Selects a zero-based index from the distinct @page templates sorted ordinally. Required for multiple routes.</summary>
    public int CanonicalRouteIndex { get; set; } = -1;

    /// <summary>Annotates equivalent translations using the configured locale parameter.</summary>
    public bool Hreflang { get; set; }

    /// <summary>Requires supplied freshness for a runtime-dependent page such as a collection index.</summary>
    public bool DynamicContent { get; set; }
}
