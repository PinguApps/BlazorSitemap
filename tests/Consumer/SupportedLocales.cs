using PinguApps.BlazorSitemap;

namespace Consumer;

public static class SupportedLocales
{
    public static IReadOnlyList<SitemapLocale> All { get; } = [new("en-gb", "en-GB"), new("cy-gb", "cy-GB")];
}
