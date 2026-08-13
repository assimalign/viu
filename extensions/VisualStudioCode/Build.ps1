<#
.SYNOPSIS
    Publishes the standalone Viu language server for every packaged runtime identifier, stages the
    payloads into this extension's server/ folder, and compiles the TypeScript client.

.DESCRIPTION
    The publish recipe itself lives in build\Targets\Build.LanguageServer.targets — the same shared
    target extensions\VisualStudio\Build.ps1 and the Visual Studio extension build drive — so the
    two editor hosts cannot drift apart on trimming, single-file, or debug-type settings.

    The two hosts differ in exactly one property: the Visual Studio VSIX embeds only win-x64 and
    win-arm64 (five payloads at roughly 18 MB apiece would push a single VSIX past the Marketplace
    size gate), while Visual Studio Code ships one platform-specific package per runtime identifier
    and therefore asks for the full set through ViuLanguageServerPublishAllRuntimeIdentifiers. Each
    host also publishes to its OWN ViuLanguageServerPublishRoot, so neither can sweep the other's
    payloads into its package.

    This script does not run `vsce`. Packaging is per-platform and is documented in README.md.
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

$extensionDirectory = $PSScriptRoot
$repositoryDirectory = [System.IO.Path]::GetFullPath(
    (Join-Path $extensionDirectory '..\..'))
# The language-server project is named once, here, only so the shared target can be invoked on a
# project that imports it. Every publish argument still comes from the target.
$languageServerProject = Join-Path $repositoryDirectory `
    'tooling\Editor\Assimalign.Viu.LanguageServer\src\Assimalign.Viu.LanguageServer.csproj'

$publishRoot = Join-Path $repositoryDirectory `
    "_out\extensions\VisualStudioCode\$Configuration"
$publishDirectory = [System.IO.Path]::GetFullPath((Join-Path $publishRoot 'LanguageServer'))
$stagingDirectory = Join-Path $extensionDirectory 'server'

$versionBuildArguments = @()
if (-not [string]::IsNullOrWhiteSpace($Version)) {
    if ($Version -notmatch '^(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)$') {
        throw "The Visual Studio Code extension version must contain three numeric components, for example 0.1.0."
    }

    $versionBuildArguments = @(
        "-p:VersionPrefix=$Version",
        '-p:VersionSuffix='
    )
    Write-Host "Viu language server version: $Version"
}

if ($RuntimeIdentifier.Count -gt 0) {
    $requestedRuntimeIdentifiers = $RuntimeIdentifier
    $runtimeIdentifierArguments = @(
        "-p:ViuLanguageServerRuntimeIdentifiers=$($RuntimeIdentifier -join ';')"
    )
}
else {
    # The full set is defined once, in the shared target. Reading it back rather than restating it
    # here is what keeps this script from becoming a second source of truth.
    $requestedRuntimeIdentifiers = (
        & dotnet msbuild $languageServerProject `
            -nologo `
            -getProperty:ViuLanguageServerAllRuntimeIdentifiers) -split ';' |
        Where-Object { -not [string]::IsNullOrWhiteSpace($_) } |
        ForEach-Object { $_.Trim() }
    if ($LASTEXITCODE -ne 0 -or $requestedRuntimeIdentifiers.Count -eq 0) {
        throw "Could not read ViuLanguageServerAllRuntimeIdentifiers from the shared language-server target."
    }

    $runtimeIdentifierArguments = @(
        '-p:ViuLanguageServerPublishAllRuntimeIdentifiers=true'
    )
}

Write-Host "Publishing the Viu language server for: $($requestedRuntimeIdentifiers -join ', ')"
& dotnet msbuild $languageServerProject `
    -target:ViuPublishLanguageServer `
    "-p:Configuration=$Configuration" `
    -p:ViuLanguageServerPublishEnabled=true `
    "-p:ViuLanguageServerPublishRoot=$publishRoot" `
    "-p:ViuLanguageServerPublishPath=$publishDirectory" `
    -nologo `
    @runtimeIdentifierArguments `
    @versionBuildArguments
if ($LASTEXITCODE -ne 0) {
    throw "Publishing the Viu language server failed with exit code $LASTEXITCODE."
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

    # Only Windows runtimes carry the .exe suffix: `dotnet publish` names the apphost after the
    # target platform, and the TypeScript client resolves the same two spellings.
    $executableName = if ($runtimeIdentifierName.StartsWith('win-')) {
        'Assimalign.Viu.LanguageServer.exe'
    }
    else {
        'Assimalign.Viu.LanguageServer'
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
    Write-Host 'Installing the Visual Studio Code extension dependencies'
    Push-Location -LiteralPath $extensionDirectory
    try {
        & npm install
        if ($LASTEXITCODE -ne 0) {
            throw "npm install failed with exit code $LASTEXITCODE."
        }

        Write-Host 'Compiling the Visual Studio Code extension client'
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
Write-Host 'See README.md for the runtime-identifier to vsce target mapping.'
