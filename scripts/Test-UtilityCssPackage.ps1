<#
.SYNOPSIS
    Validates the standalone UtilityCss MSBuild package through external consumers.

.DESCRIPTION
    Packs the standalone engine and build-integration packages, stages Razor, plain-SDK, and
    package-only .viu consumers behind an isolated NuGet boundary, and verifies source discovery,
    single-file-component slicing, editor sidecars, Viu class-catalog completion over a real
    language-server process, both delivery paths, and byte-compare incremental behavior.
    Specified by [V01.01.12.30], issue #346.

.PARAMETER SkipPack
    Reuses current-version packages already present in PackageDirectory.

.PARAMETER PackageDirectory
    Local package feed. A non-default path requires SkipPack.

.PARAMETER ScratchRoot
    Parent for the isolated staged consumers. It must be below the repository _out directory.

.PARAMETER KeepScratch
    Retains the unique staged consumers for diagnosis.
#>
[CmdletBinding()]
param(
    [switch] $SkipPack,
    [string] $PackageDirectory =
        (Join-Path (Split-Path $PSScriptRoot -Parent) '_out/packages'),
    [string] $ScratchRoot =
        (Join-Path (Split-Path $PSScriptRoot -Parent) '_out/scratch/utility-css-package'),
    [switch] $KeepScratch
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$repositoryRootPath = [System.IO.Path]::GetFullPath(
    (Split-Path $PSScriptRoot -Parent))
$repositoryOutputPath = [System.IO.Path]::GetFullPath(
    (Join-Path $repositoryRootPath '_out'))
$pathComparison = if ([System.OperatingSystem]::IsWindows()) {
    [System.StringComparison]::OrdinalIgnoreCase
}
else {
    [System.StringComparison]::Ordinal
}
$repositoryOutputPrefix = $repositoryOutputPath.TrimEnd(
    [System.IO.Path]::DirectorySeparatorChar,
    [System.IO.Path]::AltDirectorySeparatorChar) +
    [System.IO.Path]::DirectorySeparatorChar

function Resolve-RepositoryOutputChild {
    param(
        [Parameter(Mandatory)]
        [string] $Path,

        [Parameter(Mandatory)]
        [string] $Description
    )

    $resolvedPath = [System.IO.Path]::GetFullPath($Path)
    if (-not $resolvedPath.StartsWith($repositoryOutputPrefix, $pathComparison)) {
        throw "$Description must be below the repository _out directory: $resolvedPath"
    }

    return $resolvedPath
}

function Assert-PathHasNoReparsePoint {
    param(
        [Parameter(Mandatory)]
        [string] $Path
    )

    $resolvedPath = [System.IO.Path]::GetFullPath($Path)
    $existingPath = $resolvedPath
    while (-not [System.IO.Directory]::Exists($existingPath) -and
        -not [System.IO.File]::Exists($existingPath)) {
        $parent = [System.IO.Directory]::GetParent($existingPath)
        if ($null -eq $parent) {
            throw "Could not resolve an existing ancestor for $resolvedPath."
        }

        $existingPath = $parent.FullName
    }

    while ($existingPath.StartsWith($repositoryOutputPath, $pathComparison)) {
        $item = Get-Item -LiteralPath $existingPath -Force
        if (($item.Attributes -band [System.IO.FileAttributes]::ReparsePoint) -ne 0) {
            throw "Refusing to operate through a reparse point: $($item.FullName)"
        }

        if ($existingPath.Equals($repositoryOutputPath, $pathComparison)) {
            break
        }

        $parent = [System.IO.Directory]::GetParent($existingPath)
        if ($null -eq $parent) {
            break
        }

        $existingPath = $parent.FullName
    }
}

function Assert-TreeHasNoReparsePoint {
    param(
        [Parameter(Mandatory)]
        [string] $Path
    )

    $root = Get-Item -LiteralPath $Path -Force
    if (($root.Attributes -band [System.IO.FileAttributes]::ReparsePoint) -ne 0) {
        throw "Refusing to operate on a reparse point: $($root.FullName)"
    }

    $reparsePoints = @(
        Get-ChildItem -LiteralPath $root.FullName -Force -Recurse |
            Where-Object {
                ($_.Attributes -band [System.IO.FileAttributes]::ReparsePoint) -ne 0
            })
    if ($reparsePoints.Count -ne 0) {
        throw "Refusing to operate below a directory containing reparse points: $($root.FullName)"
    }
}

function Invoke-DotNet {
    param(
        [Parameter(Mandatory)]
        [string] $Description,

        [Parameter(Mandatory)]
        [string[]] $Arguments
    )

    Write-Host $Description -ForegroundColor Green
    & dotnet @Arguments
    if ($LASTEXITCODE -ne 0) {
        throw "$Description failed with exit code $LASTEXITCODE."
    }
}

function Invoke-DotNetForJson {
    param(
        [Parameter(Mandatory)]
        [string] $Description,

        [Parameter(Mandatory)]
        [string[]] $Arguments
    )

    Write-Host $Description -ForegroundColor Green
    $output = @(& dotnet @Arguments 2>&1 | ForEach-Object { $_.ToString() })
    if ($LASTEXITCODE -ne 0) {
        throw "$Description failed with exit code $LASTEXITCODE.`n$($output -join [System.Environment]::NewLine)"
    }

    $text = $output -join [System.Environment]::NewLine
    try {
        return $text | ConvertFrom-Json
    }
    catch {
        throw "$Description did not return valid JSON.`n$text"
    }
}

function Get-JsonPropertyValue {
    param(
        [Parameter(Mandatory)]
        [object] $InputObject,

        [Parameter(Mandatory)]
        [string] $Name
    )

    $property = $InputObject.PSObject.Properties[$Name]
    if ($null -eq $property -or $null -eq $property.Value) {
        return ''
    }

    return $property.Value.ToString()
}

function Get-RequiredJsonProperty {
    param(
        [Parameter(Mandatory)]
        [object] $InputObject,

        [Parameter(Mandatory)]
        [string] $Name,

        [Parameter(Mandatory)]
        [string] $Description,

        [switch] $AllowNull
    )

    $property = $InputObject.PSObject.Properties[$Name]
    if ($null -eq $property) {
        throw "$Description is missing the '$Name' property."
    }
    if ($null -eq $property.Value -and -not $AllowNull) {
        throw "$Description has a null '$Name' property."
    }

    return $property.Value
}

function Resolve-ProjectPath {
    param(
        [Parameter(Mandatory)]
        [string] $ProjectDirectory,

        [Parameter(Mandatory)]
        [string] $Path
    )

    if ([System.IO.Path]::IsPathRooted($Path)) {
        return [System.IO.Path]::GetFullPath($Path)
    }

    return [System.IO.Path]::GetFullPath(
        (Join-Path $ProjectDirectory $Path))
}

function Get-UtilityCssProjectProperties {
    param(
        [Parameter(Mandatory)]
        [string] $ProjectPath,

        [Parameter(Mandatory)]
        [string[]] $CommonProperties
    )

    $result = Invoke-DotNetForJson `
        -Description "Reading UtilityCss properties from $([System.IO.Path]::GetFileName($ProjectPath))" `
        -Arguments (@(
            'msbuild',
            $ProjectPath,
            '-nologo',
            '-verbosity:quiet',
            '-getProperty:IntermediateOutputPath;BaseIntermediateOutputPath;ProjectAssetsFile;TargetDir;AssemblyName;ViuUtilityCssBundleName;UsingMicrosoftNETSdkStaticWebAssets',
            '-property:Configuration=Release') + $CommonProperties)
    if ($null -eq $result.Properties) {
        throw "MSBuild returned no property set for $ProjectPath."
    }

    return $result.Properties
}

function Assert-OnlyBuildIntegrationPackageWasResolved {
    param(
        [Parameter(Mandatory)]
        [string] $AssetsPath,

        [Parameter(Mandatory)]
        [string] $Version,

        [Parameter(Mandatory)]
        [string] $FixtureName
    )

    if (-not [System.IO.File]::Exists($AssetsPath)) {
        throw "$FixtureName did not produce project.assets.json: $AssetsPath"
    }

    $assets = Get-Content -Raw -LiteralPath $AssetsPath | ConvertFrom-Json
    $viuLibraries = @(
        $assets.libraries.PSObject.Properties.Name |
            Where-Object {
                $_.StartsWith(
                    'Assimalign.Viu.',
                    [System.StringComparison]::OrdinalIgnoreCase)
            })
    $expectedLibrary = "Assimalign.Viu.UtilityCss.Build/$Version"
    if ($viuLibraries.Count -ne 1 -or
        -not $viuLibraries[0].Equals(
            $expectedLibrary,
            [System.StringComparison]::OrdinalIgnoreCase)) {
        throw "$FixtureName must resolve exactly $expectedLibrary; resolved Viu libraries: $($viuLibraries -join ', ')"
    }
}

function Assert-UtilityCssBuildPackageLayout {
    param(
        [Parameter(Mandatory)]
        [string] $PackagePath
    )

    Add-Type -AssemblyName System.IO.Compression.FileSystem
    $archive = [System.IO.Compression.ZipFile]::OpenRead($PackagePath)
    try {
        $entryNames = @(
            $archive.Entries |
                Where-Object { -not [string]::IsNullOrEmpty($_.Name) } |
                ForEach-Object FullName)
        $libraryEntries = @(
            $entryNames |
                Where-Object {
                    $_.StartsWith(
                        'lib/',
                        [System.StringComparison]::OrdinalIgnoreCase)
                })
        if ($libraryEntries.Count -ne 0) {
            throw "The build-integration package must not expose lib/ assets: $($libraryEntries -join ', ')"
        }

        $expectedBuildEntries = @(
            'build/Assimalign.Viu.UtilityCss.Build.props',
            'build/Assimalign.Viu.UtilityCss.Build.targets') | Sort-Object
        $actualBuildEntries = @(
            $entryNames |
                Where-Object {
                    $_.StartsWith(
                        'build/',
                        [System.StringComparison]::OrdinalIgnoreCase)
                } |
                Sort-Object)
        $buildDifference = @(
            Compare-Object $expectedBuildEntries $actualBuildEntries)
        if ($buildDifference.Count -ne 0) {
            throw "The build-integration package build/ payload differs from the contract: $($buildDifference | Out-String)"
        }

        $expectedTaskEntries = @(
            'tasks/netstandard2.0/Assimalign.Viu.Syntax.dll',
            'tasks/netstandard2.0/Assimalign.Viu.Syntax.SingleFileComponent.dll',
            'tasks/netstandard2.0/Assimalign.Viu.UtilityCss.Build.dll',
            'tasks/netstandard2.0/Assimalign.Viu.UtilityCss.dll',
            'tasks/netstandard2.0/Assimalign.Viu.UtilityCss.THIRD-PARTY-NOTICES.md') |
            Sort-Object
        $actualTaskEntries = @(
            $entryNames |
                Where-Object {
                    $_.StartsWith(
                        'tasks/',
                        [System.StringComparison]::OrdinalIgnoreCase)
                } |
                Sort-Object)
        $taskDifference = @(
            Compare-Object $expectedTaskEntries $actualTaskEntries)
        if ($taskDifference.Count -ne 0) {
            throw "The build-integration package tasks/ payload differs from the four-DLL-plus-notice contract: $($taskDifference | Out-String)"
        }

        foreach ($requiredEntry in @(
                'README.md',
                'Assimalign.Viu.UtilityCss.Build.nuspec')) {
            if ($entryNames -notcontains $requiredEntry) {
                throw "The build-integration package is missing $requiredEntry."
            }
        }
    }
    finally {
        $archive.Dispose()
    }

    Write-Host `
        'Build package layout passed: no lib/, two auto-imports, and exactly four task DLLs plus the notice.' `
        -ForegroundColor Green
}

function Assert-UtilityCssEditorSidecar {
    param(
        [Parameter(Mandatory)]
        [string] $ProjectPath,

        [Parameter(Mandatory)]
        [string] $ProjectDirectory,

        [Parameter(Mandatory)]
        [string] $BundlePath,

        [Parameter(Mandatory)]
        [string] $BundleName,

        [Parameter(Mandatory)]
        [string[]] $ExpectedSourceRelativePaths,

        [Parameter(Mandatory)]
        [string[]] $ExpectedCatalogClasses,

        [Parameter(Mandatory)]
        [AllowEmptyCollection()]
        [string[]] $ForbiddenCatalogClasses,

        [Parameter(Mandatory)]
        [string[]] $CommonProperties
    )

    $sidecarDirectory = [System.IO.Path]::GetDirectoryName($BundlePath)
    $manifestPath = Join-Path $sidecarDirectory 'utilitycss.manifest.v1.json'
    $catalogPath = Join-Path $sidecarDirectory 'utilitycss.classcatalog.v1.json'
    foreach ($sidecarPath in @($manifestPath, $catalogPath)) {
        if (-not [System.IO.File]::Exists($sidecarPath)) {
            throw "The UtilityCss build did not emit the editor sidecar: $sidecarPath"
        }
    }

    try {
        $manifest = [System.IO.File]::ReadAllText($manifestPath) |
            ConvertFrom-Json
    }
    catch {
        throw "The UtilityCss editor manifest is not valid JSON: $manifestPath`n$($_.Exception.Message)"
    }

    $manifestDescription = 'The UtilityCss editor manifest'
    $manifestSchemaVersion = Get-RequiredJsonProperty `
        -InputObject $manifest `
        -Name 'schemaVersion' `
        -Description $manifestDescription
    if ($manifestSchemaVersion -isnot [long] -and
        $manifestSchemaVersion -isnot [int]) {
        throw "$manifestDescription schemaVersion must be an integer."
    }
    if ([long]$manifestSchemaVersion -ne 1) {
        throw "$manifestDescription schemaVersion was '$manifestSchemaVersion'; expected 1."
    }

    $engineVersion = Get-RequiredJsonProperty `
        -InputObject $manifest `
        -Name 'engineVersion' `
        -Description $manifestDescription
    if ([string]::IsNullOrWhiteSpace($engineVersion.ToString())) {
        throw "$manifestDescription engineVersion must not be empty."
    }

    $entryStylesheetPath = Get-RequiredJsonProperty `
        -InputObject $manifest `
        -Name 'entryStylesheetPath' `
        -Description $manifestDescription `
        -AllowNull
    if ($null -ne $entryStylesheetPath) {
        $resolvedEntryStylesheetPath = $entryStylesheetPath.ToString()
        if (-not [System.IO.Path]::IsPathRooted($resolvedEntryStylesheetPath) -or
            -not [System.IO.File]::Exists($resolvedEntryStylesheetPath)) {
            throw "$manifestDescription entryStylesheetPath is not an existing absolute file: $resolvedEntryStylesheetPath"
        }
    }

    $sourceFiles = @(
        Get-RequiredJsonProperty `
            -InputObject $manifest `
            -Name 'sourceFiles' `
            -Description $manifestDescription |
            ForEach-Object { $_.ToString() })
    foreach ($sourceFile in $sourceFiles) {
        if (-not [System.IO.Path]::IsPathRooted($sourceFile) -or
            -not [System.IO.File]::Exists($sourceFile)) {
            throw "$manifestDescription contains a source file that is not an existing absolute path: $sourceFile"
        }
    }
    foreach ($expectedSourceRelativePath in $ExpectedSourceRelativePaths) {
        $expectedSourcePath = [System.IO.Path]::GetFullPath(
            (Join-Path $ProjectDirectory $expectedSourceRelativePath))
        $matchingSourceFiles = @(
            $sourceFiles |
                Where-Object { $_.Equals($expectedSourcePath, $pathComparison) })
        if ($matchingSourceFiles.Count -ne 1) {
            throw "$manifestDescription does not contain the resolved fixture source $expectedSourcePath."
        }
    }

    $themeContentHash = Get-RequiredJsonProperty `
        -InputObject $manifest `
        -Name 'themeContentHash' `
        -Description $manifestDescription
    if (-not $themeContentHash.ToString().Equals(
            $themeContentHash.ToString().ToLowerInvariant(),
            [System.StringComparison]::Ordinal) -or
        $themeContentHash.ToString() -notmatch '^[0-9a-f]{64}$') {
        throw "$manifestDescription themeContentHash is not a lowercase SHA-256 value."
    }

    $manifestBundle = Get-RequiredJsonProperty `
        -InputObject $manifest `
        -Name 'bundle' `
        -Description $manifestDescription
    $manifestBundlePath = Get-RequiredJsonProperty `
        -InputObject $manifestBundle `
        -Name 'path' `
        -Description "$manifestDescription bundle"
    $resolvedBundlePath = [System.IO.Path]::GetFullPath($BundlePath)
    if (-not [System.IO.Path]::IsPathRooted($manifestBundlePath.ToString()) -or
        -not $manifestBundlePath.ToString().Equals(
            $resolvedBundlePath,
            $pathComparison) -or
        -not [System.IO.File]::Exists($manifestBundlePath.ToString())) {
        throw "$manifestDescription does not reference the generated bundle at $resolvedBundlePath."
    }
    $manifestBundleName = Get-RequiredJsonProperty `
        -InputObject $manifestBundle `
        -Name 'name' `
        -Description "$manifestDescription bundle"
    if (-not $manifestBundleName.ToString().Equals(
            $BundleName,
            [System.StringComparison]::Ordinal)) {
        throw "$manifestDescription bundle name was '$manifestBundleName'; expected '$BundleName'."
    }

    try {
        $catalog = [System.IO.File]::ReadAllText($catalogPath) |
            ConvertFrom-Json
    }
    catch {
        throw "The UtilityCss class catalog is not valid JSON: $catalogPath`n$($_.Exception.Message)"
    }

    $catalogDescription = 'The UtilityCss class catalog'
    $catalogVersion = Get-RequiredJsonProperty `
        -InputObject $catalog `
        -Name 'version' `
        -Description $catalogDescription
    if ($catalogVersion -isnot [long] -and
        $catalogVersion -isnot [int]) {
        throw "$catalogDescription version must be an integer."
    }
    if ([long]$catalogVersion -ne 1) {
        throw "$catalogDescription version was '$catalogVersion'; expected 1."
    }

    $catalogTruncated = Get-RequiredJsonProperty `
        -InputObject $catalog `
        -Name 'truncated' `
        -Description $catalogDescription
    if ($catalogTruncated -isnot [bool]) {
        throw "$catalogDescription truncated signal must be Boolean."
    }
    if (-not $catalogTruncated) {
        throw "$catalogDescription should be truncated at the default 500-item editor budget."
    }

    $catalogEntries = @(
        Get-RequiredJsonProperty `
            -InputObject $catalog `
            -Name 'entries' `
            -Description $catalogDescription)
    if ($catalogEntries.Count -eq 0) {
        throw "$catalogDescription contains no entries."
    }
    if ($catalogEntries.Count -gt 500) {
        throw "$catalogDescription contains $($catalogEntries.Count) entries; expected at most 500."
    }

    foreach ($expectedCatalogClass in $ExpectedCatalogClasses) {
        $matchingCatalogEntries = @(
            $catalogEntries |
                Where-Object {
                    $classProperty = $_.PSObject.Properties['class']
                    $null -ne $classProperty -and
                        $null -ne $classProperty.Value -and
                        $classProperty.Value.ToString().Equals(
                            $expectedCatalogClass,
                            [System.StringComparison]::Ordinal)
                })
        if ($matchingCatalogEntries.Count -ne 1) {
            throw "$catalogDescription does not contain exactly one '$expectedCatalogClass' entry."
        }
        $matchingCatalogCss = Get-RequiredJsonProperty `
            -InputObject $matchingCatalogEntries[0] `
            -Name 'css' `
            -Description "$catalogDescription '$expectedCatalogClass' entry"
        if ([string]::IsNullOrWhiteSpace($matchingCatalogCss.ToString())) {
            throw "$catalogDescription '$expectedCatalogClass' entry has empty CSS."
        }
    }

    foreach ($forbiddenCatalogClass in $ForbiddenCatalogClasses) {
        $forbiddenCatalogEntries = @(
            $catalogEntries |
                Where-Object {
                    $classProperty = $_.PSObject.Properties['class']
                    $null -ne $classProperty -and
                        $null -ne $classProperty.Value -and
                        $classProperty.Value.ToString().Equals(
                            $forbiddenCatalogClass,
                            [System.StringComparison]::Ordinal)
                })
        if ($forbiddenCatalogEntries.Count -ne 0) {
            throw "$catalogDescription unexpectedly contains script-only class '$forbiddenCatalogClass'."
        }
    }

    $catalogItemsResult = Invoke-DotNetForJson `
        -Description 'Resolving the packaged ViuClassCatalog editor-discovery item' `
        -Arguments (@(
            'msbuild',
            $ProjectPath,
            '-nologo',
            '-verbosity:quiet',
            '-getItem:ViuClassCatalog',
            '-property:Configuration=Release') + $CommonProperties)
    $resultItemsProperty = $catalogItemsResult.PSObject.Properties['Items']
    $catalogItemsProperty = if ($null -eq $resultItemsProperty -or
        $null -eq $resultItemsProperty.Value) {
        $null
    }
    else {
        $resultItemsProperty.Value.PSObject.Properties['ViuClassCatalog']
    }
    $catalogItems = if ($null -eq $catalogItemsProperty) {
        @()
    }
    else {
        @($catalogItemsProperty.Value)
    }
    $matchingCatalogItems = @(
        $catalogItems |
            Where-Object {
                $catalogItemPath = Get-JsonPropertyValue `
                    -InputObject $_ `
                    -Name 'FullPath'
                if ([string]::IsNullOrWhiteSpace($catalogItemPath)) {
                    $catalogItemPath = Get-JsonPropertyValue `
                        -InputObject $_ `
                        -Name 'Identity'
                }

                -not [string]::IsNullOrWhiteSpace($catalogItemPath) -and
                    (Resolve-ProjectPath `
                        -ProjectDirectory $ProjectDirectory `
                        -Path $catalogItemPath).Equals(
                            [System.IO.Path]::GetFullPath($catalogPath),
                            $pathComparison)
            })
    if ($matchingCatalogItems.Count -ne 1) {
        throw "The packaged targets did not expose exactly one ViuClassCatalog item for $catalogPath."
    }

    Write-Host `
        "Editor manifest and bounded class catalog passed for $([System.IO.Path]::GetFileName($ProjectPath))." `
        -ForegroundColor Green
}

function Assert-UtilityCssStaticWebAssetPair {
    param(
        [Parameter(Mandatory)]
        [string] $ProjectPath,

        [Parameter(Mandatory)]
        [string] $BundlePath,

        [Parameter(Mandatory)]
        [string] $ExpectedSourceIdentifier,

        [Parameter(Mandatory)]
        [string[]] $CommonProperties
    )

    $result = Invoke-DotNetForJson `
        -Description 'Resolving the Razor StaticWebAsset and StaticWebAssetEndpoint pair' `
        -Arguments (@(
            'msbuild',
            $ProjectPath,
            '-nologo',
            '-verbosity:quiet',
            '-target:ResolveStaticWebAssetsInputs',
            '-getItem:StaticWebAsset;StaticWebAssetEndpoint',
            '-property:Configuration=Release') + $CommonProperties)
    if ($null -eq $result.Items) {
        throw 'MSBuild returned no item set after ResolveStaticWebAssetsInputs.'
    }

    $assetProperty = $result.Items.PSObject.Properties['StaticWebAsset']
    $endpointProperty = $result.Items.PSObject.Properties['StaticWebAssetEndpoint']
    $assets = if ($null -eq $assetProperty) { @() } else { @($assetProperty.Value) }
    $endpoints = if ($null -eq $endpointProperty) { @() } else { @($endpointProperty.Value) }
    $projectDirectory = [System.IO.Path]::GetDirectoryName($ProjectPath)
    $resolvedBundlePath = [System.IO.Path]::GetFullPath($BundlePath)

    $matchingAssets = @(
        $assets |
            Where-Object {
                $fullPath = Get-JsonPropertyValue -InputObject $_ -Name 'FullPath'
                if ([string]::IsNullOrWhiteSpace($fullPath)) {
                    $fullPath = Get-JsonPropertyValue -InputObject $_ -Name 'Identity'
                }

                -not [string]::IsNullOrWhiteSpace($fullPath) -and
                    (Resolve-ProjectPath `
                        -ProjectDirectory $projectDirectory `
                        -Path $fullPath).Equals(
                            $resolvedBundlePath,
                            $pathComparison)
            })
    if ($matchingAssets.Count -ne 1) {
        throw "Expected one generated StaticWebAsset for $resolvedBundlePath, found $($matchingAssets.Count)."
    }

    $sourceIdentifier = Get-JsonPropertyValue `
        -InputObject $matchingAssets[0] `
        -Name 'SourceId'
    if (-not $sourceIdentifier.Equals(
            $ExpectedSourceIdentifier,
            [System.StringComparison]::Ordinal)) {
        throw "Generated StaticWebAsset SourceId was '$sourceIdentifier'; expected '$ExpectedSourceIdentifier'."
    }

    $matchingEndpoints = @(
        $endpoints |
            Where-Object {
                $assetFile = Get-JsonPropertyValue -InputObject $_ -Name 'AssetFile'
                -not [string]::IsNullOrWhiteSpace($assetFile) -and
                    (Resolve-ProjectPath `
                        -ProjectDirectory $projectDirectory `
                        -Path $assetFile).Equals(
                            $resolvedBundlePath,
                            $pathComparison)
            })
    if ($matchingEndpoints.Count -eq 0) {
        throw "The generated StaticWebAsset has no StaticWebAssetEndpoint whose AssetFile is $resolvedBundlePath."
    }

    Write-Host `
        "Static Web Assets registered one asset with $($matchingEndpoints.Count) endpoint(s)." `
        -ForegroundColor Green
}

function Assert-UtilityCssStaticWebAssetRemoval {
    param(
        [Parameter(Mandatory)]
        [string] $ProjectPath,

        [Parameter(Mandatory)]
        [string] $BundleName,

        [Parameter(Mandatory)]
        [string] $IntermediateOutputPath,

        [Parameter(Mandatory)]
        [string] $TargetDirectory,

        [Parameter(Mandatory)]
        [string] $TargetName,

        [Parameter(Mandatory)]
        [string] $RetainedAssetName,

        [Parameter(Mandatory)]
        [string[]] $CommonProperties
    )

    $removalProperties = @($CommonProperties) +
        '-property:ViuUtilityCssAutomaticSourceDiscovery=false'
    Invoke-DotNet `
        -Description 'Rebuilding the Razor consumer after removing every utility source' `
        -Arguments (@(
            'build',
            $ProjectPath,
            '--configuration',
            'Release',
            '--no-restore',
            '-warnaserror') + $removalProperties)

    $result = Invoke-DotNetForJson `
        -Description 'Resolving Static Web Assets after utility removal' `
        -Arguments (@(
            'msbuild',
            $ProjectPath,
            '-nologo',
            '-verbosity:quiet',
            '-target:ResolveStaticWebAssetsInputs',
            '-getItem:StaticWebAsset;StaticWebAssetEndpoint',
            '-property:Configuration=Release') + $removalProperties)
    $resolvedItems = $result.Items | ConvertTo-Json -Depth 20 -Compress
    if ($resolvedItems.Contains($BundleName, [System.StringComparison]::Ordinal)) {
        throw "The removed utility bundle remains in the Static Web Asset item graph."
    }

    $developmentManifestPath = Join-Path `
        $IntermediateOutputPath `
        'staticwebassets.development.json'
    $runtimeManifestPath = Join-Path `
        $TargetDirectory `
        "$TargetName.staticwebassets.runtime.json"
    foreach ($obsoleteManifestPath in @(
            $developmentManifestPath,
            $runtimeManifestPath)) {
        if (-not [System.IO.File]::Exists($obsoleteManifestPath)) {
            throw "Utility removal also removed the manifest for the unrelated Razor asset: $obsoleteManifestPath"
        }

        $manifest = [System.IO.File]::ReadAllText($obsoleteManifestPath)
        if ($manifest.Contains($BundleName, [System.StringComparison]::Ordinal)) {
            throw "The removed utility bundle remains in $obsoleteManifestPath."
        }
        if (-not $manifest.Contains(
                $RetainedAssetName,
                [System.StringComparison]::Ordinal)) {
            throw "Utility removal failed to preserve '$RetainedAssetName' in $obsoleteManifestPath."
        }
    }

    Write-Host `
        'Static Web Assets retired the removed bundle while preserving unrelated manifest entries.' `
        -ForegroundColor Green
}

function Assert-UtilityCssPlainOutputRemoval {
    param(
        [Parameter(Mandatory)]
        [string] $ProjectPath,

        [Parameter(Mandatory)]
        [string] $IntermediateBundlePath,

        [Parameter(Mandatory)]
        [string] $DeliveredBundlePath,

        [Parameter(Mandatory)]
        [string[]] $CommonProperties
    )

    Invoke-DotNet `
        -Description 'Rebuilding the plain consumer after removing every utility source' `
        -Arguments (@(
            'build',
            $ProjectPath,
            '--configuration',
            'Release',
            '--no-restore',
            '-warnaserror',
            '-property:ViuUtilityCssAutomaticSourceDiscovery=false') + $CommonProperties)
    foreach ($obsoleteBundlePath in @(
            $IntermediateBundlePath,
            $DeliveredBundlePath)) {
        if ([System.IO.File]::Exists($obsoleteBundlePath)) {
            throw "The plain-host removal build retained $obsoleteBundlePath."
        }
    }

    Write-Host `
        'The plain content pipeline retired both generated and delivered bundle copies.' `
        -ForegroundColor Green
}

function Assert-ViuClassCatalogCompletion {
    param(
        [Parameter(Mandatory)]
        [string] $ProjectDirectory,

        [Parameter(Mandatory)]
        [string] $CatalogPath,

        [Parameter(Mandatory)]
        [string] $DocumentPath,

        [Parameter(Mandatory)]
        [string] $ClassName,

        [Parameter(Mandatory)]
        [string] $CompletionPrefix,

        [Parameter(Mandatory)]
        [string] $LanguageServerExecutable,

        [Parameter(Mandatory)]
        [string] $LanguageServerTestProjectPath
    )

    try {
        $catalog = [System.IO.File]::ReadAllText($CatalogPath) |
            ConvertFrom-Json
    }
    catch {
        throw "The class catalog is not valid JSON: $CatalogPath`n$($_.Exception.Message)"
    }

    $matchingEntries = @(
        $catalog.entries |
            Where-Object {
                $classProperty = $_.PSObject.Properties['class']
                $null -ne $classProperty -and
                    $null -ne $classProperty.Value -and
                    $classProperty.Value.ToString().Equals(
                        $ClassName,
                        [System.StringComparison]::Ordinal)
            })
    if ($matchingEntries.Count -ne 1) {
        throw "The class-catalog language-server probe expected one '$ClassName' entry."
    }

    $colorValue = Get-RequiredJsonProperty `
        -InputObject $matchingEntries[0] `
        -Name 'colorValue' `
        -Description "The '$ClassName' class-catalog entry"
    if ([string]::IsNullOrWhiteSpace($colorValue.ToString())) {
        throw "The '$ClassName' class-catalog entry has no colorValue."
    }

    $fixturePath = Join-Path `
        (Join-Path $ProjectDirectory 'obj') `
        'viu-class-catalog-process-fixture.json'
    @{
        serverExecutable = $LanguageServerExecutable
        documentPath = $DocumentPath
        className = $ClassName
        completionPrefix = $CompletionPrefix
        colorValue = $colorValue.ToString()
    } | ConvertTo-Json | Set-Content -LiteralPath $fixturePath -Encoding utf8

    $previousFixture = [System.Environment]::GetEnvironmentVariable(
        'VIU_CLASS_CATALOG_FIXTURE',
        [System.EnvironmentVariableTarget]::Process)
    [System.Environment]::SetEnvironmentVariable(
        'VIU_CLASS_CATALOG_FIXTURE',
        $fixturePath,
        [System.EnvironmentVariableTarget]::Process)
    try {
        Invoke-DotNet `
            -Description 'Running the build-catalog-to-Viu-completion process proof' `
            -Arguments @(
                'test',
                $LanguageServerTestProjectPath,
                '--configuration',
                'Release',
                '--no-build',
                '--no-restore',
                '--filter',
                'FullyQualifiedName~LanguageServerClassCatalogProcessTests',
                '--blame-hang-timeout',
                '2m',
                '--logger',
                'console;verbosity=detailed')
    }
    finally {
        [System.Environment]::SetEnvironmentVariable(
            'VIU_CLASS_CATALOG_FIXTURE',
            $previousFixture,
            [System.EnvironmentVariableTarget]::Process)
    }

    Write-Host `
        "Viu completion received '$ClassName' with colorValue '$colorValue' from the build catalog." `
        -ForegroundColor Green
}

function Test-UtilityCssFixture {
    param(
        [Parameter(Mandatory)]
        [hashtable] $Fixture,

        [Parameter(Mandatory)]
        [string] $StagingRoot,

        [Parameter(Mandatory)]
        [string] $NuGetConfigurationPath,

        [Parameter(Mandatory)]
        [string] $Version,

        [Parameter(Mandatory)]
        [string] $LanguageServerExecutable,

        [Parameter(Mandatory)]
        [string] $LanguageServerTestProjectPath
    )

    $fixtureName = $Fixture.Name
    $projectDirectory = Join-Path $StagingRoot $fixtureName
    $projectPath = Join-Path $projectDirectory "$fixtureName.csproj"
    $commonProperties = @(
        "-property:ViuConsumerVersion=$Version",
        '-property:NuGetAudit=false',
        '-property:AllowMissingPrunePackageData=true')

    Invoke-DotNet `
        -Description "Restoring $fixtureName from the local UtilityCss package feed" `
        -Arguments (@(
            'restore',
            $projectPath,
            '--configfile',
            $NuGetConfigurationPath,
            '--ignore-failed-sources') + $commonProperties)

    $properties = Get-UtilityCssProjectProperties `
        -ProjectPath $projectPath `
        -CommonProperties $commonProperties
    $assemblyName = Get-JsonPropertyValue `
        -InputObject $properties `
        -Name 'AssemblyName'
    if (-not $assemblyName.Equals($fixtureName, [System.StringComparison]::Ordinal)) {
        throw "$fixtureName evaluated AssemblyName '$assemblyName'."
    }

    $bundleName = Get-JsonPropertyValue `
        -InputObject $properties `
        -Name 'ViuUtilityCssBundleName'
    $expectedBundleName = "$fixtureName.utilities.css"
    if (-not $bundleName.Equals($expectedBundleName, [System.StringComparison]::Ordinal)) {
        throw "$fixtureName evaluated bundle name '$bundleName'; expected '$expectedBundleName'."
    }

    $usingStaticWebAssets = Get-JsonPropertyValue `
        -InputObject $properties `
        -Name 'UsingMicrosoftNETSdkStaticWebAssets'
    $expectedStaticWebAssets = [bool]$Fixture.StaticWebAssets
    if ($expectedStaticWebAssets -ne $usingStaticWebAssets.Equals(
            'true',
            [System.StringComparison]::OrdinalIgnoreCase)) {
        throw "$fixtureName evaluated UsingMicrosoftNETSdkStaticWebAssets='$usingStaticWebAssets'; expected $expectedStaticWebAssets."
    }

    $intermediateOutputPath = Resolve-ProjectPath `
        -ProjectDirectory $projectDirectory `
        -Path (Get-JsonPropertyValue `
            -InputObject $properties `
            -Name 'IntermediateOutputPath')
    $bundlePath = Join-Path `
        (Join-Path $intermediateOutputPath 'utilitycss') `
        $bundleName
    $projectAssetsPath = Get-JsonPropertyValue `
        -InputObject $properties `
        -Name 'ProjectAssetsFile'
    if ([string]::IsNullOrWhiteSpace($projectAssetsPath)) {
        $baseIntermediateOutputPath = Resolve-ProjectPath `
            -ProjectDirectory $projectDirectory `
            -Path (Get-JsonPropertyValue `
                -InputObject $properties `
                -Name 'BaseIntermediateOutputPath')
        $projectAssetsPath = Join-Path $baseIntermediateOutputPath 'project.assets.json'
    }
    else {
        $projectAssetsPath = Resolve-ProjectPath `
            -ProjectDirectory $projectDirectory `
            -Path $projectAssetsPath
    }

    Assert-OnlyBuildIntegrationPackageWasResolved `
        -AssetsPath $projectAssetsPath `
        -Version $Version `
        -FixtureName $fixtureName

    Invoke-DotNet `
        -Description "Building $fixtureName" `
        -Arguments (@(
            'build',
            $projectPath,
            '--configuration',
            'Release',
            '--no-restore',
            '-warnaserror') + $commonProperties)
    if (-not [System.IO.File]::Exists($bundlePath)) {
        throw "$fixtureName did not generate its intermediate bundle: $bundlePath"
    }

    $bundleText = [System.IO.File]::ReadAllText($bundlePath)
    foreach ($requiredText in $Fixture.RequiredText) {
        if (-not $bundleText.Contains(
                $requiredText,
                [System.StringComparison]::Ordinal)) {
            throw "$fixtureName bundle is missing '$requiredText'."
        }
    }
    foreach ($forbiddenText in $Fixture.ForbiddenText) {
        if ($bundleText.Contains(
                $forbiddenText,
                [System.StringComparison]::Ordinal)) {
            throw "$fixtureName bundle unexpectedly contains '$forbiddenText'."
        }
    }

    Assert-UtilityCssEditorSidecar `
        -ProjectPath $projectPath `
        -ProjectDirectory $projectDirectory `
        -BundlePath $bundlePath `
        -BundleName $bundleName `
        -ExpectedSourceRelativePaths $Fixture.ExpectedSourceRelativePaths `
        -ExpectedCatalogClasses $Fixture.ExpectedCatalogClasses `
        -ForbiddenCatalogClasses $Fixture.ForbiddenCatalogClasses `
        -CommonProperties $commonProperties

    if ($Fixture.ContainsKey('ClassCatalogCompletionClass')) {
        Assert-ViuClassCatalogCompletion `
            -ProjectDirectory $projectDirectory `
            -CatalogPath (Join-Path `
                ([System.IO.Path]::GetDirectoryName($bundlePath)) `
                'utilitycss.classcatalog.v1.json') `
            -DocumentPath (Join-Path $projectDirectory 'SlicingProbe.viu') `
            -ClassName $Fixture.ClassCatalogCompletionClass `
            -CompletionPrefix $Fixture.ClassCatalogCompletionPrefix `
            -LanguageServerExecutable $LanguageServerExecutable `
            -LanguageServerTestProjectPath $LanguageServerTestProjectPath
    }

    $bundleItem = Get-Item -LiteralPath $bundlePath
    $firstBundleTimestamp = $bundleItem.LastWriteTimeUtc.Ticks
    $firstBundleHash = (Get-FileHash -LiteralPath $bundlePath -Algorithm SHA256).Hash
    $deliveredBundlePath = $null
    $firstDeliveredTimestamp = $null
    if (-not $expectedStaticWebAssets) {
        $targetDirectory = Resolve-ProjectPath `
            -ProjectDirectory $projectDirectory `
            -Path (Get-JsonPropertyValue `
                -InputObject $properties `
                -Name 'TargetDir')
        $deliveredBundlePath = Join-Path $targetDirectory $bundleName
        if (-not [System.IO.File]::Exists($deliveredBundlePath)) {
            throw "$fixtureName did not copy its bundle into the plain-host output: $deliveredBundlePath"
        }

        $deliveredHash = (Get-FileHash `
            -LiteralPath $deliveredBundlePath `
            -Algorithm SHA256).Hash
        if (-not $deliveredHash.Equals(
                $firstBundleHash,
                [System.StringComparison]::Ordinal)) {
            throw "$fixtureName output copy does not match the generated intermediate bundle."
        }

        $firstDeliveredTimestamp =
            (Get-Item -LiteralPath $deliveredBundlePath).LastWriteTimeUtc.Ticks
    }

    if ($expectedStaticWebAssets) {
        Assert-UtilityCssStaticWebAssetPair `
            -ProjectPath $projectPath `
            -BundlePath $bundlePath `
            -ExpectedSourceIdentifier $fixtureName `
            -CommonProperties $commonProperties
        $timestampAfterStaticWebAssetResolution =
            (Get-Item -LiteralPath $bundlePath).LastWriteTimeUtc.Ticks
        if ($timestampAfterStaticWebAssetResolution -ne $firstBundleTimestamp) {
            throw "$fixtureName rewrote its bundle while resolving Static Web Assets."
        }
    }

    Start-Sleep -Milliseconds 1200
    Invoke-DotNet `
        -Description "Rebuilding $fixtureName without changes" `
        -Arguments (@(
            'build',
            $projectPath,
            '--configuration',
            'Release',
            '--no-restore',
            '-warnaserror') + $commonProperties)

    $secondBundleTimestamp =
        (Get-Item -LiteralPath $bundlePath).LastWriteTimeUtc.Ticks
    $secondBundleHash = (Get-FileHash -LiteralPath $bundlePath -Algorithm SHA256).Hash
    if ($secondBundleTimestamp -ne $firstBundleTimestamp) {
        throw "$fixtureName rewrote its unchanged bundle."
    }
    if (-not $secondBundleHash.Equals(
            $firstBundleHash,
            [System.StringComparison]::Ordinal)) {
        throw "$fixtureName changed bundle bytes during an unchanged rebuild."
    }

    if ($null -ne $deliveredBundlePath) {
        $secondDeliveredTimestamp =
            (Get-Item -LiteralPath $deliveredBundlePath).LastWriteTimeUtc.Ticks
        if ($secondDeliveredTimestamp -ne $firstDeliveredTimestamp) {
            throw "$fixtureName recopied its unchanged plain-host bundle."
        }
    }

    if ($expectedStaticWebAssets) {
        Assert-UtilityCssStaticWebAssetRemoval `
            -ProjectPath $projectPath `
            -BundleName $bundleName `
            -IntermediateOutputPath $intermediateOutputPath `
            -TargetDirectory (Resolve-ProjectPath `
                -ProjectDirectory $projectDirectory `
                -Path (Get-JsonPropertyValue `
                    -InputObject $properties `
                    -Name 'TargetDir')) `
            -TargetName $assemblyName `
            -RetainedAssetName 'site.css' `
            -CommonProperties $commonProperties
        if ([System.IO.File]::Exists($bundlePath)) {
            throw "$fixtureName retained its generated bundle after every utility source was removed."
        }
    }
    else {
        Assert-UtilityCssPlainOutputRemoval `
            -ProjectPath $projectPath `
            -IntermediateBundlePath $bundlePath `
            -DeliveredBundlePath $deliveredBundlePath `
            -CommonProperties $commonProperties
    }

    Write-Host `
        "$fixtureName passed source, editor-sidecar, delivery, package-isolation, and incremental checks." `
        -ForegroundColor Green
}

$buildProjectPath = Join-Path `
    $repositoryRootPath `
    'libraries/Utilities/Assimalign.Viu.UtilityCss.Build/src/Assimalign.Viu.UtilityCss.Build.csproj'
$versionOutput = @(
    & dotnet msbuild `
        $buildProjectPath `
        -nologo `
        -verbosity:quiet `
        -getProperty:ViuVersion 2>&1 |
        ForEach-Object { $_.ToString() })
if ($LASTEXITCODE -ne 0) {
    throw "Reading the canonical Viu version failed with exit code $LASTEXITCODE.`n$($versionOutput -join [System.Environment]::NewLine)"
}
$viuVersion = ($versionOutput -join [System.Environment]::NewLine).Trim()
if ([string]::IsNullOrWhiteSpace($viuVersion)) {
    throw 'The canonical Viu version evaluated to an empty value.'
}

$packageDirectoryPath = [System.IO.Path]::GetFullPath($PackageDirectory)
$defaultPackageDirectoryPath = [System.IO.Path]::GetFullPath(
    (Join-Path $repositoryRootPath '_out/packages'))
if (-not $SkipPack -and -not $packageDirectoryPath.Equals(
        $defaultPackageDirectoryPath,
        $pathComparison)) {
    throw '-PackageDirectory may differ from repository _out/packages only with -SkipPack.'
}

if (-not $SkipPack) {
    Write-Host `
        "Packing Viu $viuVersion standalone libraries for the UtilityCss consumers..." `
        -ForegroundColor Cyan
    & pwsh `
        -NoProfile `
        -File (Join-Path $PSScriptRoot 'Install-Local.ps1') `
        -BaseOnly `
        -SkipSdk `
        -SkipFramework `
        -SkipCachePrune `
        -Configuration Release `
        -RestoreConfigurationFile (Join-Path $repositoryRootPath 'nuget.config')
    if ($LASTEXITCODE -ne 0) {
        throw "Install-Local.ps1 failed with exit code $LASTEXITCODE."
    }
}

foreach ($requiredPackage in @(
        "Assimalign.Viu.UtilityCss.$viuVersion.nupkg",
        "Assimalign.Viu.UtilityCss.Build.$viuVersion.nupkg")) {
    if (-not [System.IO.File]::Exists(
            (Join-Path $packageDirectoryPath $requiredPackage))) {
        throw "The package feed is missing $requiredPackage."
    }
}
Assert-UtilityCssBuildPackageLayout `
    -PackagePath (Join-Path `
        $packageDirectoryPath `
        "Assimalign.Viu.UtilityCss.Build.$viuVersion.nupkg")

$languageServerProjectPath = Join-Path `
    $repositoryRootPath `
    'tooling/Editor/Assimalign.Viu.LanguageServer/src/Assimalign.Viu.LanguageServer.csproj'
$languageServerTestProjectPath = Join-Path `
    $repositoryRootPath `
    'tooling/Editor/Assimalign.Viu.LanguageServer/test/Assimalign.Viu.LanguageServer.Tests.csproj'
Invoke-DotNet `
    -Description 'Building the in-repository Viu language server and process proof' `
    -Arguments @(
        'build',
        $languageServerTestProjectPath,
        '--configuration',
        'Release',
        '-warnaserror')
$languageServerPropertyResult = Invoke-DotNetForJson `
    -Description 'Resolving the in-repository Viu language-server executable' `
    -Arguments @(
        'msbuild',
        $languageServerProjectPath,
        '-nologo',
        '-verbosity:quiet',
        '-getProperty:TargetDir;TargetName',
        '-property:Configuration=Release')
if ($null -eq $languageServerPropertyResult.Properties) {
    throw 'MSBuild returned no property set for the Viu language server.'
}
$languageServerTargetDirectory = Get-JsonPropertyValue `
    -InputObject $languageServerPropertyResult.Properties `
    -Name 'TargetDir'
$languageServerTargetName = Get-JsonPropertyValue `
    -InputObject $languageServerPropertyResult.Properties `
    -Name 'TargetName'
$languageServerExecutableName = if ([System.OperatingSystem]::IsWindows()) {
    "$languageServerTargetName.exe"
}
else {
    $languageServerTargetName
}
$languageServerExecutablePath = Join-Path `
    $languageServerTargetDirectory `
    $languageServerExecutableName
if (-not [System.IO.File]::Exists($languageServerExecutablePath)) {
    throw "The in-repository Viu language-server executable was not built: $languageServerExecutablePath"
}

$scratchRootPath = Resolve-RepositoryOutputChild `
    -Path $ScratchRoot `
    -Description 'ScratchRoot'
Assert-PathHasNoReparsePoint -Path $scratchRootPath
$null = New-Item -ItemType Directory -Path $scratchRootPath -Force
$temporaryRootPath = [System.IO.Path]::GetFullPath(
    (Join-Path `
        $scratchRootPath `
        "run-$([System.Guid]::NewGuid().ToString('N'))"))
$scratchRootPrefix = $scratchRootPath.TrimEnd(
    [System.IO.Path]::DirectorySeparatorChar,
    [System.IO.Path]::AltDirectorySeparatorChar) +
    [System.IO.Path]::DirectorySeparatorChar
if (-not $temporaryRootPath.StartsWith($scratchRootPrefix, $pathComparison) -or
    -not [System.IO.Path]::GetFileName($temporaryRootPath).StartsWith(
        'run-',
        [System.StringComparison]::Ordinal)) {
    throw 'The staged UtilityCss consumer path escaped its validated scratch root.'
}
$null = New-Item -ItemType Directory -Path $temporaryRootPath

$fixtureDefinitions = @(
    @{
        Name = 'UtilityCssRazorConsumer'
        StaticWebAssets = $true
        RequiredText = @(
            '.sr-only {'
            '.p-4 {'
            'padding: calc(var(--spacing) * 4);'
            'width: 37px;'
            '.grid-cols-3 {'
            'grid-template-columns: repeat(3, minmax(0, 1fr));')
        ForbiddenText = @()
        ExpectedSourceRelativePaths = @(
            'UtilityProbe.razor'
            'Views/UtilityProbe.cshtml')
        ExpectedCatalogClasses = @('sr-only', 'p-4', 'w-[37px]')
        ForbiddenCatalogClasses = @()
    },
    @{
        Name = 'UtilityCssPlainConsumer'
        StaticWebAssets = $false
        RequiredText = @(
            '.sr-only {'
            '.rounded-lg {'
            'border-radius: var(--radius-lg);'
            'height: 41px;')
        ForbiddenText = @()
        ExpectedSourceRelativePaths = @('index.html')
        ExpectedCatalogClasses = @('sr-only', 'rounded-lg', 'h-[41px]')
        ForbiddenCatalogClasses = @()
    },
    @{
        Name = 'UtilityCssViuFileConsumer'
        StaticWebAssets = $false
        RequiredText = @(
            '.sr-only {'
            '.bg-blue-500 {'
            'background-color: var(--color-blue-500);'
            'opacity: 0.7654321;')
        ForbiddenText = @('0.1234567')
        ExpectedSourceRelativePaths = @('SlicingProbe.viu')
        ExpectedCatalogClasses = @('sr-only', 'bg-blue-500', 'opacity-[0.7654321]')
        ForbiddenCatalogClasses = @('opacity-[0.1234567]')
        ClassCatalogCompletionClass = 'bg-blue-500'
        ClassCatalogCompletionPrefix = 'bg-blue-'
    })

$previousNuGetPackages = [System.Environment]::GetEnvironmentVariable(
    'NUGET_PACKAGES',
    [System.EnvironmentVariableTarget]::Process)
try {
    foreach ($fixture in $fixtureDefinitions) {
        Copy-Item `
            -LiteralPath (Join-Path `
                $PSScriptRoot `
                "fixtures/$($fixture.Name)") `
            -Destination (Join-Path $temporaryRootPath $fixture.Name) `
            -Recurse
    }

    foreach ($buildFileName in @(
            'Directory.Build.props',
            'Directory.Build.targets')) {
        Set-Content `
            -LiteralPath (Join-Path $temporaryRootPath $buildFileName) `
            -Value '<Project />' `
            -Encoding utf8
    }

    $repositoryGlobalJson = Get-Content `
        -Raw `
        -LiteralPath (Join-Path $repositoryRootPath 'global.json') |
        ConvertFrom-Json
    $globalJson = @{
        sdk = @{
            version = $repositoryGlobalJson.sdk.version
            rollForward = 'latestPatch'
        }
    } | ConvertTo-Json -Depth 3
    Set-Content `
        -LiteralPath (Join-Path $temporaryRootPath 'global.json') `
        -Value $globalJson `
        -Encoding utf8

    $isolatedGlobalPackagesPath = Join-Path $temporaryRootPath '.nuget/packages'
    $null = New-Item `
        -ItemType Directory `
        -Path $isolatedGlobalPackagesPath
    [System.Environment]::SetEnvironmentVariable(
        'NUGET_PACKAGES',
        $isolatedGlobalPackagesPath,
        [System.EnvironmentVariableTarget]::Process)

    $escapedPackageDirectory =
        [System.Security.SecurityElement]::Escape($packageDirectoryPath)
    $escapedIsolatedGlobalPackagesPath =
        [System.Security.SecurityElement]::Escape($isolatedGlobalPackagesPath)
    $nugetConfiguration = @"
<?xml version="1.0" encoding="utf-8"?>
<configuration>
  <packageSources>
    <clear />
    <add key="viu-local" value="$escapedPackageDirectory" />
    <add key="dotnet10" value="https://pkgs.dev.azure.com/dnceng/public/_packaging/dotnet10/nuget/v3/index.json" />
    <add key="dotnet-public" value="https://pkgs.dev.azure.com/dnceng/public/_packaging/dotnet-public/nuget/v3/index.json" />
  </packageSources>
  <packageSourceMapping>
    <packageSource key="viu-local">
      <package pattern="Assimalign.Viu.*" />
    </packageSource>
    <packageSource key="dotnet10">
      <package pattern="Microsoft.*" />
      <package pattern="System.*" />
      <package pattern="runtime.*" />
      <package pattern="NETStandard.Library" />
    </packageSource>
    <packageSource key="dotnet-public">
      <package pattern="Microsoft.*" />
      <package pattern="System.*" />
      <package pattern="runtime.*" />
      <package pattern="NETStandard.Library" />
    </packageSource>
  </packageSourceMapping>
  <config>
    <add key="globalPackagesFolder" value="$escapedIsolatedGlobalPackagesPath" />
  </config>
</configuration>
"@
    $nugetConfigurationPath = Join-Path $temporaryRootPath 'nuget.config'
    Set-Content `
        -LiteralPath $nugetConfigurationPath `
        -Value $nugetConfiguration `
        -Encoding utf8

    foreach ($fixture in $fixtureDefinitions) {
        Test-UtilityCssFixture `
            -Fixture $fixture `
            -StagingRoot $temporaryRootPath `
            -NuGetConfigurationPath $nugetConfigurationPath `
            -Version $viuVersion `
            -LanguageServerExecutable $languageServerExecutablePath `
            -LanguageServerTestProjectPath $languageServerTestProjectPath
    }

    Write-Host `
        'Standalone UtilityCss package consumers passed Razor, plain-host, .viu slicing, editor-sidecar, Viu completion, delivery, and incremental checks.' `
        -ForegroundColor Green
}
finally {
    [System.Environment]::SetEnvironmentVariable(
        'NUGET_PACKAGES',
        $previousNuGetPackages,
        [System.EnvironmentVariableTarget]::Process)
    if ($KeepScratch) {
        Write-Host `
            "Retained UtilityCss package scratch: $temporaryRootPath" `
            -ForegroundColor Yellow
    }
    elseif ([System.IO.Directory]::Exists($temporaryRootPath)) {
        if (-not $temporaryRootPath.StartsWith($scratchRootPrefix, $pathComparison) -or
            -not [System.IO.Path]::GetFileName($temporaryRootPath).StartsWith(
                'run-',
                [System.StringComparison]::Ordinal)) {
            throw "Refusing to remove unexpected UtilityCss scratch path: $temporaryRootPath"
        }

        Assert-TreeHasNoReparsePoint -Path $temporaryRootPath
        Remove-Item -LiteralPath $temporaryRootPath -Recurse -Force
    }
}
