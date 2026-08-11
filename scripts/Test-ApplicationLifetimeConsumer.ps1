<#
.SYNOPSIS
    Validates the packaged base and Browser SDK consumer topologies.

.DESCRIPTION
    Builds and packs a component library through Assimalign.Viu.Sdk, proves its
    dependency closure is browser-free, then builds and publishes the tracked
    Browser application through Assimalign.Viu.Sdk.Browser while consuming that
    component-library package. The fixtures use packages only; no repository
    project reference can hide SDK or framework-manifest drift.

.PARAMETER PackageDirectory
    The directory containing the Viu SDK, framework, and library packages.

.PARAMETER Version
    The package version. When omitted, it is inferred from the single Browser SDK
    package, or from the base SDK in ComponentLibraryOnly mode.

.PARAMETER ScratchRoot
    A repository-local directory under _out used for an isolated copy and publish
    outputs. The script creates and removes only a uniquely named child directory.

.PARAMETER OfflinePackageDirectory
    An optional local source for non-Viu packages. When supplied, the isolated
    restore maps all non-Viu packages to this source instead of nuget.org.

.PARAMETER ComponentLibraryOnly
    Run only the base-SDK component-library Build + Pack lane. This is used by
    CI without the WebAssembly workload to prove the host-neutral topology.
