<#
.SYNOPSIS
    Builds Assimalign.Viu.VisualStudio.Registration, the companion VSIX whose only payload is the
    .pkgdef claiming the .viu file extension for the Source Code (Text) Editor.

.DESCRIPTION
    The companion is a classic VSSDK package. Microsoft.VSSDK.BuildTools ships .NET Framework
    MSBuild tasks and is not usable from `dotnet build`, so this script is separate from Build.ps1
    (which stays runnable anywhere the .NET SDK is) and locates Visual Studio's MSBuild through
    vswhere instead.
#>
[CmdletBinding()]
param(
    [ValidateSet('Debug', 'Release')]
    [string] $Configuration = 'Debug',

    [string] $Version
)

$ErrorActionPreference = 'Stop'

$versionBuildArguments = @()
if (-not [string]::IsNullOrWhiteSpace($Version)) {
    if ($Version -notmatch '^(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)(?:\.(0|[1-9][0-9]*))?$') {
        throw "The Visual Studio extension version must contain three or four numeric components, for example 10.0.1 or 10.0.1.42."
    }

    foreach ($versionSegment in $Version.Split('.')) {
        $versionSegmentValue = [int]::Parse(
            $versionSegment,
            [System.Globalization.CultureInfo]::InvariantCulture)
        if ($versionSegmentValue -gt 65534) {
            throw "Visual Studio extension version components cannot exceed 65534."
        }
    }

    # The manifest reads its Identity version from the built assembly through the VSSDK
    # GetVsixVersion token, so the ordinary .NET version properties drive the VSIX version too.
    $versionBuildArguments = @(
        "-p:VersionPrefix=$Version",
        '-p:VersionSuffix='
    )
    Write-Host "Registration extension version: $Version"
}

$visualStudioDirectory = $PSScriptRoot
$repositoryDirectory = [System.IO.Path]::GetFullPath(
    (Join-Path $visualStudioDirectory '..\..'))
$registrationProject = Join-Path $visualStudioDirectory `
    'Assimalign.Viu.VisualStudio.Registration\src\Assimalign.Viu.VisualStudio.Registration.csproj'
$extensionOutputDirectory = Join-Path $repositoryDirectory `
    "_out\extensions\VisualStudio\$Configuration"

# #region: Locate Visual Studio's MSBuild
$visualStudioInstaller = Join-Path ${env:ProgramFiles(x86)} 'Microsoft Visual Studio\Installer\vswhere.exe'
if (-not (Test-Path -LiteralPath $visualStudioInstaller)) {
    throw "vswhere.exe was not found at $visualStudioInstaller. Install Visual Studio with the 'Visual Studio extension development' workload."
}

function Find-VisualStudioInstallation {
    param([switch] $IncludePrerelease)

    $arguments = @(
        '-latest',
        '-products', '*',
        '-requires', 'Microsoft.Component.MSBuild',
        '-property', 'installationPath')
    if ($IncludePrerelease) {
        $arguments += '-prerelease'
    }

    $installationPath = & $visualStudioInstaller @arguments
    if ($LASTEXITCODE -ne 0) {
        throw "vswhere.exe failed with exit code $LASTEXITCODE."
    }

    return ($installationPath | Select-Object -First 1)
}

# A released instance is preferred; a preview-only machine still builds.
$visualStudioInstallation = Find-VisualStudioInstallation
if ([string]::IsNullOrWhiteSpace($visualStudioInstallation)) {
    $visualStudioInstallation = Find-VisualStudioInstallation -IncludePrerelease
}
if ([string]::IsNullOrWhiteSpace($visualStudioInstallation)) {
    throw "No Visual Studio installation with MSBuild was found. Install the 'Visual Studio extension development' workload."
}

# Prefer the MSBuild matching this machine's architecture; the 32-bit host is the last resort.
$msbuildCandidates = switch ($env:PROCESSOR_ARCHITECTURE) {
    'ARM64' { @('MSBuild\Current\Bin\arm64\MSBuild.exe', 'MSBuild\Current\Bin\amd64\MSBuild.exe', 'MSBuild\Current\Bin\MSBuild.exe') }
    'AMD64' { @('MSBuild\Current\Bin\amd64\MSBuild.exe', 'MSBuild\Current\Bin\MSBuild.exe') }
    default { @('MSBuild\Current\Bin\MSBuild.exe') }
}

$msbuild = $null
foreach ($msbuildCandidate in $msbuildCandidates) {
    $msbuildCandidatePath = Join-Path $visualStudioInstallation $msbuildCandidate
    if (Test-Path -LiteralPath $msbuildCandidatePath) {
        $msbuild = $msbuildCandidatePath
        break
    }
}
if ($null -eq $msbuild) {
    throw "MSBuild.exe was not found under $visualStudioInstallation."
}

Write-Host "Visual Studio installation: $visualStudioInstallation"
Write-Host "MSBuild: $msbuild"
# #endregion

Write-Host 'Building the Viu registration extension'
& $msbuild $registrationProject `
    -restore `
    "-p:Configuration=$Configuration" `
    -nologo `
    -verbosity:minimal `
    @versionBuildArguments
if ($LASTEXITCODE -ne 0) {
    throw "Building the Viu registration extension failed with exit code $LASTEXITCODE."
}

$registrationBinDirectory = Join-Path `
    (Split-Path -Parent $registrationProject) `
    "bin\$Configuration"
$registrationPackages = @(
    Get-ChildItem `
        -LiteralPath $registrationBinDirectory `
        -Filter 'Assimalign.Viu.VisualStudio.Registration.vsix' `
        -File `
        -Recurse
)
if ($registrationPackages.Count -ne 1) {
    throw "Expected exactly one Assimalign.Viu.VisualStudio.Registration.vsix under $registrationBinDirectory, found $($registrationPackages.Count)."
}

if (-not (Test-Path -LiteralPath $extensionOutputDirectory)) {
    New-Item -ItemType Directory -Path $extensionOutputDirectory | Out-Null
}

$packagedRegistration = Join-Path $extensionOutputDirectory `
    'Assimalign.Viu.VisualStudio.Registration.vsix'
Copy-Item -LiteralPath $registrationPackages[0].FullName -Destination $packagedRegistration -Force

Write-Host "Viu registration extension: $packagedRegistration"
