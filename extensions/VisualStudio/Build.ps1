<#
.SYNOPSIS
    Builds and packages the Visual Studio extensions.

.DESCRIPTION
    This is the stable build entry point for the Visual Studio area. Each extension package owns
    its Visual Studio MSBuild invocation, language-server publishing, package validation, and output
    artifact. This script invokes those package builds explicitly so the independently shipped
    VSIXs remain independently buildable while the repository and CI retain one complete gate.
#>
[CmdletBinding()]
param(
    [ValidateSet('Debug', 'Release')]
    [string] $Configuration = 'Debug',

    [string] $Version
)

$ErrorActionPreference = 'Stop'

$repositoryDirectory = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\..'))
$artifactDirectory = Join-Path $repositoryDirectory `
    "_out\extensions\VisualStudio\$Configuration"

$extensions = @(
    [pscustomobject]@{
        Name = 'Assimalign.Viu.VisualStudio'
        Build = Join-Path $PSScriptRoot 'Assimalign.Viu.VisualStudio\Build.ps1'
        Artifact = Join-Path $artifactDirectory 'Assimalign.Viu.VisualStudio.vsix'
    },
    [pscustomobject]@{
        Name = 'Assimalign.Viu.VisualStudio.UtilityCss'
        Build = Join-Path $PSScriptRoot 'Assimalign.Viu.VisualStudio.UtilityCss\Build.ps1'
        Artifact = Join-Path $artifactDirectory 'Assimalign.Viu.VisualStudio.UtilityCss.vsix'
    }
)

$buildArguments = @{
    Configuration = $Configuration
}
if (-not [string]::IsNullOrWhiteSpace($Version)) {
    $buildArguments.Version = $Version
}

foreach ($extension in $extensions) {
    if (-not (Test-Path -LiteralPath $extension.Build)) {
        throw "The $($extension.Name) build script was not found at $($extension.Build)."
    }

    Write-Host "Building Visual Studio extension: $($extension.Name)"
    & $extension.Build @buildArguments

    if (-not (Test-Path -LiteralPath $extension.Artifact)) {
        throw "The $($extension.Name) build did not produce $($extension.Artifact)."
    }
}

Write-Host ''
Write-Host 'Visual Studio VSIX artifacts:'
foreach ($extension in $extensions) {
    Write-Host "  $($extension.Artifact)"
}
