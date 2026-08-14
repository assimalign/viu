<#
.SYNOPSIS
    Builds Assimalign.Viu.UtilityCss.VisualStudio and its Utility CSS language-server payload.

.DESCRIPTION
    The extension is a classic in-process VSSDK MEF package, so packaging it needs Visual Studio's
    MSBuild. This script locates that MSBuild through vswhere, publishes the standalone Utility CSS
    language server through build\Targets\Build.LanguageServer.targets, packages the VSIX, and
    verifies its manifest, runtime payloads, Roslyn-free dependency closure, and Marketplace budget.

    This script is independently runnable. extensions\VisualStudio\Build.ps1 orchestrates it
    together with the Viu extension build.
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
        throw 'The Visual Studio extension version must contain three or four numeric components, for example 10.0.1 or 10.0.1.42.'
    }

    foreach ($versionSegment in $Version.Split('.')) {
        $versionSegmentValue = [int]::Parse(
            $versionSegment,
            [System.Globalization.CultureInfo]::InvariantCulture)
        if ($versionSegmentValue -gt 65534) {
            throw 'Visual Studio extension version components cannot exceed 65534.'
        }
    }

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
    'src\Assimalign.Viu.UtilityCss.VisualStudio.csproj'
$languageServerProject = Join-Path $repositoryDirectory `
    'libraries\Utilities\Assimalign.Viu.UtilityCss.LanguageServer\src\Assimalign.Viu.UtilityCss.LanguageServer.csproj'
$languageServerExecutableBaseName = 'Assimalign.Viu.UtilityCss.LanguageServer'
$extensionOutputDirectory = Join-Path $repositoryDirectory `
    "_out\extensions\VisualStudio\$Configuration"
$languageServerPublishRoot = Join-Path $extensionOutputDirectory `
    'Assimalign.Viu.UtilityCss.VisualStudio'
$languageServerPublishDirectory = Join-Path $languageServerPublishRoot `
    'LanguageServer'

$normalizedExtensionOutputDirectory =
    [System.IO.Path]::GetFullPath($extensionOutputDirectory)
$normalizedLanguageServerPublishRoot =
    [System.IO.Path]::GetFullPath($languageServerPublishRoot)
$normalizedLanguageServerPublishDirectory =
    [System.IO.Path]::GetFullPath($languageServerPublishDirectory)
$normalizedExtensionOutputPrefix =
    $normalizedExtensionOutputDirectory.TrimEnd(
        [char[]]@(
            [System.IO.Path]::DirectorySeparatorChar,
            [System.IO.Path]::AltDirectorySeparatorChar)) +
    [System.IO.Path]::DirectorySeparatorChar
if (-not $normalizedLanguageServerPublishRoot.StartsWith(
        $normalizedExtensionOutputPrefix,
        [System.StringComparison]::OrdinalIgnoreCase)) {
    throw "The language-server publish root must remain inside $normalizedExtensionOutputDirectory."
}

if (-not $normalizedLanguageServerPublishDirectory.StartsWith(
        $normalizedLanguageServerPublishRoot.TrimEnd(
            [char[]]@(
                [System.IO.Path]::DirectorySeparatorChar,
                [System.IO.Path]::AltDirectorySeparatorChar)) +
            [System.IO.Path]::DirectorySeparatorChar,
        [System.StringComparison]::OrdinalIgnoreCase)) {
    throw "The language-server publish directory must remain inside $normalizedLanguageServerPublishRoot."
}

$languageServerBuildArguments = @(
    "-p:ViuLanguageServerProjectPath=$languageServerProject",
    "-p:ViuLanguageServerExecutableBaseName=$languageServerExecutableBaseName",
    "-p:ViuLanguageServerPublishRoot=$normalizedLanguageServerPublishRoot",
    "-p:ViuLanguageServerPublishPath=$normalizedLanguageServerPublishDirectory"
)

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

$visualStudioInstallation = Find-VisualStudioInstallation
if ([string]::IsNullOrWhiteSpace($visualStudioInstallation)) {
    $visualStudioInstallation = Find-VisualStudioInstallation -IncludePrerelease
}
if ([string]::IsNullOrWhiteSpace($visualStudioInstallation)) {
    throw "No Visual Studio installation with MSBuild was found. Install the 'Visual Studio extension development' workload."
}

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

if (Test-Path -LiteralPath $normalizedLanguageServerPublishRoot) {
    Remove-Item -LiteralPath $normalizedLanguageServerPublishRoot -Recurse -Force
}

