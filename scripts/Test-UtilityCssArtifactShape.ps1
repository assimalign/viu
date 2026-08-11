<#
.SYNOPSIS
    Verifies the Viu Utilities payload embedded in distributable NuGet and VSIX archives.

.PARAMETER BrowserSdkPackage
    Optional Assimalign.Viu.Sdk.Browser NuGet package to inspect.

.PARAMETER UtilityCssPackage
    Optional Assimalign.Viu.UtilityCss NuGet package to inspect.

.PARAMETER VisualStudioExtension
    Optional Assimalign.Viu.VisualStudio VSIX package to inspect.
#>
[CmdletBinding()]
param(
    [string] $BrowserSdkPackage = '',
    [string] $UtilityCssPackage = '',
    [string] $VisualStudioExtension = ''
)

$ErrorActionPreference = 'Stop'

if ([string]::IsNullOrWhiteSpace($BrowserSdkPackage) -and
    [string]::IsNullOrWhiteSpace($UtilityCssPackage) -and
    [string]::IsNullOrWhiteSpace($VisualStudioExtension)) {
    throw 'Specify at least one Browser SDK package, utility CSS package, or Visual Studio extension.'
}

Add-Type -AssemblyName System.IO.Compression.FileSystem

function Open-ArtifactArchive {
    param(
        [Parameter(Mandatory)]
        [string] $Path
    )

    $resolvedPath = (Resolve-Path -LiteralPath $Path).Path
    return [System.IO.Compression.ZipFile]::OpenRead($resolvedPath)
}

function Assert-ArchiveEntries {
    param(
        [Parameter(Mandatory)]
        [System.IO.Compression.ZipArchive] $Archive,

        [Parameter(Mandatory)]
        [string[]] $EntryNames,

        [Parameter(Mandatory)]
        [string] $ArtifactName
    )

    foreach ($entryName in $EntryNames) {
        $entry = $Archive.GetEntry($entryName)
        if ($null -eq $entry) {
            throw "$ArtifactName is missing $entryName."
        }
        if ($entry.Length -eq 0) {
            throw "$ArtifactName contains an empty $entryName."
        }
    }
}

function Assert-ThirdPartyNotice {
    param(
        [Parameter(Mandatory)]
        [System.IO.Compression.ZipArchive] $Archive,

        [Parameter(Mandatory)]
        [string] $EntryName,

        [Parameter(Mandatory)]
        [string] $ArtifactName
    )

    $entry = $Archive.GetEntry($EntryName)
    if ($null -eq $entry) {
        throw "$ArtifactName is missing $EntryName."
    }

    $reader = [System.IO.StreamReader]::new($entry.Open())
    try {
        $notice = $reader.ReadToEnd()
    }
    finally {
        $reader.Dispose()
    }

    foreach ($requiredText in @(
        'Tailwind CSS v4.3.3',
        'MIT License',
        'not affiliated with or endorsed',
        'Tailwind Labs'
    )) {
        if (-not $notice.Contains(
                $requiredText,
                [System.StringComparison]::Ordinal)) {
            throw "$ArtifactName notice $EntryName is missing '$requiredText'."
        }
    }
}

function Assert-NoForbiddenNuGetDependencies {
    param(
        [Parameter(Mandatory)]
        [System.IO.Compression.ZipArchive] $Archive,

        [Parameter(Mandatory)]
        [string] $EntryName,

        [Parameter(Mandatory)]
        [string] $ArtifactName
    )

    $entry = $Archive.GetEntry($EntryName)
    if ($null -eq $entry) {
        throw "$ArtifactName is missing $EntryName."
    }

    $reader = [System.IO.StreamReader]::new($entry.Open())
    try {
        $nuspec = $reader.ReadToEnd()
    }
    finally {
        $reader.Dispose()
    }

    if ($nuspec -match
        '(?is)<dependency\b[^>]*\bid\s*=\s*["''][^"'']*(?:tailwind|postcss|vite)[^"'']*["'']') {
        throw "$ArtifactName declares a forbidden dependency in $EntryName."
    }
}

