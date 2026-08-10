<#
.SYNOPSIS
    Produces the complete, version-consistent Viu NuGet release set.

.DESCRIPTION
    Strictly packs every public Viu library plus both SDKs, both targeting
    packs, the Browser browser-wasm runtime pack, and the dotnet-new template pack into
    _out/release/packages. The generated package-order.txt records
    dependency-safe publication order.

.PARAMETER Version
    The SemVer package version to produce, for example 10.0.1 or
    10.0.1-beta.42.

.PARAMETER Configuration
    Build configuration (default Release).

.PARAMETER PackageValidationBaselineVersion
    Optional previously published version used by the .NET SDK package-validation
    baseline. PublicAPI baselines and physical package validation always run.
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [string] $Version,

    [ValidateSet('Debug', 'Release')]
    [string] $Configuration = 'Release',

    [string] $PackageValidationBaselineVersion
)

$ErrorActionPreference = 'Stop'

$versionPattern = '^(?<major>0|[1-9][0-9]*)\.(?<minor>0|[1-9][0-9]*)\.(?<patch>0|[1-9][0-9]*)(?:-(?<suffix>[0-9A-Za-z-]+(?:\.[0-9A-Za-z-]+)*))?$'
$versionMatch = [System.Text.RegularExpressions.Regex]::Match(
    $Version,
    $versionPattern,
    [System.Text.RegularExpressions.RegexOptions]::CultureInvariant)
if (-not $versionMatch.Success) {
    throw "Version '$Version' must be SemVer in the supported form MAJOR.MINOR.PATCH[-PRERELEASE]."
}
if ($versionMatch.Groups['suffix'].Success) {
    foreach ($identifier in $versionMatch.Groups['suffix'].Value.Split('.')) {
        if ([string]::IsNullOrWhiteSpace($identifier)) {
            throw "Version '$Version' contains an empty prerelease identifier."
        }
        if ($identifier -match '^[0-9]+$' -and $identifier -notmatch '^(0|[1-9][0-9]*)$') {
            throw "Version '$Version' contains a numeric prerelease identifier with a leading zero."
        }
    }
}

$majorVersion = $versionMatch.Groups['major'].Value
$minorVersion = $versionMatch.Groups['minor'].Value
$patchCoreVersion = $versionMatch.Groups['patch'].Value
$versionSuffix = $versionMatch.Groups['suffix'].Value
$versionPrefix = "$majorVersion.$minorVersion.$patchCoreVersion"
$patchVersion = $patchCoreVersion
if (-not [string]::IsNullOrWhiteSpace($versionSuffix)) {
    $patchVersion = "$patchCoreVersion-$versionSuffix"
}

$repositoryDirectory = [System.IO.Path]::GetFullPath(
    (Split-Path $PSScriptRoot -Parent))

Import-Module (Join-Path $PSScriptRoot 'modules/ViuPackaging.psm1') -Force
$releaseOutputDirectory = [System.IO.Path]::GetFullPath(
    (Join-Path $repositoryDirectory '_out/release'))
$packageDirectory = [System.IO.Path]::GetFullPath(
    (Join-Path $releaseOutputDirectory 'packages'))
$releaseOutputPrefix = $releaseOutputDirectory.TrimEnd(
    [char[]]@(
        [System.IO.Path]::DirectorySeparatorChar,
        [System.IO.Path]::AltDirectorySeparatorChar)) +
    [System.IO.Path]::DirectorySeparatorChar
if (-not $packageDirectory.StartsWith(
        $releaseOutputPrefix,
        [System.StringComparison]::OrdinalIgnoreCase)) {
    throw "The release package directory must remain inside $releaseOutputDirectory."
}

New-Item -ItemType Directory -Path $packageDirectory -Force | Out-Null
Get-ChildItem -LiteralPath $packageDirectory -File |
    Where-Object {
        $_.Name.EndsWith('.nupkg', [System.StringComparison]::OrdinalIgnoreCase) -or
        $_.Name.EndsWith('.snupkg', [System.StringComparison]::OrdinalIgnoreCase) -or
        $_.Name -eq 'checksums.sha256' -or
        $_.Name -eq 'package-order.txt' -or
        $_.Name -eq 'symbol-package-order.txt'
    } |
    ForEach-Object {
        Remove-Item -LiteralPath $_.FullName -Force
    }

$repositoryCommit = $env:GITHUB_SHA
if ([string]::IsNullOrWhiteSpace($repositoryCommit)) {
    $repositoryCommit = (& git -C $repositoryDirectory rev-parse HEAD).Trim()
    if ($LASTEXITCODE -ne 0) {
        throw 'Resolving the repository commit failed.'
    }
}

