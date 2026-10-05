using System.Diagnostics;
using System.Text.RegularExpressions;
using PinguApps.BlazorSitemap.Tests.Support;
using Reqnroll;
using Xunit;

namespace PinguApps.BlazorSitemap.Tests.Steps;

[Binding]
public sealed class DocumentationSteps
{
    private string? _directory;
    private int _exitCode;
    private string _output = "";

    [When("the README setup snippet is compiled in a fresh packed consumer")]
    public async Task Compile()
    {
        string root = ScenarioState.RepositoryRoot();
        _directory = Path.Combine(Path.GetTempPath(), "BlazorSitemapDocs-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_directory);
        string readme = await File.ReadAllTextAsync(Path.Combine(root, "README.md"));
        string program = Regex.Match(readme, "```csharp\\r?\\n(.*?)```", RegexOptions.Singleline | RegexOptions.CultureInvariant).Groups[1].Value;
        Assert.NotEmpty(program);
        string registrations = string.Join('\n', Regex.Matches(readme, "builder\\.Services\\.AddSitemap(?:Entries|LastModified)<[^;\\r\\n]+;", RegexOptions.CultureInvariant)
            .Select(x => x.Value).Distinct(StringComparer.Ordinal));
        registrations += "\n" + string.Join('\n', Regex.Matches(readme, "builder\\.Services\\.AddSitemapUrls(?:<[^>]+>)?\\(.*?\\);", RegexOptions.Singleline | RegexOptions.CultureInvariant)
            .Select(x => x.Value).Distinct(StringComparer.Ordinal));
        program = program.Replace("var app = builder.Build();", registrations + "\nvar app = builder.Build();", StringComparison.Ordinal);
        await File.WriteAllTextAsync(Path.Combine(_directory, "Program.cs"), program);
        await File.WriteAllTextAsync(Path.Combine(_directory, "App.razor"), "@namespace YourApp.Components\n<h1>Documentation consumer</h1>\n");
        await File.WriteAllTextAsync(Path.Combine(_directory, "_Imports.razor"), "@using Microsoft.AspNetCore.Components\n@using Microsoft.AspNetCore.Components.Web\n@using PinguApps.BlazorSitemap\n");
        int example = 0;
        foreach (Match block in Regex.Matches(readme, "```razor\\r?\\n(.*?)```", RegexOptions.Singleline | RegexOptions.CultureInvariant))
        {
            string razor = block.Groups[1].Value;
            string name = razor.Contains("/blog/{slug}", StringComparison.Ordinal) ? "BlogPost"
                : razor.Contains("@page \"/blog\"", StringComparison.Ordinal) ? "BlogIndex" : "Example" + example++;
            await File.WriteAllTextAsync(Path.Combine(_directory, name + ".razor"), "@namespace YourApp.Components\n" + razor);
        }
        await File.WriteAllTextAsync(Path.Combine(_directory, "BlogStore.cs"), (await File.ReadAllTextAsync(Path.Combine(root, "tests", "Consumer", "BlogStore.cs")))
            .Replace("namespace Consumer;", "namespace YourApp.Components;", StringComparison.Ordinal));
        await File.WriteAllTextAsync(Path.Combine(_directory, "Article.cs"), (await File.ReadAllTextAsync(Path.Combine(root, "tests", "Consumer", "Article.cs")))
            .Replace("namespace Consumer;", "namespace YourApp.Components;", StringComparison.Ordinal));
        IEnumerable<string> providers = Regex.Matches(readme, "```csharp\\r?\\n(.*?)```", RegexOptions.Singleline | RegexOptions.CultureInvariant)
            .Select(x => x.Groups[1].Value).Where(x => x.Contains("public sealed class", StringComparison.Ordinal))
            .Select(x => x[x.IndexOf("public sealed class", StringComparison.Ordinal)..]);
        foreach (string provider in providers)
        {
            string name = Regex.Match(provider, "public sealed class (\\w+)", RegexOptions.CultureInvariant).Groups[1].Value;
            await File.WriteAllTextAsync(Path.Combine(_directory, name + ".cs"), "using PinguApps.BlazorSitemap;\nnamespace YourApp.Components;\n" + provider);
        }

        await File.WriteAllTextAsync(Path.Combine(_directory, "CatalogueIndexFreshness.cs"), "using PinguApps.BlazorSitemap;\nnamespace YourApp.Components;\n" + """

            public sealed class CatalogueIndexFreshness : ISitemapLastModifiedProvider
            {
                public ValueTask<SitemapLastModified?> GetLastModifiedAsync(SitemapPageContext page, CancellationToken cancellationToken = default)
                    => ValueTask.FromResult<SitemapLastModified?>(new SitemapLastModified(new DateOnly(2026, 9, 5)));
            }
            """);
        await File.WriteAllTextAsync(Path.Combine(_directory, "CatalogueIndex.razor"), "@namespace YourApp.Components\n@page \"/catalogue\"\n@attribute [SitemapPage(DynamicContent = true)]\n<h1>Catalogue</h1>\n");
        // Read the tested artifact's version from the consumer's resolved assets, including release-tag overrides.
        string assets = await File.ReadAllTextAsync(Path.Combine(root, "tests", "Consumer", "obj", "project.assets.json"));
        string version = Regex.Match(assets, "\"PinguApps.BlazorSitemap/([^\"]+)\"", RegexOptions.CultureInvariant).Groups[1].Value;
        Assert.NotEmpty(version);
        await File.WriteAllTextAsync(Path.Combine(_directory, "Documentation.csproj"), $"""
            <Project Sdk="Microsoft.NET.Sdk.Web">
              <PropertyGroup><TargetFramework>net10.0</TargetFramework><ImplicitUsings>enable</ImplicitUsings><Nullable>enable</Nullable><TreatWarningsAsErrors>true</TreatWarningsAsErrors></PropertyGroup>
              <ItemGroup><PackageReference Include="PinguApps.BlazorSitemap" Version="{version}" /></ItemGroup>
            </Project>
            """);
        ProcessStartInfo start = new("dotnet") { WorkingDirectory = _directory, RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false, CreateNoWindow = true };
        foreach (string argument in new[] { "build", "Documentation.csproj", "-c", "Release", "--configfile", Path.Combine(root, "artifacts", "consumer.NuGet.Config"),
            "-p:RestorePackagesPath=" + Path.Combine(_directory, ".packages"), "-p:UseSharedCompilation=false", "-nodeReuse:false" })
        {
            start.ArgumentList.Add(argument);
        }

        using Process process = Process.Start(start)!;
        Task<string> stdout = process.StandardOutput.ReadToEndAsync();
        Task<string> stderr = process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();
        _exitCode = process.ExitCode;
        _output = await stdout + await stderr;
    }

    [Then("the documentation build succeeds")]
    public void Success() => Assert.True(_exitCode == 0, _output);

    [AfterScenario]
    public void Cleanup()
    {
        if (_directory is not null)
        {
            Directory.Delete(_directory, recursive: true);
        }
    }
}
