using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.Json;
using PinguApps.BlazorSitemap.Tests.Support;
using Reqnroll;
using Xunit;

namespace PinguApps.BlazorSitemap.Tests.Steps;

[Binding]
public sealed class DemoRebuildSteps
{
    private string? _work;
    private string _version = "";
    private string _output = "";
    private int _exitCode;
    private string Demo => Path.Combine(_work!, "demo");
    private string AmbientCache => Path.Combine(_work!, "ambient-cache");

    [Given("a fresh independent demo with a nondefault package version")]
    public async Task Generate()
    {
        string root = ScenarioState.RepositoryRoot();
        _work = Path.Combine(Path.GetTempPath(), "BlazorSitemapDemoTest-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_work);
        using JsonDocument assets = JsonDocument.Parse(await File.ReadAllTextAsync(Path.Combine(root, "tests", "Consumer", "obj", "project.assets.json")));
        string publishedVersion = assets.RootElement.GetProperty("libraries").EnumerateObject()
            .Single(x => x.Name.StartsWith("PinguApps.BlazorSitemap/", StringComparison.Ordinal)).Name.Split('/')[1];
        _version = publishedVersion + "-demo";
        await Run(Environment.GetEnvironmentVariable("BLAZOR_SITEMAP_PWSH") ?? "pwsh", root,
            "-NoProfile", "-File", Path.Combine(root, "eng", "New-Demo.ps1"), "-Destination", Demo, "-PackageVersion", _version);
        Assert.True(_exitCode == 0, _output);
    }

    [Given("an incompatible copy of that package exists in its ambient NuGet cache")]
    public async Task PoisonCache()
    {
        string source = Path.Combine(Demo, ".packages", "pinguapps.blazorsitemap", _version);
        string target = Path.Combine(AmbientCache, "pinguapps.blazorsitemap", _version);
        foreach (string file in Directory.EnumerateFiles(source, "*", SearchOption.AllDirectories))
        {
            string destination = Path.Combine(target, Path.GetRelativePath(source, file));
            Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
            File.Copy(file, destination);
        }

        // NuGet finds an already extracted package before consulting source mapping.
        // An unusable assembly makes any accidental use of this ambient copy observable.
        await File.WriteAllTextAsync(Path.Combine(target, "lib", "net10.0", "PinguApps.BlazorSitemap.dll"), "Incompatible ambient package copy.");
    }

    [When("the demo is built normally in {string}")]
    public Task Build(string configuration) => Run("dotnet", Demo, "build", "Consumer/Consumer.csproj", "-c", configuration);

    [Then("the demo build succeeds using its persistent local package cache")]
    public async Task Verify()
    {
        Assert.True(_exitCode == 0, _output);
        string cache = Path.GetFullPath(Path.Combine(Demo, ".packages"));
        foreach (string project in new[] { "Consumer", "Consumer.Client", "Consumer.Library" })
        {
            using JsonDocument assets = JsonDocument.Parse(await File.ReadAllTextAsync(Path.Combine(Demo, project, "obj", "project.assets.json")));
            string actual = assets.RootElement.GetProperty("project").GetProperty("restore").GetProperty("packagesPath").GetString()!;
            Assert.Equal(cache.TrimEnd(Path.DirectorySeparatorChar), actual.TrimEnd(Path.DirectorySeparatorChar));
            Assert.True(assets.RootElement.GetProperty("libraries").TryGetProperty("PinguApps.BlazorSitemap.Abstractions/" + _version, out _));
        }

        foreach (string id in new[] { "PinguApps.BlazorSitemap", "PinguApps.BlazorSitemap.Abstractions" })
        {
            byte[] local = await File.ReadAllBytesAsync(Path.Combine(Demo, "local-feed", id + "." + _version + ".nupkg"));
            byte[] restored = await File.ReadAllBytesAsync(Path.Combine(cache, id.ToLowerInvariant(), _version, id.ToLowerInvariant() + "." + _version + ".nupkg"));
            Assert.Equal(SHA256.HashData(local), SHA256.HashData(restored));
        }
    }

    private async Task Run(string executable, string directory, params string[] arguments)
    {
        ProcessStartInfo start = new(executable) { WorkingDirectory = directory, RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false, CreateNoWindow = true };
        foreach (string argument in arguments) { start.ArgumentList.Add(argument); }
        // Poison only consumer builds. Packing the repository must retain its normal
        // build-task cache, which an attached IDE may load and keep open.
        if (directory == Demo) { start.Environment["NUGET_PACKAGES"] = AmbientCache; }
        start.Environment["MSBUILDDISABLENODEREUSE"] = "1";
        start.Environment["DOTNET_CLI_USE_MSBUILD_SERVER"] = "0";
        start.Environment["UseSharedCompilation"] = "false";
        using Process process = Process.Start(start)!;
        Task<string> stdout = process.StandardOutput.ReadToEndAsync();
        Task<string> stderr = process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();
        _exitCode = process.ExitCode;
        _output = await stdout + await stderr;
    }

    [AfterScenario]
    public void Cleanup()
    {
        if (_work is not null) { Directory.Delete(_work, recursive: true); }
    }
}
