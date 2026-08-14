<#
.SYNOPSIS
    Publishes the standalone Viu Utility CSS language server for every packaged runtime identifier,
    stages the payloads into this extension's server/ folder, and compiles the TypeScript client.

.DESCRIPTION
    The publish recipe lives in build\Targets\Build.LanguageServer.targets. This package overrides
    only the server project and executable name, keeping runtime identifiers, self-contained
    publishing, single-file compression, and debug settings aligned with packages\viu.

    This script does not run `vsce`. The root extensions\VisualStudioCode\Build.ps1 orchestrator
    invokes this script once per runtime identifier and creates the platform-specific VSIX files.
#>
[CmdletBinding()]
param(
    [ValidateSet('Debug', 'Release')]
    [string] $Configuration = 'Debug',

    [string] $Version,

    # Narrows the publish to a subset of the packaged runtime identifiers. Leave empty for the full
    # set. Staging exactly one runtime identifier is the normal preparation for
    # `vsce package --target <platform>`, which has no per-target payload filtering of its own.
    [ValidateSet('win-x64', 'win-arm64', 'linux-x64', 'osx-arm64', 'osx-x64')]
    [string[]] $RuntimeIdentifier = @(),

    # Skips `npm install` and the TypeScript compile. Useful when only the server payload changed,
    # or when running without network access.
    [switch] $SkipNodeBuild
)

$ErrorActionPreference = 'Stop'

$packageDirectory = $PSScriptRoot
$repositoryDirectory = [System.IO.Path]::GetFullPath(
    (Join-Path $packageDirectory '..\..\..\..'))
$languageServerProject = Join-Path $repositoryDirectory `
    'libraries\Utilities\Assimalign.Viu.UtilityCss.LanguageServer\src\Assimalign.Viu.UtilityCss.LanguageServer.csproj'
$languageServerExecutableBaseName = 'Assimalign.Viu.UtilityCss.LanguageServer'

$publishRoot = Join-Path $repositoryDirectory `
    "_out\extensions\VisualStudioCode\viu-utilitycss\$Configuration"
$publishDirectory = [System.IO.Path]::GetFullPath((Join-Path $publishRoot 'LanguageServer'))
$stagingDirectory = Join-Path $packageDirectory 'server'

$versionBuildArguments = @()
if (-not [string]::IsNullOrWhiteSpace($Version)) {
    if ($Version -notmatch '^(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)$') {
        throw 'The Visual Studio Code extension version must contain three numeric components, for example 0.1.0.'
    }

    $versionBuildArguments = @(
        "-p:VersionPrefix=$Version",
        '-p:VersionSuffix='
    )
    Write-Host "Viu Utility CSS language server version: $Version"
}

if ($RuntimeIdentifier.Count -gt 0) {
    $requestedRuntimeIdentifiers = $RuntimeIdentifier
    $runtimeIdentifierArguments = @(
        "-p:ViuLanguageServerRuntimeIdentifiers=$($RuntimeIdentifier -join ';')"
    )
}
else {
    $requestedRuntimeIdentifiers = (
        & dotnet msbuild $languageServerProject `
            -nologo `
            -getProperty:ViuLanguageServerAllRuntimeIdentifiers) -split ';' |
        Where-Object { -not [string]::IsNullOrWhiteSpace($_) } |
        ForEach-Object { $_.Trim() }
    if ($LASTEXITCODE -ne 0 -or $requestedRuntimeIdentifiers.Count -eq 0) {
        throw 'Could not read ViuLanguageServerAllRuntimeIdentifiers from the shared language-server target.'
    }

    $runtimeIdentifierArguments = @(
        '-p:ViuLanguageServerPublishAllRuntimeIdentifiers=true'
    )
}

# The root orchestrator invokes package builds one RID at a time. A publish-root-wide stamp could
# therefore let a fresh RID suppress another RID's stale publish. Key the stamp by the exact
# requested set and explicit version, keeping it beside the payload directory so it is never staged.
$publishStampRuntimeIdentifier = $requestedRuntimeIdentifiers -join '.'
$publishStampVersion = if ([string]::IsNullOrWhiteSpace($Version)) { 'default' } else { $Version }
$publishStampFile = Join-Path $publishRoot `
    ".languageserver-publish.$publishStampRuntimeIdentifier.$publishStampVersion.stamp"

Write-Host "Publishing the Viu Utility CSS language server for: $($requestedRuntimeIdentifiers -join ', ')"
& dotnet msbuild $languageServerProject `
    -target:ViuPublishLanguageServer `
    "-p:Configuration=$Configuration" `
    -p:ViuLanguageServerPublishEnabled=true `
    "-p:ViuLanguageServerProjectPath=$languageServerProject" `
    "-p:ViuLanguageServerExecutableBaseName=$languageServerExecutableBaseName" `
    "-p:ViuLanguageServerPublishRoot=$publishRoot" `
    "-p:ViuLanguageServerPublishPath=$publishDirectory" `
    "-p:ViuLanguageServerPublishStampFile=$publishStampFile" `
    -nologo `
    @runtimeIdentifierArguments `
    @versionBuildArguments
if ($LASTEXITCODE -ne 0) {
    throw "Publishing the Viu Utility CSS language server failed with exit code $LASTEXITCODE."
}

Write-Host "Staging the language-server payloads into $stagingDirectory"
if (Test-Path -LiteralPath $stagingDirectory) {
    Remove-Item -LiteralPath $stagingDirectory -Recurse -Force
}
New-Item -ItemType Directory -Path $stagingDirectory -Force | Out-Null

foreach ($runtimeIdentifierName in $requestedRuntimeIdentifiers) {
    $sourceDirectory = Join-Path $publishDirectory $runtimeIdentifierName
    if (-not (Test-Path -LiteralPath $sourceDirectory)) {
        throw "The $runtimeIdentifierName language-server payload is missing at $sourceDirectory."
    }

    $executableName = if ($runtimeIdentifierName.StartsWith('win-')) {
        "$languageServerExecutableBaseName.exe"
    }
    else {
        $languageServerExecutableBaseName
    }

    $sourceExecutable = Join-Path $sourceDirectory $executableName
    if (-not (Test-Path -LiteralPath $sourceExecutable)) {
        throw "The $runtimeIdentifierName language-server executable is missing at $sourceExecutable."
    }

    $targetDirectory = Join-Path $stagingDirectory $runtimeIdentifierName
    Copy-Item -LiteralPath $sourceDirectory -Destination $targetDirectory -Recurse -Force
    Write-Host "  $runtimeIdentifierName -> $targetDirectory"
}

if ($SkipNodeBuild) {
    Write-Host 'Skipping npm install and the TypeScript compile.'
}
else {
    Write-Host 'Installing the Viu Utilities extension dependencies'
    Push-Location -LiteralPath $packageDirectory
    try {
        & npm install
        if ($LASTEXITCODE -ne 0) {
            throw "npm install failed with exit code $LASTEXITCODE."
        }

        Write-Host 'Compiling the Viu Utilities extension client'
        & npm run compile
        if ($LASTEXITCODE -ne 0) {
            throw "The TypeScript compile failed with exit code $LASTEXITCODE."
        }
    }
    finally {
        Pop-Location
    }
}

Write-Host ''
Write-Host "Staged runtime identifiers: $($requestedRuntimeIdentifiers -join ', ')"
Write-Host 'Package a platform-specific VSIX with, for example:'
Write-Host '  npx @vscode/vsce package --target win32-x64'
Write-Host 'The root extensions\VisualStudioCode\Build.ps1 orchestrator builds the complete VSIX set.'
