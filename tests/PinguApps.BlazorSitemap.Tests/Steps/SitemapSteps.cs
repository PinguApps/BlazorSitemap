using System.Globalization;
using System.Net;
using System.Text;
using System.Xml.Linq;
using Microsoft.Extensions.DependencyInjection;
using PinguApps.BlazorSitemap.Tests.Support;
using Reqnroll;
using Xunit;

namespace PinguApps.BlazorSitemap.Tests.Steps;

[Binding]
public sealed class SitemapSteps(ScenarioState state)
{
    private static readonly string[] EnglishWelshDefault = ["cy-GB", "en-GB", "x-default"];
    [Given("an included page with route {string}")]
    public void Included(string route) => state.Pages.Add(new PageSpec(route));

    [Given("an excluded page with route {string}")]
    public void Excluded(string route) => state.Pages.Add(new PageSpec(route) { Included = false });

    [Given("the page also has alias {string}")]
    public void Alias(string route) => state.Pages[^1].Routes = [.. state.Pages[^1].Routes, route];

    [Given("the canonical route index is {int}")]
    public void Canonical(int index) => state.Pages[^1].Canonical = index;

    [Given("the page requires authorization")]
    public void Authorized() => state.Pages[^1].Authorized = true;

    [Given("the page enables hreflang")]
    public void Hreflang() => state.Pages[^1].Hreflang = true;

    [Given("the page uses a runtime provider")]
    public void Dynamic() => state.Pages[^1].Dynamic = true;

    [Given("the page has dynamic content")]
    public void DynamicContent() => state.Pages[^1].DynamicContent = true;

    [Given("finite values for {string} are {string}")]
    public void Finite(string parameter, string list)
        => state.Configure.Add(options => options.RouteValues(parameter, list.Split(',').Select(x => x == "<null>" ? null : x)));

    [Given("{int} finite values for {string}")]
    public void ManyFinite(int count, string parameter)
        => state.Configure.Add(options => options.RouteValues(parameter, Enumerable.Range(0, count).Select(x => "value-" + x.ToString(CultureInfo.InvariantCulture))));

    [Given("English and Welsh locales with English x-default")]
    public void Locales() => state.Configure.Add(options => options.Locales([new("en-gb", "en-GB"), new("cy-gb", "cy-GB")], xDefaultLocale: "en-gb"));

    [Given("locales with duplicate language codes")]
    public void DuplicateLanguages() => state.Configure.Add(options => options.Locales([new("a", "en-GB"), new("b", "en-GB")]));

    [Given("an unregistered x-default locale")]
    public void BadDefault() => state.Configure.Add(options => options.Locales([new("en-gb", "en-GB")], xDefaultLocale: "cy-gb"));

    [Given("public base URL {string}")]
    public void BaseUrl(string url) => state.BaseUrl = url;

    [Given("the public base URL has a path of {int} characters")]
    public void LongBaseUrl(int length) => state.BaseUrl = "https://example.com/" + new string('a', length);

    [Given("the application path base is {string}")]
    public void PathBase(string path) => state.PathBase = path;

    [Given("lastmod is required")]
    public void Required() => state.RequireFreshness = true;

    [Given("trustworthy freshness for {string} is {string}")]
    public void Freshness(string path, string value) => state.Content.Freshness[path] = ParseFreshness(value)!;

    [Given("the cache lifetime is {int} seconds")]
    public void Lifetime(int seconds) => state.LifetimeSeconds = seconds;

    [Given("the URL limit is {int}")]
    public void UrlLimit(int count) => state.UrlLimit = count;

    [Given("the byte limit is {int}")]
    public void ByteLimit(int count) => state.ByteLimit = count;

