<#
.SYNOPSIS
    Packs the complete Viu package set — every independently published library plus
    the base/Browser SDK and shared-framework chains — into the repo-local NuGet
    feed (_out/packages), exercising both targeting-only component-library
    consumption and Browser application runtime-pack resolution end-to-end.
    ([V01.01.12.19], #174.)

.DESCRIPTION
    The library set and the cache-discovery logic come from
    scripts/modules/ViuPackaging.psm1, shared with scripts/Pack-Release.ps1, so the
    local feed and the release set cannot disagree about what ships.

    Because local packs reuse the same version, cached extracts are pruned first.
    Pruning covers the machine-global cache AND any repo-local globalPackagesFolder
    a consumer declares in its own nuget.config — the sibling viu-examples repository
    does exactly that, and a prune that missed it left consumers building against a
    previous build's analyzer while the feed looked current.

.PARAMETER Rids
    Runtime identifiers to produce runtime packs for. browser-wasm is the
    shipping RID for Viu apps.

.PARAMETER SkipLibraries
    Skip the independently published libraries.

.PARAMETER SkipSdk
    Skip both SDK packages.

.PARAMETER SkipFramework
    Skip both targeting packs and the Browser runtime packs.

.PARAMETER ConsumerRoot
    Extra directories to search for a consumer nuget.config, for consumers outside
    the repository's sibling set.

.PARAMETER SkipCachePrune
    Leave cached package extracts alone. Only safe when the version was bumped.

.PARAMETER BaseOnly
    Pack only the host-neutral SDK/framework path (plus standalone libraries).
    This path does not require the WebAssembly workload.

.PARAMETER Configuration
    Build configuration (default Release).
#>
[CmdletBinding()]
param(
    [string[]] $Rids = @('browser-wasm'),
    [switch] $SkipLibraries,
    [switch] $SkipSdk,
    [switch] $SkipFramework,
    [string[]] $ConsumerRoot = @(),
    [switch] $SkipCachePrune,
    [switch] $BaseOnly,
    [string] $Configuration = 'Release'
)

$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path $PSScriptRoot -Parent
$feed = Join-Path $repoRoot '_out\packages'

Import-Module (Join-Path $PSScriptRoot 'modules\ViuPackaging.psm1') -Force

# Resolve the version the build will stamp, straight from the MSBuild props
# so PowerShell can never drift from MSBuild.
$tfLatest = ([xml](Get-Content (Join-Path $repoRoot 'build\Targets\Build.TargetFramework.props'))).Project.PropertyGroup.TargetFrameworkLatest |
    Where-Object { $_ } | Select-Object -First 1
$versionProps = ([xml](Get-Content (Join-Path $repoRoot 'build\Targets\Build.Version.props'))).Project.PropertyGroup
$major = ([version]($tfLatest.Trim() -replace '^net', '')).Major
$minor = ($versionProps.ViuMinorVersion | Where-Object { $_ } | Select-Object -First 1).Trim()
$patch = ($versionProps.ViuPatchVersion | Where-Object { $_ } | Select-Object -First 1).Trim()
$viuVersion = "$major.$minor.$patch"
Write-Host "Viu version: $viuVersion -> feed $feed" -ForegroundColor Cyan

# Same-version repack workaround: prune cached package extracts so NuGet
# re-extracts fresh content instead of serving a stale same-version copy.
if (-not $SkipCachePrune) {
    $cacheRoots = Get-ViuPackageCacheRoot `
        -RepositoryDirectory $repoRoot `
        -AdditionalRoot $ConsumerRoot
    Write-Host "Pruning cached extracts from $($cacheRoots.Count) cache root(s):" -ForegroundColor Green
    foreach ($root in $cacheRoots) {
        Write-Host "  root $root"
    }

    $pruned = Clear-ViuPackageCache `
        -CacheRoot $cacheRoots `
        -PackageId (Get-ViuPackageId -Rids $Rids)
    Write-Host "  $pruned extract(s) pruned"
}

New-Item -ItemType Directory -Force $feed | Out-Null

function Invoke-ViuPack {
    param(
        [Parameter(Mandatory)] [string] $Project,
        [string[]] $AdditionalArguments = @()
    )

    dotnet pack $Project `
        --configuration $Configuration `
        -p:PackageOutputPath=$feed `
        @AdditionalArguments
    if ($LASTEXITCODE -ne 0) {
        throw "Packing $Project failed with exit code $LASTEXITCODE."
    }
}

if (-not $SkipLibraries) {
    # Get-ViuLibraryProject carries the drift guard: a packable library added under
    # either code root — libraries/ or tooling/ — but missing from the shared
    # inventory fails here rather than silently producing an incomplete feed.
    $libraryProjects = Get-ViuLibraryProject -RepositoryDirectory $repoRoot
    $index = 0
    foreach ($libraryProject in $libraryProjects) {
        $index++
        $name = [System.IO.Path]::GetFileNameWithoutExtension($libraryProject)
        Write-Host "[libraries $index/$($libraryProjects.Count)] Packing $name" -ForegroundColor Green
        Invoke-ViuPack -Project $libraryProject
    }
}

if (-not $SkipSdk) {
    Write-Host '[sdk] Packing Assimalign.Viu.Sdk' -ForegroundColor Green
    $sdkProject = Join-Path $repoRoot 'sdks\Assimalign.Viu.Sdk\Tasks\Assimalign.Viu.Sdk.Tasks.csproj'
    # CollectSdkTaskFiles intentionally packages the complete task output
    # closure. Clean first so a same-worktree rename cannot leave an obsolete
    # assembly in the SDK package.
    dotnet clean $sdkProject --configuration $Configuration
    if ($LASTEXITCODE -ne 0) { throw 'SDK clean failed.' }

    Invoke-ViuPack -Project $sdkProject

    if (-not $BaseOnly) {
        Write-Host '[sdk] Packing Assimalign.Viu.Sdk.Browser' -ForegroundColor Green
        $browserSdkProject = Join-Path $repoRoot 'sdks\Assimalign.Viu.Sdk.Browser\Tasks\Assimalign.Viu.Sdk.Browser.Tasks.csproj'
        dotnet clean $browserSdkProject --configuration $Configuration
        if ($LASTEXITCODE -ne 0) { throw 'Browser SDK clean failed.' }

        Invoke-ViuPack -Project $browserSdkProject
    }
}

if (-not $SkipFramework) {
    if (-not $BaseOnly) {
        foreach ($rid in $Rids) {
            Write-Host "[framework] Packing Assimalign.Viu.App.Browser.Runtime.$rid" -ForegroundColor Green
            Invoke-ViuPack `
                -Project (Join-Path $repoRoot 'frameworks\Assimalign.Viu.App.Browser.Runtime\src\Assimalign.Viu.App.Browser.Runtime.csproj') `
                -AdditionalArguments @("-p:RuntimeIdentifier=$rid")
        }
    }

    Write-Host '[framework] Packing Assimalign.Viu.App.Ref' -ForegroundColor Green
    Invoke-ViuPack `
        -Project (Join-Path $repoRoot 'frameworks\Assimalign.Viu.App.Refs\src\Assimalign.Viu.App.Refs.csproj')

    if (-not $BaseOnly) {
        Write-Host '[framework] Packing Assimalign.Viu.App.Browser.Ref' -ForegroundColor Green
        Invoke-ViuPack `
            -Project (Join-Path $repoRoot 'frameworks\Assimalign.Viu.App.Browser.Refs\src\Assimalign.Viu.App.Browser.Refs.csproj')
    }
}

Write-Host "Done. Packages in $feed :" -ForegroundColor Cyan
Get-ChildItem $feed -Filter "Assimalign.Viu.*$viuVersion.nupkg" |
    Sort-Object Name |
    ForEach-Object { Write-Host "  $($_.Name)" }
