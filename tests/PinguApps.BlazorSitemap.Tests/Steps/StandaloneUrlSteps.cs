using System.Globalization;
using Microsoft.Extensions.DependencyInjection;
using PinguApps.BlazorSitemap.Tests.Support;
using Reqnroll;
using Xunit;

namespace PinguApps.BlazorSitemap.Tests.Steps;

[Binding]
public sealed class StandaloneUrlSteps(ScenarioState state)
{
    private readonly List<string> _registrationErrors = [];
    [Given("fixed standalone URL {string} with lastmod {string}")]
    public void Fixed(string location, string date) => state.StaticUrls.Add(Entry(location, date));

    [Given("a standalone URL provider")]
    public void Provider() => state.UseUrlProvider = true;

    [Given("the standalone URL provider is registered again")]
    public void Repeat() => state.DuplicateUrlProvider = true;

    [Given("published standalone URL {string} with lastmod {string}")]
    public void Row(string location, string date) => state.Content.UrlRows.Add(Entry(location, date));

    [Given("another URL provider supplies {string} with lastmod {string}")]
    public void Other(string location, string date)
    {
        state.UseOtherUrlProvider = true;
        state.Content.OtherUrlRows.Add(Entry(location, date));
    }

    [When("the published standalone URLs become {string} dated {string}")]
    public void Replace(string location, string date)
    {
        state.Content.UrlRows.Clear();
        state.Content.UrlRows.Add(Entry(location, date));
    }

    [Then("the standalone provider was enumerated {int} times")]
    public void Reads(int count) => Assert.Equal(count, state.Content.UrlReads);

    [Then("standalone entries have no hreflang annotations")]
    public void NoAlternates() => Assert.Empty(state.Document.Descendants(ScenarioState.XhtmlNamespace + "link"));

    [When("standalone registrations are attempted before sitemap setup")]
    public void BadRegistrationOrder()
    {
        ServiceCollection services = new();
        _registrationErrors.Add(Assert.Throws<InvalidOperationException>(() => services.AddSitemapUrls(Entry("/api/a", "2026-09-01"))).Message);
        _registrationErrors.Add(Assert.Throws<InvalidOperationException>(() => services.AddSitemapUrls<MemoryUrlProvider>()).Message);
    }

    [Then("all standalone registration attempts fail clearly")]
    public void RegistrationChecked()
    {
        Assert.Equal(2, _registrationErrors.Count);
        Assert.All(_registrationErrors, error => Assert.Contains("Call AddBlazorSitemap", error, StringComparison.Ordinal));
    }

    [Given("a standalone URL with {int} characters")]
    public void TooLong(int count) => Fixed("/" + new string('a', count), "2026-09-01");

    [Given("the standalone provider fails after enumeration")]
    public void Fail() => state.Content.FailDuringRead = true;

    private static SitemapUrlEntry Entry(string location, string date) => new(location, date switch
    {
        "<none>" => null!,
        { Length: 10 } => new SitemapLastModified(DateOnly.Parse(date, CultureInfo.InvariantCulture)),
        _ => new SitemapLastModified(DateTimeOffset.Parse(date, CultureInfo.InvariantCulture))
    });
}