if (-not [string]::IsNullOrWhiteSpace($BrowserSdkPackage)) {
    $archive = Open-ArtifactArchive -Path $BrowserSdkPackage
    try {
        $artifactName = [System.IO.Path]::GetFileName($BrowserSdkPackage)
        $requiredEntries = @(
            'Assimalign.Viu.Sdk.Browser.nuspec',
            'Sdk/Sdk.targets',
            'Targets/Assimalign.Viu.Sdk.Browser.UtilityCss.targets',
            'Targets/Assimalign.Viu.Sdk.Browser.Css.HotReload.targets',
            'Tasks/Assimalign.Viu.Sdk.Browser.Tasks.dll',
            'Tasks/Assimalign.Viu.UtilityCss.dll',
            'Tasks/Assimalign.Viu.UtilityCss.THIRD-PARTY-NOTICES.md',
            'Watch/Assimalign.Viu.Sdk.CssHotReload.deps.json',
            'Watch/Assimalign.Viu.Sdk.CssHotReload.dll',
            'Watch/Assimalign.Viu.Sdk.CssHotReload.runtimeconfig.json'
        )
        Assert-ArchiveEntries `
            -Archive $archive `
            -EntryNames $requiredEntries `
            -ArtifactName $artifactName
        Assert-ThirdPartyNotice `
            -Archive $archive `
            -EntryName 'Tasks/Assimalign.Viu.UtilityCss.THIRD-PARTY-NOTICES.md' `
            -ArtifactName $artifactName
        Assert-NoForbiddenNuGetDependencies `
            -Archive $archive `
            -EntryName 'Assimalign.Viu.Sdk.Browser.nuspec' `
            -ArtifactName $artifactName

        $expectedWatchEntries = @(
            $requiredEntries |
                Where-Object {
                    $_.StartsWith(
                        'Watch/',
                        [System.StringComparison]::Ordinal)
                } |
                Sort-Object
        )
        $actualWatchEntries = @(
            $archive.Entries |
                ForEach-Object FullName |
                Where-Object {
                    $_.StartsWith(
                        'Watch/',
                        [System.StringComparison]::Ordinal)
                } |
                Sort-Object
        )
        $watchDifference = @(
            Compare-Object $expectedWatchEntries $actualWatchEntries
        )
        if ($watchDifference.Count -ne 0) {
            throw "$artifactName Watch payload differs from the platform-neutral worker contract: $($watchDifference | Out-String)"
        }
    }
    finally {
        $archive.Dispose()
    }
}

if (-not [string]::IsNullOrWhiteSpace($UtilityCssPackage)) {
    $archive = Open-ArtifactArchive -Path $UtilityCssPackage
    try {
        $artifactName = [System.IO.Path]::GetFileName($UtilityCssPackage)
        Assert-ArchiveEntries `
            -Archive $archive `
            -EntryNames @(
                'Assimalign.Viu.UtilityCss.nuspec',
                'lib/netstandard2.0/Assimalign.Viu.UtilityCss.dll',
                'THIRD-PARTY-NOTICES.md'
            ) `
            -ArtifactName $artifactName
        Assert-ThirdPartyNotice `
            -Archive $archive `
            -EntryName 'THIRD-PARTY-NOTICES.md' `
            -ArtifactName $artifactName
        Assert-NoForbiddenNuGetDependencies `
            -Archive $archive `
            -EntryName 'Assimalign.Viu.UtilityCss.nuspec' `
            -ArtifactName $artifactName
    }
    finally {
        $archive.Dispose()
    }
}

if (-not [string]::IsNullOrWhiteSpace($VisualStudioExtension)) {
    $archive = Open-ArtifactArchive -Path $VisualStudioExtension
    try {
        $artifactName = [System.IO.Path]::GetFileName($VisualStudioExtension)
        $runtimeIdentifiers = @('win-x64', 'win-arm64')
        $requiredEntries = @(
            'extension.vsixmanifest',
            'language-server.json'
        )
        foreach ($runtimeIdentifier in $runtimeIdentifiers) {
            $requiredEntries += @(
                "LanguageServer/$runtimeIdentifier/Assimalign.Viu.LanguageServer.exe",
                "LanguageServer/$runtimeIdentifier/Assimalign.Viu.UtilityCss.THIRD-PARTY-NOTICES.md"
            )
        }

        Assert-ArchiveEntries `
            -Archive $archive `
            -EntryNames $requiredEntries `
            -ArtifactName $artifactName

        foreach ($runtimeIdentifier in $runtimeIdentifiers) {
            Assert-ThirdPartyNotice `
                -Archive $archive `
                -EntryName "LanguageServer/$runtimeIdentifier/Assimalign.Viu.UtilityCss.THIRD-PARTY-NOTICES.md" `
                -ArtifactName $artifactName
        }
    }
    finally {
        $archive.Dispose()
    }
}

Write-Host 'Viu Utilities artifact-shape validation passed.'