$buildProperties = @(
    "-p:ViuMajorVersion=$majorVersion",
    "-p:ViuMinorVersion=$minorVersion",
    "-p:ViuPatchVersion=$patchVersion",
    "-p:ViuVersionPrefix=$versionPrefix",
    "-p:ViuVersionSuffix=$versionSuffix",
    "-p:ViuVersion=$Version",
    "-p:VersionPrefix=$versionPrefix",
    "-p:VersionSuffix=$versionSuffix",
    "-p:PackageVersion=$Version",
    '-p:ContinuousIntegrationBuild=true',
    "-p:RepositoryCommit=$repositoryCommit",
    "-p:PackageOutputPath=$packageDirectory"
)
if (-not [string]::IsNullOrWhiteSpace($PackageValidationBaselineVersion)) {
    $buildProperties += "-p:ViuPackageValidationBaselineVersion=$PackageValidationBaselineVersion"
}

# The library inventory and its drift guard live in the shared packaging module, so the
# release set and the local dogfooding feed (scripts/Install-Local.ps1) cannot disagree
# about what ships.
$libraryPackageIds = Get-ViuLibraryPackageId
$configuredLibraryProjects = Get-ViuLibraryProject -RepositoryDirectory $repositoryDirectory

function Invoke-PackageBuild {
    param(
        [Parameter(Mandatory)]
        [string] $Project,

        [string[]] $AdditionalArguments = @()
    )

    Write-Host "Packing $Project" -ForegroundColor Green
    & dotnet pack $Project `
        --configuration $Configuration `
        -warnaserror `
        @buildProperties `
        @AdditionalArguments
    if ($LASTEXITCODE -ne 0) {
        throw "Packing $Project failed with exit code $LASTEXITCODE."
    }
}

Write-Host "Packing Viu $Version from $repositoryCommit" -ForegroundColor Cyan
foreach ($libraryProject in $configuredLibraryProjects) {
    Invoke-PackageBuild -Project $libraryProject
}

$runtimeProject = Join-Path $repositoryDirectory `
    'frameworks/Assimalign.Viu.App.Browser.Runtime/src/Assimalign.Viu.App.Browser.Runtime.csproj'
Invoke-PackageBuild `
    -Project $runtimeProject `
    -AdditionalArguments @('-p:RuntimeIdentifier=browser-wasm')

$referenceProject = Join-Path $repositoryDirectory `
    'frameworks/Assimalign.Viu.App.Refs/src/Assimalign.Viu.App.Refs.csproj'
Invoke-PackageBuild -Project $referenceProject

$browserReferenceProject = Join-Path $repositoryDirectory `
    'frameworks/Assimalign.Viu.App.Browser.Refs/src/Assimalign.Viu.App.Browser.Refs.csproj'
Invoke-PackageBuild -Project $browserReferenceProject

$sdkProject = Join-Path $repositoryDirectory `
    'sdks/Assimalign.Viu.Sdk/Tasks/Assimalign.Viu.Sdk.Tasks.csproj'
Write-Host 'Cleaning the Viu SDK task closure' -ForegroundColor Green
& dotnet clean $sdkProject --configuration $Configuration
if ($LASTEXITCODE -ne 0) {
    throw "Cleaning $sdkProject failed with exit code $LASTEXITCODE."
}
Invoke-PackageBuild -Project $sdkProject

$browserSdkProject = Join-Path $repositoryDirectory `
    'sdks/Assimalign.Viu.Sdk.Browser/Tasks/Assimalign.Viu.Sdk.Browser.Tasks.csproj'
Write-Host 'Cleaning the Viu Browser SDK task closure' -ForegroundColor Green
& dotnet clean $browserSdkProject --configuration $Configuration
if ($LASTEXITCODE -ne 0) {
    throw "Cleaning $browserSdkProject failed with exit code $LASTEXITCODE."
}
Invoke-PackageBuild -Project $browserSdkProject

$templateProject = Join-Path $repositoryDirectory `
    'templates/Assimalign.Viu.Templates/Assimalign.Viu.Templates.csproj'
Invoke-PackageBuild -Project $templateProject

