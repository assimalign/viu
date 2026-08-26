<#
.SYNOPSIS
    Runs Viu's packaged, published real-browser end-to-end harness.

.DESCRIPTION
    Packs the current Viu libraries, SDKs, targeting packs, and Browser runtime pack; stages
    purpose-built external consumers behind an isolated global.json and NuGet configuration;
    publishes trimmed output; generates the hydration document through the packaged
    ServerRenderAdaptor; serves both applications; and drives them through Playwright .NET.

    The full browser matrix is intentionally on-demand/nightly rather than a per-pull-request
    cost. The same isolated publish path also feeds the publish-size, AOT, and startup gates.
    Specified by [V01.01.11.03], #87.

.PARAMETER BrowserEngine
    Chromium, Firefox, WebKit, or All. Chromium is the default local lane.

.PARAMETER Headed
    Launches visible browsers for local debugging.

.PARAMETER MeasureStartup
    Adds Chromium boot-to-interactive warm-up and measured runs and writes stable JSON.

.PARAMETER HotReload
    Runs the isolated packaged Debug .vue development-loop scenarios instead of the ordinary
    three-scenario published fixture lane. A Visual Studio-shaped session first launches through the
    packaged RunHost with a protocol-asserting BrowserRefresh endpoint and proves one .viu edit
    delivers component and utility stylesheet updates in real Chromium ([V01.01.12.30.05], #357).
    The unchanged watch lane
    then proves structural add/remove/v-if managed deltas, new-file NewTypeDefinition application,
    accepted style/template/script updates, automatic browser reload after a script-signature
    rude-edit restart, generated utility CSS creation, and last-source retirement through the
    generic generated-asset seam ([V01.01.12.30.04], #355).
    One session omits the runtime argument and proves component and utility stylesheet live swaps
    through the natural Browser SDK invocation ([V01.01.12.33], #356).
    It requires Chromium and owns the complete RunHost and dotnet-watch process trees for its staged
    consumer.

.PARAMETER StartupResultsPath
    Destination for startup measurement JSON.

.PARAMETER StartupWarmupRuns
    Fresh-context warm-up loads before measurement. Defaults to one.

.PARAMETER StartupMeasuredRuns
    Fresh-context measured loads. Defaults to ten.

.PARAMETER PublishOnly
    Stops after publishing the selected Browser fixture. No Playwright build, install, serve, or
    test occurs.

.PARAMETER PackagedVuePublish
    With PublishOnly, publishes the isolated .vue-only Browser consumer and verifies its generated
    component-CSS asset. Release and AOT metadata-absence checks then inspect that .vue consumer assembly.

.PARAMETER PublishDirectory
    Exact retained output for PublishOnly. It must be a child of this repository's _out directory.

.PARAMETER Aot
    With PublishOnly, restores and publishes the Browser fixture with AOT compilation enabled.

.PARAMETER SkipPack
    Reuses an already packed current-version feed. Intended for a fast local rerun only.

.PARAMETER SkipPackRestore
    Uses pre-existing restore assets while packing and building the harness. It may accompany
    -SkipPack to reuse both current packages and prepared harness assets. This is a restricted
    local-gate fallback; normal and CI runs restore every project.

.PARAMETER SkipBrowserInstall
    Skips Playwright's idempotent selected-browser installation step.

.PARAMETER PackageDirectory
    Package feed to consume. A custom directory is accepted only with -SkipPack; fresh packing
    always consumes Install-Local.ps1's repository _out/packages feed.
#>
[CmdletBinding()]
param(
    [ValidateSet('Chromium', 'Firefox', 'WebKit', 'All')]
    [string[]] $BrowserEngine = @('Chromium'),
    [switch] $Headed,
    [switch] $MeasureStartup,
    [switch] $HotReload,
    [string] $StartupResultsPath =
        (Join-Path (Split-Path $PSScriptRoot -Parent) '_out/test-results/end-to-end/startup.json'),
    [ValidateRange(1, 100)]
    [int] $StartupWarmupRuns = 1,
    [ValidateRange(10, 100)]
    [int] $StartupMeasuredRuns = 10,
    [switch] $PublishOnly,
    [switch] $PackagedVuePublish,
    [string] $PublishDirectory,
    [switch] $Aot,
    [switch] $SkipPack,
    [switch] $SkipPackRestore,
    [switch] $SkipBrowserInstall,
    [string] $PackageDirectory =
        (Join-Path (Split-Path $PSScriptRoot -Parent) '_out/packages'),
    [string] $ScratchRoot =
        (Join-Path (Split-Path $PSScriptRoot -Parent) '_out/scratch/end-to-end'),
    [string] $ArtifactsDirectory =
        (Join-Path (Split-Path $PSScriptRoot -Parent) '_out/test-results/end-to-end'),
    [ValidateSet('Debug', 'Release')]
    [string] $Configuration = 'Release',
    [switch] $KeepScratch
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$repositoryRootPath = [System.IO.Path]::GetFullPath(
    (Split-Path $PSScriptRoot -Parent))
$pathComparison = if ([System.OperatingSystem]::IsWindows()) {
    [System.StringComparison]::OrdinalIgnoreCase
}
else {
    [System.StringComparison]::Ordinal
}
$repositoryRootPrefix = $repositoryRootPath.TrimEnd(
    [System.IO.Path]::DirectorySeparatorChar,
    [System.IO.Path]::AltDirectorySeparatorChar) +
    [System.IO.Path]::DirectorySeparatorChar
$repositoryOutputPath = [System.IO.Path]::GetFullPath(
    (Join-Path $repositoryRootPath '_out'))
$repositoryOutputPrefix = $repositoryOutputPath.TrimEnd(
    [System.IO.Path]::DirectorySeparatorChar,
    [System.IO.Path]::AltDirectorySeparatorChar) +
    [System.IO.Path]::DirectorySeparatorChar

function Resolve-RepositoryOutputChild {
    param(
        [Parameter(Mandatory)]
        [string] $Path,
        [Parameter(Mandatory)]
        [string] $ParameterName
    )

    $resolvedPath = [System.IO.Path]::GetFullPath($Path)
    if (-not $resolvedPath.StartsWith(
            $repositoryOutputPrefix,
            $pathComparison)) {
        throw "$ParameterName must be a child of the repository _out directory: $resolvedPath"
    }

    return $resolvedPath
}

function Assert-NoReparsePoint {
    param(
        [Parameter(Mandatory)]
        [string] $Path
    )

    $resolvedPath = [System.IO.Path]::GetFullPath($Path)
    if (-not $resolvedPath.Equals($repositoryRootPath, $pathComparison) -and
        -not $resolvedPath.StartsWith($repositoryRootPrefix, $pathComparison)) {
        throw "Refusing to validate a path outside the repository: $resolvedPath"
    }

    $nearestExistingPath = $resolvedPath
    while (-not [System.IO.Directory]::Exists($nearestExistingPath) -and
        -not [System.IO.File]::Exists($nearestExistingPath)) {
        $parentPath = [System.IO.Directory]::GetParent($nearestExistingPath)
        if ($null -eq $parentPath) {
            throw "Could not locate an existing ancestor for output path: $resolvedPath"
        }
        $nearestExistingPath = $parentPath.FullName
    }

    $nearestExistingItem = Get-Item -LiteralPath $nearestExistingPath -Force
    if (($nearestExistingItem.Attributes -band [System.IO.FileAttributes]::ReparsePoint) -ne 0) {
        throw "Refusing to operate through a reparse-point path: $($nearestExistingItem.FullName)"
    }
    $ancestorItem = if ($nearestExistingItem.PSIsContainer) {
        $nearestExistingItem
    }
    else {
        $nearestExistingItem.Directory
    }
    while ($null -ne $ancestorItem) {
        if (($ancestorItem.Attributes -band [System.IO.FileAttributes]::ReparsePoint) -ne 0) {
            throw "Refusing to operate through a reparse-point path: $($ancestorItem.FullName)"
        }
        if ($ancestorItem.FullName.Equals($repositoryRootPath, $pathComparison)) {
            break
        }
        if (-not $ancestorItem.FullName.StartsWith($repositoryRootPrefix, $pathComparison)) {
            throw "Output path escaped the repository while validating ancestors: $Path"
        }
        $ancestorItem = $ancestorItem.Parent
    }
    if ($null -eq $ancestorItem) {
        throw "Could not validate output ancestors through the repository root: $Path"
    }

    if ([System.IO.Directory]::Exists($resolvedPath)) {
        $reparsePoints = @(
            Get-ChildItem -LiteralPath $resolvedPath -Force -Recurse |
                Where-Object {
                    ($_.Attributes -band [System.IO.FileAttributes]::ReparsePoint) -ne 0
                })
        if ($reparsePoints.Count -ne 0) {
            throw "Refusing to operate on a directory containing reparse points: $resolvedPath"
        }
    }
}

function Reset-RepositoryOutputDirectory {
    param(
        [Parameter(Mandatory)]
        [string] $Path
    )

    $resolvedPath = Resolve-RepositoryOutputChild -Path $Path -ParameterName 'output path'
    Assert-NoReparsePoint -Path $resolvedPath
    if ([System.IO.Directory]::Exists($resolvedPath)) {
        Remove-Item -LiteralPath $resolvedPath -Recurse -Force
    }

    $null = New-Item -ItemType Directory -Path $resolvedPath
    Assert-NoReparsePoint -Path $resolvedPath
    return $resolvedPath
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

if ($Aot -and -not $PublishOnly) {
    throw '-Aot is supported only with -PublishOnly.'
}

if ($PublishOnly -and [string]::IsNullOrWhiteSpace($PublishDirectory)) {
    throw '-PublishOnly requires -PublishDirectory.'
}

if ($PackagedVuePublish -and -not $PublishOnly) {
    throw '-PackagedVuePublish requires -PublishOnly.'
}

if ($PackagedVuePublish -and $HotReload) {
    throw '-PackagedVuePublish cannot be combined with -HotReload.'
}

if ($PackagedVuePublish -and $Configuration -ne 'Release') {
    throw '-PackagedVuePublish requires -Configuration Release.'
}

if (-not $PublishOnly -and -not [string]::IsNullOrWhiteSpace($PublishDirectory)) {
    throw '-PublishDirectory is supported only with -PublishOnly.'
}

if ($MeasureStartup -and $PublishOnly) {
    throw '-MeasureStartup cannot be combined with -PublishOnly.'
}

if ($MeasureStartup -and -not ($BrowserEngine -contains 'Chromium' -or $BrowserEngine -contains 'All')) {
    throw '-MeasureStartup requires Chromium or All in -BrowserEngine.'
}

if ($HotReload -and $Configuration -ne 'Debug') {
    throw '-HotReload requires -Configuration Debug.'
}

if ($HotReload -and $PublishOnly) {
    throw '-HotReload cannot be combined with -PublishOnly.'
}

if ($HotReload -and $MeasureStartup) {
    throw '-HotReload cannot be combined with -MeasureStartup.'
}

if ($HotReload -and ($BrowserEngine.Count -ne 1 -or $BrowserEngine[0] -ne 'Chromium')) {
    throw '-HotReload requires exactly -BrowserEngine Chromium.'
}

$targetFrameworkProperties = [xml](
    Get-Content -Raw -LiteralPath (
        Join-Path $repositoryRootPath 'build/Targets/Build.TargetFramework.props'))
$targetFrameworkNode = $targetFrameworkProperties.SelectSingleNode(
    '/Project/PropertyGroup/TargetFrameworkLatest')
$targetFrameworkLatest = $targetFrameworkNode.InnerText.Trim()
$versionProperties = [xml](
    Get-Content -Raw -LiteralPath (
        Join-Path $repositoryRootPath 'build/Targets/Build.Version.props'))
$majorVersion = ([version]($targetFrameworkLatest -replace '^net', '')).Major
$minorVersionNode = $versionProperties.SelectSingleNode(
    '/Project/PropertyGroup/ViuMinorVersion')
$patchVersionNode = $versionProperties.SelectSingleNode(
    '/Project/PropertyGroup/ViuPatchVersion')
$minorVersion = $minorVersionNode.InnerText.Trim()
$patchVersion = $patchVersionNode.InnerText.Trim()
$viuVersion = "$majorVersion.$minorVersion.$patchVersion"
$packageDirectoryPath = [System.IO.Path]::GetFullPath($PackageDirectory)
$defaultPackageDirectoryPath = [System.IO.Path]::GetFullPath(
    (Join-Path $repositoryRootPath '_out/packages'))
if (-not $SkipPack -and -not $packageDirectoryPath.Equals(
        $defaultPackageDirectoryPath,
        $pathComparison)) {
    throw '-PackageDirectory may differ from repository _out/packages only with -SkipPack.'
}

if (-not $SkipPack) {
    Write-Host "Packing Viu $viuVersion for the end-to-end consumer..." -ForegroundColor Cyan
    $installLocalScriptPath = Join-Path $PSScriptRoot 'Install-Local.ps1'
    $previousTargetPlatformSdkPath = [System.Environment]::GetEnvironmentVariable(
        'TargetPlatformSdkPath',
        [System.EnvironmentVariableTarget]::Process)
    $previousTargetPlatformDisplayName = [System.Environment]::GetEnvironmentVariable(
        'TargetPlatformDisplayName',
        [System.EnvironmentVariableTarget]::Process)
    try {
        if ([System.OperatingSystem]::IsWindows() -and
            [string]::IsNullOrWhiteSpace($previousTargetPlatformSdkPath)) {
            $windowsKitPath = Join-Path `
                ([System.Environment]::GetFolderPath(
                    [System.Environment+SpecialFolder]::ProgramFilesX86)) `
                'Windows Kits/10'
            if (Test-Path -LiteralPath $windowsKitPath -PathType Container) {
                # MSBuild otherwise probes the per-user Microsoft SDK directory, which is not
                # readable in restricted local gates. The installed kit is the canonical input.
                [System.Environment]::SetEnvironmentVariable(
                    'TargetPlatformSdkPath',
                    $windowsKitPath,
                    [System.EnvironmentVariableTarget]::Process)
                if ([string]::IsNullOrWhiteSpace($previousTargetPlatformDisplayName)) {
                    [System.Environment]::SetEnvironmentVariable(
                        'TargetPlatformDisplayName',
                        'Windows',
                        [System.EnvironmentVariableTarget]::Process)
                }
            }
        }

        # A child process does not inherit this orchestrator's strict mode. The explicit restore
        # configuration prevents NuGet from merging an ambient user configuration.
        $installLocalArguments = @(
            '-NoProfile',
            '-File',
            $installLocalScriptPath,
            '-Rids',
            'browser-wasm',
            '-SkipCachePrune',
            '-Configuration',
            $Configuration)
        if ($SkipPackRestore) {
            $installLocalArguments += '-SkipRestore'
        }
        else {
            $installLocalArguments += @(
                '-RestoreConfigurationFile',
                (Join-Path $repositoryRootPath 'nuget.config'))
        }
        & pwsh @installLocalArguments
    }
    finally {
        [System.Environment]::SetEnvironmentVariable(
            'TargetPlatformSdkPath',
            $previousTargetPlatformSdkPath,
            [System.EnvironmentVariableTarget]::Process)
        [System.Environment]::SetEnvironmentVariable(
            'TargetPlatformDisplayName',
            $previousTargetPlatformDisplayName,
            [System.EnvironmentVariableTarget]::Process)
    }
    if ($LASTEXITCODE -ne 0) {
        throw "Install-Local.ps1 failed with exit code $LASTEXITCODE."
    }
}

$requiredPackages = @(
    "Assimalign.Viu.Sdk.$viuVersion.nupkg",
    "Assimalign.Viu.Sdk.Browser.$viuVersion.nupkg",
    "Assimalign.Viu.App.Ref.$viuVersion.nupkg",
    "Assimalign.Viu.App.Browser.Ref.$viuVersion.nupkg",
    "Assimalign.Viu.App.Browser.Runtime.browser-wasm.$viuVersion.nupkg")
if ($HotReload) {
    $requiredPackages += "Assimalign.Viu.UtilityCss.Build.$viuVersion.nupkg"
}
elseif (-not $PackagedVuePublish) {
    $requiredPackages += @(
        "Assimalign.Viu.Router.$viuVersion.nupkg",
        "Assimalign.Viu.Browser.Router.$viuVersion.nupkg",
        "Assimalign.Viu.ServerRenderer.$viuVersion.nupkg")
}
foreach ($requiredPackage in $requiredPackages) {
    $requiredPackagePath = Join-Path $packageDirectoryPath $requiredPackage
    if (-not [System.IO.File]::Exists($requiredPackagePath)) {
        throw "The current package feed is missing $requiredPackagePath."
    }
}

$scratchRootPath = Resolve-RepositoryOutputChild `
    -Path $ScratchRoot `
    -ParameterName 'ScratchRoot'
Assert-NoReparsePoint -Path $scratchRootPath
$null = New-Item -ItemType Directory -Path $scratchRootPath -Force
Assert-NoReparsePoint -Path $scratchRootPath
$temporaryRootPath = [System.IO.Path]::GetFullPath(
    (Join-Path $scratchRootPath "run-$([System.Guid]::NewGuid().ToString('N'))"))
$scratchRootPrefix = $scratchRootPath.TrimEnd(
    [System.IO.Path]::DirectorySeparatorChar,
    [System.IO.Path]::AltDirectorySeparatorChar) +
    [System.IO.Path]::DirectorySeparatorChar
if (-not $temporaryRootPath.StartsWith(
        $scratchRootPrefix,
        $pathComparison)) {
    throw 'The end-to-end run directory escaped the validated scratch root.'
}

Assert-NoReparsePoint -Path $temporaryRootPath
$null = New-Item -ItemType Directory -Path $temporaryRootPath
Assert-NoReparsePoint -Path $temporaryRootPath
$previousNuGetPackages = [System.Environment]::GetEnvironmentVariable(
    'NUGET_PACKAGES',
    [System.EnvironmentVariableTarget]::Process)
$nuGetPackagesScoped = $false
try {
$fixtureSourceDirectory = Join-Path $PSScriptRoot 'fixtures'
$fixtureStageDirectory = Join-Path $temporaryRootPath 'fixtures'
$null = New-Item -ItemType Directory -Path $fixtureStageDirectory
$fixtureNames = if ($HotReload) {
    @('EndToEndHotReloadApp')
}
elseif ($PackagedVuePublish) {
    @('PackagedVueBrowserConsumer')
}
else {
    @(
        'EndToEndBrowserApp',
        'EndToEndHydrationApp',
        'EndToEndHydrationShared',
        'EndToEndServerMarkup')
}
foreach ($fixtureName in $fixtureNames) {
    Copy-Item `
        -LiteralPath (Join-Path $fixtureSourceDirectory $fixtureName) `
        -Destination $fixtureStageDirectory `
        -Recurse
}

$repositoryGlobalJson = Get-Content `
    -Raw `
    -LiteralPath (Join-Path $repositoryRootPath 'global.json') |
    ConvertFrom-Json
$globalJsonValue = @{
    sdk = @{
        version = $repositoryGlobalJson.sdk.version
        rollForward = 'latestPatch'
    }
}
if (-not $SkipPackRestore) {
    $globalJsonValue['msbuild-sdks'] = @{
        'Assimalign.Viu.Sdk' = $viuVersion
        'Assimalign.Viu.Sdk.Browser' = $viuVersion
    }
}
$globalJson = $globalJsonValue | ConvertTo-Json -Depth 4
Set-Content `
    -LiteralPath (Join-Path $temporaryRootPath 'global.json') `
    -Value $globalJson `
    -Encoding utf8
Set-Content `
    -LiteralPath (Join-Path $temporaryRootPath 'Directory.Build.props') `
    -Value '<Project />' `
    -Encoding utf8
Set-Content `
    -LiteralPath (Join-Path $temporaryRootPath 'Directory.Build.targets') `
    -Value '<Project />' `
    -Encoding utf8

$globalPackagesOutput = @(& dotnet nuget locals global-packages --list 2>$null)
if ($LASTEXITCODE -eq 0 -and $globalPackagesOutput.Count -eq 1) {
    $globalPackagesSeparator = $globalPackagesOutput[0].IndexOf(':')
    if ($globalPackagesSeparator -lt 0) {
        throw "Unexpected global-packages output: $($globalPackagesOutput[0])"
    }
    $globalPackagesPath = $globalPackagesOutput[0].Substring(
        $globalPackagesSeparator + 1).Trim()
}
else {
    $globalPackagesPath = [System.Environment]::GetEnvironmentVariable(
        'NUGET_PACKAGES',
        [System.EnvironmentVariableTarget]::Process)
    if ([string]::IsNullOrWhiteSpace($globalPackagesPath)) {
        $userProfilePath = [System.Environment]::GetFolderPath(
            [System.Environment+SpecialFolder]::UserProfile)
        if ([string]::IsNullOrWhiteSpace($userProfilePath)) {
            throw 'Could not resolve the user profile for NuGet global-packages discovery.'
        }
        $globalPackagesPath = Join-Path $userProfilePath '.nuget/packages'
    }
}
$globalPackagesPath = [System.IO.Path]::GetFullPath($globalPackagesPath)
if (-not [System.IO.Directory]::Exists($globalPackagesPath)) {
    throw "The resolved NuGet global-packages directory does not exist: $globalPackagesPath"
}
$isolatedGlobalPackagesPath = Join-Path $temporaryRootPath '.nuget/packages'
Assert-NoReparsePoint -Path $isolatedGlobalPackagesPath
$null = New-Item -ItemType Directory -Path $isolatedGlobalPackagesPath
Assert-NoReparsePoint -Path $isolatedGlobalPackagesPath
foreach ($sdkPackageId in @('Assimalign.Viu.Sdk', 'Assimalign.Viu.Sdk.Browser')) {
    Install-IsolatedNuGetPackage `
        -PackageId $sdkPackageId `
        -PackageVersion $viuVersion `
        -PackagePath (Join-Path $packageDirectoryPath "$sdkPackageId.$viuVersion.nupkg") `
        -PackagesRoot $isolatedGlobalPackagesPath `
        -PackageSource $packageDirectoryPath
}
[System.Environment]::SetEnvironmentVariable(
    'NUGET_PACKAGES',
    $isolatedGlobalPackagesPath,
    [System.EnvironmentVariableTarget]::Process)
$nuGetPackagesScoped = $true
if ($SkipPackRestore) {
    # The NuGet SDK resolver runs before restore applies --configfile and still loads ambient
    # settings even when NUGET_PACKAGES is preseeded. Restricted local gates therefore import the
    # freshly packed SDK contents explicitly in the staged copies. Default/CI runs retain canonical
    # NuGet SDK resolution through the unchanged fixture and global.json.
    $isolatedSdksPath = Join-Path $temporaryRootPath 'packaged-sdks'
    $null = New-Item -ItemType Directory -Path $isolatedSdksPath
    foreach ($sdkPackageId in @('Assimalign.Viu.Sdk', 'Assimalign.Viu.Sdk.Browser')) {
        $sdkPackagePath = Join-Path `
            $packageDirectoryPath `
            "$sdkPackageId.$viuVersion.nupkg"
        $sdkDestinationPath = Join-Path $isolatedSdksPath $sdkPackageId
        $null = New-Item -ItemType Directory -Path $sdkDestinationPath
        [System.IO.Compression.ZipFile]::ExtractToDirectory(
            $sdkPackagePath,
            $sdkDestinationPath,
            $true)
    }

    $baseSdkPath = Join-Path $isolatedSdksPath 'Assimalign.Viu.Sdk'
    $browserSdkPath = Join-Path $isolatedSdksPath 'Assimalign.Viu.Sdk.Browser'
    $baseSdkPropsPath = Join-Path $baseSdkPath 'Sdk/Sdk.props'
    $baseSdkTargetsPath = Join-Path $baseSdkPath 'Sdk/Sdk.targets'
    $browserSdkPropsPath = Join-Path $browserSdkPath 'Sdk/Sdk.props'
    $browserSdkTargetsPath = Join-Path $browserSdkPath 'Sdk/Sdk.targets'
    $escapedBaseSdkPropsPath = [System.Security.SecurityElement]::Escape($baseSdkPropsPath)
    $escapedBaseSdkTargetsPath = [System.Security.SecurityElement]::Escape($baseSdkTargetsPath)
    $browserSdkProps = [System.IO.File]::ReadAllText($browserSdkPropsPath).Replace(
        '<Import Sdk="Assimalign.Viu.Sdk" Project="Sdk.props" />',
        "<Import Project=`"$escapedBaseSdkPropsPath`" />")
    $browserSdkTargets = [System.IO.File]::ReadAllText($browserSdkTargetsPath).Replace(
        '<Import Sdk="Assimalign.Viu.Sdk" Project="Sdk.targets" />',
        "<Import Project=`"$escapedBaseSdkTargetsPath`" />")
    if ($browserSdkProps.Contains('Sdk="Assimalign.Viu.Sdk"', [System.StringComparison]::Ordinal) -or
        $browserSdkTargets.Contains('Sdk="Assimalign.Viu.Sdk"', [System.StringComparison]::Ordinal)) {
        throw 'Could not adapt the packaged Browser SDK imports for the restricted local gate.'
    }
    [System.IO.File]::WriteAllText($browserSdkPropsPath, $browserSdkProps)
    [System.IO.File]::WriteAllText($browserSdkTargetsPath, $browserSdkTargets)

    $escapedBrowserSdkPropsPath =
        [System.Security.SecurityElement]::Escape($browserSdkPropsPath)
    $escapedBrowserSdkTargetsPath =
        [System.Security.SecurityElement]::Escape($browserSdkTargetsPath)
    $stagedBrowserProjects = if ($HotReload) {
        @((Join-Path $fixtureStageDirectory 'EndToEndHotReloadApp/EndToEndHotReloadApp.csproj'))
    }
    elseif ($PackagedVuePublish) {
        @((Join-Path $fixtureStageDirectory 'PackagedVueBrowserConsumer/PackagedVueBrowserConsumer.csproj'))
    }
    else {
        @(
            (Join-Path $fixtureStageDirectory 'EndToEndBrowserApp/EndToEndBrowserApp.csproj'),
            (Join-Path $fixtureStageDirectory 'EndToEndHydrationApp/EndToEndHydrationApp.csproj'))
    }
    foreach ($stagedBrowserProject in $stagedBrowserProjects) {
        $projectContent = [System.IO.File]::ReadAllText($stagedBrowserProject)
        $projectContent = $projectContent.Replace(
            '<Project Sdk="Assimalign.Viu.Sdk.Browser">',
            "<Project>`r`n  <Import Project=`"$escapedBrowserSdkPropsPath`" />")
        $projectContent = $projectContent.Replace(
            '</Project>',
            "  <Import Project=`"$escapedBrowserSdkTargetsPath`" />`r`n</Project>")
        if ($projectContent.Contains(
                '<Project Sdk="Assimalign.Viu.Sdk.Browser">',
                [System.StringComparison]::Ordinal)) {
            throw "Could not adapt the staged Browser SDK project: $stagedBrowserProject"
        }
        [System.IO.File]::WriteAllText($stagedBrowserProject, $projectContent)
    }
}
$escapedPackageDirectory =
    [System.Security.SecurityElement]::Escape($packageDirectoryPath)
$escapedGlobalPackagesPath =
    [System.Security.SecurityElement]::Escape($globalPackagesPath)
$escapedIsolatedGlobalPackagesPath =
    [System.Security.SecurityElement]::Escape($isolatedGlobalPackagesPath)
$onlinePackageSources = ''
$onlinePackageSourceMappings = ''
if (-not $SkipPackRestore) {
    $onlinePackageSources = @"
    <add key="dotnet10" value="https://pkgs.dev.azure.com/dnceng/public/_packaging/dotnet10/nuget/v3/index.json" />
    <add key="dotnet-public" value="https://pkgs.dev.azure.com/dnceng/public/_packaging/dotnet-public/nuget/v3/index.json" />
"@
    $onlinePackageSourceMappings = @"
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
"@
}
$nugetConfiguration = @"
<?xml version="1.0" encoding="utf-8"?>
<configuration>
  <packageSources>
    <clear />
    <add key="viu-local" value="$escapedPackageDirectory" />
    <add key="restored-cache" value="$escapedGlobalPackagesPath" />
$onlinePackageSources
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
$onlinePackageSourceMappings
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
$fixtureRestoreArguments = @(
    '--configfile',
    $nugetConfigurationPath)
if ($SkipPackRestore) {
    # A restricted local run is intentionally cache-only, so its effective sources never consume
    # the network. NuGet provider initialization may still inspect ambient settings before applying
    # this explicit configuration; the source map itself remains local and fail-closed.
    $fixtureRestoreArguments += '--no-http-cache'
}
else {
    $fixtureRestoreArguments += '--ignore-failed-sources'
}

$browserProjectRelativePath = if ($HotReload) {
    'EndToEndHotReloadApp/EndToEndHotReloadApp.csproj'
}
elseif ($PackagedVuePublish) {
    'PackagedVueBrowserConsumer/PackagedVueBrowserConsumer.csproj'
}
else {
    'EndToEndBrowserApp/EndToEndBrowserApp.csproj'
}
$browserProject = Join-Path $fixtureStageDirectory $browserProjectRelativePath
$browserAssemblyName = [System.IO.Path]::GetFileNameWithoutExtension($browserProject)
$hydrationProject = Join-Path `
    $fixtureStageDirectory `
    'EndToEndHydrationApp/EndToEndHydrationApp.csproj'
$serverMarkupProject = Join-Path `
    $fixtureStageDirectory `
    'EndToEndServerMarkup/EndToEndServerMarkup.csproj'
$restoreProperties = @(
    "-p:ViuConsumerVersion=$viuVersion",
    '-p:NuGetAudit=false',
    '-p:AllowMissingPrunePackageData=true')
$aotValue = if ($Aot) { 'true' } else { 'false' }
$browserRestoreProperties = @(
    $restoreProperties
    "-p:RunAOTCompilation=$aotValue"
    "-p:ViuRunAotCompilation=$aotValue")
if ($HotReload) {
    $browserRestoreProperties += '-p:RuntimeIdentifier=browser-wasm'
}

    Invoke-DotNet `
        -Description "Restoring the packaged $([System.IO.Path]::GetFileNameWithoutExtension($browserProject)) fixture" `
         -Arguments (@(
             'restore',
             $browserProject) + $fixtureRestoreArguments + $browserRestoreProperties)

if (-not $HotReload) {
    $browserPublishDirectory = if ($PublishOnly) {
        Reset-RepositoryOutputDirectory -Path $PublishDirectory
    }
    else {
        $path = Join-Path $temporaryRootPath "publish/$browserAssemblyName"
        $null = New-Item -ItemType Directory -Path $path -Force
        $path
    }
    $browserGeneratedSourceDirectory = Join-Path `
        $temporaryRootPath `
        "generated/$browserAssemblyName"
    $null = New-Item `
        -ItemType Directory `
        -Path $browserGeneratedSourceDirectory `
        -Force
    $browserPublishArguments = @(
        'publish',
        $browserProject,
        '--configuration',
        $Configuration,
        '--no-restore',
        '-warnaserror',
        '--output',
        $browserPublishDirectory,
        '-p:PublishTrimmed=true',
        '-p:TrimMode=full',
        '-p:EmitCompilerGeneratedFiles=true',
        "-p:CompilerGeneratedFilesOutputPath=$browserGeneratedSourceDirectory") +
        $browserRestoreProperties
    Invoke-DotNet `
        -Description "Publishing $browserAssemblyName (trimmed, AOT=$aotValue)" `
        -Arguments $browserPublishArguments

    $browserWebRoot = Join-Path $browserPublishDirectory 'wwwroot'
    foreach ($requiredOutput in @(
            (Join-Path $browserWebRoot 'index.html'),
            (Join-Path $browserWebRoot '_content/Assimalign.Viu.Browser/viu-dom.js'))) {
        if (-not [System.IO.File]::Exists($requiredOutput)) {
            throw "The Browser publish is missing required output: $requiredOutput"
        }
    }
    if (@(Get-ChildItem -LiteralPath (Join-Path $browserWebRoot '_framework') -Filter 'dotnet*.js' -File).Count -eq 0) {
        throw 'The Browser publish did not contain the .NET WebAssembly boot module.'
    }
    if (@(Get-ChildItem -LiteralPath $browserWebRoot -Filter 'main*.js' -File).Count -eq 0) {
        throw 'The Browser publish did not contain the fingerprinted application boot module.'
    }

    if ($PackagedVuePublish) {
        $requiredCssAssetNames = @("$browserAssemblyName.viu.css")
        foreach ($requiredCssAssetName in $requiredCssAssetNames) {
            $requiredCssAsset = Join-Path $browserWebRoot $requiredCssAssetName
            if (-not [System.IO.File]::Exists($requiredCssAsset) -or
                (Get-Item -LiteralPath $requiredCssAsset).Length -eq 0) {
                throw "The packaged .vue Browser publish is missing a non-empty CSS asset: $requiredCssAsset"
            }
        }

        $publishedVueHostPage = [System.IO.File]::ReadAllText(
            (Join-Path $browserWebRoot 'index.html'))
        foreach ($requiredCssAssetName in $requiredCssAssetNames) {
            $hrefPattern =
                'href\s*=\s*["'']' +
                [System.Text.RegularExpressions.Regex]::Escape($requiredCssAssetName) +
                '["'']'
            $hrefMatches = [System.Text.RegularExpressions.Regex]::Matches(
                $publishedVueHostPage,
                $hrefPattern,
                [System.Text.RegularExpressions.RegexOptions]::IgnoreCase)
            if ($hrefMatches.Count -ne 1) {
                throw "The packaged .vue Browser host page must link '$requiredCssAssetName' exactly once; found $($hrefMatches.Count)."
            }
        }
        $vueGeneratedSources = @(
            Get-ChildItem `
                -LiteralPath $browserGeneratedSourceDirectory `
                -Recurse `
                -File `
                -Filter '*.SingleFileComponent.g.cs')
        if ($vueGeneratedSources.Count -ne 1) {
            throw "Expected one .vue generated component in the Browser publish, found $($vueGeneratedSources.Count)."
        }
        $vueGeneratedSource = [System.IO.File]::ReadAllText(
            $vueGeneratedSources[0].FullName)
        if (-not $vueGeneratedSource.Contains(
                '"PublishedVuePage.vue"',
                [System.StringComparison]::Ordinal) -or
            $vueGeneratedSource.Contains(
                '"PublishedVuePage.viu"',
                [System.StringComparison]::Ordinal)) {
            throw 'The packaged Browser publish did not compile exclusively from its authored .vue source.'
        }

        Write-Host `
            'Packaged .vue Browser publish contains its component CSS asset.' `
            -ForegroundColor Green
    }

    if ($Configuration -eq 'Release') {
        $hotReloadMetadataTokens = @(
            'SingleFileComponentHotReloadMetadataUpdateHandler',
            'SingleFileComponentTemplateUpdateMarker',
            'SingleFileComponentScriptUpdateMarker',
            'SingleFileComponentStyleUpdateMarker',
            'ComponentHotReload.Register(')
        $generatedSourceFiles = @(
            Get-ChildItem `
                -LiteralPath $browserGeneratedSourceDirectory `
                -Recurse `
                -File `
                -Filter '*.cs')
        if ($generatedSourceFiles.Count -eq 0) {
            throw 'The Release Browser publish emitted no compiler-generated sources to inspect.'
        }
        foreach ($generatedSourceFile in $generatedSourceFiles) {
            $generatedSource = [System.IO.File]::ReadAllText($generatedSourceFile.FullName)
            foreach ($hotReloadMetadataToken in $hotReloadMetadataTokens) {
                if ($generatedSource.Contains(
                        $hotReloadMetadataToken,
                        [System.StringComparison]::Ordinal)) {
                    throw "Release generated source contains hot-reload metadata '$hotReloadMetadataToken': $($generatedSourceFile.FullName)"
                }
            }
        }

        $publishedConsumerAssemblies = @(
            Get-ChildItem `
                -LiteralPath (Join-Path $browserWebRoot '_framework') `
                -File `
                -Filter "$browserAssemblyName*.wasm")
        if ($publishedConsumerAssemblies.Count -eq 0) {
            throw 'The Release Browser publish did not expose its consumer assembly for metadata inspection.'
        }
        foreach ($publishedConsumerAssembly in $publishedConsumerAssemblies) {
            $publishedConsumerText = [System.Text.Encoding]::UTF8.GetString(
                [System.IO.File]::ReadAllBytes($publishedConsumerAssembly.FullName))
            foreach ($hotReloadMetadataToken in $hotReloadMetadataTokens) {
                if ($publishedConsumerText.Contains(
                        $hotReloadMetadataToken,
                        [System.StringComparison]::Ordinal)) {
                    throw "Release consumer assembly contains hot-reload metadata '$hotReloadMetadataToken': $($publishedConsumerAssembly.FullName)"
                }
            }
        }

        $developmentArtifactFiles = @(
            Get-ChildItem -LiteralPath $browserPublishDirectory -Recurse -File |
                Where-Object {
                    $_.Name -match
                        '(?i)(Assimalign\.Viu\.Sdk\.(CssHotReload|Browser\.RunHost)|browser[-.]?refresh|dotnet[-.]?watch)'
                })
        if ($developmentArtifactFiles.Count -ne 0) {
            $relativeDevelopmentArtifactFiles = @(
                $developmentArtifactFiles |
                    ForEach-Object {
                        [System.IO.Path]::GetRelativePath(
                            $browserPublishDirectory,
                            $_.FullName)
                    })
            throw (
                'The Release Browser publish contains development-loop files: ' +
                ($relativeDevelopmentArtifactFiles -join ', '))
        }

        $publishedHostPage = Get-Content -Raw -LiteralPath (Join-Path $browserWebRoot 'index.html')
        if ($publishedHostPage -match
            '(?i)(aspnetcore-browser-refresh|browser-refresh|dotnet-watch|ViuCssHotReloadWorker)') {
            throw 'The Release Browser host page contains a development-loop transport reference.'
        }

        Write-Host 'Release publish contains no hot-reload metadata, development worker, or browser-refresh transport.' `
            -ForegroundColor Green
    }

    if ($PublishOnly) {
        Write-Host `
            "VIU_END_TO_END_PUBLISH_DIRECTORY=$browserPublishDirectory" `
            -ForegroundColor Cyan
        return
    }

    Invoke-DotNet `
        -Description 'Restoring the packaged ServerRenderAdaptor markup generator' `
         -Arguments (@(
             'restore',
             $serverMarkupProject) + $fixtureRestoreArguments + $restoreProperties)
    Invoke-DotNet `
        -Description 'Building the packaged ServerRenderAdaptor markup generator' `
        -Arguments (@(
            'build',
            $serverMarkupProject,
            '--configuration',
            $Configuration,
            '--no-restore',
            '-warnaserror') + $restoreProperties)

    $generatedIndexPath = Join-Path `
        (Split-Path $hydrationProject -Parent) `
        'wwwroot/index.html'
    $serverMarkupAssembly = Join-Path `
        (Split-Path $serverMarkupProject -Parent) `
        "bin/$Configuration/net10.0/EndToEndServerMarkup.dll"
    Invoke-DotNet `
        -Description 'Generating SSR markup through ServerRenderAdaptor' `
        -Arguments @($serverMarkupAssembly, $generatedIndexPath)

    Invoke-DotNet `
        -Description 'Restoring the packaged EndToEndHydrationApp fixture' `
         -Arguments (@(
             'restore',
             $hydrationProject) + $fixtureRestoreArguments + $restoreProperties)
    $hydrationPublishDirectory = Join-Path `
        $temporaryRootPath `
        'publish/EndToEndHydrationApp'
    Invoke-DotNet `
        -Description 'Publishing EndToEndHydrationApp (trimmed)' `
        -Arguments (@(
            'publish',
            $hydrationProject,
            '--configuration',
            $Configuration,
            '--no-restore',
            '-warnaserror',
            '--output',
            $hydrationPublishDirectory,
            '-p:PublishTrimmed=true',
            '-p:TrimMode=full',
            '-p:RunAOTCompilation=false',
            '-p:ViuRunAotCompilation=false') + $restoreProperties)
    $hydrationWebRoot = Join-Path $hydrationPublishDirectory 'wwwroot'
    if (-not [System.IO.File]::Exists((Join-Path $hydrationWebRoot 'index.html'))) {
        throw 'The hydration publish did not contain adaptor-generated index.html.'
    }
}

    $harnessProject = Join-Path `
        $repositoryRootPath `
        'benchmarks/Assimalign.Viu.Testing.EndToEnd/src/Assimalign.Viu.Testing.EndToEnd.csproj'
    if ($SkipPackRestore) {
        $harnessAssetsPath = Join-Path `
            (Split-Path $harnessProject -Parent) `
            'obj/project.assets.json'
        if (-not [System.IO.File]::Exists($harnessAssetsPath)) {
            throw "-SkipPackRestore requires prepared harness assets: $harnessAssetsPath"
        }

        $packageCatalogPath = Join-Path `
            $repositoryRootPath `
            'build/Targets/Build.References.Packages.targets'
        $packageCatalog = [xml](Get-Content -Raw -LiteralPath $packageCatalogPath)
        $playwrightPackageNode = $packageCatalog.SelectSingleNode(
            "/Project/ItemGroup/_ViuPackage[@Include='Microsoft.Playwright']")
        if ($null -eq $playwrightPackageNode) {
            throw "The central package catalog does not declare Microsoft.Playwright: $packageCatalogPath"
        }
        $expectedPlaywrightVersion = $playwrightPackageNode.Version
        $harnessAssets = Get-Content -Raw -LiteralPath $harnessAssetsPath | ConvertFrom-Json
        $resolvedPlaywrightLibraries = @(
            $harnessAssets.libraries.PSObject.Properties |
                ForEach-Object { $_.Name } |
                Where-Object { $_.StartsWith('Microsoft.Playwright/', [System.StringComparison]::Ordinal) })
        $expectedPlaywrightLibrary = "Microsoft.Playwright/$expectedPlaywrightVersion"
        if ($resolvedPlaywrightLibraries.Count -ne 1 -or
            -not $resolvedPlaywrightLibraries[0].Equals(
                $expectedPlaywrightLibrary,
                [System.StringComparison]::Ordinal)) {
            $resolvedVersions = if ($resolvedPlaywrightLibraries.Count -eq 0) {
                '<none>'
            }
            else {
                $resolvedPlaywrightLibraries -join ', '
            }
            throw "-SkipPackRestore harness assets are stale: expected $expectedPlaywrightLibrary; resolved $resolvedVersions. Run a canonical restore first."
        }
    }
    else {
        Invoke-DotNet `
            -Description 'Restoring the Playwright end-to-end driver' `
            -Arguments @(
                'restore',
                $harnessProject,
                '--configfile',
                (Join-Path $repositoryRootPath 'nuget.config'),
                '--ignore-failed-sources',
                '-p:NuGetAudit=false')
    }
    Invoke-DotNet `
        -Description 'Building the Playwright end-to-end driver' `
        -Arguments @(
            'build',
            $harnessProject,
            '--configuration',
            $Configuration,
            '--no-restore',
            '-warnaserror')
    $harnessOutputDirectory = Join-Path `
        (Split-Path $harnessProject -Parent) `
        "bin/$Configuration/net10.0"
    $harnessAssembly = Join-Path `
        $harnessOutputDirectory `
        'Assimalign.Viu.Testing.EndToEnd.dll'

    if (-not $SkipBrowserInstall) {
        $playwrightScript = Join-Path $harnessOutputDirectory 'playwright.ps1'
        if (-not [System.IO.File]::Exists($playwrightScript)) {
            throw "The Playwright build did not emit its installation script: $playwrightScript"
        }

        $selectedBrowserNames = [System.Collections.Generic.List[string]]::new()
        foreach ($engine in $BrowserEngine) {
            if ($engine -eq 'All') {
                foreach ($name in @('chromium', 'firefox', 'webkit')) {
                    if (-not $selectedBrowserNames.Contains($name)) {
                        $selectedBrowserNames.Add($name)
                    }
                }
            }
            else {
                $name = $engine.ToLowerInvariant()
                if (-not $selectedBrowserNames.Contains($name)) {
                    $selectedBrowserNames.Add($name)
                }
            }
        }

        Write-Host `
            "Ensuring Playwright browsers: $($selectedBrowserNames -join ', ')" `
            -ForegroundColor Green
        $playwrightInstallArguments = [System.Collections.Generic.List[string]]::new()
        $playwrightInstallArguments.Add('install')
        if ([System.OperatingSystem]::IsLinux()) {
            $playwrightInstallArguments.Add('--with-deps')
        }
        $playwrightInstallArguments.AddRange($selectedBrowserNames)
        & pwsh -NoProfile -File $playwrightScript @playwrightInstallArguments
        if ($LASTEXITCODE -ne 0) {
            throw "Playwright browser installation failed with exit code $LASTEXITCODE."
        }
    }

    $artifactsDirectoryPath = Reset-RepositoryOutputDirectory `
        -Path $ArtifactsDirectory
    $harnessArguments = [System.Collections.Generic.List[string]]::new()
    $harnessArguments.Add($harnessAssembly)
    if ($HotReload) {
        $harnessArguments.Add('--hot-reload-project')
        $harnessArguments.Add($browserProject)
        $harnessArguments.Add('--hot-reload-viu-version')
        $harnessArguments.Add($viuVersion)
    }
    else {
        $harnessArguments.Add('--browser-root')
        $harnessArguments.Add($browserWebRoot)
        $harnessArguments.Add('--hydration-root')
        $harnessArguments.Add($hydrationWebRoot)
    }
    foreach ($argument in @(
            '--artifacts',
            $artifactsDirectoryPath)) {
        $harnessArguments.Add($argument)
    }
    foreach ($engine in $BrowserEngine) {
        $harnessArguments.Add('--browser-engine')
        $harnessArguments.Add($engine)
    }
    if ($Headed) {
        $harnessArguments.Add('--headed')
    }
    if ($MeasureStartup) {
        $startupResultsPathValue = Resolve-RepositoryOutputChild `
            -Path $StartupResultsPath `
            -ParameterName 'StartupResultsPath'
        Assert-NoReparsePoint -Path $startupResultsPathValue
        $harnessArguments.Add('--measure-startup')
        $harnessArguments.Add('--startup-results')
        $harnessArguments.Add($startupResultsPathValue)
        $harnessArguments.Add('--startup-warmup-runs')
        $harnessArguments.Add(
            $StartupWarmupRuns.ToString([System.Globalization.CultureInfo]::InvariantCulture))
        $harnessArguments.Add('--startup-measured-runs')
        $harnessArguments.Add(
            $StartupMeasuredRuns.ToString([System.Globalization.CultureInfo]::InvariantCulture))
    }

    $harnessDescription = if ($HotReload) {
        'Running the isolated packaged hot-reload browser scenario'
    }
    else {
        'Running the real-browser end-to-end scenarios'
    }
    Invoke-DotNet `
        -Description $harnessDescription `
        -Arguments $harnessArguments.ToArray()

    if ($HotReload) {
        $hotReloadResultPath = Join-Path $artifactsDirectoryPath 'results.json'
        if (-not [System.IO.File]::Exists($hotReloadResultPath)) {
            throw "The hot-reload harness did not write its result summary: $hotReloadResultPath"
        }

        $hotReloadResult = Get-Content `
            -Raw `
            -LiteralPath $hotReloadResultPath |
            ConvertFrom-Json
        # [V01.01.12.30.05], #357: keep the new Visual Studio-shaped RunHost case
        # separate while pinning all nine established dotnet-watch scenarios by name.
        $requiredHotReloadScenarios = @(
            'packaged-vue-run-host-visual-studio-generated-assets',
            # [V01.01.12.33], #356: the natural watch invocation must launch
            # the worker and deliver both registered generated-asset families.
            'packaged-vue-watch-no-runtime-css-and-utility',
            'packaged-vue-watch-structural-add-delta',
            'packaged-vue-watch-structural-remove-delta',
            'packaged-vue-watch-structural-v-if-delta',
            'packaged-vue-watch-new-file-new-type-definition-delta',
            'packaged-vue-watch-css-and-remount',
            'packaged-vue-watch-script-signature-restart-reload',
            # [V01.01.12.30.04], #355: these names pin both generated-asset
            # creation and PreserveEmpty retirement into the packaged watch lane.
            'packaged-vue-watch-utility-css-new-class',
            'packaged-vue-watch-utility-css-last-source-removal')
        if (@($hotReloadResult.scenarios).Count -ne $requiredHotReloadScenarios.Count) {
            $actualScenarioCount = @($hotReloadResult.scenarios).Count
            throw "Expected exactly $($requiredHotReloadScenarios.Count) hot-reload scenarios; " +
                "found $actualScenarioCount."
        }
        foreach ($requiredHotReloadScenario in $requiredHotReloadScenarios) {
            $matchingScenarios = @(
                $hotReloadResult.scenarios |
                    Where-Object {
                        $_.scenario -eq $requiredHotReloadScenario
                    })
            if ($matchingScenarios.Count -ne 1) {
                throw "Expected exactly one hot-reload result for " +
                    "'$requiredHotReloadScenario'; found $($matchingScenarios.Count)."
            }
            if (-not $matchingScenarios[0].succeeded) {
                throw "The required hot-reload scenario failed: $requiredHotReloadScenario"
            }
        }
    }

    if ($MeasureStartup -and -not [System.IO.File]::Exists(
            [System.IO.Path]::GetFullPath($StartupResultsPath))) {
        throw "The startup measurement result was not written: $StartupResultsPath"
    }

    Write-Host `
        "End-to-end diagnostics: $artifactsDirectoryPath" `
        -ForegroundColor Cyan
}
finally {
    try {
        if ($nuGetPackagesScoped) {
            [System.Environment]::SetEnvironmentVariable(
                'NUGET_PACKAGES',
                $previousNuGetPackages,
                [System.EnvironmentVariableTarget]::Process)
        }
    }
    finally {
        if ($KeepScratch) {
            Write-Host "Retained end-to-end scratch: $temporaryRootPath" -ForegroundColor Yellow
        }
        elseif ([System.IO.Directory]::Exists($temporaryRootPath)) {
            Assert-NoReparsePoint -Path $temporaryRootPath
            Remove-Item -LiteralPath $temporaryRootPath -Recurse -Force
        }
    }
}