Write-Host 'Publishing the Viu Utility CSS language server through the shared MSBuild target'
& dotnet msbuild $extensionProject `
    -target:ViuPublishLanguageServer `
    "-p:Configuration=$Configuration" `
    @languageServerBuildArguments `
    -nologo `
    @versionBuildArguments
if ($LASTEXITCODE -ne 0) {
    throw "Publishing the Viu Utility CSS language server failed with exit code $LASTEXITCODE."
}

Write-Host 'Building and packaging the Viu Utility CSS Visual Studio extension'
& $msbuild $extensionProject `
    -restore `
    -target:Rebuild `
    "-p:Configuration=$Configuration" `
    @languageServerBuildArguments `
    -p:ViuSkipLanguageServerPublish=true `
    -nologo `
    -verbosity:minimal `
    @versionBuildArguments
if ($LASTEXITCODE -ne 0) {
    throw "Building the Viu Utility CSS Visual Studio extension failed with exit code $LASTEXITCODE."
}

$extensionProjectDirectory = Split-Path -Parent $extensionProject
$targetVsixContainer = & $msbuild $extensionProject `
    -nologo `
    -getProperty:TargetVsixContainer `
    "-p:Configuration=$Configuration"
if ($LASTEXITCODE -ne 0) {
    throw "Reading TargetVsixContainer from the Viu Utility CSS extension project failed with exit code $LASTEXITCODE."
}

$extensionPackagePath = [System.IO.Path]::GetFullPath(
    (Join-Path $extensionProjectDirectory ($targetVsixContainer | Select-Object -First 1).Trim()))
if (-not (Test-Path -LiteralPath $extensionPackagePath)) {
    throw "The Viu Utility CSS extension build did not produce $extensionPackagePath."
}

if (-not (Test-Path -LiteralPath $normalizedExtensionOutputDirectory)) {
    New-Item -ItemType Directory -Path $normalizedExtensionOutputDirectory | Out-Null
}

$packagedExtension = Join-Path $normalizedExtensionOutputDirectory `
    'Assimalign.Viu.UtilityCss.VisualStudio.vsix'
Copy-Item -LiteralPath $extensionPackagePath -Destination $packagedExtension -Force

# #region: Verify the package
$requiredEntries = @(
    'extension.vsixmanifest',
    'Assimalign.Viu.UtilityCss.VisualStudio.dll',
    'language-server.json',
    'LanguageServer/win-x64/Assimalign.Viu.UtilityCss.LanguageServer.exe',
    'LanguageServer/win-arm64/Assimalign.Viu.UtilityCss.LanguageServer.exe'
)

Add-Type -AssemblyName System.IO.Compression.FileSystem
$extensionArchive = [System.IO.Compression.ZipFile]::OpenRead($packagedExtension)
try {
    $entryNames = @($extensionArchive.Entries | ForEach-Object FullName)
    $missingEntries = @($requiredEntries | Where-Object { $entryNames -notcontains $_ })
    if ($missingEntries.Count -gt 0) {
        throw "The packaged extension is missing required entries: $($missingEntries -join ', ')."
    }

    $packageDefinitionEntries = @($entryNames | Where-Object {
        [System.IO.Path]::GetExtension($_) -ieq '.pkgdef'
    })
    if ($packageDefinitionEntries.Count -gt 0) {
        throw "The Viu Utility CSS extension must remain MEF-only and contains unexpected pkgdef entries: $($packageDefinitionEntries -join ', ')."
    }

    $roslynArchiveEntries = @($entryNames | Where-Object {
        [System.IO.Path]::GetFileName($_) -like 'Microsoft.CodeAnalysis*'
    })
    if ($roslynArchiveEntries.Count -gt 0) {
        throw "The Viu Utility CSS extension contains forbidden Roslyn payload entries: $($roslynArchiveEntries -join ', ')."
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
    $assets = @($manifest.SelectNodes(
        '/vsix:PackageManifest/vsix:Assets/vsix:Asset',
        $namespaceManager))
    $mefAssets = @($assets | Where-Object {
        $_.Type -eq 'Microsoft.VisualStudio.MefComponent'
    })
    if ($mefAssets.Count -eq 0) {
        throw 'The packaged extension declares no Microsoft.VisualStudio.MefComponent asset, so its language client would never compose.'
    }

    $unexpectedAssets = @($assets | Where-Object {
        $_.Type -ne 'Microsoft.VisualStudio.MefComponent'
    })
    if ($unexpectedAssets.Count -gt 0) {
        $unexpectedAssetTypes = @($unexpectedAssets | ForEach-Object Type)
        throw "The Viu Utility CSS extension must remain MEF-only and declares unexpected asset types: $($unexpectedAssetTypes -join ', ')."
    }

    $identity = $manifest.SelectSingleNode(
        '/vsix:PackageManifest/vsix:Metadata/vsix:Identity',
        $namespaceManager)
    if ($null -eq $identity) {
        throw 'The packaged extension manifest does not contain an Identity element.'
    }
    if ($identity.Id -ne 'Assimalign.Viu.UtilityCss.VisualStudio.8fcd5c9a-f62f-467c-8655-b7791c41775b') {
        throw "Unexpected Viu Utility CSS VSIX identity: $($identity.Id)."
    }
    if ($identity.Publisher -ne 'Assimalign') {
        throw "Unexpected Viu Utility CSS VSIX publisher: $($identity.Publisher)."
    }

    $entryCount = $entryNames.Count
}
finally {
    $extensionArchive.Dispose()
}
# #endregion

# #region: Verify the single-file bundle dependency closure
# The VSIX contains one apphost per RID, so embedded managed assembly names do not appear as archive
# entries. The nested publish's deps manifests are the authoritative input closure for those bundles.
$languageServerProjectDirectory = Split-Path -Parent $languageServerProject
$nestedPublishBuildRoot = Join-Path $languageServerProjectDirectory `
    "obj\language-server\$Configuration"