    [Given("published route instances:")]
    public void Rows(Table table)
    {
        foreach (DataTableRow row in table.Rows)
        {
            Dictionary<string, string?> values = row.Where(x => x.Key is not ("group" or "lastmod" or "published"))
                .ToDictionary(x => x.Key, x => x.Value == "<null>" ? null : x.Value, StringComparer.OrdinalIgnoreCase);
            if (!row.TryGetValue("published", out string? published) || published == "true")
            {
                state.Content.Rows.Add(new SitemapEntry(values)
                {
                    AlternateGroup = row.TryGetValue("group", out string? group) ? group : null,
                    LastModified = ParseFreshness(row.TryGetValue("lastmod", out string? lastmod) ? lastmod : null)
                });
            }
        }
    }

    [Given("{int} published instances with parameter {string} and value length {int}")]
    public void Catalogue(int count, string parameter, int length)
    {
        for (int i = 0; i < count; i++)
        {
            string value = i.ToString("D8", CultureInfo.InvariantCulture).PadRight(length, 'a');
            state.Content.Rows.Add(new SitemapEntry(new Dictionary<string, string?> { [parameter] = value }) { LastModified = new SitemapLastModified(new DateOnly(2026, 9, 1)) });
        }
    }

    [Given("the packed Blazor consumer")]
    public void Consumer() => state.IsConsumer = true;

    [Given("source estimates are disabled for the consumer")]
    public void DisableConsumerEstimates() => state.UseConsumerSourceEstimates = false;

    [When("the sitemap is requested")]
    public async Task Request()
    {
        if (state.Client is null)
        {
            await state.StartAsync();
        }

        await state.FetchAsync();
    }

    [Given("a second freshness provider is registered for the same page")]
    public void DuplicateFreshness() => state.DuplicateFreshness = true;

    [Then("every consumer URL has lastmod")]
    public void ConsumerFreshness() => Assert.All(state.Document.Root!.Elements(ScenarioState.SitemapNamespace + "url"),
        entry => Assert.NotNull(entry.Element(ScenarioState.SitemapNamespace + "lastmod")));

    [Then("lastmod for {string} matches source state {string}")]
    public async Task SourceState(string url, string file)
    {
        using System.Text.Json.JsonDocument json = System.Text.Json.JsonDocument.Parse(await File.ReadAllTextAsync(Path.Combine(ScenarioState.RepositoryRoot(), file)));
        Assert.Equal(json.RootElement.GetProperty("lastChangedUtc").GetString(), Entry(url).Element(ScenarioState.SitemapNamespace + "lastmod")?.Value);
    }

    [Given("locale parameter {string} uses English and Welsh")]
    public void NamedLocales(string parameter) => state.Configure.Add(options => options.Locales([new("en-gb", "en-GB"), new("cy-gb", "cy-GB")], parameter: parameter));

    [Then("alternates for each item stay within that item's group")]
    public void ItemGroups()
    {
        foreach (XElement entry in state.Document.Root!.Elements(ScenarioState.SitemapNamespace + "url"))
        {
            string item = new Uri(entry.Element(ScenarioState.SitemapNamespace + "loc")!.Value).Segments[1];
            XElement[] links = entry.Elements(ScenarioState.XhtmlNamespace + "link").ToArray();
            Assert.Equal(2, links.Length);
            Assert.All(links, link => Assert.Equal(item, new Uri(link.Attribute("href")!.Value).Segments[1]));
        }
    }

    [When("the sitemap cache is invalidated")]
    public void Invalidate() => state.App!.Services.GetRequiredService<SitemapCache>().Invalidate();

    [When("{int} seconds elapse")]
    public void Elapse(int seconds) => state.Clock.Advance(TimeSpan.FromSeconds(seconds));

    [When("all published instances are removed")]
    public void Remove() => state.Content.Rows.Clear();

    [When("published instance {int} is deleted")]
    public void Delete(int index) => state.Content.Rows.RemoveAt(index);

    [When("published instance {int} is updated to {string}")]
    public void Edit(int index, string date) => state.Content.Rows[index] = state.Content.Rows[index] with { LastModified = ParseFreshness(date) };

