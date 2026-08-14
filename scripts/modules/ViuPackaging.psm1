<#
.SYNOPSIS
    The single source of truth for which Viu projects produce NuGet packages, and
    for locating the package caches a same-version repack must invalidate.

.DESCRIPTION
    Both scripts/Install-Local.ps1 (the local dogfooding feed) and
    scripts/Pack-Release.ps1 (the release set) import this module, so the two can
    never disagree about what ships. Get-ViuLibraryProject carries the drift guard:
    a new packable library under either code root — libraries/ (public package
    surfaces) or tooling/ (developer tooling: build-time and editor libraries) —
    that is not in the inventory fails the pack rather than silently shipping an
    incomplete set. Tooling currently contributes no package, but remains scanned
    so a future packability change cannot bypass the inventory.
#>

Set-StrictMode -Version Latest

# Every independently published Viu library, in dependency-safe order. A library
# added here must exist at <root>/<id>/src/<id>.csproj or
# <root>/<area>/<id>/src/<id>.csproj under one of $script:ViuCodeRoot;
# a packable library that exists but is missing here fails Get-ViuLibraryProject.
$script:ViuLibraryPackageIds = @(
    'Assimalign.Viu.Reactivity',
    'Assimalign.Viu.Components',
    'Assimalign.Viu.State',
    'Assimalign.Viu.Router',
    'Assimalign.Viu.UtilityCss',
    'Assimalign.Viu.UtilityCss.Build',
    'Assimalign.Viu.Core',
    # [V01.01.10.01] is an opt-in package and deliberately stays outside both App frameworks.
    'Assimalign.Viu.DevTools',
    'Assimalign.Viu.Browser',
    'Assimalign.Viu.ServerRenderer',
    'Assimalign.Viu.Testing',
    'Assimalign.Viu.Browser.Router'
)

