param([string]$PackageVersion = '1.0.0')
$ErrorActionPreference = 'Stop'
$PSNativeCommandUseErrorActionPreference = $true
$env:BLAZOR_SITEMAP_PWSH = (Get-Process -Id $PID).Path
$repoRoot = Split-Path $PSScriptRoot -Parent
Push-Location $repoRoot
try {
    $feed = Join-Path $repoRoot 'artifacts/packages'
    New-Item -ItemType Directory -Force $feed | Out-Null
    $ci = "-p:ContinuousIntegrationBuild=$($env:CI -eq 'true')"
    dotnet pack src/PinguApps.BlazorSitemap.Abstractions -c Release -o $feed "-p:PackageVersion=$PackageVersion" $ci
    dotnet pack src/PinguApps.BlazorSitemap -c Release -o $feed "-p:PackageVersion=$PackageVersion" $ci
    $config = Join-Path $repoRoot 'artifacts/consumer.NuGet.Config'
    $escapedFeed = [System.Security.SecurityElement]::Escape($feed)
    @"
<?xml version="1.0" encoding="utf-8"?>
<configuration>
  <packageSources><clear /><add key="packed-package" value="$escapedFeed" /><add key="nuget.org" value="https://api.nuget.org/v3/index.json" /></packageSources>
  <packageSourceMapping><packageSource key="packed-package"><package pattern="PinguApps.BlazorSitemap*" /></packageSource><packageSource key="nuget.org"><package pattern="*" /></packageSource></packageSourceMapping>
</configuration>
"@ | Set-Content -LiteralPath $config -Encoding utf8
    # Pack net10.0 once, then exercise the same artifacts from both consumer runtimes.
    foreach ($framework in @('net10.0', 'net11.0')) {
        $packageCache = Join-Path $repoRoot ('artifacts/nuget/' + [Guid]::NewGuid().ToString('N'))
        dotnet restore BlazorSitemap.slnx --configfile $config --packages $packageCache "-p:SitemapPackageVersion=$PackageVersion" "-p:SitemapConsumerTargetFramework=$framework"
        dotnet test --solution BlazorSitemap.slnx -c Release --no-restore "-p:SitemapPackageVersion=$PackageVersion" "-p:SitemapConsumerTargetFramework=$framework" --report-xunit-trx --report-xunit-trx-filename "test-results-$framework.trx" --results-directory TestResults
    }
}
finally { Pop-Location }
