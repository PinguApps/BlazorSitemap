using System.Xml.Linq;
using PinguApps.BlazorSitemap.Tests.Support;
using Reqnroll;
using Xunit;

namespace PinguApps.BlazorSitemap.Tests.Steps;

[Binding]
public sealed class TrailingSlashSteps(ScenarioState state)
{
    [Given("trailing slash policy {string}")]
    public void Setting(string mode) => state.Configure.Add(options => options.AppendTrailingSlash = mode == "append");

    [Given("the consumer appends trailing slashes")]
    public void ConsumerSetting() => state.ConsumerAppendTrailingSlash = true;

    [Given("a standalone page URL of {int} characters")]
    public void UrlLength(int length) => state.StaticUrls.Add(new SitemapUrlEntry(
        "/" + new string('a', length - "https://example.com/".Length), new SitemapLastModified(new DateOnly(2026, 9, 1))));

    [Then("all page locations and language links use trailing slash policy {string}")]
    public void Policy(string mode)
    {
        Assert.Null(state.Error);
        bool append = mode == "append";
        XElement[] entries = (state.Children.Count == 0 ? [state.Document] : state.Children.Values.Select(x => XDocument.Parse(System.Text.Encoding.UTF8.GetString(x))))
            .SelectMany(x => x.Root!.Elements(ScenarioState.SitemapNamespace + "url")).ToArray();
        Assert.NotEmpty(entries);
        string[] locations = entries.Select(x => x.Element(ScenarioState.SitemapNamespace + "loc")!.Value).ToArray();
        Assert.Equal(locations.Length, locations.Distinct(StringComparer.Ordinal).Count());
        foreach (string location in locations.Concat(entries.SelectMany(x => x.Elements(ScenarioState.XhtmlNamespace + "link")).Select(x => x.Attribute("href")!.Value)))
        {
            string path = new Uri(location).AbsolutePath;
            Assert.Equal(append || path == "/", path.EndsWith('/'));
            Assert.False(path.EndsWith("//", StringComparison.Ordinal));
        }

        foreach (XElement entry in entries.Where(x => x.Elements(ScenarioState.XhtmlNamespace + "link").Any()))
        {
            string ownLocation = entry.Element(ScenarioState.SitemapNamespace + "loc")!.Value;
            XElement[] links = entry.Elements(ScenarioState.XhtmlNamespace + "link").ToArray();
            Assert.Contains(links, x => x.Attribute("href")!.Value == ownLocation);
            Assert.Equal(links.Single(x => x.Attribute("hreflang")!.Value == "en-GB").Attribute("href")!.Value,
                links.Single(x => x.Attribute("hreflang")!.Value == "x-default").Attribute("href")!.Value);
            foreach (XElement link in links)
            {
                XElement sibling = entries.Single(x => x.Element(ScenarioState.SitemapNamespace + "loc")!.Value == link.Attribute("href")!.Value);
                Assert.Equal(links.Select(x => x.ToString()), sibling.Elements(ScenarioState.XhtmlNamespace + "link").Select(x => x.ToString()));
            }
        }
    }

    [Then("the sitemap contains exactly these page locations:")]
    public void Locations(Table table)
    {
        Assert.Null(state.Error);
        Assert.Equal(table.Rows.Select(x => x["url"]).Order(StringComparer.Ordinal),
            state.Document.Descendants(ScenarioState.SitemapNamespace + "loc").Select(x => x.Value).Order(StringComparer.Ordinal));
    }

    [Then("child document addresses end in xml without a slash")]
    public void DocumentAddresses() => Assert.All(state.Children.Keys, location =>
    {
        Uri url = new(location);
        Assert.StartsWith(state.PathBase + "/sitemap-", url.AbsolutePath, StringComparison.Ordinal);
        Assert.EndsWith(".xml", url.AbsolutePath, StringComparison.Ordinal);
        Assert.Empty(url.Query);
    });
}
