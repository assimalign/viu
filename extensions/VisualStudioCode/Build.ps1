<#
.SYNOPSIS
    Builds the Visual Studio Code extension packages.

.DESCRIPTION
    This is the stable build entry point for the Visual Studio Code area. Each package owns its build
    implementation; this script invokes those implementations explicitly so adding a package does
    not depend on directory discovery or naming conventions.
#>
[CmdletBinding()]
param(
    [ValidateSet('Debug', 'Release')]
    [string] $Configuration = 'Debug',

    [string] $Version,

    [ValidateSet('win-x64', 'win-arm64', 'linux-x64', 'osx-arm64', 'osx-x64')]
    [string[]] $RuntimeIdentifier = @(),

    [switch] $SkipNodeBuild
)

$ErrorActionPreference = 'Stop'

$viuBuild = Join-Path $PSScriptRoot 'packages\viu\Build.ps1'

Write-Host 'Building Visual Studio Code package: viu'
& $viuBuild @PSBoundParameters