# The repository's code roots that can hold independently published libraries. Projects can be
# direct children or grouped one level below an area folder; both shapes retain the inverted
# <assembly id>/{src,test} layout. The tooling root currently contributes no package, but both
# roots remain scanned so a future packable project is caught by the drift guard.
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
                            $codeRootDirectory = Join-Path $RepositoryDirectory $_
                            $candidateProjectDirectories = @(
                                Join-Path $codeRootDirectory $packageId
                                Get-ChildItem `
                                    -LiteralPath $codeRootDirectory `
                                    -Directory `
                                    -ErrorAction SilentlyContinue |
                                    ForEach-Object {
                                        Join-Path $_.FullName $packageId
                                    }
                            )

                            $candidateProjectDirectories |
                                ForEach-Object {
                                    [System.IO.Path]::GetFullPath(
                                        (Join-Path $_ "src/$packageId.csproj"))
                                }
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
                $codeRootDirectory = Join-Path $RepositoryDirectory $_
                Get-ChildItem `
                    -LiteralPath $codeRootDirectory `
                    -Directory `
                    -ErrorAction SilentlyContinue
            } |
            ForEach-Object {
                @($_) + @(
                    Get-ChildItem `
                        -LiteralPath $_.FullName `
                        -Directory `
                        -ErrorAction SilentlyContinue)
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
        Every Viu package id a repack can invalidate: the libraries, both SDKs,
        both targeting packs, one Browser runtime pack per runtime identifier, and
        the dotnet-new template pack.

    .PARAMETER Rids
        Runtime identifiers whose runtime packs are produced.
    #>
    [CmdletBinding()]
    param(
        [string[]] $Rids = @('browser-wasm')
    )

    return @($script:ViuLibraryPackageIds) +
        @(
            'Assimalign.Viu.Sdk',
            'Assimalign.Viu.Sdk.Browser',
            'Assimalign.Viu.App.Ref',
            'Assimalign.Viu.App.Browser.Ref'
        ) +
        @($Rids | ForEach-Object { "Assimalign.Viu.App.Browser.Runtime.$_" }) +
        @('Assimalign.Viu.Templates')
}

function Test-ViuPackageContainsManagedAssembly {
    <#
    .SYNOPSIS
        Reports whether a main package carries a managed assembly payload.

    .DESCRIPTION
        Executable library/runtime packages with managed assemblies normally require a portable-PDB
        symbol package. Content-only packages contain no managed payload, while the release validator
        separately names the deliberate targeting, SDK, and MSBuild-task container exceptions. This helper reports archive
        content only; it does not decide the symbol-package policy.
    #>
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)]
        [string] $PackagePath
    )

    Add-Type -AssemblyName System.IO.Compression.FileSystem
    $archive = [System.IO.Compression.ZipFile]::OpenRead(
        [System.IO.Path]::GetFullPath($PackagePath))
    try {
        return @(
            $archive.Entries |
                Where-Object {
                    $_.Name.EndsWith(
                        '.dll',
                        [System.StringComparison]::OrdinalIgnoreCase)
                }).Count -gt 0
    }
    finally {
        $archive.Dispose()
    }
}

function Test-ViuPackageRequiresSymbolPackage {
    <#
    .SYNOPSIS
        Reports whether a release package must have a companion .snupkg.

    .DESCRIPTION
        Applies Viu's symbol-publication policy to the physical main package. Executable
        library/runtime packages with managed assemblies require symbols. The two targeting packs
        and the SDK/MSBuild-task distribution containers deliberately do not publish implementation symbols.

    .PARAMETER PackageId
        Package id whose distribution role selects any explicit container exception.

    .PARAMETER PackagePath
        Main .nupkg inspected for a managed assembly payload.
    #>
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)]
        [string] $PackageId,

        [Parameter(Mandatory)]
        [string] $PackagePath
    )

    $packageIdWithoutSymbolPackage = @(
        'Assimalign.Viu.App.Ref',
        'Assimalign.Viu.App.Browser.Ref',
        'Assimalign.Viu.Sdk',
        'Assimalign.Viu.Sdk.Browser',
        'Assimalign.Viu.UtilityCss.Build')
    if ($packageIdWithoutSymbolPackage -contains $PackageId) {
        return $false
    }

    return Test-ViuPackageContainsManagedAssembly -PackagePath $PackagePath
}

function Assert-ViuSymbolPackageMatchesMainPackage {
    <#
    .SYNOPSIS
        Verifies that every symbol-package PDB maps to a main-package managed PE.

    .DESCRIPTION
        nuget.org accepts a portable PDB only when the corresponding DLL, EXE, or WinMD exists in
        the main package at the same relative archive path. This assertion applies that publication
        contract to custom Viu package layouts as well as conventional lib/<tfm>/ packages.

    .PARAMETER MainPackagePath
        Main .nupkg whose managed PE entries own the symbols.

    .PARAMETER SymbolPackagePath
        Companion .snupkg whose PDB entries must mirror the main package.

    .PARAMETER PackageId
        Package id used in validation errors.
    #>
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)]
        [string] $MainPackagePath,

        [Parameter(Mandatory)]
        [string] $SymbolPackagePath,

        [Parameter(Mandatory)]
        [string] $PackageId
    )

    Add-Type -AssemblyName System.IO.Compression.FileSystem

    $managedPortableExecutablePath = [System.Collections.Generic.HashSet[string]]::new(
        [System.StringComparer]::OrdinalIgnoreCase)
    $mainArchive = [System.IO.Compression.ZipFile]::OpenRead(
        [System.IO.Path]::GetFullPath($MainPackagePath))
    try {
        foreach ($entry in $mainArchive.Entries) {
            if ($entry.Name.EndsWith(
                    '.dll',
                    [System.StringComparison]::OrdinalIgnoreCase) -or
                $entry.Name.EndsWith(
                    '.exe',
                    [System.StringComparison]::OrdinalIgnoreCase) -or
                $entry.Name.EndsWith(
                    '.winmd',
                    [System.StringComparison]::OrdinalIgnoreCase)) {
                $null = $managedPortableExecutablePath.Add($entry.FullName)
            }
        }
    }
    finally {
        $mainArchive.Dispose()
    }

    $symbolArchive = [System.IO.Compression.ZipFile]::OpenRead(
        [System.IO.Path]::GetFullPath($SymbolPackagePath))
    try {
        $portableSymbols = @(
            $symbolArchive.Entries |
                Where-Object {
                    $_.Name.EndsWith(
                        '.pdb',
                        [System.StringComparison]::OrdinalIgnoreCase)
                })
        if ($portableSymbols.Count -eq 0) {
            throw "$PackageId symbol package contains no portable PDB."
        }

        foreach ($portableSymbol in $portableSymbols) {
            $matchedPortableExecutable = $false
            foreach ($extension in @('.dll', '.exe', '.winmd')) {
                $portableExecutablePath = [System.IO.Path]::ChangeExtension(
                    $portableSymbol.FullName,
                    $extension)
                if ($managedPortableExecutablePath.Contains($portableExecutablePath)) {
                    $matchedPortableExecutable = $true
                    break
                }
            }
            if (-not $matchedPortableExecutable) {
                throw "$PackageId symbol package PDB '$($portableSymbol.FullName)' does not have a managed PE at the same relative path in its main package."
            }
        }
    }
    finally {
        $symbolArchive.Dispose()
    }
}

function Assert-ViuReleasePackageSet {
    <#
    .SYNOPSIS
        Validates the complete Viu release package set before publication.

    .DESCRIPTION
        Enforces the [V01.01.12.03] package contract over the physical archives:
        coherent versions, required NuGet metadata and embedded files, bounded
        intra-Viu dependency ranges, the deliberate symbol-package inventory, exact PDB-to-PE
        relative-path matches for every .snupkg, and
        a targeting-pack analyzer manifest that exactly matches the
        analyzers/dotnet/cs payload. The function is deliberately shared by the
        local validator and release packer so publication cannot bypass it.

    .PARAMETER PackageDirectory
        Directory containing only the release .nupkg and .snupkg files.

    .PARAMETER Version
        Exact SemVer expected in every package.

    .PARAMETER ExpectedPackageId
        Exact release inventory. Defaults to Get-ViuPackageId.
    #>
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)]
        [string] $PackageDirectory,

        [Parameter(Mandatory)]
        [string] $Version,

        [string[]] $ExpectedPackageId = @(Get-ViuPackageId)
    )

    Add-Type -AssemblyName System.IO.Compression.FileSystem

    $resolvedPackageDirectory = [System.IO.Path]::GetFullPath($PackageDirectory)
    if (-not (Test-Path -LiteralPath $resolvedPackageDirectory -PathType Container)) {
        throw "The package directory does not exist: $resolvedPackageDirectory"
    }

    $versionMatch = [System.Text.RegularExpressions.Regex]::Match(
        $Version,
        '^(?<major>0|[1-9][0-9]*)\.(?<minor>0|[1-9][0-9]*)\.(?<patch>0|[1-9][0-9]*)(?:-[0-9A-Za-z-]+(?:\.[0-9A-Za-z-]+)*)?$')
    if (-not $versionMatch.Success) {
        throw "Package-set version '$Version' is not supported SemVer."
    }
    $nextMajorVersion = ([int] $versionMatch.Groups['major'].Value) + 1
    $compatibleRange = "[$Version,$nextMajorVersion.0.0)"
    $exactRange = "[$Version]"

    $expectedMainPackageFile = @(
        $ExpectedPackageId |
            ForEach-Object { "$($_).$Version.nupkg" } |
            Sort-Object)
    $actualMainPackageFile = @(
        Get-ChildItem -LiteralPath $resolvedPackageDirectory -Filter '*.nupkg' -File |
            Where-Object {
                $_.Name.EndsWith(
                    ".$Version.nupkg",
                    [System.StringComparison]::OrdinalIgnoreCase) -and
                -not $_.Name.EndsWith(
                    '.symbols.nupkg',
                    [System.StringComparison]::OrdinalIgnoreCase)
            } |
            ForEach-Object Name |
            Sort-Object)
    $actualSymbolPackageFile = @(
        Get-ChildItem -LiteralPath $resolvedPackageDirectory -Filter '*.snupkg' -File |
            Where-Object {
                $_.Name.EndsWith(
                    ".$Version.snupkg",
                    [System.StringComparison]::OrdinalIgnoreCase)
            } |
            ForEach-Object Name |
            Sort-Object)

    $mainPackageDifference = @(
        Compare-Object $expectedMainPackageFile $actualMainPackageFile)
    if ($mainPackageDifference.Count -ne 0) {
        throw "The main release package inventory differs from the contract: $($mainPackageDifference | Out-String)"
    }

    $expectedSymbolPackageId = @(
        $ExpectedPackageId |
            Where-Object {
                Test-ViuPackageRequiresSymbolPackage `
                    -PackageId $_ `
                    -PackagePath (Join-Path $resolvedPackageDirectory "$($_).$Version.nupkg")
            })
    $expectedSymbolPackageFile = @(
        $expectedSymbolPackageId |
            ForEach-Object { "$($_).$Version.snupkg" } |
            Sort-Object)
    $symbolPackageDifference = @(
        Compare-Object $expectedSymbolPackageFile $actualSymbolPackageFile)
    if ($symbolPackageDifference.Count -ne 0) {
        throw "The symbol release package inventory differs from the contract: $($symbolPackageDifference | Out-String)"
    }

    $exactDependencyPackageId = @(
        'Assimalign.Viu.App.Ref',
        'Assimalign.Viu.App.Browser.Ref',
        'Assimalign.Viu.App.Browser.Runtime.browser-wasm',
        'Assimalign.Viu.Sdk.Browser')

    foreach ($packageId in $ExpectedPackageId) {
        $packagePath = Join-Path $resolvedPackageDirectory "$packageId.$Version.nupkg"
        $archive = [System.IO.Compression.ZipFile]::OpenRead($packagePath)
        try {
            $nuspecEntry = @(
                $archive.Entries |
                    Where-Object {
                        $_.FullName.EndsWith(
                            '.nuspec',
                            [System.StringComparison]::OrdinalIgnoreCase)
                    })
            if ($nuspecEntry.Count -ne 1) {
                throw "$packageId must contain exactly one nuspec, found $($nuspecEntry.Count)."
            }

            $nuspecReader = [System.IO.StreamReader]::new($nuspecEntry[0].Open())
            try {
                [xml] $nuspec = $nuspecReader.ReadToEnd()
            }
            finally {
                $nuspecReader.Dispose()
            }
            $metadata = $nuspec.package.metadata

            if ([string] $metadata.id -ne $packageId) {
                throw "$packagePath declares package id '$($metadata.id)' instead of '$packageId'."
            }
            if ([string] $metadata.version -ne $Version) {
                throw "$packageId declares version '$($metadata.version)' instead of '$Version'."
            }
            foreach ($requiredText in @(
                    @{ Name = 'authors'; Value = [string] $metadata.authors },
                    @{ Name = 'description'; Value = [string] $metadata.description },
                    @{ Name = 'tags'; Value = [string] $metadata.tags })) {
                if ([string]::IsNullOrWhiteSpace($requiredText.Value)) {
                    throw "$packageId is missing required $($requiredText.Name) metadata."
                }
            }
            $license = $metadata.SelectSingleNode("./*[local-name()='license']")
            if ($null -eq $license -or
                $license.GetAttribute('type') -ne 'file' -or
                $license.InnerText -ne 'LICENSE' -or
                $null -eq $archive.GetEntry('LICENSE')) {
                throw "$packageId must declare and embed LICENSE as its package license."
            }
            $readme = $metadata.SelectSingleNode("./*[local-name()='readme']")
            $readmePath = if ($null -eq $readme) { '' } else { $readme.InnerText }
            if ([string]::IsNullOrWhiteSpace($readmePath) -or
                $null -eq $archive.GetEntry($readmePath)) {
                throw "$packageId must declare and embed a package README."
            }

            $repository = $metadata.repository
            if ([string] $repository.type -ne 'git' -or
                [string] $repository.url -ne 'https://github.com/assimalign/viu' -or
                [string] $repository.commit -notmatch '^[0-9a-fA-F]{40,64}$') {
                throw "$packageId must carry git repository URL and commit metadata."
            }

            $mainPackageSymbols = @(
                $archive.Entries |
                    Where-Object { $_.Name.EndsWith('.pdb', [System.StringComparison]::OrdinalIgnoreCase) })
            if ($mainPackageSymbols.Count -ne 0) {
                throw "$packageId main package must not contain portable PDBs; symbol-bearing packages place them in the required .snupkg, while symbol-less containers omit them."
            }

            $expectedDependencyRange = $compatibleRange
            if ($exactDependencyPackageId -contains $packageId) {
                $expectedDependencyRange = $exactRange
            }
            $dependencies = @(
                $metadata.SelectNodes(".//*[local-name()='dependency']") |
                    Where-Object {
                        ([string] $_.id).StartsWith(
                            'Assimalign.Viu.',
                            [System.StringComparison]::Ordinal)
                    })
            foreach ($dependency in $dependencies) {
                if ($ExpectedPackageId -notcontains [string] $dependency.id) {
                    throw "$packageId has an unknown Viu dependency '$($dependency.id)'."
                }
                $actualDependencyRange = ([string] $dependency.version) -replace '\s', ''
                if ($actualDependencyRange -ne $expectedDependencyRange) {
                    throw "$packageId dependency '$($dependency.id)' uses '$($dependency.version)'; expected '$expectedDependencyRange'."
                }
            }

            if ($packageId -eq 'Assimalign.Viu.App.Ref') {
                $manifestEntry = $archive.GetEntry('data/FrameworkList.xml')
                if ($null -eq $manifestEntry) {
                    throw "$packageId is missing data/FrameworkList.xml."
                }
                $manifestReader = [System.IO.StreamReader]::new($manifestEntry.Open())
                try {
                    [xml] $manifest = $manifestReader.ReadToEnd()
                }
                finally {
                    $manifestReader.Dispose()
                }
                $manifestAnalyzerPath = @(
                    $manifest.DocumentElement.File |
                        Where-Object Type -eq 'Analyzer' |
                        ForEach-Object Path |
                        Sort-Object)
                $archiveAnalyzerPath = @(
                    $archive.Entries |
                        Where-Object {
                            $_.FullName.StartsWith(
                                'analyzers/dotnet/cs/',
                                [System.StringComparison]::Ordinal)
                        } |
                        ForEach-Object FullName |
                        Sort-Object)
                $analyzerDifference = @(
                    Compare-Object $manifestAnalyzerPath $archiveAnalyzerPath)
                if ($analyzerDifference.Count -ne 0) {
                    throw "$packageId analyzer archive and FrameworkList.xml differ: $($analyzerDifference | Out-String)"
                }
                foreach ($requiredGenerator in @(
                        'analyzers/dotnet/cs/Assimalign.Viu.Generators.Reactivity.dll',
                        'analyzers/dotnet/cs/Assimalign.Viu.Generators.Syntax.dll')) {
                    if ($archiveAnalyzerPath -notcontains $requiredGenerator) {
                        throw "$packageId is missing required generator $requiredGenerator."
                    }
                }
                $hostRoslynAssembly = @(
                    $archiveAnalyzerPath |
                        Where-Object {
                            [System.IO.Path]::GetFileName($_).StartsWith(
                                'Microsoft.CodeAnalysis',
                                [System.StringComparison]::Ordinal)
                        })
                if ($hostRoslynAssembly.Count -ne 0) {
                    throw "$packageId must not carry host-provided Roslyn assemblies: $($hostRoslynAssembly -join ', ')."
                }
            }
        }
        finally {
            $archive.Dispose()
        }

        if ($expectedSymbolPackageId -contains $packageId) {
            $symbolPackagePath = Join-Path $resolvedPackageDirectory "$packageId.$Version.snupkg"
            Assert-ViuSymbolPackageMatchesMainPackage `
                -MainPackagePath $packagePath `
                -SymbolPackagePath $symbolPackagePath `
                -PackageId $packageId
        }
    }

    Write-Host "Validated $($ExpectedPackageId.Count) Viu packages and $($expectedSymbolPackageId.Count) symbol packages at $Version." -ForegroundColor Green
}