if (-not (Test-Path -LiteralPath $nestedPublishBuildRoot)) {
    throw "The nested language-server build output was not found at $nestedPublishBuildRoot."
}

$dependencyManifestCandidates = @(Get-ChildItem `
    -LiteralPath $nestedPublishBuildRoot `
    -Recurse `
    -File `
    -Filter "$languageServerExecutableBaseName.deps.json")
$validatedDependencyManifestCount = 0
foreach ($runtimeIdentifierName in @('win-x64', 'win-arm64')) {
    $runtimeIdentifierSegment =
        [System.IO.Path]::DirectorySeparatorChar +
        $runtimeIdentifierName +
        [System.IO.Path]::DirectorySeparatorChar
    $runtimeDependencyManifests = @($dependencyManifestCandidates | Where-Object {
        $_.FullName.IndexOf(
            $runtimeIdentifierSegment,
            [System.StringComparison]::OrdinalIgnoreCase) -ge 0
    })
    if ($runtimeDependencyManifests.Count -eq 0) {
        throw "No $runtimeIdentifierName dependency manifest was produced under $nestedPublishBuildRoot."
    }

    foreach ($dependencyManifestFile in $runtimeDependencyManifests) {
        $dependencyManifestText = Get-Content -LiteralPath $dependencyManifestFile.FullName -Raw
        $dependencyManifest = $dependencyManifestText | ConvertFrom-Json
        $dependencyLibraryNames = @(
            $dependencyManifest.libraries.PSObject.Properties | ForEach-Object Name)
        $roslynDependencies = @($dependencyLibraryNames | Where-Object {
            $_ -match '(?i)^Microsoft\.CodeAnalysis(?:[./]|$)'
        })
        if ($roslynDependencies.Count -gt 0) {
            throw "The $runtimeIdentifierName Utility CSS language-server bundle contains forbidden Roslyn dependencies: $($roslynDependencies -join ', ')."
        }
        if ($dependencyManifestText -match '(?i)Microsoft\.CodeAnalysis') {
            throw "The $runtimeIdentifierName Utility CSS language-server dependency manifest contains a forbidden Microsoft.CodeAnalysis entry: $($dependencyManifestFile.FullName)."
        }

        $validatedDependencyManifestCount++
    }
}
# #endregion

$packagedExtensionFile = Get-Item -LiteralPath $packagedExtension
$maximumPackageSize = 50MB
if ($packagedExtensionFile.Length -gt $maximumPackageSize) {
    throw "The Viu Utility CSS VSIX is $($packagedExtensionFile.Length) bytes; Marketplace packages must remain at or below $maximumPackageSize bytes."
}

$packageSizeInMegabytes = $packagedExtensionFile.Length / 1MB
Write-Host "Viu Utility CSS Visual Studio extension: $packagedExtension"
Write-Host ("  {0} entries, {1:N2} MB ({2} bytes)" -f `
    $entryCount,
    $packageSizeInMegabytes,
    $packagedExtensionFile.Length)
Write-Host "  Roslyn-free dependency manifests: $validatedDependencyManifestCount"
