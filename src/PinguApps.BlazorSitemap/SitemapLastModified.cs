using System.Globalization;

namespace PinguApps.BlazorSitemap;

/// <summary>A supplied date or timestamp; static source tracking also represents its documented estimate with this type.</summary>
public sealed record SitemapLastModified
{
    /// <summary>Initializes a date-only last modification value.</summary>
    public SitemapLastModified(DateOnly date) => Value = date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    /// <summary>Initializes a timestamp, preserving its offset and precision.</summary>
    public SitemapLastModified(DateTimeOffset timestamp) => Value = timestamp.ToString("o", CultureInfo.InvariantCulture);

    /// <summary>Gets the protocol-formatted date or timestamp.</summary>
    public string Value { get; }
}
