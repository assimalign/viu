<#
.SYNOPSIS
    The single source of truth for which Viu projects produce NuGet packages, and
    for locating the package caches a same-version repack must invalidate.

.DESCRIPTION
    Both scripts/Install-Local.ps1 (the local dogfooding feed) and
    scripts/Pack-Release.ps1 (the release set) import this module, so the two can
    never disagree about what ships. Get-ViuLibraryProject carries the drift guard:
    a new packable library under either code root — libraries/ (the runtime
    framework) or tooling/ (developer tooling: build-time and editor libraries) —
    that is not in the inventory fails the pack rather than silently shipping an
    incomplete set.
#>

Set-StrictMode -Version Latest

# Every independently published Viu library, in dependency-safe order. A library
# added here must exist at <root>/<id>/src/<id>.csproj under one of $script:ViuCodeRoot;
# a packable library that exists but is missing here fails Get-ViuLibraryProject.
$script:ViuLibraryPackageIds = @(
    'Assimalign.Viu.Reactivity',
    'Assimalign.Viu.Shared',
    'Assimalign.Viu.Components',
    'Assimalign.Viu.State',
    'Assimalign.Viu.Router',
    'Assimalign.Viu.UtilityCss',
    'Assimalign.Viu.Core',
    'Assimalign.Viu.Browser',
    'Assimalign.Viu.ServerRenderer',
    'Assimalign.Viu.Testing',
    'Assimalign.Viu.Browser.Router'
)

# The repository's code roots that hold independently published libraries, each using the
# inverted <root>/<assembly id>/{src,test} layout. libraries/ holds the runtime framework;
# tooling/ holds developer tooling (the build-time cores, the language service, the language
# server). Both are scanned so a new project in either is caught by the drift guard.
$script:ViuCodeRoot = @('libraries', 'tooling')

function Test-ViuProjectPackable {
    <#
    .SYNOPSIS
        Whether a project produces a NuGet package, read from its declared IsPackable.

    .DESCRIPTION
        Non-packable projects live beside the packable tooling libraries and must never enter
        the published inventory, so discovery
        filters on the same property the build honors. Reading the csproj directly keeps the
        guard free of an MSBuild evaluation; every Viu project that opts out declares
        IsPackable literally.

    .PARAMETER ProjectPath
        The project file to inspect.
    #>
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)]
        [string] $ProjectPath
    )

    $project = [xml](Get-Content -LiteralPath $ProjectPath -Raw)
    $isPackable = $project.SelectSingleNode('/Project/PropertyGroup/IsPackable')
    return -not ($isPackable -and $isPackable.InnerText.Trim() -eq 'false')
}

function Get-ViuLibraryPackageId {
    <#
    .SYNOPSIS
        The package ids of every independently published Viu library, in
        dependency-safe publication order.
    #>
    [CmdletBinding()]
    param()

    return $script:ViuLibraryPackageIds
}

