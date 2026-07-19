<#
.SYNOPSIS
    Packs the Viu SDK + shared-framework chain into the repo-local NuGet feed
    (_out/packages), exercising SDK resolution -> framework reference ->
    targeting pack -> runtime pack end-to-end for local consumers.
    ([V01.01.12.19], #174 - the Viu port of cohesion's installer/scripts/Install-Local.ps1.)

.PARAMETER Rids
    Runtime identifiers to produce runtime packs for. browser-wasm is the
    shipping RID for Viu apps.

.PARAMETER SkipSdk
    Re-pack the framework only.

.PARAMETER SkipFramework
    Re-pack the SDK only.

.PARAMETER Configuration
    Build configuration (default Release).
#>
[CmdletBinding()]
param(
    [string[]] $Rids = @('browser-wasm'),
    [switch] $SkipSdk,
    [switch] $SkipFramework,
    [string] $Configuration = 'Release'
)

$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path $PSScriptRoot -Parent
$feed = Join-Path $repoRoot '_out\packages'

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
$packageIds = @('assimalign.viu.sdk', 'assimalign.viu.app.ref') + ($Rids | ForEach-Object { "assimalign.viu.app.runtime.$_" })
$nugetCache = Join-Path $HOME '.nuget\packages'
foreach ($id in $packageIds) {
    $extract = Join-Path $nugetCache $id
    if (Test-Path $extract) {
        Write-Host "Pruning cached extract: $extract"
        Remove-Item -Recurse -Force $extract
    }
}

New-Item -ItemType Directory -Force $feed | Out-Null

if (-not $SkipSdk) {
    Write-Host "[1/3] Packing Assimalign.Viu.Sdk" -ForegroundColor Green
    dotnet pack (Join-Path $repoRoot 'sdks\Assimalign.Viu.Sdk\Tasks\Assimalign.Viu.Sdk.Tasks.csproj') `
        --configuration $Configuration -p:PackageOutputPath=$feed
    if ($LASTEXITCODE -ne 0) { throw 'SDK pack failed.' }
}

if (-not $SkipFramework) {
    foreach ($rid in $Rids) {
        Write-Host "[2/3] Packing Assimalign.Viu.App.Runtime.$rid" -ForegroundColor Green
        dotnet pack (Join-Path $repoRoot 'frameworks\Assimalign.Viu.App.Runtime\src\Assimalign.Viu.App.Runtime.csproj') `
            --configuration $Configuration -p:RuntimeIdentifier=$rid -p:PackageOutputPath=$feed
        if ($LASTEXITCODE -ne 0) { throw "Runtime pack failed for $rid." }
    }

    Write-Host "[3/3] Packing Assimalign.Viu.App.Ref" -ForegroundColor Green
    dotnet pack (Join-Path $repoRoot 'frameworks\Assimalign.Viu.App.Refs\src\Assimalign.Viu.App.Refs.csproj') `
        --configuration $Configuration -p:PackageOutputPath=$feed
    if ($LASTEXITCODE -ne 0) { throw 'Ref pack failed.' }
}

Write-Host "Done. Packages in $feed :" -ForegroundColor Cyan
Get-ChildItem $feed -Filter "Assimalign.Viu.*$viuVersion.nupkg" | ForEach-Object { Write-Host "  $($_.Name)" }
