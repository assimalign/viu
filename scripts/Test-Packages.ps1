<#
.SYNOPSIS
    Validates the complete Viu NuGet package set without publishing it.

.DESCRIPTION
    Applies the shared [V01.01.12.03] release contract from ViuPackaging.psm1:
    exact inventory and version coherence, required metadata and embedded files,
    dependency ranges, analyzer layout, and portable-PDB symbol packages. The
    script is used by packaging CI and can be run against either _out/packages
    or the release packer's output.

.PARAMETER PackageDirectory
    Directory containing the .nupkg and .snupkg set.

.PARAMETER Version
    Expected package version. When omitted, it is inferred from the single base
    Assimalign.Viu.Sdk package in PackageDirectory.
#>
[CmdletBinding()]
param(
    [string] $PackageDirectory =
        (Join-Path (Split-Path $PSScriptRoot -Parent) '_out/packages'),

    [string] $Version
)

$ErrorActionPreference = 'Stop'

$repositoryDirectory = [System.IO.Path]::GetFullPath(
    (Split-Path $PSScriptRoot -Parent))
$resolvedPackageDirectory = [System.IO.Path]::GetFullPath($PackageDirectory)
$repositoryOutputDirectory = [System.IO.Path]::GetFullPath(
    (Join-Path $repositoryDirectory '_out'))
$repositoryOutputPrefix = $repositoryOutputDirectory.TrimEnd(
    [char[]]@(
        [System.IO.Path]::DirectorySeparatorChar,
        [System.IO.Path]::AltDirectorySeparatorChar)) +
    [System.IO.Path]::DirectorySeparatorChar
if (-not $resolvedPackageDirectory.StartsWith(
        $repositoryOutputPrefix,
        [System.StringComparison]::OrdinalIgnoreCase)) {
    throw "PackageDirectory must remain under the repository _out directory: $resolvedPackageDirectory"
}

Import-Module (Join-Path $PSScriptRoot 'modules/ViuPackaging.psm1') -Force

function Test-ViuMismatchedSymbolLayoutIsRejected {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)]
        [string] $PackageDirectory,

        [Parameter(Mandatory)]
        [string] $Version,

        [Parameter(Mandatory)]
        [string[]] $ExpectedPackageId,

        [Parameter(Mandatory)]
        [string] $SymbolPackageId
    )

    Add-Type -AssemblyName System.IO.Compression.FileSystem

    $temporaryRoot = [System.IO.Path]::GetFullPath(
        [System.IO.Path]::GetTempPath()).TrimEnd(
            [char[]]@(
                [System.IO.Path]::DirectorySeparatorChar,
                [System.IO.Path]::AltDirectorySeparatorChar))
    $temporaryDirectory = Join-Path `
        $temporaryRoot `
        "viu-symbol-layout-$([System.Guid]::NewGuid().ToString('N'))"
    $resolvedTemporaryDirectory = [System.IO.Path]::GetFullPath($temporaryDirectory)
    $expectedTemporaryPrefix = $temporaryRoot + [System.IO.Path]::DirectorySeparatorChar
    if (-not $resolvedTemporaryDirectory.StartsWith(
            $expectedTemporaryPrefix,
            [System.StringComparison]::OrdinalIgnoreCase) -or
        -not [System.IO.Path]::GetFileName($resolvedTemporaryDirectory).StartsWith(
            'viu-symbol-layout-',
            [System.StringComparison]::Ordinal)) {
        throw "Refusing to use an unexpected symbol-layout regression directory: $resolvedTemporaryDirectory"
    }

    New-Item -ItemType Directory -Path $resolvedTemporaryDirectory | Out-Null
    try {
        Get-ChildItem -LiteralPath $PackageDirectory -File |
            Where-Object {
                $_.Name.EndsWith(
                    ".$Version.nupkg",
                    [System.StringComparison]::OrdinalIgnoreCase) -or
                $_.Name.EndsWith(
                    ".$Version.snupkg",
                    [System.StringComparison]::OrdinalIgnoreCase)
            } |
            Copy-Item -Destination $resolvedTemporaryDirectory

        $symbolPackage = @(
            Get-ChildItem `
                -LiteralPath $resolvedTemporaryDirectory `
                -Filter "$SymbolPackageId.$Version.snupkg" `
                -File)
        if ($symbolPackage.Count -ne 1) {
            throw "The symbol-layout regression requires exactly one $SymbolPackageId symbol package, found $($symbolPackage.Count)."
        }

        $archive = [System.IO.Compression.ZipFile]::Open(
            $symbolPackage[0].FullName,
            [System.IO.Compression.ZipArchiveMode]::Update)
        try {
            $portableSymbol = @(
                $archive.Entries |
                    Where-Object {
                        $_.Name.EndsWith(
                            '.pdb',
                            [System.StringComparison]::OrdinalIgnoreCase)
                    } |
                    Select-Object -First 1)
            if ($portableSymbol.Count -ne 1) {
                throw "$($symbolPackage[0].Name) contains no PDB to move for the regression."
            }

            $symbolBytes = [System.IO.MemoryStream]::new()
            $symbolStream = $portableSymbol[0].Open()
            try {
                $symbolStream.CopyTo($symbolBytes)
            }
            finally {
                $symbolStream.Dispose()
            }
            $portableSymbol[0].Delete()

            $mismatchedEntry = $archive.CreateEntry(
                "mismatched/$($portableSymbol[0].Name)",
                [System.IO.Compression.CompressionLevel]::Optimal)
            $mismatchedStream = $mismatchedEntry.Open()
            try {
                $symbolBytes.Position = 0
                $symbolBytes.CopyTo($mismatchedStream)
            }
            finally {
                $mismatchedStream.Dispose()
                $symbolBytes.Dispose()
            }
        }
        finally {
            $archive.Dispose()
        }

        $mismatchRejected = $false
        try {
            Assert-ViuReleasePackageSet `
                -PackageDirectory $resolvedTemporaryDirectory `
                -Version $Version `
                -ExpectedPackageId $ExpectedPackageId
        }
        catch {
            if (-not $_.Exception.Message.Contains(
                    'does not have a managed PE at the same relative path',
                    [System.StringComparison]::Ordinal)) {
                throw
            }
            $mismatchRejected = $true
        }
        if (-not $mismatchRejected) {
            throw 'The release validator accepted a symbol-package PDB whose main-package PE is at a different relative path.'
        }

        Write-Host 'Rejected a deliberately mismatched symbol-package PDB path.' -ForegroundColor Green
    }
    finally {
        $temporaryFiles = @(
            Get-ChildItem -LiteralPath $resolvedTemporaryDirectory -File -Force)
        foreach ($temporaryFile in $temporaryFiles) {
            Remove-Item -LiteralPath $temporaryFile.FullName -Force
        }
        Remove-Item -LiteralPath $resolvedTemporaryDirectory -Force
    }
}