    [When("the collection publication time for {string} changes to {string}")]
    public void Collection(string path, string timestamp) => Freshness(path, timestamp);

    [When("the provider invalidates the cache during its next read")]
    public void ConcurrentInvalidation()
    {
        state.Content.InvalidateDuringRead = true;
        Invalidate();
    }

    [When("the provider fails its next read")]
    public void FailRead()
    {
        state.Content.FailDuringRead = true;
        Invalidate();
    }

    [When("the provider recovers")]
    public void Recover()
    {
        state.Content.FailDuringRead = false;
        state.Error = null;
    }

    [When("provider order is reversed")]
    public void Reverse() => state.Content.Rows.Reverse();

    [When("the sitemap is regenerated with a byte limit {int} bytes below its actual size")]
    public async Task ExactLimit(int below)
    {
        state.ByteLimit = state.Xml.Length - below;
        await state.StopAsync();
        await state.StartAsync();
        await state.FetchAsync();
    }

    [When("the incoming Host header is {string}")]
    public void Host(string host) => state.Client!.DefaultRequestHeaders.Host = host;

    [When("all child sitemaps are fetched")]
    public async Task FetchChildren()
    {
        Assert.Null(state.Error);
        state.Children.Clear();
        foreach (XElement location in state.Document.Descendants(ScenarioState.SitemapNamespace + "loc"))
        {
            Uri url = new(location.Value);
            using HttpResponseMessage response = await state.Client!.GetAsync(url.PathAndQuery);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            state.Children.Add(location.Value, await response.Content.ReadAsByteArrayAsync());
        }
    }

    [When("the demo publication operation is {string}")]
    public async Task DemoOperation(string operation)
    {
        using HttpResponseMessage response = await state.Client!.PostAsync(state.PathBase + "/demo/" + operation, null);
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
    }

    [Then("generation fails with diagnostic containing {string}")]
    public void Failure(string text)
    {
        Assert.NotNull(state.Error);
        Assert.Contains(text, state.Error!.ToString(), StringComparison.OrdinalIgnoreCase);
    }

    [Then("the sitemap contains exactly these canonical paths:")]
    public void Paths(Table table)
    {
        Assert.Null(state.Error);
        string[] expected = table.Rows.Select(x => x["path"]).Order(StringComparer.Ordinal).ToArray();
        string[] actual = state.Document.Descendants(ScenarioState.SitemapNamespace + "loc").Select(x => new Uri(x.Value).AbsolutePath).Order(StringComparer.Ordinal).ToArray();
        Assert.Equal(expected, actual);
    }

    [Then("the sitemap contains {int} canonical URLs")]
    public void Count(int count)
    {
        Assert.Null(state.Error);
        Assert.Equal(count, state.Document.Root!.Elements(ScenarioState.SitemapNamespace + "url").Count());
    }

    [Then("the sitemap is valid UTF-8 XML with the protocol namespace and content type")]
    public void XmlContract()
    {
        Assert.Null(state.Error);
        Assert.Equal(HttpStatusCode.OK, state.Response!.StatusCode);
        Assert.Equal("application/xml", state.Response.Content.Headers.ContentType!.MediaType);
        Assert.Equal("utf-8", state.Response.Content.Headers.ContentType!.CharSet);
        Assert.Equal(ScenarioState.SitemapNamespace, state.Document.Root!.Name.Namespace);
        Assert.Empty(state.Document.Descendants(ScenarioState.SitemapNamespace + "priority"));
        Assert.Empty(state.Document.Descendants(ScenarioState.SitemapNamespace + "changefreq"));
        Assert.False(state.Xml.AsSpan().StartsWith(Encoding.UTF8.Preamble));
        Assert.StartsWith("<?xml version=\"1.0\" encoding=\"UTF-8\"?>\n", Encoding.UTF8.GetString(state.Xml), StringComparison.Ordinal);
    }

