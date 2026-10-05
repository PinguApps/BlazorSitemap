using System.Diagnostics;
using System.Globalization;
using System.Reflection;
using System.Text.Json;
using System.Xml.Linq;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using PinguApps.BlazorSitemap.Tests.Support;
using Reqnroll;
using Xunit;

namespace PinguApps.BlazorSitemap.Tests.Steps;

[Binding]
public sealed class SourceFreshnessSteps
{
    private string? _directory;
    private string _output = "";
    private int _exitCode;
    private string? _previousState;
    private string? _previousDate;
    private string? _sitemapDate;
    private DateTimeOffset _started;
    private DateTimeOffset _finished;
    private bool _suppliedDate;
    private string? _deployedState;
    private string PageFile => Path.Combine(_directory!, "Pages", "Counter.razor");
    private string StateFile => PageFile + ".sitemap.json";

    [Given("a fresh Razor project consuming the packed sitemap package")]
    public async Task Create()
    {
        string root = ScenarioState.RepositoryRoot();
        _directory = Path.Combine(Path.GetTempPath(), "BlazorSitemapSource-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.GetDirectoryName(PageFile)!);
        using JsonDocument assets = JsonDocument.Parse(await File.ReadAllTextAsync(Path.Combine(root, "tests", "Consumer", "obj", "project.assets.json")));
        string version = assets.RootElement.GetProperty("libraries").EnumerateObject().Single(x => x.Name.StartsWith("PinguApps.BlazorSitemap/", StringComparison.Ordinal)).Name.Split('/')[1];
        await File.WriteAllTextAsync(Path.Combine(_directory, "SourceConsumer.csproj"), $"""
            <Project Sdk="Microsoft.NET.Sdk.Razor">
              <PropertyGroup><TargetFramework>net10.0</TargetFramework><Nullable>enable</Nullable><ImplicitUsings>enable</ImplicitUsings><TreatWarningsAsErrors>true</TreatWarningsAsErrors><RootNamespace>SourceConsumer</RootNamespace><ContinuousIntegrationBuild>true</ContinuousIntegrationBuild><PathMap>$(MSBuildProjectDirectory)=/_/</PathMap></PropertyGroup>
              <ItemGroup><FrameworkReference Include="Microsoft.AspNetCore.App" /><PackageReference Include="PinguApps.BlazorSitemap" Version="{version}" /></ItemGroup>
            </Project>
            """);
        await File.WriteAllTextAsync(PageFile, """
            @namespace Custom.Pages
            @page "/counter"
            @attribute [PinguApps.BlazorSitemap.SitemapPage]
            <h1>First version</h1>
            """);
        await File.WriteAllTextAsync(PageFile + ".cs", "namespace Custom.Pages; public partial class Counter { public string Message => \"first\"; }");
    }

    [When("the source consumer is built")]
    public async Task Build()
    {
        await Attempt();
        Assert.True(_exitCode == 0, _output);
        await FetchAsync();
    }

    [When("the source consumer build is attempted")]
    public async Task Attempt()
    {
        _previousState = File.Exists(StateFile) ? await File.ReadAllTextAsync(StateFile) : null;
        _previousDate = _sitemapDate;
        ProcessStartInfo start = new("dotnet") { WorkingDirectory = _directory!, RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false, CreateNoWindow = true };
        foreach (string argument in new[] { "build", "SourceConsumer.csproj", "-c", "Release", "--configfile", Path.Combine(ScenarioState.RepositoryRoot(), "artifacts", "consumer.NuGet.Config"),
            "-p:RestorePackagesPath=" + Path.Combine(_directory!, ".packages"), "-p:UseSharedCompilation=false", "-nodeReuse:false" })
        {
            start.ArgumentList.Add(argument);
        }
        start.Environment["DOTNET_CLI_USE_MSBUILD_SERVER"] = "0";
        _started = DateTimeOffset.UtcNow;
        using Process process = Process.Start(start)!;
        Task<string> stdout = process.StandardOutput.ReadToEndAsync();
        Task<string> stderr = process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();
        _finished = DateTimeOffset.UtcNow;
        _exitCode = process.ExitCode;
        _output = await stdout + await stderr;
    }

    private async Task FetchAsync()
    {
        // Load bytes so later builds can replace the DLL on Windows. Each test gets a fresh assembly instance.
        Assembly assembly = Assembly.Load(await File.ReadAllBytesAsync(Path.Combine(_directory!, "bin", "Release", "net10.0", "SourceConsumer.dll")));
        WebApplicationBuilder builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Services.AddBlazorSitemap<SourceFreshnessSteps>(options => { options.PublicBaseUrl = "https://example.com"; options.AddAssembly(assembly); });
        if (_suppliedDate)
        {
            typeof(SitemapServiceCollectionExtensions).GetMethod(nameof(SitemapServiceCollectionExtensions.AddSitemapLastModified))!
                .MakeGenericMethod(assembly.GetType("Custom.Pages.Counter")!, typeof(SourceOverrideFreshness)).Invoke(null, [builder.Services]);
        }
        await using WebApplication app = builder.Build();
        app.MapBlazorSitemap();
        await app.StartAsync();
        using HttpClient client = app.GetTestClient();
        XDocument sitemap = XDocument.Parse(await client.GetStringAsync("/sitemap.xml"));
        Assert.Equal("https://example.com/counter", sitemap.Descendants(ScenarioState.SitemapNamespace + "loc").Single().Value);
        _sitemapDate = sitemap.Descendants(ScenarioState.SitemapNamespace + "lastmod").Single().Value;
    }

    [Then("its sitemap date matches its generated sibling state")]
    public async Task Matches()
    {
        using JsonDocument state = JsonDocument.Parse(_deployedState ?? await File.ReadAllTextAsync(StateFile));
        Assert.Equal(state.RootElement.GetProperty("lastChangedUtc").GetString(), _sitemapDate);
        Assert.Equal(64, state.RootElement.GetProperty("sha256").GetString()!.Length);
    }

    [Then("its first source timestamp falls within the build interval")]
    public void FirstDate() => Assert.InRange(DateTimeOffset.Parse(_sitemapDate!, CultureInfo.InvariantCulture), _started, _finished);

    [Then("its source state and sitemap date are unchanged")]
    public async Task Unchanged()
    {
        Assert.Equal(_previousState, await File.ReadAllTextAsync(StateFile));
        Assert.Equal(_previousDate, _sitemapDate);
    }

    [Then("its source timestamp advances and matches the sitemap")]
    public async Task Changed()
    {
        Assert.True(DateTimeOffset.Parse(_sitemapDate!, CultureInfo.InvariantCulture) > DateTimeOffset.Parse(_previousDate!, CultureInfo.InvariantCulture));
        await Matches();
    }

    [When("the Razor page content changes")]
    public async Task ChangeRazor() => await File.AppendAllTextAsync(PageFile, "\n<p>New published wording</p>\n");

    [When("the Razor code-behind content changes")]
    public async Task ChangeCode() => await File.WriteAllTextAsync(PageFile + ".cs", "namespace Custom.Pages; public partial class Counter { public string Message => \"second\"; }");

    [When("the Razor code-behind is removed")]
    public void RemoveCode() => File.Delete(PageFile + ".cs");

    [When("the sibling source state is deleted")]
    public void Reset() => File.Delete(StateFile);

    [When("the sibling source state is corrupted")]
    public async Task Corrupt() => await File.WriteAllTextAsync(StateFile, "not valid source history");

    [When("the source consumer is built with tracked inputs removed after compilation")]
    public async Task Deployed()
    {
        await Build();
        _deployedState = await File.ReadAllTextAsync(StateFile);
        File.Delete(PageFile);
        File.Delete(StateFile);
        if (File.Exists(PageFile + ".cs")) { File.Delete(PageFile + ".cs"); }
        await FetchAsync();
    }

    [Then("the source build fails with a useful state-file diagnostic")]
    public void InvalidState()
    {
        Assert.NotEqual(0, _exitCode);
        Assert.Contains("Invalid source freshness state", _output, StringComparison.Ordinal);
    }

    [Given("the source consumer supplies an accurate page date")]
    public void SupplyDate() => _suppliedDate = true;

    [Then("its sitemap uses the supplied page date instead of the source estimate")]
    public async Task SuppliedDate()
    {
        Assert.Equal("2020-01-02", _sitemapDate);
        using JsonDocument state = JsonDocument.Parse(await File.ReadAllTextAsync(StateFile));
        Assert.NotEqual(_sitemapDate, state.RootElement.GetProperty("lastChangedUtc").GetString());
    }

    [AfterScenario]
    public void Cleanup()
    {
        if (_directory is not null) { Directory.Delete(_directory, recursive: true); }
    }
}
