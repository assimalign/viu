<#
.SYNOPSIS
    Installs and validates the packaged Viu dotnet-new templates.

.DESCRIPTION
    Packs or reuses the complete package feed, installs Assimalign.Viu.Templates into an isolated
    dotnet CLI home, and proves the eight-row application nullable-by-SSR, library nullable, and
    kebab-case naming matrix against that package feed. Generated projects never consume repository
    project references. The
    application cases build, all SSR cases execute a server render, and the default case also
    publishes. The component-library cases build, test, and pack.

    This script-driven harness is intentional: validation must cross the installed template-package
    and custom MSBuild-SDK resolver boundaries, which source-only template verification does not
    exercise. [V01.01.12.04]

.PARAMETER Configuration
    Build configuration used for the package and generated projects.

.PARAMETER PackageDirectory
    Package feed containing the freshly packed Viu artifacts.

.PARAMETER ScratchRoot
    Repository-output directory in which the isolated run is created.

.PARAMETER SkipPack
    Reuses the exact current-version packages already present in PackageDirectory.

.PARAMETER Aot
    Publishes the default application with WebAssembly ahead-of-time compilation. The wasm-tools
    workload must be installed. Without this switch the same application receives a normal trimmed
    Release publish.

.PARAMETER RetainScratch
    Retains a successful isolated run for inspection. Failed runs are always retained.
