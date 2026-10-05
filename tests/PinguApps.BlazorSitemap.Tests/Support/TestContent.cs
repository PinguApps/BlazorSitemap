namespace PinguApps.BlazorSitemap.Tests.Support;

public sealed class TestContent
{
    public List<SitemapEntry> Rows { get; } = [];
    public List<SitemapUrlEntry> UrlRows { get; } = [];
    public List<SitemapUrlEntry> OtherUrlRows { get; } = [];
    public int UrlReads { get; set; }
    public Dictionary<string, SitemapLastModified> Freshness { get; } = new(StringComparer.Ordinal);
    public int Reads { get; set; }
    public bool InvalidateDuringRead { get; set; }
    public bool FailDuringRead { get; set; }
}
