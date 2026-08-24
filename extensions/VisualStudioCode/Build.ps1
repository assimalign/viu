<#
.SYNOPSIS
    Builds and packages the Visual Studio Code extensions.

.DESCRIPTION
    This is the stable build entry point for the Visual Studio Code area. Each package owns server
    publishing, payload staging, and client compilation. This script invokes those package builds
    explicitly, then creates one platform-specific VSIX per package and runtime identifier.

    Use SkipVsix to retain the package-build-only workflow. In that mode the runtime-identifier
    arguments are forwarded unchanged, so an empty set stages every supported payload exactly as a
    direct package Build.ps1 invocation would. PackageName narrows orchestration to one extension
    for release-matrix isolation. PreRelease marks the generated VSIX for the Marketplace
    prerelease channel; Version still supplies its numeric three-component manifest version.
#>
[CmdletBinding()]
param(
    [ValidateSet('Debug', 'Release')]
    [string] $Configuration = 'Debug',

    [string] $Version,

    [ValidateSet('viu', 'viu-utilitycss')]
    [string[]] $PackageName = @(),

    [ValidateSet('win-x64', 'win-arm64', 'linux-x64', 'osx-arm64', 'osx-x64')]
    [string[]] $RuntimeIdentifier = @(),

    [switch] $SkipNodeBuild,

    [switch] $PreRelease,

    [switch] $SkipVsix
)

$ErrorActionPreference = 'Stop'

$repositoryDirectory = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\..'))
$runtimeIdentifierSourceProject = Join-Path $repositoryDirectory `
    'libraries\Utilities\Assimalign.Viu.UtilityCss.LanguageServer\src\Assimalign.Viu.UtilityCss.LanguageServer.csproj'
$artifactDirectory = Join-Path $repositoryDirectory `
    "_out\extensions\VisualStudioCode\$Configuration\Vsix"

$packages = @(
    [pscustomobject]@{
        Name = 'viu'
        Directory = Join-Path $PSScriptRoot 'packages\viu'
        Build = Join-Path $PSScriptRoot 'packages\viu\Build.ps1'
    },
    [pscustomobject]@{
        Name = 'viu-utilitycss'
        Directory = Join-Path $PSScriptRoot 'packages\viu-utilitycss'
        Build = Join-Path $PSScriptRoot 'packages\viu-utilitycss\Build.ps1'
    }
)

if ($PackageName.Count -gt 0) {
    $packages = @($packages | Where-Object { $PackageName -contains $_.Name })
}

$visualStudioCodeTargetByRuntimeIdentifier = @{
    'win-x64' = 'win32-x64'
    'win-arm64' = 'win32-arm64'
    'linux-x64' = 'linux-x64'
    'osx-arm64' = 'darwin-arm64'
    'osx-x64' = 'darwin-x64'
}

function Get-PackageBuildParameter {
    param(
        [string[]] $RequestedRuntimeIdentifier,
        [bool] $SkipClientBuild
    )

    $arguments = @{
        Configuration = $Configuration
        RuntimeIdentifier = $RequestedRuntimeIdentifier
    }

    if (-not [string]::IsNullOrWhiteSpace($Version)) {
        $arguments.Version = $Version
    }

    if ($SkipClientBuild) {
        $arguments.SkipNodeBuild = $true
    }

    return $arguments
}

if ($SkipNodeBuild -and -not $SkipVsix) {
    # `vsce package` always invokes vscode:prepublish, which compiles the TypeScript client. Keep
    # the established SkipNodeBuild contract truthful by retaining the former package-build-only
    # behavior when this switch is present.
    Write-Host 'SkipNodeBuild also skips VSIX packaging because vsce runs the client prepublish script.'
    $SkipVsix = $true
}

if ($SkipVsix) {
    foreach ($package in $packages) {
        Write-Host "Building Visual Studio Code package: $($package.Name)"
        $packageBuildArguments = Get-PackageBuildParameter `
            -RequestedRuntimeIdentifier $RuntimeIdentifier `
            -SkipClientBuild $SkipNodeBuild
        & $package.Build @packageBuildArguments
    }

    Write-Host 'Skipping VSIX packaging.'
    return
}

