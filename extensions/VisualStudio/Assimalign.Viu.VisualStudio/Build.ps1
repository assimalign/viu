<#
.SYNOPSIS
    Builds Assimalign.Viu.VisualStudio, the Viu Visual Studio extension, and its language-server
    payload.

.DESCRIPTION
    The extension is a classic in-process VSSDK package, so packaging it needs Visual Studio's
    MSBuild: Microsoft.VSSDK.BuildTools ships .NET Framework MSBuild tasks that cannot load under
    `dotnet build`. This script locates that MSBuild through vswhere. It is the independently runnable
    build entry point for this VSIX; extensions\VisualStudio\Build.ps1 orchestrates it together with
    the other Visual Studio extension packages.

    The language-server payload is produced first, by driving the shared ViuPublishLanguageServer
    target through the .NET CLI. That target shells out to `dotnet publish`, so running it under the
    .NET CLI rather than nested inside Visual Studio's MSBuild keeps the SDK resolution
    straightforward. The packaging pass then sets ViuSkipLanguageServerPublish so the in-build hook
    does not publish a second time; the absence, staleness, and runtime-identifier backstops in
    build\Targets\Build.LanguageServer.targets all still run.

    The publish recipe itself is never restated here. It lives in that shared target so this script
    and an in-IDE F5 cannot drift apart, and the language-server project is resolved from
    ViuLanguageServerProjectPath rather than named a second time.
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

    # The manifest reads its Identity version through the VSSDK GetVsixVersion token, which the
    # project answers with VersionPrefix, so the ordinary .NET version properties drive the VSIX
    # version too.
    $versionBuildArguments = @(
        "-p:VersionPrefix=$Version",
        '-p:VersionSuffix='
    )
    Write-Host "Visual Studio extension version: $Version"
}

$extensionDirectory = $PSScriptRoot
$repositoryDirectory = [System.IO.Path]::GetFullPath(
    (Join-Path $extensionDirectory '..\..\..'))
$extensionProject = Join-Path $extensionDirectory `
    'src\Assimalign.Viu.VisualStudio.csproj'
$extensionOutputDirectory = Join-Path $repositoryDirectory `
    "_out\extensions\VisualStudio\$Configuration"
$languageServerPublishDirectory = Join-Path $extensionOutputDirectory `
    'LanguageServer'
$normalizedExtensionOutputDirectory =
    [System.IO.Path]::GetFullPath($extensionOutputDirectory)
$normalizedLanguageServerPublishDirectory =
    [System.IO.Path]::GetFullPath($languageServerPublishDirectory)
$normalizedExtensionOutputPrefix =
    $normalizedExtensionOutputDirectory.TrimEnd(
        [char[]]@(
            [System.IO.Path]::DirectorySeparatorChar,
            [System.IO.Path]::AltDirectorySeparatorChar)) +
    [System.IO.Path]::DirectorySeparatorChar
if (-not $normalizedLanguageServerPublishDirectory.StartsWith(
        $normalizedExtensionOutputPrefix,
        [System.StringComparison]::OrdinalIgnoreCase)) {
    throw "The language-server publish directory must remain inside $normalizedExtensionOutputDirectory."
}

# #region: Locate Visual Studio's MSBuild
# Done before the language-server publish so a machine without the tooling fails in seconds rather
# than after a multi-minute self-contained publish.
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

if (Test-Path -LiteralPath $normalizedLanguageServerPublishDirectory) {
    Remove-Item -LiteralPath $normalizedLanguageServerPublishDirectory -Recurse -Force
}

Write-Host 'Publishing the Viu language server through the shared MSBuild target'
& dotnet msbuild $extensionProject `
    -target:ViuPublishLanguageServer `
    "-p:Configuration=$Configuration" `
    "-p:ViuLanguageServerPublishPath=$normalizedLanguageServerPublishDirectory" `
    -nologo `
    @versionBuildArguments
if ($LASTEXITCODE -ne 0) {
    throw "Publishing the Viu language server failed with exit code $LASTEXITCODE."
}

# Rebuild rather than Build: the VSIX is assembled from whatever sits in the output directory, so a
# stale artifact from an earlier configuration must not survive into the package.
Write-Host 'Building and packaging the Visual Studio extension'
& $msbuild $extensionProject `
    -restore `
    -target:Rebuild `
    "-p:Configuration=$Configuration" `
    "-p:ViuLanguageServerPublishPath=$normalizedLanguageServerPublishDirectory" `
    -p:ViuSkipLanguageServerPublish=true `
    -nologo `
    -verbosity:minimal `
    @versionBuildArguments
if ($LASTEXITCODE -ne 0) {
    throw "Building the Visual Studio extension failed with exit code $LASTEXITCODE."
}

