<#
.SYNOPSIS
    Compiles the D5 application-lifetime example through the packaged Viu SDK.

.DESCRIPTION
    Copies the tracked consumer fixture to an isolated temporary directory, points
    NuGet and the MSBuild SDK resolver at a Viu package feed, and builds the exact
    fluent application/middleware/router composition shape documented by D5. The
    fixture uses packages only; no repository project reference can hide package
    or framework-manifest drift.

.PARAMETER PackageDirectory
    The directory containing the Viu SDK, framework, and library packages.

.PARAMETER Version
    The package version. When omitted, it is inferred from the single SDK package
    in PackageDirectory.
#>
[CmdletBinding()]
param(
    [string] $PackageDirectory =
        (Join-Path (Split-Path $PSScriptRoot -Parent) '_out/packages'),
    [string] $Version
)

$ErrorActionPreference = 'Stop'

$fixtureDirectory = Join-Path $PSScriptRoot 'fixtures/ApplicationLifetimeConsumer'
$packageDirectoryPath = [System.IO.Path]::GetFullPath($PackageDirectory)
if (-not [System.IO.Directory]::Exists($packageDirectoryPath)) {
    throw "The package directory does not exist: $packageDirectoryPath"
}

if ([string]::IsNullOrWhiteSpace($Version)) {
    $sdkPackages = @(
        Get-ChildItem `
            -LiteralPath $packageDirectoryPath `
            -Filter 'Assimalign.Viu.Sdk.*.nupkg' `
            -File)
    if ($sdkPackages.Count -ne 1) {
        throw "Expected exactly one Assimalign.Viu.Sdk package, found $($sdkPackages.Count)."
    }

    $prefix = 'Assimalign.Viu.Sdk.'
    $suffix = '.nupkg'
    $Version = $sdkPackages[0].Name.Substring(
        $prefix.Length,
        $sdkPackages[0].Name.Length - $prefix.Length - $suffix.Length)
}

$temporaryRoot = Join-Path `
    ([System.IO.Path]::GetTempPath()) `
    "viu-application-lifetime-consumer-$([System.Guid]::NewGuid().ToString('N'))"
$temporaryRootPath = [System.IO.Path]::GetFullPath($temporaryRoot)
$systemTemporaryPath = [System.IO.Path]::GetFullPath(
    [System.IO.Path]::GetTempPath())
if (-not $temporaryRootPath.StartsWith(
        $systemTemporaryPath,
        [System.StringComparison]::OrdinalIgnoreCase)) {
    throw "The consumer fixture must remain inside the system temporary directory."
}

try {
    Copy-Item `
        -LiteralPath $fixtureDirectory `
        -Destination $temporaryRootPath `
        -Recurse

    $globalJson = @{
        'msbuild-sdks' = @{
            'Assimalign.Viu.Sdk' = $Version
        }
    } | ConvertTo-Json -Depth 3
    Set-Content `
        -LiteralPath (Join-Path $temporaryRootPath 'global.json') `
        -Value $globalJson `
        -Encoding utf8

    $escapedPackageDirectory =
        [System.Security.SecurityElement]::Escape($packageDirectoryPath)
    $nugetConfiguration = @"
<?xml version="1.0" encoding="utf-8"?>
<configuration>
  <packageSources>
    <clear />
    <add key="viu-local" value="$escapedPackageDirectory" />
    <add key="nuget.org" value="https://api.nuget.org/v3/index.json" />
  </packageSources>
  <packageSourceMapping>
    <packageSource key="viu-local">
      <package pattern="Assimalign.Viu.*" />
    </packageSource>
    <packageSource key="nuget.org">
      <package pattern="*" />
    </packageSource>
  </packageSourceMapping>
  <config>
    <add key="globalPackagesFolder" value=".nuget/packages" />
  </config>
</configuration>
"@
    Set-Content `
        -LiteralPath (Join-Path $temporaryRootPath 'nuget.config') `
        -Value $nugetConfiguration `
        -Encoding utf8

    $project = Join-Path $temporaryRootPath 'ApplicationLifetimeConsumer.csproj'
    & dotnet restore $project "-p:ViuConsumerVersion=$Version"
    if ($LASTEXITCODE -ne 0) {
        throw "Restoring the application-lifetime package consumer failed with exit code $LASTEXITCODE."
    }

    & dotnet build `
        $project `
        --configuration Release `
        --no-restore `
        -warnaserror `
        "-p:ViuConsumerVersion=$Version"
    if ($LASTEXITCODE -ne 0) {
        throw "Building the application-lifetime package consumer failed with exit code $LASTEXITCODE."
    }
}
finally {
    if ([System.IO.Directory]::Exists($temporaryRootPath)) {
        Remove-Item -LiteralPath $temporaryRootPath -Recurse -Force
    }
}

Write-Host `
    "Application-lifetime package consumer compiled against Viu $Version." `
    -ForegroundColor Cyan
