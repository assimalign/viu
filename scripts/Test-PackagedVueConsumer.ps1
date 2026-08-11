<#
.SYNOPSIS
    Verifies tag-based .vue compatibility through the installed base Viu SDK package.

.DESCRIPTION
    Stages an external Assimalign.Viu.Sdk component library in an isolated restore boundary,
    then proves package-only compilation, exact .vue source mapping, component-style packing,
    malformed-script diagnostics, and the canonical .viu-over-.vue shadow rule. Browser Release
    and AOT assets are exercised by Test-EndToEnd.ps1 -PackagedVuePublish; the live-update fallback
    is exercised by Test-EndToEnd.ps1 -HotReload.

    Specified by [V01.01.06.09.01], issue #253.

.PARAMETER SkipPack
    Reuses the current-version packages already present in PackageDirectory.

.PARAMETER PackageDirectory
    Local package feed. A non-default path requires SkipPack.

.PARAMETER ScratchRoot
    Parent for the isolated staged consumer. It must be below the repository _out directory.

.PARAMETER KeepScratch
    Retains the unique staged consumer for diagnosis.
#>
[CmdletBinding()]
param(
    [switch] $SkipPack,
    [string] $PackageDirectory =
        (Join-Path (Split-Path $PSScriptRoot -Parent) '_out/packages'),
    [string] $ScratchRoot =
        (Join-Path (Split-Path $PSScriptRoot -Parent) '_out/scratch/packaged-vue-consumer'),
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

function Assert-NoReparsePoint {
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

    $item = Get-Item -LiteralPath $existingPath -Force
    if (($item.Attributes -band [System.IO.FileAttributes]::ReparsePoint) -ne 0) {
        throw "Refusing to operate through a reparse point: $($item.FullName)"
    }
    if ($item.PSIsContainer) {
        $reparsePoints = @(
            Get-ChildItem -LiteralPath $item.FullName -Force -Recurse |
                Where-Object {
                    ($_.Attributes -band [System.IO.FileAttributes]::ReparsePoint) -ne 0
                })
        if ($reparsePoints.Count -ne 0) {
            throw "Refusing to operate below a directory containing reparse points: $($item.FullName)"
        }
    }
}

function Install-IsolatedNuGetPackage {
    param(
        [Parameter(Mandatory)]
        [string] $PackageId,
        [Parameter(Mandatory)]
        [string] $PackageVersion,
        [Parameter(Mandatory)]
        [string] $PackagePath,
        [Parameter(Mandatory)]
        [string] $PackagesRoot,
        [Parameter(Mandatory)]
        [string] $PackageSource
    )

    $normalizedPackageId = $PackageId.ToLowerInvariant()
    $normalizedPackageVersion = $PackageVersion.ToLowerInvariant()
    $destinationPath = Join-Path `
        (Join-Path $PackagesRoot $normalizedPackageId) `
        $normalizedPackageVersion
    $null = New-Item -ItemType Directory -Path $destinationPath
    [System.IO.Compression.ZipFile]::ExtractToDirectory(
        $PackagePath,
        $destinationPath,
        $true)
    $normalizedPackageFileName =
        "$normalizedPackageId.$normalizedPackageVersion.nupkg"
    [System.IO.File]::Copy(
        $PackagePath,
        (Join-Path $destinationPath $normalizedPackageFileName),
        $true)
    $hashAlgorithm = [System.Security.Cryptography.SHA512]::Create()
    try {
        $packageStream = [System.IO.File]::OpenRead($PackagePath)
        try {
            $contentHash = [System.Convert]::ToBase64String(
                $hashAlgorithm.ComputeHash($packageStream))
        }
        finally {
            $packageStream.Dispose()
        }
    }
    finally {
        $hashAlgorithm.Dispose()
    }
    [System.IO.File]::WriteAllText(
        (Join-Path $destinationPath "$normalizedPackageFileName.sha512"),
        $contentHash)
    $metadata = [ordered]@{
        version = 2
        contentHash = $contentHash
        source = $PackageSource
    } | ConvertTo-Json
    [System.IO.File]::WriteAllText(
        (Join-Path $destinationPath '.nupkg.metadata'),
        $metadata)
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

function Invoke-DotNetExpectFailure {
    param(
        [Parameter(Mandatory)]
        [string[]] $Arguments
    )

    $output = @(& dotnet @Arguments 2>&1 | ForEach-Object { $_.ToString() })
    $exitCode = $LASTEXITCODE
    if ($exitCode -eq 0) {
        throw "Expected dotnet to fail: dotnet $($Arguments -join ' ')"
    }

    # The non-zero code is the asserted result and is fully consumed here, so clear it. The
    # shadow-diagnostic build is the script's last native command; leaving $LASTEXITCODE at 1
    # made the GitHub Actions pwsh shell -- which exits with $LASTEXITCODE when the variable is
    # set -- fail the step after every check had passed. Failures are signalled by throw.
    $global:LASTEXITCODE = 0

    return $output -join [System.Environment]::NewLine
}

$targetFrameworkProperties = [xml](
    Get-Content -Raw -LiteralPath (
        Join-Path $repositoryRootPath 'build/Targets/Build.TargetFramework.props'))
$targetFrameworkLatest = $targetFrameworkProperties.SelectSingleNode(
    '/Project/PropertyGroup/TargetFrameworkLatest').InnerText.Trim()
$versionProperties = [xml](
    Get-Content -Raw -LiteralPath (
        Join-Path $repositoryRootPath 'build/Targets/Build.Version.props'))
$majorVersion = ([version]($targetFrameworkLatest -replace '^net', '')).Major
$minorVersion = $versionProperties.SelectSingleNode(
    '/Project/PropertyGroup/ViuMinorVersion').InnerText.Trim()
$patchVersion = $versionProperties.SelectSingleNode(
    '/Project/PropertyGroup/ViuPatchVersion').InnerText.Trim()
$viuVersion = "$majorVersion.$minorVersion.$patchVersion"
$consumerVersion = '1.0.0'

$packageDirectoryPath = [System.IO.Path]::GetFullPath($PackageDirectory)
$defaultPackageDirectoryPath = [System.IO.Path]::GetFullPath(
    (Join-Path $repositoryRootPath '_out/packages'))
if (-not $SkipPack -and -not $packageDirectoryPath.Equals(
        $defaultPackageDirectoryPath,
        $pathComparison)) {
    throw '-PackageDirectory may differ from repository _out/packages only with -SkipPack.'
}

if (-not $SkipPack) {
    Write-Host "Packing Viu $viuVersion for the packaged .vue consumer..." `
        -ForegroundColor Cyan
    & pwsh `
        -NoProfile `
        -File (Join-Path $PSScriptRoot 'Install-Local.ps1') `
        -Rids browser-wasm `
        -SkipCachePrune `
        -Configuration Release `
        -RestoreConfigurationFile (Join-Path $repositoryRootPath 'nuget.config')
    if ($LASTEXITCODE -ne 0) {
        throw "Install-Local.ps1 failed with exit code $LASTEXITCODE."
    }
}

foreach ($requiredPackage in @(
        "Assimalign.Viu.Sdk.$viuVersion.nupkg",
        "Assimalign.Viu.App.Ref.$viuVersion.nupkg")) {
    if (-not [System.IO.File]::Exists((Join-Path $packageDirectoryPath $requiredPackage))) {
        throw "The package feed is missing $requiredPackage."
    }
}

$scratchRootPath = Resolve-RepositoryOutputChild `
    -Path $ScratchRoot `
    -Description 'ScratchRoot'
$null = New-Item -ItemType Directory -Path $scratchRootPath -Force
Assert-NoReparsePoint -Path $scratchRootPath
$temporaryRootPath = Join-Path `
    $scratchRootPath `
    "run-$([System.Guid]::NewGuid().ToString('N'))"
$scratchRootPrefix = $scratchRootPath.TrimEnd(
    [System.IO.Path]::DirectorySeparatorChar,
    [System.IO.Path]::AltDirectorySeparatorChar) +
    [System.IO.Path]::DirectorySeparatorChar
$temporaryRootPath = [System.IO.Path]::GetFullPath($temporaryRootPath)
if (-not $temporaryRootPath.StartsWith($scratchRootPrefix, $pathComparison)) {
    throw 'The staged consumer path escaped its validated scratch root.'
}
$null = New-Item -ItemType Directory -Path $temporaryRootPath
Assert-NoReparsePoint -Path $temporaryRootPath

$previousNuGetPackages = [System.Environment]::GetEnvironmentVariable(
    'NUGET_PACKAGES',
    [System.EnvironmentVariableTarget]::Process)
try {
    $projectDirectory = Join-Path $temporaryRootPath 'PackagedVueComponentConsumer'
    Copy-Item `
        -LiteralPath (Join-Path $PSScriptRoot 'fixtures/PackagedVueComponentConsumer') `
        -Destination $projectDirectory `
        -Recurse
    Set-Content `
        -LiteralPath (Join-Path $temporaryRootPath 'Directory.Build.props') `
        -Value '<Project />' `
        -Encoding utf8
    Set-Content `
        -LiteralPath (Join-Path $temporaryRootPath 'Directory.Build.targets') `
        -Value '<Project />' `
        -Encoding utf8
    $repositoryGlobalJson = Get-Content `
        -Raw `
        -LiteralPath (Join-Path $repositoryRootPath 'global.json') |
        ConvertFrom-Json
    $globalJson = @{
        sdk = @{
            version = $repositoryGlobalJson.sdk.version
            rollForward = 'latestPatch'
        }
        'msbuild-sdks' = @{
            'Assimalign.Viu.Sdk' = $viuVersion
        }
    } | ConvertTo-Json -Depth 4
    Set-Content `
        -LiteralPath (Join-Path $temporaryRootPath 'global.json') `
        -Value $globalJson `
        -Encoding utf8

    $globalPackagesOutput = @(& dotnet nuget locals global-packages --list 2>$null)
    if ($LASTEXITCODE -ne 0 -or $globalPackagesOutput.Count -ne 1) {
        throw 'Could not resolve the existing NuGet global-packages directory.'
    }
    $globalPackagesSeparator = $globalPackagesOutput[0].IndexOf(':')
    if ($globalPackagesSeparator -lt 0) {
        throw "Unexpected global-packages output: $($globalPackagesOutput[0])"
    }
    $globalPackagesPath = [System.IO.Path]::GetFullPath(
        $globalPackagesOutput[0].Substring($globalPackagesSeparator + 1).Trim())
    $isolatedGlobalPackagesPath = Join-Path $temporaryRootPath '.nuget/packages'
    $null = New-Item -ItemType Directory -Path $isolatedGlobalPackagesPath
    Install-IsolatedNuGetPackage `
        -PackageId 'Assimalign.Viu.Sdk' `
        -PackageVersion $viuVersion `
        -PackagePath (Join-Path $packageDirectoryPath "Assimalign.Viu.Sdk.$viuVersion.nupkg") `
        -PackagesRoot $isolatedGlobalPackagesPath `
        -PackageSource $packageDirectoryPath
    [System.Environment]::SetEnvironmentVariable(
        'NUGET_PACKAGES',
        $isolatedGlobalPackagesPath,
        [System.EnvironmentVariableTarget]::Process)

    $escapedPackageDirectory =
        [System.Security.SecurityElement]::Escape($packageDirectoryPath)
    $escapedGlobalPackagesPath =
        [System.Security.SecurityElement]::Escape($globalPackagesPath)
    $escapedIsolatedGlobalPackagesPath =
        [System.Security.SecurityElement]::Escape($isolatedGlobalPackagesPath)
    $nugetConfiguration = @"
<?xml version="1.0" encoding="utf-8"?>
<configuration>
  <packageSources>
    <clear />
    <add key="viu-local" value="$escapedPackageDirectory" />
    <add key="restored-cache" value="$escapedGlobalPackagesPath" />
    <add key="dotnet10" value="https://pkgs.dev.azure.com/dnceng/public/_packaging/dotnet10/nuget/v3/index.json" />
    <add key="dotnet-public" value="https://pkgs.dev.azure.com/dnceng/public/_packaging/dotnet-public/nuget/v3/index.json" />
  </packageSources>
  <packageSourceMapping>
    <packageSource key="viu-local">
      <package pattern="Assimalign.Viu.*" />
    </packageSource>
    <packageSource key="restored-cache">
      <package pattern="Microsoft.*" />
      <package pattern="System.*" />
      <package pattern="runtime.*" />
      <package pattern="NETStandard.Library" />
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

    $projectPath = Join-Path $projectDirectory 'PackagedVueComponentConsumer.csproj'
    $commonProperties = @(
        "-p:ViuConsumerVersion=$viuVersion",
        "-p:PackagedVueConsumerVersion=$consumerVersion",
        '-p:NuGetAudit=false',
        '-p:AllowMissingPrunePackageData=true')
    Invoke-DotNet `
        -Description 'Restoring the package-only base-SDK .vue consumer' `
        -Arguments (@(
            'restore',
            $projectPath,
            '--configfile',
            $nugetConfigurationPath,
            '--ignore-failed-sources') + $commonProperties)

    $generatedSourceDirectory = Join-Path $temporaryRootPath 'generated/valid'
    Invoke-DotNet `
        -Description 'Building the package-only base-SDK .vue consumer' `
        -Arguments (@(
            'build',
            $projectPath,
            '--configuration',
            'Release',
            '--no-restore',
            '-warnaserror',
            '-p:EmitCompilerGeneratedFiles=true',
            "-p:CompilerGeneratedFilesOutputPath=$generatedSourceDirectory") +
            $commonProperties)

    $componentGeneratedSources = @(
        Get-ChildItem `
            -LiteralPath $generatedSourceDirectory `
            -Recurse `
            -File `
            -Filter '*PackagedCard.SingleFileComponent.g.cs')
    if ($componentGeneratedSources.Count -ne 1) {
        throw "Expected one generated PackagedCard source, found $($componentGeneratedSources.Count)."
    }
    $componentGeneratedSource = [System.IO.File]::ReadAllText(
        $componentGeneratedSources[0].FullName)
    foreach ($requiredText in @(
            '"PackagedCard.vue"',
            '#line 5',
            'Package-only .vue component',
            '.packaged-card')) {
        if (-not $componentGeneratedSource.Contains(
                $requiredText,
                [System.StringComparison]::Ordinal)) {
            throw "The package-only generated source is missing '$requiredText'."
        }
    }
    if ($componentGeneratedSource.Contains(
            'SingleFileComponentTemplateUpdateMarker',
            [System.StringComparison]::Ordinal)) {
        throw 'The Release package-only consumer emitted Debug hot-reload markers.'
    }

    $packageOutputPath = Join-Path $temporaryRootPath 'packages'
    Invoke-DotNet `
        -Description 'Packing the base-SDK .vue component library' `
        -Arguments (@(
            'pack',
            $projectPath,
            '--configuration',
            'Release',
            '--no-build',
            '--no-restore',
            '--output',
            $packageOutputPath) + $commonProperties)
    $componentPackagePath = Join-Path `
        $packageOutputPath `
        "PackagedVueComponentConsumer.$consumerVersion.nupkg"
    if (-not [System.IO.File]::Exists($componentPackagePath)) {
        throw "The .vue component package was not created: $componentPackagePath"
    }
    $archive = [System.IO.Compression.ZipFile]::OpenRead($componentPackagePath)
    try {
        $nuspecEntry = @(
            $archive.Entries |
                Where-Object {
                    $_.FullName.EndsWith('.nuspec', [System.StringComparison]::OrdinalIgnoreCase)
                })
        if ($nuspecEntry.Count -ne 1) {
            throw "Expected one nuspec in the .vue component package, found $($nuspecEntry.Count)."
        }
        $reader = [System.IO.StreamReader]::new($nuspecEntry[0].Open())
        try {
            $nuspec = $reader.ReadToEnd()
        }
        finally {
            $reader.Dispose()
        }
        if ($nuspec.Contains(
                'Assimalign.Viu.Browser',
                [System.StringComparison]::OrdinalIgnoreCase)) {
            throw 'The base-SDK .vue component package declares a Browser dependency.'
        }
        $styleEntry = $archive.GetEntry(
            'viu/styles/PackagedVueComponentConsumer.viu.css')
        $registrationEntry = $archive.GetEntry(
            'buildTransitive/PackagedVueComponentConsumer.props')
        if ($null -eq $styleEntry -or $styleEntry.Length -eq 0) {
            throw 'The base-SDK .vue component package is missing its non-empty style asset.'
        }
        if ($null -eq $registrationEntry -or $registrationEntry.Length -eq 0) {
            throw 'The base-SDK .vue component package is missing buildTransitive style registration.'
        }
    }
    finally {
        $archive.Dispose()
    }

    $brokenSourcePath = Join-Path $projectDirectory 'Broken.vue'
    [System.IO.File]::WriteAllText(
        $brokenSourcePath,
        '<script lang="csharp">public int Value = ;</script>',
        [System.Text.UTF8Encoding]::new($false))
    $diagnosticOutput = Invoke-DotNetExpectFailure `
        -Arguments (@(
            'build',
            $projectPath,
            '--configuration',
            'Release',
            '--no-restore',
            '-warnaserror') + $commonProperties)
    if ($diagnosticOutput -notmatch 'Broken\.vue\(1,42\): error VIU1201') {
        throw "The packaged generator did not map VIU1201 to Broken.vue(1,42).`n$diagnosticOutput"
    }
    Remove-Item -LiteralPath $brokenSourcePath -Force

    $canonicalSourcePath = Join-Path $projectDirectory 'PackagedCard.viu'
    [System.IO.File]::WriteAllText(
        $canonicalSourcePath,
        "<template>`n    <article>Canonical component</article>`n</template>`n",
        [System.Text.UTF8Encoding]::new($false))
    $shadowGeneratedSourceDirectory = Join-Path $temporaryRootPath 'generated/shadow'
    $shadowOutput = Invoke-DotNetExpectFailure `
        -Arguments (@(
            'build',
            $projectPath,
            '--configuration',
            'Release',
            '--no-restore',
            '-warnaserror',
            '-p:EmitCompilerGeneratedFiles=true',
            "-p:CompilerGeneratedFilesOutputPath=$shadowGeneratedSourceDirectory") +
            $commonProperties)
    if ($shadowOutput -notmatch 'PackagedCard\.vue\([0-9]+,[0-9]+\): error VIU1004') {
        throw "The packaged generator did not report the .viu-over-.vue shadow diagnostic.`n$shadowOutput"
    }
    $shadowGeneratedSources = @(
        Get-ChildItem `
            -LiteralPath $shadowGeneratedSourceDirectory `
            -Recurse `
            -File `
            -Filter '*PackagedCard.SingleFileComponent.g.cs')
    if ($shadowGeneratedSources.Count -ne 1) {
        throw "Expected one canonical shadow output, found $($shadowGeneratedSources.Count)."
    }
    $shadowGeneratedSource = [System.IO.File]::ReadAllText(
        $shadowGeneratedSources[0].FullName)
    if (-not $shadowGeneratedSource.Contains(
            '"PackagedCard.viu"',
            [System.StringComparison]::Ordinal) -or
        $shadowGeneratedSource.Contains(
            '"PackagedCard.vue"',
            [System.StringComparison]::Ordinal)) {
        throw 'The packaged shadow build did not compile only the canonical .viu source.'
    }

    Write-Host `
        'Packaged .vue consumer passed compile, source-map, diagnostic, shadow, and base-package asset checks.' `
        -ForegroundColor Green
}
finally {
    [System.Environment]::SetEnvironmentVariable(
        'NUGET_PACKAGES',
        $previousNuGetPackages,
        [System.EnvironmentVariableTarget]::Process)
    if ($KeepScratch) {
        Write-Host "Retained packaged .vue scratch: $temporaryRootPath" `
            -ForegroundColor Yellow
    }
    elseif ([System.IO.Directory]::Exists($temporaryRootPath)) {
        Assert-NoReparsePoint -Path $temporaryRootPath
        Remove-Item -LiteralPath $temporaryRootPath -Recurse -Force
    }
}
