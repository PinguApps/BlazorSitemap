using System.Diagnostics;
using PinguApps.BlazorSitemap.Tests.Support;
using Reqnroll;
using Xunit;

namespace PinguApps.BlazorSitemap.Tests.Steps;

[Binding]
public sealed class ReleaseVersionSteps
{
    private int _exitCode;
    private string _output = "";
    private string _error = "";

    [When("release tag {string} is validated")]
    public Task ValidateAsync(string tag) => RunAsync(tag);

    [When("release tag {string} with name {string} is validated")]
    public Task ValidateNameAsync(string tag, string name) => RunAsync(tag, name);

    [When("scheduled release tag {string} is validated")]
    public Task ValidateStableAsync(string tag) => RunAsync(tag, stable: true);

    [When("a release tag with a trailing newline is validated")]
    public Task ValidateNewlineAsync() => RunAsync("v2.0.1\n");

    [Then("the package version is {string}")]
    public void Version(string expected)
    {
        Assert.True(_exitCode == 0, _error);
        Assert.Equal(expected, _output.TrimEnd('\r', '\n'));
    }

    [Then("release validation fails with {string}")]
    public void Failure(string diagnostic)
    {
        Assert.NotEqual(0, _exitCode);
        Assert.Empty(_output);
        Assert.Contains(diagnostic, _error, StringComparison.Ordinal);
    }

    private async Task RunAsync(string tag, string? name = null, bool stable = false)
    {
        ProcessStartInfo start = new(Environment.GetEnvironmentVariable("BLAZOR_SITEMAP_PWSH") ?? "pwsh")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        foreach (string argument in new[] { "-NoProfile", "-File", Path.Combine(ScenarioState.RepositoryRoot(), "eng", "Get-ReleaseVersion.ps1"), "-Tag", tag })
        {
            start.ArgumentList.Add(argument);
        }
        if (name is not null)
        {
            start.ArgumentList.Add("-Name");
            start.ArgumentList.Add(name);
        }
        if (stable) { start.ArgumentList.Add("-Stable"); }
        using Process process = Process.Start(start)!;
        Task<string> stdout = process.StandardOutput.ReadToEndAsync();
        Task<string> stderr = process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();
        _exitCode = process.ExitCode;
        _output = await stdout;
        _error = await stderr;
    }
}