$packageIds = $libraryPackageIds + @(
    'Assimalign.Viu.App.Ref',
    'Assimalign.Viu.App.Browser.Runtime.browser-wasm',
    'Assimalign.Viu.App.Browser.Ref',
    'Assimalign.Viu.Sdk',
    'Assimalign.Viu.Sdk.Browser',
    'Assimalign.Viu.Templates'
)
$expectedPackageFiles = @(
    $packageIds |
        ForEach-Object {
            "$_.$Version.nupkg"
        }
)
$expectedSymbolPackageFiles = @(
    $packageIds |
        Where-Object {
            Test-ViuPackageRequiresSymbolPackage `
                -PackageId $_ `
                -PackagePath (Join-Path $packageDirectory "$($_).$Version.nupkg")
        } |
        ForEach-Object { "$_.$Version.snupkg" }
)
$actualPackageFiles = @(
    Get-ChildItem -LiteralPath $packageDirectory -Filter '*.nupkg' -File |
        ForEach-Object Name
)
$packageDifference = @(
    Compare-Object `
        ($expectedPackageFiles | Sort-Object) `
        ($actualPackageFiles | Sort-Object)
)
if ($packageDifference.Count -ne 0) {
    throw "The release package set is incomplete: $($packageDifference | Out-String)"
}
$actualSymbolPackageFiles = @(
    Get-ChildItem -LiteralPath $packageDirectory -Filter '*.snupkg' -File |
        ForEach-Object Name
)
$symbolPackageDifference = @(
    Compare-Object `
        ($expectedSymbolPackageFiles | Sort-Object) `
        ($actualSymbolPackageFiles | Sort-Object)
)
if ($symbolPackageDifference.Count -ne 0) {
    throw "The release symbol package set is incomplete: $($symbolPackageDifference | Out-String)"
}

$baseFrameworkAssemblies = @(
    'Assimalign.Viu.Reactivity',
    'Assimalign.Viu.Components',
    'Assimalign.Viu.State',
    'Assimalign.Viu.Core'
)
$baseFrameworkPackageOverrides = @(
    'Assimalign.Viu.Reactivity',
    'Assimalign.Viu.Components',
    'Assimalign.Viu.State',
    'Assimalign.Viu.Core'
)
$browserFrameworkAssemblies = @('Assimalign.Viu.Browser')

Assert-ViuFrameworkPackage `
    -PackagePath (Join-Path $packageDirectory "Assimalign.Viu.App.Ref.$Version.nupkg") `
    -ManifestPath 'data/FrameworkList.xml' `
    -ExpectedFrameworkAssembly $baseFrameworkAssemblies `
    -ExpectedPackageOverride $baseFrameworkPackageOverrides
Assert-ViuFrameworkPackage `
    -PackagePath (Join-Path $packageDirectory "Assimalign.Viu.App.Browser.Ref.$Version.nupkg") `
    -ManifestPath 'data/FrameworkList.xml' `
    -ExpectedFrameworkAssembly $browserFrameworkAssemblies `
    -ExpectedPackageOverride $browserFrameworkAssemblies
Assert-ViuFrameworkPackage `
    -PackagePath (Join-Path $packageDirectory "Assimalign.Viu.App.Browser.Runtime.browser-wasm.$Version.nupkg") `
    -ManifestPath 'data/RuntimeList.xml' `
    -ExpectedFrameworkAssembly @($baseFrameworkAssemblies + $browserFrameworkAssemblies)
Write-Host 'Validated base and Browser framework package manifests.' -ForegroundColor Green

Assert-ViuReleasePackageSet `
    -PackageDirectory $packageDirectory `
    -Version $Version `
    -ExpectedPackageId $packageIds

$packageOrderPath = Join-Path $packageDirectory 'package-order.txt'
$expectedPackageFiles |
    Set-Content -LiteralPath $packageOrderPath -Encoding utf8

$symbolPackageOrderPath = Join-Path $packageDirectory 'symbol-package-order.txt'
$expectedSymbolPackageFiles |
    Set-Content -LiteralPath $symbolPackageOrderPath -Encoding utf8

$checksumPath = Join-Path $packageDirectory 'checksums.sha256'
$checksumLines = foreach ($packageFile in @($expectedPackageFiles + $expectedSymbolPackageFiles)) {
    $packagePath = Join-Path $packageDirectory $packageFile
    $packageHash = Get-FileHash -LiteralPath $packagePath -Algorithm SHA256
    "$($packageHash.Hash.ToLowerInvariant())  $packageFile"
}
$checksumLines |
    Set-Content -LiteralPath $checksumPath -Encoding ascii

Write-Host "Created $($expectedPackageFiles.Count) packages and $($expectedSymbolPackageFiles.Count) symbol packages in $packageDirectory" -ForegroundColor Cyan
$expectedPackageFiles | ForEach-Object { Write-Host "  $_" }
$expectedSymbolPackageFiles | ForEach-Object { Write-Host "  $_" }