# The VSSDK names the container itself, so ask it rather than globbing bin\: a target-framework
# change leaves the old framework's .vsix sitting beside the new one, and a glob would have to guess
# between them.
$extensionProjectDirectory = Split-Path -Parent $extensionProject
$targetVsixContainer = & $msbuild $extensionProject `
    -nologo `
    -getProperty:TargetVsixContainer `
    "-p:Configuration=$Configuration"
if ($LASTEXITCODE -ne 0) {
    throw "Reading TargetVsixContainer from the Visual Studio extension project failed with exit code $LASTEXITCODE."
}

$extensionPackagePath = [System.IO.Path]::GetFullPath(
    (Join-Path $extensionProjectDirectory ($targetVsixContainer | Select-Object -First 1).Trim()))
if (-not (Test-Path -LiteralPath $extensionPackagePath)) {
    throw "The Visual Studio extension build did not produce $extensionPackagePath."
}

if (-not (Test-Path -LiteralPath $normalizedExtensionOutputDirectory)) {
    New-Item -ItemType Directory -Path $normalizedExtensionOutputDirectory | Out-Null
}

$packagedExtension = Join-Path $normalizedExtensionOutputDirectory `
    'Assimalign.Viu.VisualStudio.vsix'
Copy-Item -LiteralPath $extensionPackagePath -Destination $packagedExtension -Force

# #region: Verify the package
# The MSBuild backstops assert the payload exists on disk; this asserts it reached the container.
# The two are not the same check - an inclusion rule that silently stops matching would leave the
# publish directory perfectly healthy and the VSIX empty.
$requiredEntries = @(
    'extension.vsixmanifest',
    'Assimalign.Viu.VisualStudio.dll',
    'Assimalign.Viu.VisualStudio.pkgdef',
    'language-server.json',
    'LanguageServer/win-x64/Assimalign.Viu.LanguageServer.exe',
    'LanguageServer/win-arm64/Assimalign.Viu.LanguageServer.exe'
)

Add-Type -AssemblyName System.IO.Compression.FileSystem
$extensionArchive = [System.IO.Compression.ZipFile]::OpenRead($packagedExtension)
try {
    $entryNames = @($extensionArchive.Entries | ForEach-Object FullName)
    $missingEntries = @($requiredEntries | Where-Object { $entryNames -notcontains $_ })
    if ($missingEntries.Count -gt 0) {
        throw "The packaged extension is missing required entries: $($missingEntries -join ', ')."
    }

    $manifestEntry = $extensionArchive.GetEntry('extension.vsixmanifest')
    $manifestReader = [System.IO.StreamReader]::new($manifestEntry.Open())
    try {
        [xml] $manifest = $manifestReader.ReadToEnd()
    }
    finally {
        $manifestReader.Dispose()
    }

    $namespaceManager = [System.Xml.XmlNamespaceManager]::new($manifest.NameTable)
    $namespaceManager.AddNamespace(
        'vsix',
        'http://schemas.microsoft.com/developer/vsx-schema/2011')
    $mefAsset = $manifest.SelectSingleNode(
        "/vsix:PackageManifest/vsix:Assets/vsix:Asset[@Type='Microsoft.VisualStudio.MefComponent']",
        $namespaceManager)
    if ($null -eq $mefAsset) {
        throw 'The packaged extension declares no Microsoft.VisualStudio.MefComponent asset, so its editor parts would never compose.'
    }

    $packageAsset = $manifest.SelectSingleNode(
        "/vsix:PackageManifest/vsix:Assets/vsix:Asset[@Type='Microsoft.VisualStudio.VsPackage']",
        $namespaceManager)
    if ($null -eq $packageAsset) {
        throw 'The packaged extension declares no Microsoft.VisualStudio.VsPackage asset, so the .viu file-extension claim would never be registered.'
    }

    $entryCount = $entryNames.Count
}
finally {
    $extensionArchive.Dispose()
}
# #endregion

$packagedExtensionFile = Get-Item -LiteralPath $packagedExtension
$maximumPackageSize = 50MB
if ($packagedExtensionFile.Length -gt $maximumPackageSize) {
    throw "The Visual Studio extension is $($packagedExtensionFile.Length) bytes; Marketplace packages must remain at or below $maximumPackageSize bytes."
}

$packageSizeInMegabytes = $packagedExtensionFile.Length / 1MB
Write-Host "Visual Studio extension: $packagedExtension"
Write-Host ("  {0} entries, {1:N2} MB ({2} bytes)" -f `
    $entryCount,
    $packageSizeInMegabytes,
    $packagedExtensionFile.Length)