    [Then("all canonical URLs start with {string}")]
    public void Origins(string prefix)
        => Assert.All(state.Document.Descendants(ScenarioState.SitemapNamespace + "loc"), x => Assert.StartsWith(prefix, x.Value, StringComparison.Ordinal));

    [Then("no lastmod is fabricated")]
    public void NoLastmod() => Assert.Empty(state.Document.Descendants(ScenarioState.SitemapNamespace + "lastmod"));

    [Then("lastmod for {string} is {string}")]
    public void Lastmod(string path, string expected)
        => Assert.Equal(ParseFreshness(expected)!.Value, Entry(path).Element(ScenarioState.SitemapNamespace + "lastmod")?.Value);

    [Then("lastmod for {string} is absent")]
    public void MissingLastmod(string path) => Assert.Null(Entry(path).Element(ScenarioState.SitemapNamespace + "lastmod"));

    [Then("lastmod for {string} differs from {string}")]
    public void ChangedLastmod(string path, string previous)
        => Assert.NotEqual(ParseFreshness(previous)!.Value, Entry(path).Element(ScenarioState.SitemapNamespace + "lastmod")?.Value);

    [Then("each localized entry has reciprocal English Welsh and x-default links")]
    public void Alternates()
    {
        foreach (XElement entry in state.Document.Root!.Elements(ScenarioState.SitemapNamespace + "url"))
        {
            XElement[] links = entry.Elements(ScenarioState.XhtmlNamespace + "link").ToArray();
            Assert.Equal(3, links.Length);
            Assert.Equal(EnglishWelshDefault, links.Select(x => x.Attribute("hreflang")!.Value).Order(StringComparer.Ordinal));
            Assert.All(links, x => Assert.Equal("alternate", x.Attribute("rel")!.Value));
            Assert.Contains(links, x => x.Attribute("href")!.Value == entry.Element(ScenarioState.SitemapNamespace + "loc")!.Value);
            Assert.Equal(links.Single(x => x.Attribute("hreflang")!.Value == "en-GB").Attribute("href")!.Value,
                links.Single(x => x.Attribute("hreflang")!.Value == "x-default").Attribute("href")!.Value);
            string[] all = links.Select(x => x.ToString()).ToArray();
            Assert.All(state.Document.Root.Elements(ScenarioState.SitemapNamespace + "url"), sibling =>
                Assert.Equal(all, sibling.Elements(ScenarioState.XhtmlNamespace + "link").Select(x => x.ToString())));
        }
    }

    [Then("entry {string} has {int} alternate links")]
    public void AlternateCount(string path, int count) => Assert.Equal(count, Entry(path).Elements(ScenarioState.XhtmlNamespace + "link").Count());

    [Then("the XML contains escaped {string}")]
    public void Escaped(string value) => Assert.Contains(value, Encoding.UTF8.GetString(state.Xml), StringComparison.Ordinal);

    [Then("the sitemap XML is unchanged")]
    public void Unchanged() => Assert.Equal(state.PreviousXml, state.Xml);

    [Then("the sitemap XML has changed")]
    public void Changed() => Assert.NotEqual(state.PreviousXml, state.Xml);

    [Then("the provider has been read {int} times")]
    public void Reads(int count) => Assert.Equal(count, state.Content.Reads);

    [Then("a sitemap index is returned without lastmod")]
    public void Index()
    {
        Assert.Null(state.Error);
        Assert.StartsWith("<?xml version=\"1.0\" encoding=\"UTF-8\"?>\n", Encoding.UTF8.GetString(state.Xml), StringComparison.Ordinal);
        Assert.Equal(ScenarioState.SitemapNamespace + "sitemapindex", state.Document.Root!.Name);
        Assert.Empty(state.Document.Descendants(ScenarioState.SitemapNamespace + "lastmod"));
        Assert.InRange(state.Document.Root.Elements().Count(), 1, 50_000);
        Assert.InRange(state.Xml.Length, 1, 52_428_800);
    }