#>
[CmdletBinding()]
param(
    [ValidateSet('Debug', 'Release')]
    [string] $Configuration = 'Release',

    [string] $PackageDirectory = '',

    [string] $ScratchRoot = '',

    [switch] $SkipPack,

    [switch] $Aot,

    [switch] $RetainScratch
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$repositoryDirectory = [System.IO.Path]::GetFullPath(
    (Split-Path $PSScriptRoot -Parent))
$repositoryOutputDirectory = [System.IO.Path]::GetFullPath(
    (Join-Path $repositoryDirectory '_out'))
$pathComparison = if ([System.OperatingSystem]::IsWindows()) {
    [System.StringComparison]::OrdinalIgnoreCase
}
else {
    [System.StringComparison]::Ordinal
}

if ([string]::IsNullOrWhiteSpace($PackageDirectory)) {
    $PackageDirectory = Join-Path $repositoryOutputDirectory 'packages'
}
if ([string]::IsNullOrWhiteSpace($ScratchRoot)) {
    $ScratchRoot = Join-Path $repositoryOutputDirectory 'scratch/templates'
}

$packageDirectoryPath = [System.IO.Path]::GetFullPath($PackageDirectory)
$scratchRootPath = [System.IO.Path]::GetFullPath($ScratchRoot)

function Assert-RepositoryOutputChild {
    param(
        [Parameter(Mandatory)]
        [string] $Path,

        [Parameter(Mandatory)]
        [string] $ParameterName
    )

    $outputPrefix = $repositoryOutputDirectory.TrimEnd(
        [System.IO.Path]::DirectorySeparatorChar,
        [System.IO.Path]::AltDirectorySeparatorChar) +
        [System.IO.Path]::DirectorySeparatorChar
    if (-not $Path.StartsWith($outputPrefix, $pathComparison)) {
        throw "$ParameterName must be a child of the repository _out directory: $Path"
    }
}

function Assert-NoReparsePoint {
    param(
        [Parameter(Mandatory)]
        [string] $Path
    )

    if (-not [System.IO.File]::Exists($Path) -and
        -not [System.IO.Directory]::Exists($Path)) {
        return
    }

    $item = Get-Item -LiteralPath $Path -Force
    if (($item.Attributes -band [System.IO.FileAttributes]::ReparsePoint) -ne 0) {
        throw "Template validation refuses to traverse the reparse point: $Path"
    }
}

function Invoke-DotNet {
    param(
        [Parameter(Mandatory)]
        [string] $Description,

        [Parameter(Mandatory)]
        [string[]] $Arguments,

        [Parameter(Mandatory)]
        [string] $WorkingDirectory
    )

    Write-Host $Description -ForegroundColor Cyan
    Push-Location -LiteralPath $WorkingDirectory
    try {
        & dotnet @Arguments
        $exitCode = $LASTEXITCODE
    }
    finally {
        Pop-Location
    }

    if ($exitCode -ne 0) {
        throw "$Description failed with exit code $exitCode."
    }
}

function Get-ViuVersion {
    $targetFrameworkProperties = [xml](Get-Content -Raw -LiteralPath (
        Join-Path $repositoryDirectory 'build/Targets/Build.TargetFramework.props'))
    $targetFramework = [string]$targetFrameworkProperties.SelectSingleNode(
        '/Project/PropertyGroup/TargetFrameworkLatest').InnerText
    $majorVersion = ([System.Version]::Parse($targetFramework.TrimStart('net'))).Major

    $versionProperties = [xml](Get-Content -Raw -LiteralPath (
        Join-Path $repositoryDirectory 'build/Targets/Build.Version.props'))
    $minorVersion = [string]$versionProperties.SelectSingleNode(
        '/Project/PropertyGroup/ViuMinorVersion').InnerText
    $patchVersion = [string]$versionProperties.SelectSingleNode(
        '/Project/PropertyGroup/ViuPatchVersion').InnerText
    return "$majorVersion.$minorVersion.$patchVersion"
}

function Assert-ExactFileSet {
    param(
        [Parameter(Mandatory)]
        [string] $Root,

        [Parameter(Mandatory)]
        [string[]] $Expected
    )

    $actual = @(
        Get-ChildItem -LiteralPath $Root -Recurse -Force -File |
            ForEach-Object {
                [System.IO.Path]::GetRelativePath($Root, $_.FullName).Replace('\', '/')
            } |
            Sort-Object
    )
    $difference = @(Compare-Object ($Expected | Sort-Object) $actual)
    if ($difference.Count -ne 0) {
        throw "Generated file set under $Root differs from the template contract: $($difference | Out-String)"
    }
}

function Assert-NoSourceTokens {
    param(
        [Parameter(Mandatory)]
        [string] $Root
    )

    $matches = @(
        Get-ChildItem -LiteralPath $Root -Recurse -Force -File |
            Select-String -Pattern '__VIU_VERSION__|__VIU_PROJECT_FILE_NAME__|ViuApplication|ViuComponentLibrary|#if|#else|#endif'
    )
    if ($matches.Count -ne 0) {
        throw "Generated output retains template source tokens: $($matches | Out-String)"
    }

    $repositoryReferenceMatches = @(
        Get-ChildItem -LiteralPath $Root -Recurse -Force -File |
            Select-String -Pattern 'ViuProjectReference|ViuPrivateProjectReference|[A-Za-z]:\\Source\\repos\\assimalign\\viu|/Source/repos/assimalign/viu'
    )
    if ($repositoryReferenceMatches.Count -ne 0) {
        throw "Generated output references the Viu repository: $($repositoryReferenceMatches | Out-String)"
    }
}

function Assert-NullableSetting {
    param(
        [Parameter(Mandatory)]
        [string[]] $ProjectPaths,

        [Parameter(Mandatory)]
        [ValidateSet('enable', 'disable')]
        [string] $Expected
    )

    foreach ($projectPath in $ProjectPaths) {
        $projectText = Get-Content -Raw -LiteralPath $projectPath
        if (-not $projectText.Contains("<Nullable>$Expected</Nullable>", [System.StringComparison]::Ordinal)) {
            throw "$projectPath does not contain the expected nullable setting '$Expected'."
        }
    }
}

function Assert-GlobalJsonVersions {
    param(
        [Parameter(Mandatory)]
        [string] $Path,

        [Parameter(Mandatory)]
        [string[]] $SdkIds,

        [Parameter(Mandatory)]
        [string] $Version
    )

    $globalJson = Get-Content -Raw -LiteralPath $Path | ConvertFrom-Json
    foreach ($sdkId in $SdkIds) {
        $actualVersion = $globalJson.'msbuild-sdks'.$sdkId
        if (-not [string]::Equals($actualVersion, $Version, [System.StringComparison]::Ordinal)) {
            throw "$Path pins $sdkId to '$actualVersion' instead of '$Version'."
        }
    }
}

Assert-RepositoryOutputChild -Path $scratchRootPath -ParameterName 'ScratchRoot'
Assert-NoReparsePoint -Path $repositoryOutputDirectory
Assert-NoReparsePoint -Path $scratchRootPath
$null = New-Item -ItemType Directory -Path $scratchRootPath -Force
Assert-NoReparsePoint -Path $scratchRootPath

$viuVersion = Get-ViuVersion
if (-not $SkipPack) {
    Write-Host "Packing the Viu $viuVersion package graph." -ForegroundColor Cyan
    & pwsh -NoProfile -File (Join-Path $PSScriptRoot 'Install-Local.ps1') `
        -Configuration $Configuration `
        -SkipCachePrune `
        -RestoreConfigurationFile (Join-Path $repositoryDirectory 'nuget.config')
    if ($LASTEXITCODE -ne 0) {
        throw "Install-Local.ps1 failed with exit code $LASTEXITCODE."
    }
}

$requiredPackageIds = @(
    'Assimalign.Viu.Templates',
    'Assimalign.Viu.Sdk',
    'Assimalign.Viu.Sdk.Browser',
    'Assimalign.Viu.App.Ref',
    'Assimalign.Viu.App.Browser.Ref',
    'Assimalign.Viu.App.Browser.Runtime.browser-wasm'
)
foreach ($packageId in $requiredPackageIds) {
    $packagePath = Join-Path $packageDirectoryPath "$packageId.$viuVersion.nupkg"
    if (-not [System.IO.File]::Exists($packagePath)) {
        throw "The current template-validation feed is missing $packagePath."
    }
}

$globalPackagesOutput = @(& dotnet nuget locals global-packages --list 2>$null)
if ($LASTEXITCODE -eq 0 -and $globalPackagesOutput.Count -eq 1) {
    $separatorIndex = $globalPackagesOutput[0].IndexOf(':')
    if ($separatorIndex -lt 0) {
        throw "Unexpected global-packages output: $($globalPackagesOutput[0])"
    }
    $restoredPackageSource = $globalPackagesOutput[0].Substring($separatorIndex + 1).Trim()
}
else {
    $restoredPackageSource = [System.Environment]::GetEnvironmentVariable(
        'NUGET_PACKAGES',
        [System.EnvironmentVariableTarget]::Process)
    if ([string]::IsNullOrWhiteSpace($restoredPackageSource)) {
        $userProfileDirectory = [System.Environment]::GetFolderPath(
            [System.Environment+SpecialFolder]::UserProfile)
        if ([string]::IsNullOrWhiteSpace($userProfileDirectory)) {
            throw 'Could not resolve the existing NuGet package cache.'
        }
        $restoredPackageSource = Join-Path $userProfileDirectory '.nuget/packages'
    }
}
$restoredPackageSource = [System.IO.Path]::GetFullPath($restoredPackageSource)
if (-not [System.IO.Directory]::Exists($restoredPackageSource)) {
    throw "The existing NuGet package cache does not exist: $restoredPackageSource"
}

$runDirectory = [System.IO.Path]::GetFullPath(
    (Join-Path $scratchRootPath "run-$([System.Guid]::NewGuid().ToString('N'))"))
$scratchPrefix = $scratchRootPath.TrimEnd(
    [System.IO.Path]::DirectorySeparatorChar,
    [System.IO.Path]::AltDirectorySeparatorChar) +
    [System.IO.Path]::DirectorySeparatorChar
if (-not $runDirectory.StartsWith($scratchPrefix, $pathComparison)) {
    throw "The template-validation run escaped the validated scratch root: $runDirectory"
}
Assert-NoReparsePoint -Path $runDirectory
$null = New-Item -ItemType Directory -Path $runDirectory
Assert-NoReparsePoint -Path $runDirectory

$isolatedCliHome = Join-Path $runDirectory '.dotnet'
$isolatedApplicationData = Join-Path $runDirectory '.appdata'
$isolatedPackages = Join-Path $runDirectory '.nuget/packages'
$isolatedHttpCache = Join-Path $runDirectory '.nuget/http-cache'
$null = New-Item `
    -ItemType Directory `
    -Path $isolatedCliHome, $isolatedApplicationData, $isolatedPackages, $isolatedHttpCache

$escapedPackageDirectory = [System.Security.SecurityElement]::Escape($packageDirectoryPath)
$escapedRestoredPackageSource = [System.Security.SecurityElement]::Escape($restoredPackageSource)
$escapedIsolatedPackages = [System.Security.SecurityElement]::Escape($isolatedPackages)
$nugetConfiguration = @"
<?xml version="1.0" encoding="utf-8"?>
<configuration>
  <packageSources>
    <clear />
    <add key="viu-local" value="$escapedPackageDirectory" />
    <add key="restored-cache" value="$escapedRestoredPackageSource" />
    <add key="dotnet10" value="https://pkgs.dev.azure.com/dnceng/public/_packaging/dotnet10/nuget/v3/index.json" />
    <add key="dotnet-public" value="https://pkgs.dev.azure.com/dnceng/public/_packaging/dotnet-public/nuget/v3/index.json" />
    <add key="nuget.org" value="https://api.nuget.org/v3/index.json" />
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
      <package pattern="xunit*" />
      <package pattern="Shouldly" />
      <package pattern="DiffEngine" />
      <package pattern="EmptyFiles" />
      <package pattern="Newtonsoft.Json" />
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
      <package pattern="xunit*" />
      <package pattern="Shouldly" />
      <package pattern="DiffEngine" />
      <package pattern="EmptyFiles" />
      <package pattern="Newtonsoft.Json" />
    </packageSource>
    <packageSource key="nuget.org">
      <package pattern="Microsoft.*" />
      <package pattern="System.*" />
      <package pattern="runtime.*" />
      <package pattern="NETStandard.Library" />
      <package pattern="xunit*" />
      <package pattern="Shouldly" />
      <package pattern="DiffEngine" />
      <package pattern="EmptyFiles" />
      <package pattern="Newtonsoft.Json" />
    </packageSource>
  </packageSourceMapping>
  <config>
    <add key="globalPackagesFolder" value="$escapedIsolatedPackages" />
  </config>
</configuration>
"@
$nugetConfigurationPath = Join-Path $runDirectory 'nuget.config'
Set-Content -LiteralPath $nugetConfigurationPath -Value $nugetConfiguration -Encoding utf8

$directoryBuildProperties = @"
<Project>
  <PropertyGroup>
    <NuGetAudit>false</NuGetAudit>
  </PropertyGroup>
</Project>
"@
Set-Content `
    -LiteralPath (Join-Path $runDirectory 'Directory.Build.props') `
    -Value $directoryBuildProperties `
    -Encoding utf8
Set-Content `
    -LiteralPath (Join-Path $runDirectory 'Directory.Build.targets') `
    -Value '<Project />' `
    -Encoding utf8

$environmentVariableNames = @(
    'APPDATA',
    'DOTNET_ADD_GLOBAL_TOOLS_TO_PATH',
    'DOTNET_CLI_HOME',
    'DOTNET_NOLOGO',
    'DOTNET_SKIP_FIRST_TIME_EXPERIENCE',
    'NUGET_PACKAGES',
    'NUGET_HTTP_CACHE_PATH'
)
$previousEnvironment = @{}
foreach ($variableName in $environmentVariableNames) {
    $previousEnvironment[$variableName] = [System.Environment]::GetEnvironmentVariable(
        $variableName,
        [System.EnvironmentVariableTarget]::Process)
}

$succeeded = $false
try {
    [System.Environment]::SetEnvironmentVariable(
        'APPDATA', $isolatedApplicationData, [System.EnvironmentVariableTarget]::Process)
    [System.Environment]::SetEnvironmentVariable(
        'DOTNET_ADD_GLOBAL_TOOLS_TO_PATH', '0', [System.EnvironmentVariableTarget]::Process)
    [System.Environment]::SetEnvironmentVariable(
        'DOTNET_CLI_HOME', $isolatedCliHome, [System.EnvironmentVariableTarget]::Process)
    [System.Environment]::SetEnvironmentVariable(
        'DOTNET_NOLOGO', '1', [System.EnvironmentVariableTarget]::Process)
    [System.Environment]::SetEnvironmentVariable(
        'DOTNET_SKIP_FIRST_TIME_EXPERIENCE', '1', [System.EnvironmentVariableTarget]::Process)
    [System.Environment]::SetEnvironmentVariable(
        'NUGET_PACKAGES', $isolatedPackages, [System.EnvironmentVariableTarget]::Process)
    [System.Environment]::SetEnvironmentVariable(
        'NUGET_HTTP_CACHE_PATH', $isolatedHttpCache, [System.EnvironmentVariableTarget]::Process)

    Invoke-DotNet `
        -Description 'Installing the packaged Viu templates' `
        -WorkingDirectory $runDirectory `
        -Arguments @(
            'new',
            'install',
            "Assimalign.Viu.Templates@$viuVersion",
            '--nuget-source',
            $packageDirectoryPath,
            '--force')

    Push-Location -LiteralPath $runDirectory
    try {
        $templateListOutput = @(& dotnet new list viu 2>&1)
        $templateListExitCode = $LASTEXITCODE
    }
    finally {
        Pop-Location
    }
    $templateListOutput | ForEach-Object { Write-Host $_ }
    if ($templateListExitCode -ne 0) {
        throw "Listing installed Viu templates failed with exit code $templateListExitCode."
    }
    $templateListText = $templateListOutput -join [System.Environment]::NewLine
    foreach ($shortName in @('viu-app', 'viu-lib')) {
        if (-not $templateListText.Contains($shortName, [System.StringComparison]::Ordinal)) {
            throw "The installed template list does not contain '$shortName'."
        }
    }

    $cases = @(
        [pscustomobject]@{
            Template = 'viu-app'
            Name = 'TemplateAppDefault'
            Nullable = 'enable'
            PassNullable = $false
            Ssr = $false
        },
        [pscustomobject]@{
            Template = 'viu-app'
            Name = 'TemplateAppNullableOff'
            Nullable = 'disable'
            PassNullable = $true
            Ssr = $false
        },
        [pscustomobject]@{
            Template = 'viu-app'
            Name = 'TemplateAppSsrDefault'
            Nullable = 'enable'
            PassNullable = $false
            Ssr = $true
        },
        [pscustomobject]@{
            Template = 'viu-app'
            Name = 'TemplateAppSsrNullableOff'
            Nullable = 'disable'
            PassNullable = $true
            Ssr = $true
        },
        [pscustomobject]@{
            Template = 'viu-app'
            Name = 'hello-viu'
            Nullable = 'enable'
            PassNullable = $false
            Ssr = $true
        },
        [pscustomobject]@{
            Template = 'viu-lib'
            Name = 'TemplateLibraryDefault'
            Nullable = 'enable'
            PassNullable = $false
            Ssr = $false
        },
        [pscustomobject]@{
            Template = 'viu-lib'
            Name = 'TemplateLibraryNullableOff'
            Nullable = 'disable'
            PassNullable = $true
            Ssr = $false
        },
        [pscustomobject]@{
            Template = 'viu-lib'
            Name = 'contoso-components'
            Nullable = 'enable'
            PassNullable = $false
            Ssr = $false
        }
    )
    $consumerMsBuildProperties = @(
        '-p:NuGetAudit=false',
        # Some Visual Studio SDK installations omit the optional net10.0 prune data.
        # That host-install detail is not part of the template package contract.
        '-p:AllowMissingPrunePackageData=true'
    )

    foreach ($case in $cases) {
        $caseDirectory = Join-Path $runDirectory $case.Name
        $creationArguments = @(
            'new',
            $case.Template,
            '--name',
            $case.Name,
            '--output',
            $caseDirectory)
        if ($case.PassNullable) {
            $creationArguments += @('--nullable', 'false')
        }
        if ($case.Ssr) {
            $creationArguments += @('--ssr', 'true')
        }

        Invoke-DotNet `
            -Description "Instantiating $($case.Template) as $($case.Name)" `
            -WorkingDirectory $runDirectory `
            -Arguments $creationArguments

        if ($case.Template -eq 'viu-app') {
            $projectPath = Join-Path $caseDirectory "$($case.Name).csproj"
            $projectPaths = @($projectPath)
            if ($case.Ssr) {
                $componentProjectPath = Join-Path `
                    $caseDirectory `
                    "Components/$($case.Name).Components.csproj"
                $serverProjectPath = Join-Path `
                    $caseDirectory `
                    "Server/$($case.Name).Server.csproj"
                $projectPaths += @($componentProjectPath, $serverProjectPath)
                $expectedFiles = @(
                    "Components/$($case.Name).Components.csproj",
                    'Components/Counter.viu',
                    'global.json',
                    'Program.cs',
                    "Server/$($case.Name).Server.csproj",
                    'Server/Program.cs',
                    "$($case.Name).csproj",
                    'wwwroot/index.html',
                    'wwwroot/main.js')
            }
            else {
                $expectedFiles = @(
                    'Counter.viu',
                    'global.json',
                    'Program.cs',
                    "$($case.Name).csproj",
                    'wwwroot/index.html',
                    'wwwroot/main.js')
            }
            Assert-ExactFileSet -Root $caseDirectory -Expected $expectedFiles
            Assert-NoSourceTokens -Root $caseDirectory
            Assert-NullableSetting -ProjectPaths $projectPaths -Expected $case.Nullable
            Assert-GlobalJsonVersions `
                -Path (Join-Path $caseDirectory 'global.json') `
                -SdkIds @('Assimalign.Viu.Sdk', 'Assimalign.Viu.Sdk.Browser') `
                -Version $viuVersion

            $restoreArguments = @(
                'restore',
                $projectPath,
                '--configfile',
                $nugetConfigurationPath,
                '--ignore-failed-sources') + $consumerMsBuildProperties
            if ($Aot) {
                $restoreArguments += @(
                    '-p:RunAOTCompilation=true',
                    '-p:ViuRunAotCompilation=true')
            }
            Invoke-DotNet `
                -Description "Restoring $($case.Name)" `
                -WorkingDirectory $caseDirectory `
                -Arguments $restoreArguments
            Invoke-DotNet `
                -Description "Building $($case.Name)" `
                -WorkingDirectory $caseDirectory `
                -Arguments (@(
                    'build',
                    $projectPath,
                    '--configuration',
                    $Configuration,
                    '--no-restore',
                    '-warnaserror') + $consumerMsBuildProperties)

            if ($case.Ssr) {
                Invoke-DotNet `
                    -Description "Restoring the $($case.Name) server host" `
                    -WorkingDirectory $caseDirectory `
                    -Arguments (@(
                        'restore',
                        $serverProjectPath,
                        '--configfile',
                        $nugetConfigurationPath,
                        '--ignore-failed-sources') + $consumerMsBuildProperties)
                Invoke-DotNet `
                    -Description "Building the $($case.Name) server host" `
                    -WorkingDirectory $caseDirectory `
                    -Arguments (@(
                        'build',
                        $serverProjectPath,
                        '--configuration',
                        $Configuration,
                        '--no-restore',
                        '-warnaserror') + $consumerMsBuildProperties)

                $serverRunArguments = @(
                    'run',
                    '--project',
                    $serverProjectPath,
                    '--configuration',
                    $Configuration,
                    '--no-build') + $consumerMsBuildProperties + @('--', '--render-once')
                Write-Host "Rendering one package-only server response for $($case.Name)" `
                    -ForegroundColor Cyan
                Push-Location -LiteralPath $caseDirectory
                try {
                    $serverOutput = @(& dotnet @serverRunArguments 2>&1)
                    $serverExitCode = $LASTEXITCODE
                }
                finally {
                    Pop-Location
                }
                $serverOutput | ForEach-Object { Write-Host $_ }
                if ($serverExitCode -ne 0) {
                    throw "$($case.Name) server-render smoke failed with exit code $serverExitCode."
                }
                $serverOutputText = $serverOutput -join [System.Environment]::NewLine
                foreach ($expectedMarkup in @('<main', 'Reactive counter', '0 clicks')) {
                    if (-not $serverOutputText.Contains(
                            $expectedMarkup,
                            [System.StringComparison]::Ordinal)) {
                        throw "$($case.Name) server output does not contain '$expectedMarkup'."
                    }
                }
            }

            if ($case.Name -eq 'TemplateAppDefault') {
                $publishDirectory = Join-Path $caseDirectory 'publish'
                $publishArguments = @(
                    'publish',
                    $projectPath,
                    '--configuration',
                    $Configuration,
                    '--no-restore',
                    '--output',
                    $publishDirectory)
                if ($Aot) {
                    $publishArguments += @(
                        '-p:RunAOTCompilation=true',
                        '-p:ViuRunAotCompilation=true')
                }
                $publishArguments += $consumerMsBuildProperties
                Invoke-DotNet `
                    -Description "Publishing $($case.Name)$(if ($Aot) { ' with AOT' } else { '' })" `
                    -WorkingDirectory $caseDirectory `
                    -Arguments $publishArguments
                if (-not [System.IO.File]::Exists((Join-Path $publishDirectory 'wwwroot/index.html'))) {
                    throw "$($case.Name) did not produce a published wwwroot/index.html."
                }
            }
        }
        else {
            $solutionPath = Join-Path $caseDirectory "$($case.Name).slnx"
            $projectPath = Join-Path $caseDirectory "$($case.Name).csproj"
            $testProjectPath = Join-Path `
                $caseDirectory `
                "test/$($case.Name).Tests/$($case.Name).Tests.csproj"
            Assert-ExactFileSet `
                -Root $caseDirectory `
                -Expected @(
                    'global.json',
                    'Greeting.viu',
                    "$($case.Name).csproj",
                    "$($case.Name).slnx",
                    "test/$($case.Name).Tests/GreetingTests.cs",
                    "test/$($case.Name).Tests/$($case.Name).Tests.csproj")
            Assert-NoSourceTokens -Root $caseDirectory
            Assert-NullableSetting `
                -ProjectPaths @($projectPath, $testProjectPath) `
                -Expected $case.Nullable
            Assert-GlobalJsonVersions `
                -Path (Join-Path $caseDirectory 'global.json') `
                -SdkIds @('Assimalign.Viu.Sdk') `
                -Version $viuVersion

            Invoke-DotNet `
                -Description "Restoring $($case.Name)" `
                -WorkingDirectory $caseDirectory `
                -Arguments (@(
                    'restore',
                    $solutionPath,
                    '--configfile',
                    $nugetConfigurationPath,
                    '--ignore-failed-sources') + $consumerMsBuildProperties)
            Invoke-DotNet `
                -Description "Building $($case.Name)" `
                -WorkingDirectory $caseDirectory `
                -Arguments (@(
                    'build',
                    $solutionPath,
                    '--configuration',
                    $Configuration,
                    '--no-restore',
                    '-warnaserror') + $consumerMsBuildProperties)
            Invoke-DotNet `
                -Description "Testing $($case.Name)" `
                -WorkingDirectory $caseDirectory `
                -Arguments @(
                    'test',
                    $testProjectPath,
                    '--configuration',
                    $Configuration,
                    '--no-build')

            $libraryPackageDirectory = Join-Path $caseDirectory 'packages'
            Invoke-DotNet `
                -Description "Packing $($case.Name)" `
                -WorkingDirectory $caseDirectory `
                -Arguments @(
                    'pack',
                    $projectPath,
                    '--configuration',
                    $Configuration,
                    '--no-build',
                    "-p:PackageOutputPath=$libraryPackageDirectory")
            $libraryPackages = @(Get-ChildItem -LiteralPath $libraryPackageDirectory -Filter '*.nupkg' -File)
            if ($libraryPackages.Count -ne 1) {
                throw "$($case.Name) produced $($libraryPackages.Count) library packages instead of one."
            }
        }

    }

    $succeeded = $true
    Write-Host 'Viu template package and eight-row instantiation matrix passed.' -ForegroundColor Green
}
finally {
    foreach ($variableName in $environmentVariableNames) {
        [System.Environment]::SetEnvironmentVariable(
            $variableName,
            $previousEnvironment[$variableName],
            [System.EnvironmentVariableTarget]::Process)
    }

    if ($succeeded -and -not $RetainScratch) {
        if (-not $runDirectory.StartsWith($scratchPrefix, $pathComparison)) {
            throw "Refusing to remove a run outside the validated scratch root: $runDirectory"
        }
        $runItem = Get-Item -LiteralPath $runDirectory -Force
        if (-not $runItem.PSIsContainer) {
            throw "The template-validation cleanup target is not a directory: $runDirectory"
        }
        $reparsePoints = @(
            Get-ChildItem -LiteralPath $runDirectory -Recurse -Force |
                Where-Object {
                    ($_.Attributes -band [System.IO.FileAttributes]::ReparsePoint) -ne 0
                }
        )
        if ($reparsePoints.Count -ne 0) {
            throw "Refusing to remove a template-validation tree containing reparse points: $($reparsePoints.FullName -join ', ')"
        }
        Remove-Item -LiteralPath $runDirectory -Recurse -Force -ErrorAction Stop
    }
    else {
        Write-Host "Retained template-validation scratch: $runDirectory" -ForegroundColor Yellow
    }
}
