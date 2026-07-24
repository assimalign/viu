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

    $versionBuildArguments = @(
        "-p:VersionPrefix=$Version",
        '-p:VersionSuffix='
    )
    Write-Host "Visual Studio extension version: $Version"
}

$visualStudioDirectory = $PSScriptRoot
$repositoryDirectory = [System.IO.Path]::GetFullPath(
    (Join-Path $visualStudioDirectory '..\..'))
$languageServerProject = Join-Path $visualStudioDirectory `
    'Assimalign.Viu.LanguageServer\src\Assimalign.Viu.LanguageServer.csproj'
$extensionProject = Join-Path $visualStudioDirectory `
    'Assimalign.Viu.VisualStudio\src\Assimalign.Viu.VisualStudio.csproj'
$extensionOutputDirectory = Join-Path $repositoryDirectory `
    "_out\extensions\VisualStudio\$Configuration"
$languageServerPublishDirectory = Join-Path $extensionOutputDirectory `
    'LanguageServer'
$languageServerRuntimeIdentifiers = @('win-x64', 'win-arm64')
$publishDebugType = if ($Configuration -eq 'Debug') { 'embedded' } else { 'none' }

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

if (Test-Path -LiteralPath $normalizedLanguageServerPublishDirectory) {
    Remove-Item -LiteralPath $normalizedLanguageServerPublishDirectory -Recurse -Force
}

foreach ($runtimeIdentifier in $languageServerRuntimeIdentifiers) {
    $runtimePublishDirectory = Join-Path `
        $normalizedLanguageServerPublishDirectory `
        $runtimeIdentifier

    Write-Host "Publishing $runtimeIdentifier Viu language server to $runtimePublishDirectory"
    & dotnet publish $languageServerProject `
        --configuration $Configuration `
        --runtime $runtimeIdentifier `
        --output $runtimePublishDirectory `
        --self-contained true `
        "-p:PublishSingleFile=true" `
        "-p:IncludeNativeLibrariesForSelfExtract=true" `
        "-p:EnableCompressionInSingleFile=true" `
        "-p:DebugType=$publishDebugType" `
        @versionBuildArguments
    if ($LASTEXITCODE -ne 0) {
        throw "Publishing the $runtimeIdentifier Viu language server failed with exit code $LASTEXITCODE."
    }
}

Write-Host 'Cleaning the Visual Studio extension output'
& dotnet clean $extensionProject --configuration $Configuration
if ($LASTEXITCODE -ne 0) {
    throw "Cleaning the Visual Studio extension failed with exit code $LASTEXITCODE."
}

Write-Host 'Building the Visual Studio extension'
& dotnet build $extensionProject `
    --configuration $Configuration `
    "-p:ViuLanguageServerPublishPath=$languageServerPublishDirectory" `
    @versionBuildArguments
if ($LASTEXITCODE -ne 0) {
    throw "Building the Visual Studio extension failed with exit code $LASTEXITCODE."
}

$extensionBinDirectory = Join-Path `
    (Split-Path -Parent $extensionProject) `
    "bin\$Configuration"
$extensionPackages = @(
    Get-ChildItem `
        -LiteralPath $extensionBinDirectory `
        -Filter 'Assimalign.Viu.VisualStudio.vsix' `
        -File `
        -Recurse
)
if ($extensionPackages.Count -ne 1) {
    throw "Expected exactly one Assimalign.Viu.VisualStudio.vsix under $extensionBinDirectory, found $($extensionPackages.Count)."
}
$extensionPackage = $extensionPackages[0]

$packagedExtension = Join-Path $extensionOutputDirectory `
    'Assimalign.Viu.VisualStudio.vsix'
Copy-Item -LiteralPath $extensionPackage.FullName -Destination $packagedExtension -Force

Write-Host "Visual Studio extension: $packagedExtension"