    [Then("all child limits hold and contain exactly {int} unique canonical URLs")]
    public void ChildLimits(int total)
    {
        List<string> urls = [];
        foreach (byte[] xml in state.Children.Values)
        {
            Assert.StartsWith("<?xml version=\"1.0\" encoding=\"UTF-8\"?>\n", Encoding.UTF8.GetString(xml), StringComparison.Ordinal);
            XDocument child = XDocument.Parse(Encoding.UTF8.GetString(xml));
            Assert.Equal(ScenarioState.SitemapNamespace + "urlset", child.Root!.Name);
            XElement[] entries = child.Root.Elements(ScenarioState.SitemapNamespace + "url").ToArray();
            Assert.InRange(entries.Length, 1, state.UrlLimit);
            Assert.InRange(xml.Length, 1, state.ByteLimit);
            string[] locations = entries.Select(x => x.Element(ScenarioState.SitemapNamespace + "loc")!.Value).ToArray();
            Assert.Equal(locations.Order(StringComparer.Ordinal), locations);
            urls.AddRange(locations);
        }

        Assert.Equal(total, urls.Count);
        Assert.Equal(total, urls.Distinct(StringComparer.Ordinal).Count());
    }

    [Then("the previously indexed unaffected children retain their URLs and bytes")]
    public async Task StableChildren()
    {
        Dictionary<string, byte[]> before = new(state.Children, StringComparer.Ordinal);
        await FetchChildren();
        int unchanged = before.Count(x => state.Children.TryGetValue(x.Key, out byte[]? xml) && x.Value.SequenceEqual(xml));
        Assert.True(unchanged >= before.Count - 2, $"Only {unchanged} of {before.Count} buckets remained unchanged.");
    }

    [Then("an unknown child endpoint returns not found")]
    public async Task UnknownChild()
    {
        using HttpResponseMessage response = await state.Client!.GetAsync(state.PathBase + "/sitemap-unknown.xml");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Then("every advertised page renders successfully")]
    public async Task AdvertisedPagesRender()
    {
        Assert.Null(state.Error);
        foreach (XElement entry in state.Document.Root!.Elements(ScenarioState.SitemapNamespace + "url"))
        {
            string url = entry.Element(ScenarioState.SitemapNamespace + "loc")!.Value;
            using HttpResponseMessage response = await state.Client!.GetAsync(new Uri(url).AbsolutePath);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            string html = await response.Content.ReadAsStringAsync();
            Assert.Contains("<h1>", html, StringComparison.Ordinal);
        }
    }

    [Then("the alias still renders the About page")]
    public async Task AliasRenders()
    {
        string html = await state.Client!.GetStringAsync(state.PathBase + "/old-about");
        Assert.Contains("<h1>About this package</h1>", html, StringComparison.Ordinal);
    }

    [Then("the excluded page renders noindex")]
    public async Task Noindex()
    {
        string html = await state.Client!.GetStringAsync(state.PathBase + "/private");
        Assert.Contains("name=\"robots\" content=\"noindex\"", html, StringComparison.Ordinal);
    }

    [AfterScenario]
    public Task Cleanup() => state.StopAsync();

    private XElement Entry(string path) => state.Document.Root!.Elements(ScenarioState.SitemapNamespace + "url")
        .Single(x => new Uri(x.Element(ScenarioState.SitemapNamespace + "loc")!.Value).AbsolutePath == path);

    private static SitemapLastModified? ParseFreshness(string? value)
    {
        if (string.IsNullOrWhiteSpace(value) || value == "<none>")
        {
            return null;
        }

        return value.Length == 10 ? new SitemapLastModified(DateOnly.Parse(value, CultureInfo.InvariantCulture))
            : new SitemapLastModified(DateTimeOffset.Parse(value, CultureInfo.InvariantCulture));
    }
}
