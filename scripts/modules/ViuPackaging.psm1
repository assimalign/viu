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
    'Assimalign.Viu.FileRouting',
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
            $readmeEntry = if ([string]::IsNullOrWhiteSpace($readmePath)) {
                $null
            }
            else {
                $archive.GetEntry($readmePath)
            }
            if ($null -eq $readmeEntry) {
                throw "$packageId must declare and embed a package README."
            }
            $readmeReader = [System.IO.StreamReader]::new($readmeEntry.Open())
            try {
                $readmeContent = $readmeReader.ReadToEnd()
            }
            finally {
                $readmeReader.Dispose()
            }
            foreach ($badgeKind in @('v', 'dt')) {
                $badgeUrl = "https://img.shields.io/nuget/$badgeKind/$packageId"
                if (-not $readmeContent.Contains(
                        $badgeUrl,
                        [System.StringComparison]::Ordinal)) {
                    throw "$packageId package README must contain its own NuGet $badgeKind badge."
                }
            }
            $icon = $metadata.SelectSingleNode("./*[local-name()='icon']")
            $iconPath = if ($null -eq $icon) { '' } else { $icon.InnerText }
            if ($iconPath -ne 'viu-nuget-mono-light-128.png' -or
                $null -eq $archive.GetEntry($iconPath)) {
                throw "$packageId must declare and embed the canonical Viu NuGet icon."
            }
            if ($null -ne $metadata.SelectSingleNode("./*[local-name()='iconUrl']")) {
                throw "$packageId must not declare the obsolete package icon URL fallback."
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

# Prerelease identifiers the CI channel reserves. A canonical ViuVersion may not contain them, so a
# release can never be mistaken for a CI build, and the pruner can never select one.
$script:ViuCiReservedIdentifier = @('local', 'ci', 'pr')

function Get-ViuCiPackageVersion {
    <#
    .SYNOPSIS
        Derives the unique CI package version .github/workflows/ci-packages.yml publishes for one
        build of the canonical Viu version.

    .DESCRIPTION
        A CI version must sort BELOW the canonical version it builds toward. Appending to the
        canonical prerelease sorts above it: 10.0.0-preview.1.ci.7 is higher than
        10.0.0-preview.1, so every CI build on the feed would outrank the real release.

        So the CI version extends the canonical version's PREDECESSOR. The final prerelease
        identifier must be a positive integer, and it is decremented. The channel and build
        identifiers then follow:

            canonical 10.0.0-preview.1   ->  base 10.0.0-preview.0
            push to main, run 57         ->  10.0.0-preview.0.ci.57
            pull request 380, run 58     ->  10.0.0-preview.0.pr.380.58

        Every CI build therefore sorts above the previous line and below the canonical version, and
        within a channel builds order by the workflow run number.

        Numeric identifiers are Int32 because NuGet's VersionComparer parses them with
        Int32.TryParse and compares a larger value as text, which orders "9" above "10". The
        parameters bind as [int], so a value NuGet would misorder is rejected before it reaches a
        package.

        A stable canonical version, or a prerelease that does not end in a positive integer, has no
        derivable predecessor and is rejected rather than given an invented one. The reserved local,
        ci, and pr identifiers may not appear in the canonical version.

    .PARAMETER Version
        The canonical ViuVersion from build/Targets/Build.Version.props.

    .PARAMETER BuildNumber
        A number unique to the build within its channel; ci-packages.yml passes github.run_number.

    .PARAMETER PullRequestNumber
        The pull request the build belongs to. Selects the pr channel; omitted for main.
    #>
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)]
        [string] $Version,

        [Parameter(Mandatory)]
        [ValidateRange(1, 2147483647)]
        [int] $BuildNumber,

        [ValidateRange(1, 2147483647)]
        [int] $PullRequestNumber
    )

    $versionPattern = '^(?<major>0|[1-9][0-9]*)\.(?<minor>0|[1-9][0-9]*)\.(?<patch>0|[1-9][0-9]*)(?:-(?<suffix>[0-9A-Za-z-]+(?:\.[0-9A-Za-z-]+)*))?$'
    $versionMatch = [System.Text.RegularExpressions.Regex]::Match(
        $Version,
        $versionPattern,
        [System.Text.RegularExpressions.RegexOptions]::CultureInvariant)
    if (-not $versionMatch.Success) {
        throw "Version '$Version' must be SemVer in the supported form MAJOR.MINOR.PATCH-PRERELEASE."
    }

    if (-not $versionMatch.Groups['suffix'].Success) {
        throw "Canonical version '$Version' is stable, so no CI version can sort between the previous release and it. Move ViuPatchVersion to a prerelease line before publishing CI packages."
    }

    $canonicalIdentifier = $versionMatch.Groups['suffix'].Value.Split('.')
    foreach ($identifier in $canonicalIdentifier) {
        if ($identifier -match '^[0-9]+$' -and $identifier -notmatch '^(0|[1-9][0-9]*)$') {
            throw "Version '$Version' contains a numeric prerelease identifier with a leading zero."
        }

        if ($script:ViuCiReservedIdentifier -contains $identifier.ToLowerInvariant()) {
            throw "Canonical version '$Version' contains the reserved '$identifier' identifier."
        }
    }

    $lastIdentifier = $canonicalIdentifier[-1]
    $lastNumber = 0
    if ($lastIdentifier -notmatch '^[1-9][0-9]*$' -or
        -not [int]::TryParse($lastIdentifier, [ref] $lastNumber)) {
        throw "Canonical version '$Version' must end in a positive integer prerelease identifier (for example preview.1) so its CI builds can sort below it."
    }

    $versionPrefix = '{0}.{1}.{2}' -f
        $versionMatch.Groups['major'].Value,
        $versionMatch.Groups['minor'].Value,
        $versionMatch.Groups['patch'].Value

    $baseIdentifier = @($canonicalIdentifier | Select-Object -SkipLast 1) + @([string] ($lastNumber - 1))
    if ($PSBoundParameters.ContainsKey('PullRequestNumber')) {
        $channel = 'pr'
        $channelIdentifier = @('pr', [string] $PullRequestNumber, [string] $BuildNumber)
    }
    else {
        $channel = 'ci'
        $channelIdentifier = @('ci', [string] $BuildNumber)
    }

    $baseSuffix = $baseIdentifier -join '.'
    $versionSuffix = @($baseIdentifier + $channelIdentifier) -join '.'

    return [pscustomobject]@{
        CanonicalVersion = $Version
        BaseVersion      = "$versionPrefix-$baseSuffix"
        Version          = "$versionPrefix-$versionSuffix"
        VersionPrefix    = $versionPrefix
        VersionSuffix    = $versionSuffix
        Channel          = $channel
    }
}

