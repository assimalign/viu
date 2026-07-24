<#
.SYNOPSIS
    Produces the complete, version-consistent Viu NuGet release set.

.DESCRIPTION
    Strictly packs every public Viu library plus the SDK, targeting pack, and
    browser-wasm runtime pack into _out/release/packages. The generated
    package-order.txt records dependency-safe publication order.

.PARAMETER Version
    The SemVer package version to produce, for example 10.0.1 or
    10.0.1-beta.42.

.PARAMETER Configuration
    Build configuration (default Release).
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [string] $Version,

    [ValidateSet('Debug', 'Release')]
    [string] $Configuration = 'Release'
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
        $_.Name -eq 'checksums.sha256' -or
        $_.Name -eq 'package-order.txt'
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

$libraryPackageIds = @(
    'Assimalign.Viu.Reactivity',
    'Assimalign.Viu.Shared',
    'Assimalign.Viu.Syntax',
    'Assimalign.Viu.Components',
    'Assimalign.Viu.Syntax.Css',
    'Assimalign.Viu.Syntax.Html',
    'Assimalign.Viu.Syntax.JavaScript',
    'Assimalign.Viu.Syntax.SingleFileComponent',
    'Assimalign.Viu.Syntax.Templates',
    'Assimalign.Viu.State',
    'Assimalign.Viu.Router',
    'Assimalign.Viu.Tooling.Css',
    'Assimalign.Viu.Core',
    'Assimalign.Viu.Browser',
    'Assimalign.Viu.ServerRenderer',
    'Assimalign.Viu.Testing',
    'Assimalign.Viu.Router.Browser'
)

$configuredLibraryProjects = @(
    $libraryPackageIds |
        ForEach-Object {
            Join-Path $repositoryDirectory "libraries/$_/src/$_.csproj"
        }
)
$discoveredLibraryProjects = @(
    Get-ChildItem `
        -LiteralPath (Join-Path $repositoryDirectory 'libraries') `
        -Directory |
        ForEach-Object {
            Get-ChildItem `
                -LiteralPath (Join-Path $_.FullName 'src') `
                -Filter '*.csproj' `
                -File `
                -ErrorAction SilentlyContinue
        } |
        ForEach-Object {
            [System.IO.Path]::GetFullPath($_.FullName)
        }
)
$projectDifference = @(
    Compare-Object `
        ($configuredLibraryProjects | Sort-Object) `
        ($discoveredLibraryProjects | Sort-Object)
)
if ($projectDifference.Count -ne 0) {
    throw "The release library inventory is incomplete: $($projectDifference | Out-String)"
}

function Invoke-PackageBuild {
    param(
        [Parameter(Mandatory)]
        [string] $Project,

        [string[]] $AdditionalArguments = @()
    )

    Write-Host "Packing $Project" -ForegroundColor Green
    & dotnet pack $Project `
        --configuration $Configuration `
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
    'frameworks/Assimalign.Viu.App.Runtime/src/Assimalign.Viu.App.Runtime.csproj'
Invoke-PackageBuild `
    -Project $runtimeProject `
    -AdditionalArguments @('-p:RuntimeIdentifier=browser-wasm')

$referenceProject = Join-Path $repositoryDirectory `
    'frameworks/Assimalign.Viu.App.Refs/src/Assimalign.Viu.App.Refs.csproj'
Invoke-PackageBuild -Project $referenceProject

$sdkProject = Join-Path $repositoryDirectory `
    'sdks/Assimalign.Viu.Sdk/Tasks/Assimalign.Viu.Sdk.Tasks.csproj'
Write-Host 'Cleaning the Viu SDK task closure' -ForegroundColor Green
& dotnet clean $sdkProject --configuration $Configuration
if ($LASTEXITCODE -ne 0) {
    throw "Cleaning $sdkProject failed with exit code $LASTEXITCODE."
}
Invoke-PackageBuild -Project $sdkProject

$packageIds = $libraryPackageIds + @(
    'Assimalign.Viu.App.Runtime.browser-wasm',
    'Assimalign.Viu.App.Ref',
    'Assimalign.Viu.Sdk'
)
$expectedPackageFiles = @(
    $packageIds |
        ForEach-Object {
            "$_.$Version.nupkg"
        }
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

$packageOrderPath = Join-Path $packageDirectory 'package-order.txt'
$expectedPackageFiles |
    Set-Content -LiteralPath $packageOrderPath -Encoding utf8

$checksumPath = Join-Path $packageDirectory 'checksums.sha256'
$checksumLines = foreach ($packageFile in $expectedPackageFiles) {
    $packagePath = Join-Path $packageDirectory $packageFile
    $packageHash = Get-FileHash -LiteralPath $packagePath -Algorithm SHA256
    "$($packageHash.Hash.ToLowerInvariant())  $packageFile"
}
$checksumLines |
    Set-Content -LiteralPath $checksumPath -Encoding ascii

Write-Host "Created $($expectedPackageFiles.Count) packages in $packageDirectory" -ForegroundColor Cyan
$expectedPackageFiles | ForEach-Object { Write-Host "  $_" }
