namespace PinguApps.BlazorSitemap.Tests.Support;

public sealed class PageSpec(string route)
{
    public string[] Routes { get; set; } = [route];
    public int Canonical { get; set; } = -1;
    public bool Included { get; set; } = true;
    public bool Hreflang { get; set; }
    public bool Authorized { get; set; }
    public bool Dynamic { get; set; }
    public bool DynamicContent { get; set; }
    public Type? Type { get; set; }
}
