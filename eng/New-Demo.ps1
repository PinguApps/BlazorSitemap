param([string]$Destination, [string]$PackageVersion = '1.0.0')
$ErrorActionPreference = 'Stop'
$PSNativeCommandUseErrorActionPreference = $true
$repoRoot = Split-Path $PSScriptRoot -Parent
if (-not $Destination) { $Destination = Join-Path ([IO.Path]::GetTempPath()) ('PinguApps.BlazorSitemap.Demo-' + [Guid]::NewGuid().ToString('N')) }
$demoRoot = [IO.Path]::GetFullPath($Destination)
if ($demoRoot.Equals($repoRoot, [StringComparison]::OrdinalIgnoreCase) -or $demoRoot.StartsWith($repoRoot + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) {
    throw 'The independent demo must be outside the package repository.'
}
if (Test-Path -LiteralPath $demoRoot) { throw "Destination already exists: $demoRoot" }
New-Item -ItemType Directory -Path $demoRoot | Out-Null
Push-Location $repoRoot
try {
    $feed = Join-Path $demoRoot 'local-feed'
    New-Item -ItemType Directory -Path $feed | Out-Null
    dotnet pack src/PinguApps.BlazorSitemap.Abstractions -c Release -o $feed "-p:PackageVersion=$PackageVersion"
    dotnet pack src/PinguApps.BlazorSitemap -c Release -o $feed "-p:PackageVersion=$PackageVersion"
    foreach ($project in @('Consumer', 'Consumer.Library', 'Consumer.Client')) {
        $source = Join-Path $repoRoot "tests/$project"
        foreach ($file in rg --files $source -g '!**/bin/**' -g '!**/obj/**') {
            $relative = [IO.Path]::GetRelativePath($source, $file)
            $target = Join-Path $demoRoot "$project/$relative"
            New-Item -ItemType Directory -Path (Split-Path $target -Parent) -Force | Out-Null
            Copy-Item -LiteralPath $file -Destination $target
        }
    }
    Copy-Item -LiteralPath (Join-Path $repoRoot 'global.json') -Destination $demoRoot
    $escapedVersion = [System.Security.SecurityElement]::Escape($PackageVersion)
    @"
<Project>
  <PropertyGroup>
    <RestorePackagesPath>`$(MSBuildThisFileDirectory).packages</RestorePackagesPath>
    <SitemapPackageVersion>$escapedVersion</SitemapPackageVersion>
  </PropertyGroup>
</Project>
"@ | Set-Content -LiteralPath (Join-Path $demoRoot 'Directory.Build.props') -Encoding utf8
    @'
<?xml version="1.0" encoding="utf-8"?>
<configuration>
  <packageSources><clear /><add key="local-feed" value="local-feed" /><add key="nuget.org" value="https://api.nuget.org/v3/index.json" /></packageSources>
  <packageSourceMapping><packageSource key="local-feed"><package pattern="PinguApps.BlazorSitemap*" /></packageSource><packageSource key="nuget.org"><package pattern="*" /></packageSource></packageSourceMapping>
</configuration>
'@ | Set-Content -LiteralPath (Join-Path $demoRoot 'NuGet.Config') -Encoding utf8
    dotnet restore "$demoRoot/Consumer/Consumer.csproj" --configfile "$demoRoot/NuGet.Config"
    dotnet build "$demoRoot/Consumer/Consumer.csproj" -c Release --no-restore
    @"
# Independent PinguApps.BlazorSitemap consumer

Packages restore from this directory's local-feed. Directory.Build.props persists the isolated .packages cache and package version for ordinary restore, build and run commands.
Project references are to the app's client and Razor component library. Package code comes from the local feed.

From this directory, run (builds Debug if needed):
dotnet run --project ./Consumer/Consumer.csproj

Or run the prebuilt Release app:
dotnet run --project ./Consumer/Consumer.csproj --no-build -c Release

The launch profile uses http://localhost:5187 and Development, with no browser launched automatically.

Open http://localhost:5187/blog and http://localhost:5187/sitemap.xml.
Edit/add/unpublish/delete controls change runtime publication state and invalidate the sitemap.
The in-memory demo resets when the process restarts.

Static dates are source-change estimates. The normal build tracks each marked Razor file and optional code-behind in its sibling .razor.sitemap.json.
Keep these JSON files with the source across builds. Unchanged source keeps its date; edits or deletion of state reset the estimate.
All entries have lastmod. Static and provider-backed API pages supplement component routes. BlogIndexFreshness is registered specifically for BlogIndex; post translations supply their own publication dates.
Home, About, finite translations, the library page and Auto page use embedded build stamps. No page URLs are mapped manually.
Stop the normal app with Ctrl+C when finished.

Use Sitemap__PublicBaseUrl or --Sitemap:PublicBaseUrl to override appsettings for another origin/mount.
Development-only publishing controls are illustrative; secure CMS writes in a real application.
"@ | Set-Content -LiteralPath "$demoRoot/README.md" -Encoding utf8
    Write-Output "DEMO_PATH=$demoRoot"
}
finally { Pop-Location }