if ([string]::IsNullOrWhiteSpace($Version)) {
    $sdkPackage = @(
        Get-ChildItem -LiteralPath $resolvedPackageDirectory -Filter 'Assimalign.Viu.Sdk.*.nupkg' -File |
            Where-Object {
                $_.Name -notlike 'Assimalign.Viu.Sdk.Browser.*' -and
                $_.Name -notlike '*.symbols.nupkg'
            })
    if ($sdkPackage.Count -ne 1) {
        throw "Expected one base Assimalign.Viu.Sdk package to infer Version, found $($sdkPackage.Count)."
    }
    $prefix = 'Assimalign.Viu.Sdk.'
    $Version = $sdkPackage[0].Name.Substring(
        $prefix.Length,
        $sdkPackage[0].Name.Length - $prefix.Length - '.nupkg'.Length)
}

$expectedPackageId = @(Get-ViuPackageId)
# [V01.01.12.30] restores the standalone UtilityCss library to the 17-main/12-symbol release set.
$expectedMainPackageCount = 17
$expectedSymbolPackageCount = 12
if ($expectedPackageId.Count -ne $expectedMainPackageCount) {
    throw "The release contract requires $expectedMainPackageCount main packages, but the configured inventory contains $($expectedPackageId.Count)."
}
$expectedSymbolPackageId = @(
    $expectedPackageId |
        Where-Object {
            Test-ViuPackageRequiresSymbolPackage `
                -PackageId $_ `
                -PackagePath (Join-Path $resolvedPackageDirectory "$($_).$Version.nupkg")
        })
if ($expectedSymbolPackageId.Count -ne $expectedSymbolPackageCount) {
    throw "The release contract requires $expectedSymbolPackageCount symbol packages, but the configured inventory requires $($expectedSymbolPackageId.Count)."
}

Test-ViuMismatchedSymbolLayoutIsRejected `
    -PackageDirectory $resolvedPackageDirectory `
    -Version $Version `
    -ExpectedPackageId $expectedPackageId `
    -SymbolPackageId 'Assimalign.Viu.DevTools'

Assert-ViuReleasePackageSet `
    -PackageDirectory $resolvedPackageDirectory `
    -Version $Version `
    -ExpectedPackageId $expectedPackageId
