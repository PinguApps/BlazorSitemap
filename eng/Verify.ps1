param([string]$PackageVersion = '1.0.0')
$ErrorActionPreference = 'Stop'
$PSNativeCommandUseErrorActionPreference = $true
$env:BLAZOR_SITEMAP_PWSH = (Get-Process -Id $PID).Path
$repoRoot = Split-Path $PSScriptRoot -Parent
Push-Location $repoRoot
try {
    $feed = Join-Path $repoRoot 'artifacts/packages'
    New-Item -ItemType Directory -Force $feed | Out-Null
    dotnet pack src/PinguApps.BlazorSitemap.Abstractions -c Release -o artifacts/packages "-p:PackageVersion=$PackageVersion"
    dotnet pack src/PinguApps.BlazorSitemap -c Release -o $feed "-p:PackageVersion=$PackageVersion" "-p:ContinuousIntegrationBuild=$([bool]$env:CI)"
    $config = Join-Path $repoRoot 'artifacts/consumer.NuGet.Config'
    $escapedFeed = [System.Security.SecurityElement]::Escape($feed)
    @"
<?xml version="1.0" encoding="utf-8"?>
<configuration>
  <packageSources><clear /><add key="packed-package" value="$escapedFeed" /><add key="nuget.org" value="https://api.nuget.org/v3/index.json" /></packageSources>
  <packageSourceMapping><packageSource key="packed-package"><package pattern="PinguApps.BlazorSitemap*" /></packageSource><packageSource key="nuget.org"><package pattern="*" /></packageSource></packageSourceMapping>
</configuration>
"@ | Set-Content -LiteralPath $config -Encoding utf8
    # An isolated package cache proves the consumer restores the newly packed artifact, never an older global copy.
    $packageCache = Join-Path $repoRoot ('artifacts/nuget/' + [Guid]::NewGuid().ToString('N'))
    dotnet restore BlazorSitemap.slnx --configfile $config --packages $packageCache "-p:SitemapPackageVersion=$PackageVersion"
    dotnet test --solution BlazorSitemap.slnx -c Release --no-restore "-p:SitemapPackageVersion=$PackageVersion" --report-xunit-trx --report-xunit-trx-filename test-results.trx --results-directory TestResults
}
finally { Pop-Location }