if ($RuntimeIdentifier.Count -gt 0) {
    $runtimeIdentifiersToPackage = $RuntimeIdentifier
}
else {
    # The supported set remains centralized in Build.LanguageServer.targets. The package clients
    # duplicate the set only because they must resolve a payload before MSBuild is available.
    $runtimeIdentifiersToPackage = (
        & dotnet msbuild $runtimeIdentifierSourceProject `
            -nologo `
            -getProperty:ViuLanguageServerAllRuntimeIdentifiers) -split ';' |
        Where-Object { -not [string]::IsNullOrWhiteSpace($_) } |
        ForEach-Object { $_.Trim() }
    if ($LASTEXITCODE -ne 0 -or $runtimeIdentifiersToPackage.Count -eq 0) {
        throw 'Could not read ViuLanguageServerAllRuntimeIdentifiers from the shared language-server target.'
    }
}

New-Item -ItemType Directory -Path $artifactDirectory -Force | Out-Null
$artifacts = @()

foreach ($package in $packages) {
    $packageManifestPath = Join-Path $package.Directory 'package.json'
    $packageManifest = Get-Content -LiteralPath $packageManifestPath -Raw | ConvertFrom-Json
    $packageVersion = $packageManifest.version
    $visualStudioCodePackageVersionArguments = @()
    if (-not [string]::IsNullOrWhiteSpace($Version)) {
        $packageVersion = $Version
        $visualStudioCodePackageVersionArguments = @(
            $Version,
            '--no-update-package-json'
        )
    }
    $visualStudioCodePreReleaseArguments = @()
    if ($PreRelease) {
        $visualStudioCodePreReleaseArguments = @('--pre-release')
    }
    $compileClient = -not $SkipNodeBuild

    foreach ($runtimeIdentifierName in $runtimeIdentifiersToPackage) {
        Write-Host "Building Visual Studio Code package: $($package.Name) ($runtimeIdentifierName)"

        # Build.ps1 replaces server/ on every invocation. Building one runtime at a time is what
        # guarantees that a platform-specific VSIX cannot accidentally carry another platform's
        # payload. The package build runs npm install only on its first pass; `vsce` still runs the
        # package's lightweight prepublish compile before each platform artifact.
        $packageBuildArguments = Get-PackageBuildParameter `
            -RequestedRuntimeIdentifier @($runtimeIdentifierName) `
            -SkipClientBuild (-not $compileClient)
        & $package.Build @packageBuildArguments
        $compileClient = $false

        $visualStudioCodeTarget = $visualStudioCodeTargetByRuntimeIdentifier[$runtimeIdentifierName]
        if ([string]::IsNullOrWhiteSpace($visualStudioCodeTarget)) {
            throw "No Visual Studio Code target is mapped for runtime identifier '$runtimeIdentifierName'."
        }

        $artifactPath = Join-Path $artifactDirectory `
            "$($package.Name)-$packageVersion-$visualStudioCodeTarget.vsix"

        Write-Host "Packaging $($package.Name) for $visualStudioCodeTarget"
        Push-Location -LiteralPath $package.Directory
        try {
            & npx --no-install vsce package `
                @visualStudioCodePackageVersionArguments `
                @visualStudioCodePreReleaseArguments `
                --target $visualStudioCodeTarget `
                --out $artifactPath
            if ($LASTEXITCODE -ne 0) {
                throw "Packaging $($package.Name) for $visualStudioCodeTarget failed with exit code $LASTEXITCODE."
            }
        }
        finally {
            Pop-Location
        }

        $artifacts += $artifactPath
    }
}

Write-Host ''
Write-Host 'Visual Studio Code VSIX artifacts:'
foreach ($artifact in $artifacts) {
    Write-Host "  $artifact"
}