function ConvertFrom-ViuCiPackageVersion {
    <#
    .SYNOPSIS
        Parses a version Get-ViuCiPackageVersion produced back into its parts, and returns nothing
        for every other version.

    .DESCRIPTION
        The inverse of Get-ViuCiPackageVersion, and the gate .github/workflows/ci-packages-prune.yml
        applies before it deletes anything: a version is a CI build only if it has exactly the shape
        the derivation emits,

            <base>.ci.<build>                   main
            <base>.pr.<pull request>.<build>    pull request

        where <base> is MAJOR.MINOR.PATCH-<prerelease>, its last prerelease identifier is a number
        (the decremented canonical number, so 0 is allowed), no identifier in it is the reserved
        local, ci, or pr, and <build> and <pull request> are positive Int32 values without a leading
        zero. Release versions (10.0.0-preview.1), stable versions, and near misses (10.0.0-ci.5,
        10.0.0-preview.0.ci.05, 10.0.0-preview.0.pr.15) all return nothing, so the pruner can never
        select them.

        The ci and pr labels compare case-insensitively, as NuGet compares prerelease labels.

    .PARAMETER Version
        A package version as the feed lists it.
    #>
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)]
        [AllowEmptyString()]
        [string] $Version
    )

    $coreMatch = [System.Text.RegularExpressions.Regex]::Match(
        $Version,
        '^(?<prefix>(?:0|[1-9][0-9]*)\.(?:0|[1-9][0-9]*)\.(?:0|[1-9][0-9]*))-(?<suffix>.+)$',
        [System.Text.RegularExpressions.RegexOptions]::CultureInvariant)
    if (-not $coreMatch.Success) {
        return
    }

    $identifier = $coreMatch.Groups['suffix'].Value.Split('.')
    foreach ($part in $identifier) {
        if ($part -notmatch '^[0-9A-Za-z-]+$') {
            return
        }

        if ($part -match '^[0-9]+$' -and $part -notmatch '^(0|[1-9][0-9]*)$') {
            return
        }
    }

    $pullRequestNumber = $null
    if ($identifier.Count -ge 4 -and $identifier[-3] -ieq 'pr') {
        $channel = 'pr'
        $pullRequestText = $identifier[-2]
        $buildText = $identifier[-1]
        $baseIdentifier = @($identifier | Select-Object -SkipLast 3)
    }
    elseif ($identifier.Count -ge 3 -and $identifier[-2] -ieq 'ci') {
        $channel = 'ci'
        $pullRequestText = $null
        $buildText = $identifier[-1]
        $baseIdentifier = @($identifier | Select-Object -SkipLast 2)
    }
    else {
        return
    }

    $buildNumber = 0
    if ($buildText -notmatch '^[1-9][0-9]*$' -or -not [int]::TryParse($buildText, [ref] $buildNumber)) {
        return
    }

    if ($channel -eq 'pr') {
        $parsedPullRequest = 0
        if ($pullRequestText -notmatch '^[1-9][0-9]*$' -or
            -not [int]::TryParse($pullRequestText, [ref] $parsedPullRequest)) {
            return
        }

        $pullRequestNumber = $parsedPullRequest
    }

    # The base is a canonical version with its last number decremented, so it is never empty, never
    # carries a reserved identifier, and ends in a number Get-ViuCiPackageVersion could emit.
    $baseNumber = 0
    if ($baseIdentifier.Count -lt 1 -or
        $baseIdentifier[-1] -notmatch '^(0|[1-9][0-9]*)$' -or
        -not [int]::TryParse($baseIdentifier[-1], [ref] $baseNumber)) {
        return
    }

    foreach ($part in $baseIdentifier) {
        if ($script:ViuCiReservedIdentifier -contains $part.ToLowerInvariant()) {
            return
        }
    }

    $versionPrefix = $coreMatch.Groups['prefix'].Value
    return [pscustomobject]@{
        Version           = $Version
        BaseVersion       = "$versionPrefix-$($baseIdentifier -join '.')"
        VersionPrefix     = $versionPrefix
        Channel           = $channel
        PullRequestNumber = $pullRequestNumber
        BuildNumber       = $buildNumber
    }
}

