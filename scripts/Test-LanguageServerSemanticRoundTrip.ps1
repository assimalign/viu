<#
.SYNOPSIS
    Runs the packed-consumer semantic round trip ([V01.01.12.23], #259, acceptance criterion 5):
    asserts the repo-local feed (_out/packages) and the published language-server executable
    exist, writes the fixture file, and drives LanguageServerPackedConsumerTests against the real
    single-file server over stdio. The test itself is inert without the
    VIU_PACKED_CONSUMER_FIXTURE environment variable this script sets, so ordinary dotnet test
    runs stay hermetic and offline.

.PARAMETER Configuration
    The extension build configuration whose published server payload is exercised
    (default Release, matching area-visual-studio.yml).
#>
[CmdletBinding()]
param(
    [ValidateSet('Debug', 'Release')]
    [string] $Configuration = 'Release'
)

$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path $PSScriptRoot -Parent

# Derive the version the build stamps straight from the MSBuild props, exactly as
# Install-Local.ps1 does — never from directory globbing, because the feed can accumulate
# versions.
$tfLatest = ([xml](Get-Content (Join-Path $repoRoot 'build\Targets\Build.TargetFramework.props'))).Project.PropertyGroup.TargetFrameworkLatest |
    Where-Object { $_ } | Select-Object -First 1
$versionProps = ([xml](Get-Content (Join-Path $repoRoot 'build\Targets\Build.Version.props'))).Project.PropertyGroup
$major = ([version]($tfLatest.Trim() -replace '^net', '')).Major
$minor = ($versionProps.ViuMinorVersion | Where-Object { $_ } | Select-Object -First 1).Trim()
$patch = ($versionProps.ViuPatchVersion | Where-Object { $_ } | Select-Object -First 1).Trim()
$viuVersion = "$major.$minor.$patch"

$feed = Join-Path $repoRoot '_out\packages'
$requiredPackages = @(
    "Assimalign.Viu.Sdk.$viuVersion.nupkg",
    "Assimalign.Viu.App.Ref.$viuVersion.nupkg",
    "Assimalign.Viu.App.Runtime.browser-wasm.$viuVersion.nupkg"
)
foreach ($requiredPackage in $requiredPackages) {
    if (-not (Test-Path (Join-Path $feed $requiredPackage))) {
        throw "The repo-local feed is missing $requiredPackage; run scripts/Install-Local.ps1 first."
    }
}

# The published server for the host architecture — the fixture must exercise the exact
# single-file payload the VSIX ships.
$rid = switch ([System.Runtime.InteropServices.RuntimeInformation]::OSArchitecture) {
    'X64' { 'win-x64' }
    'Arm64' { 'win-arm64' }
    default { throw "Unsupported host architecture '$_' for the packaged language server." }
}
$serverExecutable = Join-Path $repoRoot `
    "_out\extensions\VisualStudio\$Configuration\LanguageServer\$rid\Assimalign.Viu.Tooling.LanguageServer.exe"
if (-not (Test-Path $serverExecutable)) {
    throw "The published language server was not found at $serverExecutable; run extensions\VisualStudio\Build.ps1 -Configuration $Configuration first."
}
$serverSize = (Get-Item $serverExecutable).Length
Write-Host "Viu $viuVersion, server $serverExecutable ($('{0:N0}' -f $serverSize) bytes)" -ForegroundColor Cyan

$fixtureDirectory = Join-Path ([System.IO.Path]::GetTempPath()) 'viu-lsp-packed-consumer'
New-Item -ItemType Directory -Force $fixtureDirectory | Out-Null
$fixturePath = Join-Path $fixtureDirectory 'fixture.json'
@{
    serverExecutable = $serverExecutable
    packageFeed      = $feed
    sdkVersion       = $viuVersion
} | ConvertTo-Json | Set-Content -LiteralPath $fixturePath -Encoding utf8

$env:VIU_PACKED_CONSUMER_FIXTURE = $fixturePath
try {
    dotnet test (Join-Path $repoRoot 'tooling\Assimalign.Viu.Tooling.LanguageServer\test') `
        --filter 'FullyQualifiedName~LanguageServerPackedConsumerTests' `
        --logger 'console;verbosity=detailed'
    if ($LASTEXITCODE -ne 0) {
        throw "The packed-consumer semantic round trip failed with exit code $LASTEXITCODE."
    }
}
finally {
    Remove-Item Env:\VIU_PACKED_CONSUMER_FIXTURE -ErrorAction SilentlyContinue
}

Write-Host 'Packed-consumer semantic round trip passed.' -ForegroundColor Green