#>
[CmdletBinding()]
param(
    [string] $PackageDirectory =
        (Join-Path (Split-Path $PSScriptRoot -Parent) '_out/packages'),
    [string] $Version,
    [string] $ScratchRoot =
        (Join-Path (Split-Path $PSScriptRoot -Parent) '_out/scratch/application-lifetime-consumer'),
    [string] $OfflinePackageDirectory,
    [switch] $ComponentLibraryOnly
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$repositoryRootPath = [System.IO.Path]::GetFullPath(
    (Split-Path $PSScriptRoot -Parent))
$applicationFixtureDirectory = Join-Path `
    $PSScriptRoot `
    'fixtures/ApplicationLifetimeConsumer'
$componentFixtureDirectory = Join-Path `
    $PSScriptRoot `
    'fixtures/ComponentLibraryConsumer'
$componentLibraryConsumerVersion = '1.0.0-w51'
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
    $sdkPackageFilter = 'Assimalign.Viu.Sdk.Browser.*.nupkg'
    $sdkPackagePrefix = 'Assimalign.Viu.Sdk.Browser.'
    $sdkDisplayName = 'Assimalign.Viu.Sdk.Browser'
    if ($ComponentLibraryOnly) {
        $sdkPackageFilter = 'Assimalign.Viu.Sdk.*.nupkg'
        $sdkPackagePrefix = 'Assimalign.Viu.Sdk.'
        $sdkDisplayName = 'Assimalign.Viu.Sdk'
    }

    $sdkPackages = @(
        Get-ChildItem `
            -LiteralPath $packageDirectoryPath `
            -Filter $sdkPackageFilter `
            -File)
    if ($ComponentLibraryOnly) {
        $sdkPackages = @(
            $sdkPackages |
                Where-Object {
                    $_.Name -notlike 'Assimalign.Viu.Sdk.Browser.*.nupkg'
                })
    }
    if ($sdkPackages.Count -ne 1) {
        throw "Expected exactly one $sdkDisplayName package, found $($sdkPackages.Count)."
    }

    $suffix = '.nupkg'
    $Version = $sdkPackages[0].Name.Substring(
        $sdkPackagePrefix.Length,
        $sdkPackages[0].Name.Length - $sdkPackagePrefix.Length - $suffix.Length)
}

$requiredViuPackages = @(
    "Assimalign.Viu.Sdk.$Version.nupkg",
    "Assimalign.Viu.App.Ref.$Version.nupkg"
)
if (-not $ComponentLibraryOnly) {
    $requiredViuPackages += @(
        "Assimalign.Viu.Sdk.Browser.$Version.nupkg",
        "Assimalign.Viu.App.Browser.Ref.$Version.nupkg",
        "Assimalign.Viu.App.Browser.Runtime.browser-wasm.$Version.nupkg"
    )
}
foreach ($requiredViuPackage in $requiredViuPackages) {
    $requiredViuPackagePath = Join-Path `
        $packageDirectoryPath `
        $requiredViuPackage
    if (-not [System.IO.File]::Exists($requiredViuPackagePath)) {
        throw "The package feed is missing $requiredViuPackage."
    }
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

Add-Type -AssemblyName System.IO.Compression.FileSystem

try {
    $null = New-Item -ItemType Directory -Path $temporaryRootPath
    $componentProjectDirectory = Join-Path `
        $temporaryRootPath `
        'ComponentLibraryConsumer'
    $applicationProjectDirectory = Join-Path `
        $temporaryRootPath `
        'ApplicationLifetimeConsumer'
    $componentPackageDirectory = Join-Path `
        $temporaryRootPath `
        'component-packages'
    foreach ($directory in @(
            $componentProjectDirectory,
            $applicationProjectDirectory,
            $componentPackageDirectory)) {
        $null = New-Item -ItemType Directory -Path $directory
    }

    Get-ChildItem -LiteralPath $componentFixtureDirectory -Force | ForEach-Object {
        Copy-Item `
            -LiteralPath $_.FullName `
            -Destination $componentProjectDirectory `
            -Recurse
    }
    Get-ChildItem -LiteralPath $applicationFixtureDirectory -Force | ForEach-Object {
        Copy-Item `
            -LiteralPath $_.FullName `
            -Destination $applicationProjectDirectory `
            -Recurse
    }

    # Stop MSBuild's upward Directory.Build.* search at the fixture boundary.
    # This keeps the validation package-only even though its safe scratch root
    # is deliberately inside the Viu repository.
    foreach ($buildFileName in @('Directory.Build.props', 'Directory.Build.targets')) {
        Set-Content `
            -LiteralPath (Join-Path $temporaryRootPath $buildFileName) `
            -Value '<Project />' `
            -Encoding utf8
    }

    $globalJson = @{
        'msbuild-sdks' = @{
            'Assimalign.Viu.Sdk' = $Version
            'Assimalign.Viu.Sdk.Browser' = $Version
        }
    } | ConvertTo-Json -Depth 3
    Set-Content `
        -LiteralPath (Join-Path $temporaryRootPath 'global.json') `
        -Value $globalJson `
        -Encoding utf8

    $escapedPackageDirectory =
        [System.Security.SecurityElement]::Escape($packageDirectoryPath)
    $escapedComponentPackageDirectory =
        [System.Security.SecurityElement]::Escape($componentPackageDirectory)
    $escapedExternalPackageSourceValue =
        [System.Security.SecurityElement]::Escape($externalPackageSourceValue)
    $nugetConfiguration = @"
<?xml version="1.0" encoding="utf-8"?>
<configuration>
  <packageSources>
    <clear />
    <add key="viu-local" value="$escapedPackageDirectory" />
    <add key="consumer-local" value="$escapedComponentPackageDirectory" />
    <add key="$externalPackageSourceKey" value="$escapedExternalPackageSourceValue" />
  </packageSources>
  <packageSourceMapping>
    <packageSource key="viu-local">
      <package pattern="Assimalign.Viu.*" />
    </packageSource>
    <packageSource key="consumer-local">
      <package pattern="ComponentLibraryConsumer" />
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

    $componentProject = Join-Path `
        $componentProjectDirectory `
        'ComponentLibraryConsumer.csproj'
    $componentBuildProperties = @(
        "-p:ViuConsumerVersion=$Version",
        "-p:ComponentLibraryConsumerVersion=$componentLibraryConsumerVersion",
        # This fixture validates Viu package topology, not NuGet's online
        # vulnerability service. Keeping the audit out of the isolated restore
        # also makes OfflinePackageDirectory a genuinely offline lane.
        '-p:NuGetAudit=false',
        # Some Visual Studio SDK installations omit the optional net10.0 prune
        # data. Use it when present, but do not make that host-install detail a
        # prerequisite for validating Viu's targeting pack.
        '-p:AllowMissingPrunePackageData=true'
    )
    & dotnet restore $componentProject @componentBuildProperties
    if ($LASTEXITCODE -ne 0) {
        throw "Restoring the component-library package consumer failed with exit code $LASTEXITCODE."
    }

    & dotnet build `
        $componentProject `
        --configuration Release `
        --no-restore `
        -warnaserror `
        @componentBuildProperties
    if ($LASTEXITCODE -ne 0) {
        throw "Building the component-library package consumer failed with exit code $LASTEXITCODE."
    }

    $componentAssetsPath = Join-Path `
        $componentProjectDirectory `
        'obj/project.assets.json'
    $componentAssets = [System.IO.File]::ReadAllText($componentAssetsPath)
    if ($componentAssets.IndexOf(
            'Assimalign.Viu.Browser',
            [System.StringComparison]::OrdinalIgnoreCase) -ge 0) {
        throw 'The base-SDK component library resolved Assimalign.Viu.Browser in its dependency closure.'
    }

    $preprocessedComponentProject = Join-Path `
        $componentProjectDirectory `
        'obj/ComponentLibraryConsumer.preprocessed.xml'
    & dotnet msbuild `
        $componentProject `
        -nologo `
        @componentBuildProperties `
        "-preprocess:$preprocessedComponentProject"
    if ($LASTEXITCODE -ne 0) {
        throw "Preprocessing the component-library package consumer failed with exit code $LASTEXITCODE."
    }

    $componentEvaluation = [System.IO.File]::ReadAllText(
        $preprocessedComponentProject)
    # Microsoft.NET.Sdk itself declares KnownWebAssemblySdkPack metadata for
    # every net10.0 project, so its preprocessed XML contains the pack name even
    # when the WebAssembly SDK is not imported. The Viu Browser identifiers are
    # the package-owned markers that distinguish an actual Browser import.
    foreach ($browserOnlyMarker in @(
            'Assimalign.Viu.Sdk.Browser',
            'Assimalign.Viu.App.Browser')) {
        if ($componentEvaluation.IndexOf(
                $browserOnlyMarker,
                [System.StringComparison]::OrdinalIgnoreCase) -ge 0) {
            $matchingEvaluationLine = @(
                $componentEvaluation -split '\r?\n' |
                    Where-Object {
                        $_.IndexOf(
                            $browserOnlyMarker,
                            [System.StringComparison]::OrdinalIgnoreCase) -ge 0
                    } |
                    Select-Object -First 1)
            throw "The base-SDK component library evaluation contains browser-only marker '$browserOnlyMarker': $matchingEvaluationLine"
        }
    }

    & dotnet pack `
        $componentProject `
        --configuration Release `
        --no-restore `
        -warnaserror `
        "-p:PackageOutputPath=$componentPackageDirectory" `
        @componentBuildProperties
    if ($LASTEXITCODE -ne 0) {
        throw "Packing the component-library package consumer failed with exit code $LASTEXITCODE."
    }

    $componentPackagePath = Join-Path `
        $componentPackageDirectory `
        "ComponentLibraryConsumer.$componentLibraryConsumerVersion.nupkg"
    if (-not [System.IO.File]::Exists($componentPackagePath)) {
        throw "The component-library package was not created: $componentPackagePath"
    }

    $componentArchive = [System.IO.Compression.ZipFile]::OpenRead(
        $componentPackagePath)
    try {
        $componentNuspecEntry = @(
            $componentArchive.Entries |
                Where-Object {
                    $_.FullName.EndsWith(
                        '.nuspec',
                        [System.StringComparison]::OrdinalIgnoreCase)
                })
        if ($componentNuspecEntry.Count -ne 1) {
            throw "The component-library package must contain exactly one nuspec, found $($componentNuspecEntry.Count)."
        }

        $componentNuspecReader = [System.IO.StreamReader]::new(
            $componentNuspecEntry[0].Open())
        try {
            $componentNuspec = $componentNuspecReader.ReadToEnd()
        }
        finally {
            $componentNuspecReader.Dispose()
        }
        if ($componentNuspec.IndexOf(
                'Assimalign.Viu.Browser',
                [System.StringComparison]::OrdinalIgnoreCase) -ge 0) {
            throw 'The packed component library declares a Browser dependency.'
        }

        $componentStyleEntries = @(
            $componentArchive.Entries |
                Where-Object {
                    $_.FullName.EndsWith(
                        '.viu.css',
                        [System.StringComparison]::OrdinalIgnoreCase)
                })
        if ($componentStyleEntries.Count -eq 0 -or
            @($componentStyleEntries | Where-Object Length -eq 0).Count -ne 0) {
            throw "The packed component library must carry non-empty .viu.css assets, found $($componentStyleEntries.Count)."
        }
    }
    finally {
        $componentArchive.Dispose()
    }

    Write-Host `
        'Component-library package consumer passed Build + Pack with a browser-free dependency closure.' `
        -ForegroundColor Green

    if ($ComponentLibraryOnly) {
        Write-Host `
            'Base-SDK validation completed without loading the Browser SDK/runtime chain.' `
            -ForegroundColor Cyan
        return
    }

    $project = Join-Path `
        $applicationProjectDirectory `
        'ApplicationLifetimeConsumer.csproj'
    $applicationBuildProperties = @(
        "-p:ViuConsumerVersion=$Version",
        "-p:ComponentLibraryConsumerVersion=$componentLibraryConsumerVersion",
        '-p:NuGetAudit=false',
        '-p:AllowMissingPrunePackageData=true'
    )
    & dotnet restore $project @applicationBuildProperties
    if ($LASTEXITCODE -ne 0) {
        throw "Restoring the application-lifetime package consumer failed with exit code $LASTEXITCODE."
    }

    & dotnet build `
        $project `
        --configuration Release `
        --no-restore `
        -warnaserror `
        @applicationBuildProperties
    if ($LASTEXITCODE -ne 0) {
        throw "Building the application-lifetime package consumer failed with exit code $LASTEXITCODE."
    }

    # [PKG-2] — the fixture deliberately references every standalone package
    # that overlaps the base and Browser App framework segments. Verify that
    # both targeting-pack manifests were consumed and that no matching lib/
    # asset survived conflict resolution.
    $expectedFrameworkPackageOverrides = @(
        'Assimalign.Viu.Components',
        'Assimalign.Viu.Reactivity',
        'Assimalign.Viu.State',
        'Assimalign.Viu.Core',
        'Assimalign.Viu.Browser'
    )
    $packageConflictOverridesPath = Join-Path `
        $applicationProjectDirectory `
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
            throw "The segmented App targeting packs did not register package override $expectedOverride."
        }
    }

    $resolvedPackageReferencesPath = Join-Path `
        $applicationProjectDirectory `
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
        @applicationBuildProperties `
        -p:PublishTrimmed=true `
        -p:TrimMode=full `
        -p:RunAOTCompilation=false
    if ($LASTEXITCODE -ne 0) {
        throw "PublishTrimmed failed for the application-lifetime package consumer with exit code $LASTEXITCODE."
    }

    $publishedComponentStylePath = Join-Path `
        $trimmedOutput `
        'wwwroot/_content/ComponentLibraryConsumer/ComponentLibraryConsumer.viu.css'
    if (-not [System.IO.File]::Exists($publishedComponentStylePath)) {
        throw "PublishTrimmed did not flow the packed component library stylesheet to its package-qualified static-web-asset path: $publishedComponentStylePath"
    }
    if ((Get-Item -LiteralPath $publishedComponentStylePath).Length -eq 0) {
        throw "PublishTrimmed emitted an empty packed component library stylesheet: $publishedComponentStylePath"
    }

    $publishedBrowserAssetPath = Join-Path `
        $trimmedOutput `
        'wwwroot/_content/Assimalign.Viu.Browser/viu-dom.js'
    if (-not [System.IO.File]::Exists($publishedBrowserAssetPath)) {
        throw "PublishTrimmed did not flow the Browser runtime asset to its framework-qualified static-web-asset path: $publishedBrowserAssetPath"
    }
    if ((Get-Item -LiteralPath $publishedBrowserAssetPath).Length -eq 0) {
        throw "PublishTrimmed emitted an empty Browser runtime asset: $publishedBrowserAssetPath"
    }

    & dotnet restore `
        $project `
        @applicationBuildProperties `
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
        @applicationBuildProperties `
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
    "Browser package consumer mounted the packed component and passed Build, PublishTrimmed, and PublishAot against Viu $Version." `
    -ForegroundColor Cyan