function Assert-ViuFrameworkPackage {
    <#
    .SYNOPSIS
        Verifies a Viu targeting or runtime pack against its managed-assembly
        manifest and targeting-pack package overrides.

    .PARAMETER PackagePath
        The framework NuGet package to inspect.

    .PARAMETER ManifestPath
        The manifest entry path: data/FrameworkList.xml or data/RuntimeList.xml.

    .PARAMETER ExpectedFrameworkAssembly
        The exact managed assembly names expected in the manifest and archive.

    .PARAMETER ExpectedPackageOverride
        The exact standalone package ids expected in data/PackageOverrides.txt.
        Runtime packs pass an empty list and are asserted not to carry the file.
    #>
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)]
        [string] $PackagePath,

        [Parameter(Mandatory)]
        [string] $ManifestPath,

        [Parameter(Mandatory)]
        [string[]] $ExpectedFrameworkAssembly,

        [string[]] $ExpectedPackageOverride = @()
    )

    $package = Get-Item -LiteralPath $PackagePath -ErrorAction Stop
    $archive = [System.IO.Compression.ZipFile]::OpenRead($package.FullName)
    try {
        $manifestEntry = $archive.GetEntry($ManifestPath)
        if ($null -eq $manifestEntry) {
            throw "$($package.Name) is missing $ManifestPath."
        }

        $manifestReader = [System.IO.StreamReader]::new(
            $manifestEntry.Open())
        try {
            [xml] $manifest = $manifestReader.ReadToEnd()
        }
        finally {
            $manifestReader.Dispose()
        }

        $actualFrameworkAssemblies = @(
            $manifest.DocumentElement.File |
                Where-Object Type -eq 'Managed' |
                ForEach-Object AssemblyName |
                Sort-Object)
        $frameworkDifference = @(
            Compare-Object `
                ($ExpectedFrameworkAssembly | Sort-Object) `
                $actualFrameworkAssemblies)
        if ($frameworkDifference.Count -ne 0) {
            throw "$($package.Name) managed assembly manifest differs from the framework contract: $($frameworkDifference | Out-String)"
        }

        foreach ($assemblyName in $ExpectedFrameworkAssembly) {
            $assemblyFileName = "$assemblyName.dll"
            $matchingEntries = @(
                $archive.Entries |
                    Where-Object {
                        $_.FullName.EndsWith(
                            "/$assemblyFileName",
                            [System.StringComparison]::Ordinal)
                    })
            if ($matchingEntries.Count -ne 1) {
                throw "$($package.Name) must contain exactly one $assemblyFileName, found $($matchingEntries.Count)."
            }
        }

        $packageOverridesEntry = $archive.GetEntry(
            'data/PackageOverrides.txt')
        if ($ExpectedPackageOverride.Count -eq 0) {
            if ($null -ne $packageOverridesEntry) {
                throw "$($package.Name) must not carry targeting-only data/PackageOverrides.txt."
            }

            return
        }

        if ($null -eq $packageOverridesEntry) {
            throw "$($package.Name) is missing data/PackageOverrides.txt."
        }

        $nuspecEntries = @(
            $archive.Entries |
                Where-Object {
                    $_.FullName.EndsWith(
                        '.nuspec',
                        [System.StringComparison]::OrdinalIgnoreCase)
                })
        if ($nuspecEntries.Count -ne 1) {
            throw "$($package.Name) must contain exactly one nuspec, found $($nuspecEntries.Count)."
        }

        $nuspecReader = [System.IO.StreamReader]::new(
            $nuspecEntries[0].Open())
        try {
            [xml] $nuspec = $nuspecReader.ReadToEnd()
        }
        finally {
            $nuspecReader.Dispose()
        }
        $packageVersion = $nuspec.SelectSingleNode(
            "//*[local-name()='metadata']/*[local-name()='version']").InnerText

        $packageOverridesReader = [System.IO.StreamReader]::new(
            $packageOverridesEntry.Open())
        try {
            $actualPackageOverrides = @(
                $packageOverridesReader.ReadToEnd() -split '\r?\n' |
                    Where-Object {
                        -not [string]::IsNullOrWhiteSpace($_)
                    } |
                    Sort-Object)
        }
        finally {
            $packageOverridesReader.Dispose()
        }

        $expectedPackageOverrides = @(
            $ExpectedPackageOverride |
                ForEach-Object { "$_|$packageVersion" } |
                Sort-Object)
        $packageOverrideDifference = @(
            Compare-Object `
                $expectedPackageOverrides `
                $actualPackageOverrides)
        if ($packageOverrideDifference.Count -ne 0) {
            throw "$($package.Name) package overrides differ from the framework contract: $($packageOverrideDifference | Out-String)"
        }
    }
    finally {
        $archive.Dispose()
    }
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
    Test-ViuPackageContainsManagedAssembly,
    Test-ViuPackageRequiresSymbolPackage,
    Assert-ViuSymbolPackageMatchesMainPackage,
    Assert-ViuReleasePackageSet,
    Assert-ViuFrameworkPackage,
    Get-ViuPackageCacheRoot,
    Clear-ViuPackageCache
