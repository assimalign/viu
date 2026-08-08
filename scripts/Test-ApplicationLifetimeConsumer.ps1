<#
.SYNOPSIS
    Builds and publishes the application-lifetime fixture through the packaged Viu SDK.

.DESCRIPTION
    Copies the tracked consumer fixture to a validated repository-local scratch
    directory, points NuGet and the MSBuild SDK resolver at a Viu package feed,
    and verifies Build, PublishTrimmed, and PublishAot. The fixture uses packages
    only; no repository project reference can hide package or framework-manifest
    drift.

.PARAMETER PackageDirectory
    The directory containing the Viu SDK, framework, and library packages.

.PARAMETER Version
    The package version. When omitted, it is inferred from the single SDK package
    in PackageDirectory.

.PARAMETER ScratchRoot
    A repository-local directory under _out used for an isolated copy and publish
    outputs. The script creates and removes only a uniquely named child directory.

.PARAMETER OfflinePackageDirectory
    An optional local source for non-Viu packages. When supplied, the isolated
    restore maps all non-Viu packages to this source instead of nuget.org.
#>
[CmdletBinding()]
param(
    [string] $PackageDirectory =
        (Join-Path (Split-Path $PSScriptRoot -Parent) '_out/packages'),
    [string] $Version,
    [string] $ScratchRoot =
        (Join-Path (Split-Path $PSScriptRoot -Parent) '_out/scratch/application-lifetime-consumer'),
    [string] $OfflinePackageDirectory
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$repositoryRootPath = [System.IO.Path]::GetFullPath(
    (Split-Path $PSScriptRoot -Parent))
$fixtureDirectory = Join-Path $PSScriptRoot 'fixtures/ApplicationLifetimeConsumer'
$packageDirectoryPath = [System.IO.Path]::GetFullPath($PackageDirectory)
if (-not [System.IO.Directory]::Exists($packageDirectoryPath)) {
    throw "The package directory does not exist: $packageDirectoryPath"
}

$externalPackageSourceKey = 'nuget.org'
$externalPackageSourceValue = 'https://api.nuget.org/v3/index.json'
if (-not [string]::IsNullOrWhiteSpace($OfflinePackageDirectory)) {
    $offlinePackageDirectoryPath = [System.IO.Path]::GetFullPath(
        $OfflinePackageDirectory)
    if (-not [System.IO.Directory]::Exists($offlinePackageDirectoryPath)) {
        throw "The offline package directory does not exist: $offlinePackageDirectoryPath"
    }

    $externalPackageSourceKey = 'offline'
    $externalPackageSourceValue = $offlinePackageDirectoryPath
}

$repositoryOutputPath = [System.IO.Path]::GetFullPath(
    (Join-Path $repositoryRootPath '_out'))
$scratchRootPath = [System.IO.Path]::GetFullPath($ScratchRoot)
$repositoryOutputPrefix = $repositoryOutputPath.TrimEnd(
    [System.IO.Path]::DirectorySeparatorChar,
    [System.IO.Path]::AltDirectorySeparatorChar) +
    [System.IO.Path]::DirectorySeparatorChar
if (-not $scratchRootPath.StartsWith(
        $repositoryOutputPrefix,
        [System.StringComparison]::OrdinalIgnoreCase)) {
    throw "ScratchRoot must be a child of the repository _out directory: $scratchRootPath"
}

$existingScratchAncestor = $scratchRootPath
while (-not [System.IO.Directory]::Exists($existingScratchAncestor)) {
    $existingScratchAncestor = [System.IO.Path]::GetDirectoryName(
        $existingScratchAncestor)
    if ([string]::IsNullOrEmpty($existingScratchAncestor)) {
        throw "ScratchRoot has no existing repository-local ancestor: $scratchRootPath"
    }
}

while ($existingScratchAncestor.StartsWith(
        $repositoryRootPath,
        [System.StringComparison]::OrdinalIgnoreCase)) {
    $ancestor = Get-Item -LiteralPath $existingScratchAncestor -Force
    if (($ancestor.Attributes -band [System.IO.FileAttributes]::ReparsePoint) -ne 0) {
        throw "ScratchRoot cannot pass through a reparse point: $existingScratchAncestor"
    }

    if ([string]::Equals(
            $existingScratchAncestor,
            $repositoryRootPath,
            [System.StringComparison]::OrdinalIgnoreCase)) {
        break
    }

    $existingScratchAncestor = [System.IO.Path]::GetDirectoryName(
        $existingScratchAncestor)
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

$null = New-Item -ItemType Directory -Path $scratchRootPath -Force
$temporaryRoot = Join-Path `
    $scratchRootPath `
    "run-$([System.Guid]::NewGuid().ToString('N'))"
$temporaryRootPath = [System.IO.Path]::GetFullPath($temporaryRoot)
$scratchRootPrefix = $scratchRootPath.TrimEnd(
    [System.IO.Path]::DirectorySeparatorChar,
    [System.IO.Path]::AltDirectorySeparatorChar) +
    [System.IO.Path]::DirectorySeparatorChar
if (-not $temporaryRootPath.StartsWith(
        $scratchRootPrefix,
        [System.StringComparison]::OrdinalIgnoreCase)) {
    throw "The consumer fixture must remain inside the validated ScratchRoot."
}

try {
    $null = New-Item -ItemType Directory -Path $temporaryRootPath
    Get-ChildItem -LiteralPath $fixtureDirectory -Force | ForEach-Object {
        Copy-Item `
            -LiteralPath $_.FullName `
            -Destination $temporaryRootPath `
            -Recurse
    }

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
    $escapedExternalPackageSourceValue =
        [System.Security.SecurityElement]::Escape($externalPackageSourceValue)
    $nugetConfiguration = @"
<?xml version="1.0" encoding="utf-8"?>
<configuration>
  <packageSources>
    <clear />
    <add key="viu-local" value="$escapedPackageDirectory" />
    <add key="$externalPackageSourceKey" value="$escapedExternalPackageSourceValue" />
  </packageSources>
  <packageSourceMapping>
    <packageSource key="viu-local">
      <package pattern="Assimalign.Viu.*" />
    </packageSource>
    <packageSource key="$externalPackageSourceKey">
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

    # [PKG-2] — the fixture deliberately references every standalone package
    # that overlaps Assimalign.Viu.App. Verify that the targeting-pack manifest
    # was consumed and that no matching lib/ asset survived conflict resolution.
    $expectedFrameworkPackageOverrides = @(
        'Assimalign.Viu.Components',
        'Assimalign.Viu.Reactivity',
        'Assimalign.Viu.State',
        'Assimalign.Viu.Core',
        'Assimalign.Viu.Browser'
    )
    $packageConflictOverridesPath = Join-Path `
        $temporaryRootPath `
        'obj/viu-package-conflict-overrides.txt'
    if (-not [System.IO.File]::Exists($packageConflictOverridesPath)) {
        throw "The package consumer did not emit conflict-override evidence: $packageConflictOverridesPath"
    }

    $packageConflictOverrides = [System.IO.File]::ReadAllText(
        $packageConflictOverridesPath)
    foreach ($packageId in $expectedFrameworkPackageOverrides) {
        $expectedOverride = "$packageId|$Version"
        if ($packageConflictOverrides.IndexOf(
                $expectedOverride,
                [System.StringComparison]::OrdinalIgnoreCase) -lt 0) {
            throw "The App.Ref targeting pack did not register package override $expectedOverride."
        }
    }

    $resolvedPackageReferencesPath = Join-Path `
        $temporaryRootPath `
        'obj/viu-resolved-package-references.txt'
    if (-not [System.IO.File]::Exists($resolvedPackageReferencesPath)) {
        throw "The package consumer did not emit resolved-reference evidence: $resolvedPackageReferencesPath"
    }

    $resolvedPackageReferences = [System.IO.File]::ReadAllText(
        $resolvedPackageReferencesPath).Replace(
            [System.IO.Path]::DirectorySeparatorChar,
            [System.IO.Path]::AltDirectorySeparatorChar).ToLowerInvariant()
    foreach ($packageId in $expectedFrameworkPackageOverrides) {
        $standaloneLibraryAsset = "/$($packageId.ToLowerInvariant())/$($Version.ToLowerInvariant())/lib/"
        if ($resolvedPackageReferences.Contains($standaloneLibraryAsset)) {
            throw "Standalone package asset survived App framework conflict resolution: $standaloneLibraryAsset"
        }
    }

    $trimmedOutput = Join-Path $temporaryRootPath 'publish-trimmed'
    & dotnet publish `
        $project `
        --configuration Release `
        --no-restore `
        -warnaserror `
        --output $trimmedOutput `
        "-p:ViuConsumerVersion=$Version" `
        -p:PublishTrimmed=true `
        -p:TrimMode=full `
        -p:RunAOTCompilation=false
    if ($LASTEXITCODE -ne 0) {
        throw "PublishTrimmed failed for the application-lifetime package consumer with exit code $LASTEXITCODE."
    }

    & dotnet restore `
        $project `
        "-p:ViuConsumerVersion=$Version" `
        -p:RunAOTCompilation=true
    if ($LASTEXITCODE -ne 0) {
        throw "Restoring the AOT application-lifetime package consumer failed with exit code $LASTEXITCODE."
    }

    $aotOutput = Join-Path $temporaryRootPath 'publish-aot'
    & dotnet publish `
        $project `
        --configuration Release `
        --no-restore `
        -warnaserror `
        --output $aotOutput `
        "-p:ViuConsumerVersion=$Version" `
        -p:PublishTrimmed=true `
        -p:TrimMode=full `
        -p:RunAOTCompilation=true
    if ($LASTEXITCODE -ne 0) {
        throw "PublishAot failed for the application-lifetime package consumer with exit code $LASTEXITCODE."
    }
}
finally {
    if ([System.IO.Directory]::Exists($temporaryRootPath)) {
        $reparsePoints = @(
            Get-ChildItem `
                -LiteralPath $temporaryRootPath `
                -Force `
                -Recurse | Where-Object {
                    ($_.Attributes -band [System.IO.FileAttributes]::ReparsePoint) -ne 0
                })
        if ($reparsePoints.Count -ne 0) {
            throw "Refusing to remove consumer scratch containing reparse points: $temporaryRootPath"
        }

        Remove-Item -LiteralPath $temporaryRootPath -Recurse -Force
    }
}

Write-Host `
    "Application-lifetime package consumer passed Build, PublishTrimmed, and PublishAot against Viu $Version." `
    -ForegroundColor Cyan
