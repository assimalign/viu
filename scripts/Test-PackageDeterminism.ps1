<#
.SYNOPSIS
    Compares two NuGet archives by normalized payload rather than ZIP bytes.

.DESCRIPTION
    NuGet assigns a random OPC core-properties filename and relationship id on
    each pack. Those values and ZIP timestamps do not affect package contents.
    This [V01.01.12.03] gate normalizes that metadata and then compares every
    entry hash, so changes in a DLL, portable PDB, nuspec, SourceLink payload,
    license, README, or other shipped file fail determinism validation.

.PARAMETER FirstPackage
    First .nupkg or .snupkg archive.

.PARAMETER SecondPackage
    Second independently packed archive.
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [string] $FirstPackage,

    [Parameter(Mandatory)]
    [string] $SecondPackage
)

$ErrorActionPreference = 'Stop'

Add-Type -AssemblyName System.IO.Compression.FileSystem

function Get-NormalizedPackageEntryHash {
    param(
        [Parameter(Mandatory)]
        [string] $PackagePath
    )

    $resolvedPackagePath = [System.IO.Path]::GetFullPath($PackagePath)
    if (-not (Test-Path -LiteralPath $resolvedPackagePath -PathType Leaf)) {
        throw "Package does not exist: $resolvedPackagePath"
    }

    $result = @{}
    $archive = [System.IO.Compression.ZipFile]::OpenRead($resolvedPackagePath)
    try {
        foreach ($entry in $archive.Entries) {
            if ([string]::IsNullOrEmpty($entry.Name)) {
                continue
            }

            $normalizedName = $entry.FullName
            if ($normalizedName -match '^package/services/metadata/core-properties/[0-9a-f]+\.psmdcp$') {
                $normalizedName = 'package/services/metadata/core-properties/core-properties.psmdcp'
            }
            if ($result.ContainsKey($normalizedName)) {
                throw "$resolvedPackagePath contains duplicate normalized entry '$normalizedName'."
            }

            $memory = [System.IO.MemoryStream]::new()
            $stream = $entry.Open()
            try {
                $stream.CopyTo($memory)
            }
            finally {
                $stream.Dispose()
            }
            $bytes = $memory.ToArray()
            $memory.Dispose()

            if ($entry.FullName -eq '_rels/.rels') {
                $relationshipText = [System.Text.Encoding]::UTF8.GetString($bytes)
                $normalizedLine = @(
                    $relationshipText -split "`r?`n" |
                        ForEach-Object {
                            if ($_.Contains('/package/services/metadata/core-properties/')) {
                                $line = $_ -replace 'Target="/package/services/metadata/core-properties/[^"]+"', 'Target="/package/services/metadata/core-properties/core-properties.psmdcp"'
                                $line -replace 'Id="R[0-9A-Fa-f]+"', 'Id="RCOREPROPERTIES"'
                            }
                            else {
                                $_
                            }
                        }) -join "`n"
                $bytes = [System.Text.Encoding]::UTF8.GetBytes($normalizedLine)
            }

            $sha256 = [System.Security.Cryptography.SHA256]::Create()
            try {
                $result[$normalizedName] = [System.Convert]::ToHexString(
                    $sha256.ComputeHash($bytes))
            }
            finally {
                $sha256.Dispose()
            }
        }
    }
    finally {
        $archive.Dispose()
    }

    return $result
}

$firstEntryHash = Get-NormalizedPackageEntryHash -PackagePath $FirstPackage
$secondEntryHash = Get-NormalizedPackageEntryHash -PackagePath $SecondPackage
$allEntryName = @($firstEntryHash.Keys + $secondEntryHash.Keys | Sort-Object -Unique)
$difference = @(
    foreach ($entryName in $allEntryName) {
        if (-not $firstEntryHash.ContainsKey($entryName)) {
            "Only in second package: $entryName"
        }
        elseif (-not $secondEntryHash.ContainsKey($entryName)) {
            "Only in first package: $entryName"
        }
        elseif ($firstEntryHash[$entryName] -ne $secondEntryHash[$entryName]) {
            "Payload differs: $entryName"
        }
    })
if ($difference.Count -ne 0) {
    throw "Normalized package payloads are not deterministic:`n$($difference -join "`n")"
}

Write-Host "Normalized package payload is deterministic: $([System.IO.Path]::GetFileName($FirstPackage))" -ForegroundColor Green