function Get-ViuLibraryProject {
    <#
    .SYNOPSIS
        Resolves the library project files, failing when the inventory and the
        code roots disagree.

    .DESCRIPTION
        An inventory id is resolved against every code root in $script:ViuCodeRoot, so moving a
        library between libraries/ and tooling/ needs no inventory edit. The drift guard scans
        the same roots and compares packable projects only: non-packable source projects are
        deliberately outside the published set.

    .PARAMETER RepositoryDirectory
        The repository root.
    #>
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)]
        [string] $RepositoryDirectory
    )

    $configured = @(
        $script:ViuLibraryPackageIds |
            ForEach-Object {
                $packageId = $_
                $candidates = @(
                    $script:ViuCodeRoot |
                        ForEach-Object {
                            [System.IO.Path]::GetFullPath(
                                (Join-Path $RepositoryDirectory "$_/$packageId/src/$packageId.csproj"))
                        } |
                        Where-Object { Test-Path -LiteralPath $_ -PathType Leaf }
                )
                if ($candidates.Count -ne 1) {
                    throw "The Viu library package '$packageId' resolved to $($candidates.Count) project files under $($script:ViuCodeRoot -join ', ')."
                }

                $candidates[0]
            }
    )
    $discovered = @(
        $script:ViuCodeRoot |
            ForEach-Object {
                Get-ChildItem `
                    -LiteralPath (Join-Path $RepositoryDirectory $_) `
                    -Directory `
                    -ErrorAction SilentlyContinue
            } |
            ForEach-Object {
                Get-ChildItem `
                    -LiteralPath (Join-Path $_.FullName 'src') `
                    -Filter '*.csproj' `
                    -File `
                    -ErrorAction SilentlyContinue
            } |
            ForEach-Object { [System.IO.Path]::GetFullPath($_.FullName) } |
            Where-Object { Test-ViuProjectPackable -ProjectPath $_ }
    )

    $difference = @(Compare-Object ($configured | Sort-Object) ($discovered | Sort-Object))
    if ($difference.Count -ne 0) {
        throw "The Viu library package inventory is incomplete: $($difference | Out-String)"
    }

    return $configured
}

function Get-ViuPackageId {
    <#
    .SYNOPSIS
        Every Viu package id a repack can invalidate: the libraries, the SDK, the
        targeting pack, and one runtime pack per runtime identifier.

    .PARAMETER Rids
        Runtime identifiers whose runtime packs are produced.
    #>
    [CmdletBinding()]
    param(
        [string[]] $Rids = @('browser-wasm')
    )

    return @($script:ViuLibraryPackageIds) +
        @('Assimalign.Viu.Sdk', 'Assimalign.Viu.App.Ref') +
        @($Rids | ForEach-Object { "Assimalign.Viu.App.Runtime.$_" })
}

function Get-ViuPackageCacheRoot {
    <#
    .SYNOPSIS
        Locates every NuGet package cache that may hold a stale same-version Viu
        extract: the machine-global cache plus any repo-local globalPackagesFolder.

    .DESCRIPTION
        A consumer repository can redirect its extracts with
        <add key="globalPackagesFolder" ...> in its own nuget.config — the sibling
        viu-examples repository does exactly that. Those caches are invisible to a
        prune that only knows the global folder, which is how a same-version repack
        silently keeps serving the previous build's analyzer.

        Discovery is deliberately conservative: sibling roots are returned only when
        the cache directory already contains Assimalign.Viu extracts, so an unrelated
        repository that merely sets globalPackagesFolder is never a prune target.

    .PARAMETER RepositoryDirectory
        The Viu repository root.

    .PARAMETER AdditionalRoot
        Extra directories to search for a nuget.config, for consumers outside the
        repository's sibling set.
    #>
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)]
        [string] $RepositoryDirectory,

        [string[]] $AdditionalRoot = @()
    )

    $roots = [System.Collections.Generic.List[string]]::new()

    # The machine-global cache, honoring an explicit override.
    $global = $env:NUGET_PACKAGES
    if ([string]::IsNullOrWhiteSpace($global)) {
        $global = Join-Path $HOME '.nuget/packages'
    }
    $roots.Add([System.IO.Path]::GetFullPath($global))

    # Candidate directories: the repository itself, every sibling of it, and any
    # caller-supplied root.
    $candidates = [System.Collections.Generic.List[string]]::new()
    $candidates.Add($RepositoryDirectory)
    $parent = Split-Path $RepositoryDirectory -Parent
    if ($parent -and (Test-Path -LiteralPath $parent)) {
        Get-ChildItem -LiteralPath $parent -Directory -ErrorAction SilentlyContinue |
            ForEach-Object { $candidates.Add($_.FullName) }
    }
    foreach ($additional in $AdditionalRoot) {
        if (-not [string]::IsNullOrWhiteSpace($additional)) {
            $candidates.Add($additional)
        }
    }

    foreach ($candidate in $candidates) {
        $configPath = Join-Path $candidate 'nuget.config'
        if (-not (Test-Path -LiteralPath $configPath)) {
            continue
        }

        try {
            $config = [xml](Get-Content -LiteralPath $configPath -Raw)
        }
        catch {
            Write-Verbose "Skipping unreadable nuget.config: $configPath"
            continue
        }

        $folder = $config.SelectSingleNode(
            "/configuration/config/add[@key='globalPackagesFolder']/@value")
        if (-not $folder -or [string]::IsNullOrWhiteSpace($folder.Value)) {
            continue
        }

        # globalPackagesFolder is relative to the nuget.config that declares it.
        $resolved = [System.IO.Path]::GetFullPath(
            (Join-Path $candidate ($folder.Value -replace '/', [System.IO.Path]::DirectorySeparatorChar)))
        if (-not (Test-Path -LiteralPath $resolved)) {
            continue
        }

        # Safety gate: only a cache that already holds Viu extracts is ours to prune.
        $holdsViuPackages = @(
            Get-ChildItem -LiteralPath $resolved -Directory -Filter 'assimalign.viu*' -ErrorAction SilentlyContinue
        ).Count -gt 0
        if (-not $holdsViuPackages) {
            Write-Verbose "Skipping cache with no Viu extracts: $resolved"
            continue
        }

        if (-not $roots.Contains($resolved)) {
            $roots.Add($resolved)
        }
    }

    return $roots.ToArray()
}

function Clear-ViuPackageCache {
    <#
    .SYNOPSIS
        Removes cached extracts of the supplied Viu package ids from every cache
        root, so a same-version repack is re-extracted instead of served stale.

    .PARAMETER CacheRoot
        The cache directories to prune.

    .PARAMETER PackageId
        The Viu package ids to remove. Only directories whose name begins with
        'assimalign.viu' are ever removed.
    #>
    [CmdletBinding(SupportsShouldProcess)]
    param(
        [Parameter(Mandatory)]
        [string[]] $CacheRoot,

        [Parameter(Mandatory)]
        [string[]] $PackageId
    )

    $removed = 0
    foreach ($root in $CacheRoot) {
        if (-not (Test-Path -LiteralPath $root)) {
            continue
        }

        foreach ($id in $PackageId) {
            $name = $id.ToLowerInvariant()
            if (-not $name.StartsWith('assimalign.viu')) {
                throw "Refusing to prune non-Viu package '$id'."
            }

            $extract = Join-Path $root $name
            if (-not (Test-Path -LiteralPath $extract)) {
                continue
            }

            if ($PSCmdlet.ShouldProcess($extract, 'Remove cached package extract')) {
                Write-Host "  pruned $extract"
                Remove-Item -LiteralPath $extract -Recurse -Force
                $removed++
            }
        }
    }

    return $removed
}

Export-ModuleMember -Function `
    Get-ViuLibraryPackageId,
    Get-ViuLibraryProject,
    Get-ViuPackageId,
    Get-ViuPackageCacheRoot,
    Clear-ViuPackageCache