function Select-ViuCiPackageVersionToPrune {
    <#
    .SYNOPSIS
        Selects the CI package versions .github/workflows/ci-packages-prune.yml deletes from GitHub
        Packages.

    .DESCRIPTION
        Takes every version of every package the pruner listed and returns the ones to delete, in
        the order to delete them. Two rules select a version, and nothing else does:

            pull request   every <base>.pr.<N>.<build> version whose N is in
                           ClosedPullRequestNumber. N is compared as an integer, so closing pull
                           request 15 never selects pr.152 or pr.151.
            main           for each base, the main builds outside the MainBuildCount newest ones.
                           Omit MainBuildCount to leave main builds alone.

        A version that ConvertFrom-ViuCiPackageVersion does not recognize - a release, a stable, or
        any malformed version - is never selected, whatever the parameters say.

        Main builds are ranked as whole builds, not per package: a build's recency is the newest
        CreatedAt among its versions on any package, ties broken by the higher build number, and a
        build outside the newest MainBuildCount is selected on every package that carries it. So
        each kept build stays complete. Ranking by publish time rather than by build number keeps
        fresh builds if the workflow's run numbers ever restart, instead of deleting them as old.
        Each base is ranked on its own, so moving ViuPatchVersion to a new prerelease line does not
        count the previous line's builds against the new one.

        The output is in deletion order: closed pull requests (by number, then build), then main
        builds oldest first. Within one build the SDK packages come first, so a build the pruner
        stops partway through no longer resolves from its SDK pin, rather than failing somewhere in
        the package graph.

    .PARAMETER PackageVersion
        Every listed version, each with PackageName, VersionId, Version, and CreatedAt. CreatedAt
        is required on main versions when MainBuildCount is passed.

    .PARAMETER ClosedPullRequestNumber
        Pull requests whose builds are deleted. Pass only numbers the GitHub API reported closed.

    .PARAMETER MainBuildCount
        How many main builds to keep per base.
    #>
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)]
        [AllowEmptyCollection()]
        [object[]] $PackageVersion,

        [AllowEmptyCollection()]
        [int[]] $ClosedPullRequestNumber = @(),

        [ValidateRange(1, 2147483647)]
        [int] $MainBuildCount
    )

    $closedPullRequest = [System.Collections.Generic.HashSet[int]]::new()
    foreach ($number in $ClosedPullRequestNumber) {
        $null = $closedPullRequest.Add($number)
    }

    $parsed = foreach ($entry in $PackageVersion) {
        foreach ($property in @('PackageName', 'VersionId', 'Version')) {
            $value = $entry.PSObject.Properties[$property]
            if ($null -eq $value -or $null -eq $value.Value -or [string]::IsNullOrWhiteSpace([string] $value.Value)) {
                throw "Package version entry is missing '$property'."
            }
        }

        $ciVersion = ConvertFrom-ViuCiPackageVersion -Version ([string] $entry.Version)
        if ($null -eq $ciVersion) {
            continue
        }

        [pscustomobject]@{
            Entry       = $entry
            CiVersion   = $ciVersion
            BaseKey     = $ciVersion.BaseVersion.ToLowerInvariant()
            IsSdk       = ([string] $entry.PackageName) -match '^Assimalign\.Viu\.Sdk(\.|$)'
            PackageName = [string] $entry.PackageName
        }
    }
    $parsed = @($parsed)

    $selected = [System.Collections.Generic.List[object]]::new()

    $pullRequestCandidate = @(
        $parsed |
            Where-Object {
                $_.CiVersion.Channel -eq 'pr' -and
                $closedPullRequest.Contains([int] $_.CiVersion.PullRequestNumber)
            } |
            Sort-Object `
                @{ Expression = { $_.CiVersion.PullRequestNumber } },
                @{ Expression = { $_.CiVersion.BuildNumber } },
                @{ Expression = { -not $_.IsSdk } },
                @{ Expression = { $_.PackageName } }
    )
    foreach ($candidate in $pullRequestCandidate) {
        $selected.Add((New-ViuCiPackagePruneEntry `
                -Candidate $candidate `
                -Reason "pull request #$($candidate.CiVersion.PullRequestNumber) is closed"))
    }

    if ($PSBoundParameters.ContainsKey('MainBuildCount')) {
        $mainVersion = @($parsed | Where-Object { $_.CiVersion.Channel -eq 'ci' })
        foreach ($baseGroup in @($mainVersion | Group-Object -Property BaseKey | Sort-Object Name)) {
            $build = foreach ($buildGroup in @($baseGroup.Group | Group-Object -Property { $_.CiVersion.BuildNumber })) {
                $publishedAt = foreach ($candidate in $buildGroup.Group) {
                    ConvertTo-ViuPackageTimestamp -Value $candidate.Entry.CreatedAt -Version $candidate.Entry.Version
                }

                [pscustomobject]@{
                    BuildNumber = [int] $buildGroup.Group[0].CiVersion.BuildNumber
                    PublishedAt = ($publishedAt | Sort-Object -Descending | Select-Object -First 1)
                    Candidate   = $buildGroup.Group
                }
            }

            $newestFirst = @(
                $build |
                    Sort-Object `
                        @{ Expression = { $_.PublishedAt }; Descending = $true },
                        @{ Expression = { $_.BuildNumber }; Descending = $true }
            )
            if ($newestFirst.Count -le $MainBuildCount) {
                continue
            }

            $expired = @($newestFirst | Select-Object -Skip $MainBuildCount)
            [array]::Reverse($expired)
            foreach ($expiredBuild in $expired) {
                $orderedCandidate = @(
                    $expiredBuild.Candidate |
                        Sort-Object `
                            @{ Expression = { -not $_.IsSdk } },
                            @{ Expression = { $_.PackageName } }
                )
                foreach ($candidate in $orderedCandidate) {
                    $selected.Add((New-ViuCiPackagePruneEntry `
                            -Candidate $candidate `
                            -Reason "main build $($expiredBuild.BuildNumber) of $($candidate.CiVersion.BaseVersion) is older than the newest $MainBuildCount"))
                }
            }
        }
    }

    return , $selected.ToArray()
}

function New-ViuCiPackagePruneEntry {
    param(
        [Parameter(Mandatory)]
        [object] $Candidate,

        [Parameter(Mandatory)]
        [string] $Reason
    )

    return [pscustomobject]@{
        PackageName       = $Candidate.PackageName
        VersionId         = $Candidate.Entry.VersionId
        Version           = [string] $Candidate.Entry.Version
        CreatedAt         = $Candidate.Entry.CreatedAt
        Channel           = $Candidate.CiVersion.Channel
        BaseVersion       = $Candidate.CiVersion.BaseVersion
        PullRequestNumber = $Candidate.CiVersion.PullRequestNumber
        BuildNumber       = $Candidate.CiVersion.BuildNumber
        Reason            = $Reason
    }
}

function ConvertTo-ViuPackageTimestamp {
    param(
        [AllowNull()]
        [object] $Value,

        [Parameter(Mandatory)]
        [string] $Version
    )

    # ConvertFrom-Json turns the API's ISO 8601 strings into DateTime values on PowerShell 7, so
    # accept those as well as the raw text. Converting each to UTC keeps the order whatever their
    # Kind is.
    if ($Value -is [System.DateTimeOffset]) {
        return $Value.ToUniversalTime()
    }

    if ($Value -is [datetime]) {
        return ([System.DateTimeOffset] $Value.ToUniversalTime())
    }

    $parsed = [System.DateTimeOffset]::MinValue
    if ($Value -is [string] -and
        [System.DateTimeOffset]::TryParse(
            $Value,
            [System.Globalization.CultureInfo]::InvariantCulture,
            [System.Globalization.DateTimeStyles]::AssumeUniversal,
            [ref] $parsed)) {
        return $parsed.ToUniversalTime()
    }

    throw "Main build version '$Version' has no parseable CreatedAt, so it cannot be ranked for retention."
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
    Clear-ViuPackageCache,
    Get-ViuCiPackageVersion,
    ConvertFrom-ViuCiPackageVersion,
    Select-ViuCiPackageVersionToPrune
