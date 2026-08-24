<#
.SYNOPSIS
    Resolves one Visual Studio Code Marketplace version and its missing platform targets.

.DESCRIPTION
    Maps a canonical Viu version onto the Visual Studio Code Marketplace's numeric version lanes.
    Prereleases use the odd minor 2n+1 and stable releases use the even minor 2n. When the highest
    patch on that lane has only some required targets, the resolver reuses it and returns only the
    missing targets so a failed release can resume deterministically.

    GalleryResponse is an optional test seam for deterministic local validation. Production callers
    omit it and query the public Visual Studio Marketplace gallery directly.
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [string] $CanonicalVersion,

    [Parameter(Mandatory)]
    [ValidateSet('viu', 'viu-utilitycss')]
    [string] $PackageName,

    [ValidateSet('win32-x64', 'win32-arm64', 'linux-x64', 'darwin-x64', 'darwin-arm64')]
    [string[]] $TargetPlatform = @(
        'win32-x64',
        'win32-arm64',
        'linux-x64',
        'darwin-x64',
        'darwin-arm64'
    ),

    [object] $GalleryResponse
)

$ErrorActionPreference = 'Stop'

$canonicalVersionMatch = [System.Text.RegularExpressions.Regex]::Match(
    $CanonicalVersion,
    '^(?<major>0|[1-9][0-9]*)\.(?<minor>0|[1-9][0-9]*)\.(?<patch>0|[1-9][0-9]*)(?<suffix>-[0-9A-Za-z-]+(\.[0-9A-Za-z-]+)*)?$',
    [System.Text.RegularExpressions.RegexOptions]::CultureInvariant)
if (-not $canonicalVersionMatch.Success) {
    throw "Canonical Viu version '$CanonicalVersion' is not supported SemVer."
}

$canonicalMajor = [int]::Parse(
    $canonicalVersionMatch.Groups['major'].Value,
    [System.Globalization.CultureInfo]::InvariantCulture)
$canonicalMinor = [int]::Parse(
    $canonicalVersionMatch.Groups['minor'].Value,
    [System.Globalization.CultureInfo]::InvariantCulture)
$isPreRelease = $canonicalVersionMatch.Groups['suffix'].Success
if ($canonicalMinor -gt [Math]::Floor(([int]::MaxValue - 1) / 2)) {
    throw "Canonical minor version $canonicalMinor cannot be mapped to a Marketplace version."
}

$marketplaceMinor = ($canonicalMinor * 2) + $(if ($isPreRelease) { 1 } else { 0 })
$extensionIdentifier = "Assimalign.$PackageName"

if (-not $PSBoundParameters.ContainsKey('GalleryResponse')) {
    $query = @{
        filters = @(
            @{
                criteria = @(
                    @{
                        filterType = 7
                        value = $extensionIdentifier
                    }
                )
                pageNumber = 1
                pageSize = 1
                sortBy = 0
                sortOrder = 0
            }
        )
        assetTypes = @()
        # ExtensionQueryFlags.IncludeVersions
        flags = 1
    } | ConvertTo-Json -Depth 10 -Compress

    try {
        $GalleryResponse = Invoke-RestMethod `
            -Method Post `
            -Uri 'https://marketplace.visualstudio.com/_apis/public/gallery/extensionquery' `
            -Headers @{ Accept = 'application/json;api-version=7.1-preview.1' } `
            -ContentType 'application/json' `
            -Body $query
    }
    catch {
        throw "The Marketplace version query for $extensionIdentifier failed; refusing to publish. $($_.Exception.Message)"
    }
}

$extensions = @(
    $GalleryResponse.results |
        ForEach-Object { $_.extensions } |
        Where-Object { $null -ne $_ }
)
if ($extensions.Count -gt 1) {
    throw "The Marketplace returned more than one exact match for $extensionIdentifier."
}
if ($extensions.Count -eq 1) {
    $extension = $extensions[0]
    if (-not [string]::Equals(
        [string] $extension.publisher.publisherName,
        'Assimalign',
        [System.StringComparison]::OrdinalIgnoreCase) -or
        -not [string]::Equals(
            [string] $extension.extensionName,
            $PackageName,
            [System.StringComparison]::Ordinal)) {
        throw "The Marketplace response did not exactly match $extensionIdentifier."
    }
}

$laneVersions = @(
    if ($extensions.Count -eq 1) {
        foreach ($publishedVersion in @($extensions[0].versions)) {
            if ($null -eq $publishedVersion -or
                [string] $publishedVersion.version -notmatch '^(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)$') {
                continue
            }

            $version = [System.Version]::Parse([string] $publishedVersion.version)
            if ($version.Major -eq $canonicalMajor -and $version.Minor -eq $marketplaceMinor) {
                [pscustomobject]@{
                    Patch = $version.Build
                    TargetPlatform = [string] $publishedVersion.targetPlatform
                }
            }
        }
    }
)

$candidatePatch = 0
$publishedTargetPlatforms = @()
$reusesPartialVersion = $false
if ($laneVersions.Count -gt 0) {
    $highestPatch = ($laneVersions | Measure-Object -Property Patch -Maximum).Maximum
    $candidatePatch = $highestPatch
    $publishedTargetPlatforms = @(
        $laneVersions |
            Where-Object { $_.Patch -eq $highestPatch } |
            ForEach-Object { $_.TargetPlatform } |
            Where-Object { -not [string]::IsNullOrWhiteSpace($_) } |
            Sort-Object -Unique
    )
    $unexpectedTargetPlatforms = @(
        $publishedTargetPlatforms |
            Where-Object { $TargetPlatform -notcontains $_ }
    )
    if ($unexpectedTargetPlatforms.Count -gt 0) {
        throw "$extensionIdentifier $canonicalMajor.$marketplaceMinor.$highestPatch has unsupported Marketplace targets: $($unexpectedTargetPlatforms -join ', ')."
    }

    $missingTargetPlatforms = @(
        $TargetPlatform |
            Where-Object { $publishedTargetPlatforms -notcontains $_ }
    )
    if ($missingTargetPlatforms.Count -eq 0) {
        if ($highestPatch -ge [int]::MaxValue) {
            throw "The Marketplace patch version is exhausted for $extensionIdentifier $canonicalMajor.$marketplaceMinor."
        }

        $candidatePatch = $highestPatch + 1
        $publishedTargetPlatforms = @()
        $missingTargetPlatforms = @($TargetPlatform)
    }
    else {
        $reusesPartialVersion = $publishedTargetPlatforms.Count -gt 0
    }
}
else {
    $missingTargetPlatforms = @($TargetPlatform)
}

[pscustomobject]@{
    PackageName = $PackageName
    ExtensionIdentifier = $extensionIdentifier
    CanonicalVersion = $CanonicalVersion
    IsPreRelease = $isPreRelease
    Version = "$canonicalMajor.$marketplaceMinor.$candidatePatch"
    PublishedTargetPlatforms = @($publishedTargetPlatforms)
    TargetPlatformsToPublish = @($missingTargetPlatforms)
    ReusesPartialVersion = $reusesPartialVersion
}
