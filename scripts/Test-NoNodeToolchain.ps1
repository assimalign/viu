<#
.SYNOPSIS
    Verifies that Viu source and distributable archives contain no Node-based CSS toolchain.

.DESCRIPTION
    Scans the repository's build and dependency manifests plus optional NuGet, VSIX, or ZIP
    archives for Tailwind, Node, PostCSS, or Vite dependencies and executable payloads. The guard
    protects Viu's standalone build and distribution boundary independently of any CSS add-on.

.PARAMETER RepositoryRoot
    Repository root to inspect. Defaults to the parent of this script directory.

.PARAMETER Artifact
    Optional NuGet, VSIX, or ZIP archives whose entry names and dependency manifests are inspected.
#>
[CmdletBinding()]
param(
    [string] $RepositoryRoot = '',
    [string[]] $Artifact = @()
)

$ErrorActionPreference = 'Stop'
if ([string]::IsNullOrWhiteSpace($RepositoryRoot)) {
    $RepositoryRoot = Split-Path $PSScriptRoot -Parent
}

$resolvedRepositoryRoot = (Resolve-Path -LiteralPath $RepositoryRoot).Path
$violations = [System.Collections.Generic.List[string]]::new()
$excludedDirectoryNames = [System.Collections.Generic.HashSet[string]]::new(
    [System.StringComparer]::OrdinalIgnoreCase)
@('.git', '_out', 'bin', 'obj') | ForEach-Object {
    [void] $excludedDirectoryNames.Add($_)
}
$forbiddenDependencyExpression =
    '(?i)(?:@tailwindcss/|tailwindcss(?:/|["''\s])|postcss(?:/|["''\s])|(?:^|[/""''])vite(?:/|["''\s]))'
$forbiddenArchiveEntryExpression =
    '(?i)(?:^|/)(?:node_modules|tailwindcss|@tailwindcss|postcss|vite)(?:/|$)|(?:^|/)node(?:\.exe)?$'

function Add-Violation {
    param([string] $Message)

    $violations.Add($Message)
}

function Get-RepositoryFiles {
    $pendingDirectories = [System.Collections.Generic.Stack[string]]::new()
    $pendingDirectories.Push($resolvedRepositoryRoot)
    while ($pendingDirectories.Count -ne 0) {
        $directory = $pendingDirectories.Pop()
        foreach ($childDirectory in [System.IO.Directory]::EnumerateDirectories($directory)) {
            if (-not $excludedDirectoryNames.Contains(
                [System.IO.Path]::GetFileName($childDirectory))) {
                $pendingDirectories.Push($childDirectory)
            }
        }

        foreach ($file in [System.IO.Directory]::EnumerateFiles($directory)) {
            Write-Output $file
        }
    }
}

$repositoryFiles = @(Get-RepositoryFiles)
$buildFiles = $repositoryFiles |
    Where-Object {
        [System.IO.Path]::GetExtension($_) -in @('.csproj', '.props', '.targets')
    }

foreach ($buildFile in $buildFiles) {
    $content = Get-Content -LiteralPath $buildFile -Raw
    if ($content -match
        '(?is)<(?:Viu)?PackageReference\b[^>]*\bInclude\s*=\s*["''][^"'']*(?:tailwind|postcss|vite)(?:[^"'']*)["'']') {
        Add-Violation "Forbidden package reference in $buildFile"
    }

    if ($content -match
        '(?is)<Exec\b[^>]*\bCommand\s*=\s*["''][^"'']*(?:tailwind|postcss|(?:^|\s)npx(?:\s|$)|(?:^|\s)node(?:\s|$))') {
        Add-Violation "Forbidden executable invocation in $buildFile"
    }
}

$dependencyFiles = $repositoryFiles |
    Where-Object {
        ([System.IO.Path]::GetFileName($_) -in @(
            'package.json',
            'package-lock.json',
            'pnpm-lock.yaml',
            'yarn.lock',
            'bun.lock',
            'bun.lockb'
        ))
    }

foreach ($dependencyFile in $dependencyFiles) {
    $content = Get-Content -LiteralPath $dependencyFile -Raw
    if ($content -match $forbiddenDependencyExpression) {
        Add-Violation "Forbidden JavaScript dependency in $dependencyFile"
    }
}

Add-Type -AssemblyName System.IO.Compression.FileSystem
foreach ($artifactPath in $Artifact) {
    $resolvedArtifact = (Resolve-Path -LiteralPath $artifactPath).Path
    $archive = [System.IO.Compression.ZipFile]::OpenRead($resolvedArtifact)
    try {
        foreach ($entry in $archive.Entries) {
            $entryName = $entry.FullName.Replace('\', '/')
            if ($entryName -match $forbiddenArchiveEntryExpression) {
                Add-Violation "Forbidden payload '$entryName' in $resolvedArtifact"
            }

            $dependencyManifestNames = @(
                'package.json',
                'package-lock.json',
                'pnpm-lock.yaml',
                'yarn.lock',
                'bun.lock',
                'bun.lockb'
            )
            $isDependencyManifest =
                $entryName.EndsWith(
                    '.deps.json',
                    [System.StringComparison]::OrdinalIgnoreCase) -or
                $entryName.EndsWith(
                    '.nuspec',
                    [System.StringComparison]::OrdinalIgnoreCase) -or
                $dependencyManifestNames.Contains(
                    [System.IO.Path]::GetFileName($entryName))
            if (-not $isDependencyManifest) {
                continue
            }

            $reader = [System.IO.StreamReader]::new($entry.Open())
            try {
                $dependencyManifest = $reader.ReadToEnd()
            }
            finally {
                $reader.Dispose()
            }

            if ($dependencyManifest -match $forbiddenDependencyExpression) {
                Add-Violation "Forbidden dependency manifest entry '$entryName' in $resolvedArtifact"
            }
        }
    }
    finally {
        $archive.Dispose()
    }
}

if ($violations.Count -ne 0) {
    $violations | ForEach-Object { Write-Error $_ }
    throw "Viu no-Node-toolchain validation found $($violations.Count) violation(s)."
}

Write-Host 'Viu no-Node-toolchain validation passed.'
