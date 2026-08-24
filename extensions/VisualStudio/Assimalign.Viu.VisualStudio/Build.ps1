<#
.SYNOPSIS
    Builds Assimalign.Viu.VisualStudio, the Viu Visual Studio extension, and its language-server
    payload.

.DESCRIPTION
    The extension is a classic in-process VSSDK package, so packaging it needs Visual Studio's
    MSBuild: Microsoft.VSSDK.BuildTools ships .NET Framework MSBuild tasks that cannot load under
    `dotnet build`. This script locates that MSBuild through vswhere. It is the independently runnable
    build entry point for this VSIX; extensions\VisualStudio\Build.ps1 orchestrates it together with
    the other Visual Studio extension packages.

    The language-server payload is produced first, by driving the shared ViuPublishLanguageServer
    target through the .NET CLI. That target shells out to `dotnet publish`, so running it under the
    .NET CLI rather than nested inside Visual Studio's MSBuild keeps the SDK resolution
    straightforward. The packaging pass then sets ViuSkipLanguageServerPublish so the in-build hook
    does not publish a second time; the absence, staleness, and runtime-identifier backstops in
    build\Targets\Build.LanguageServer.targets all still run.

    The publish recipe itself is never restated here. It lives in that shared target so this script
    and an in-IDE F5 cannot drift apart, and the language-server project is resolved from
    ViuLanguageServerProjectPath rather than named a second time.

    -DeployExperimental installs the validated package into the selected released Visual Studio's
    named experimental root suffix and runs /UpdateConfiguration before returning. That explicit
    pass is required for newly deployed image manifests to invalidate the ImageLibrary cache.
#>
[CmdletBinding()]
param(
    [ValidateSet('Debug', 'Release')]
    [string] $Configuration = 'Debug',

    [string] $Version,

    [switch] $DeployExperimental,

    [ValidatePattern('^[A-Za-z][A-Za-z0-9]*$')]
    [string] $ExperimentalRootSuffix = 'Exp'
)

$ErrorActionPreference = 'Stop'

$versionBuildArguments = @()
if (-not [string]::IsNullOrWhiteSpace($Version)) {
    if ($Version -notmatch '^(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)(?:\.(0|[1-9][0-9]*))?$') {
        throw "The Visual Studio extension version must contain three or four numeric components, for example 10.0.1 or 10.0.1.42."
    }

    foreach ($versionSegment in $Version.Split('.')) {
        $versionSegmentValue = [int]::Parse(
            $versionSegment,
            [System.Globalization.CultureInfo]::InvariantCulture)
        if ($versionSegmentValue -gt 65534) {
            throw "Visual Studio extension version components cannot exceed 65534."
        }
    }

    # The manifest reads its Identity version through the VSSDK GetVsixVersion token, which the
    # project answers with VersionPrefix, so the ordinary .NET version properties drive the VSIX
    # version too.
    $versionBuildArguments = @(
        "-p:VersionPrefix=$Version",
        '-p:VersionSuffix='
    )
    Write-Host "Visual Studio extension version: $Version"
}

$extensionDirectory = $PSScriptRoot
$repositoryDirectory = [System.IO.Path]::GetFullPath(
    (Join-Path $extensionDirectory '..\..\..'))
$extensionProject = Join-Path $extensionDirectory `
    'src\Assimalign.Viu.VisualStudio.csproj'
$extensionOutputDirectory = Join-Path $repositoryDirectory `
    "_out\extensions\VisualStudio\$Configuration"
$languageServerPublishDirectory = Join-Path $extensionOutputDirectory `
    'LanguageServer'
$normalizedExtensionOutputDirectory =
    [System.IO.Path]::GetFullPath($extensionOutputDirectory)
$normalizedLanguageServerPublishDirectory =
    [System.IO.Path]::GetFullPath($languageServerPublishDirectory)
$normalizedExtensionOutputPrefix =
    $normalizedExtensionOutputDirectory.TrimEnd(
        [char[]]@(
            [System.IO.Path]::DirectorySeparatorChar,
            [System.IO.Path]::AltDirectorySeparatorChar)) +
    [System.IO.Path]::DirectorySeparatorChar
if (-not $normalizedLanguageServerPublishDirectory.StartsWith(
        $normalizedExtensionOutputPrefix,
        [System.StringComparison]::OrdinalIgnoreCase)) {
    throw "The language-server publish directory must remain inside $normalizedExtensionOutputDirectory."
}

# #region: Locate Visual Studio's MSBuild
# Done before the language-server publish so a machine without the tooling fails in seconds rather
# than after a multi-minute self-contained publish.
$visualStudioInstaller = Join-Path ${env:ProgramFiles(x86)} 'Microsoft Visual Studio\Installer\vswhere.exe'
if (-not (Test-Path -LiteralPath $visualStudioInstaller)) {
    throw "vswhere.exe was not found at $visualStudioInstaller. Install Visual Studio with the 'Visual Studio extension development' workload."
}

function Find-VisualStudioInstallation {
    param([switch] $IncludePrerelease)

    $arguments = @(
        '-latest',
        '-products', '*',
        '-requires', 'Microsoft.Component.MSBuild',
        '-property', 'installationPath')
    if ($IncludePrerelease) {
        $arguments += '-prerelease'
    }

    $installationPath = & $visualStudioInstaller @arguments
    if ($LASTEXITCODE -ne 0) {
        throw "vswhere.exe failed with exit code $LASTEXITCODE."
    }

    return ($installationPath | Select-Object -First 1)
}

# A released instance is preferred; a preview-only machine still builds.
$visualStudioInstallation = Find-VisualStudioInstallation
if ([string]::IsNullOrWhiteSpace($visualStudioInstallation)) {
    $visualStudioInstallation = Find-VisualStudioInstallation -IncludePrerelease
}
if ([string]::IsNullOrWhiteSpace($visualStudioInstallation)) {
    throw "No Visual Studio installation with MSBuild was found. Install the 'Visual Studio extension development' workload."
}

# Prefer the MSBuild matching this machine's architecture; the 32-bit host is the last resort.
$msbuildCandidates = switch ($env:PROCESSOR_ARCHITECTURE) {
    'ARM64' { @('MSBuild\Current\Bin\arm64\MSBuild.exe', 'MSBuild\Current\Bin\amd64\MSBuild.exe', 'MSBuild\Current\Bin\MSBuild.exe') }
    'AMD64' { @('MSBuild\Current\Bin\amd64\MSBuild.exe', 'MSBuild\Current\Bin\MSBuild.exe') }
    default { @('MSBuild\Current\Bin\MSBuild.exe') }
}

$msbuild = $null
foreach ($msbuildCandidate in $msbuildCandidates) {
    $msbuildCandidatePath = Join-Path $visualStudioInstallation $msbuildCandidate
    if (Test-Path -LiteralPath $msbuildCandidatePath) {
        $msbuild = $msbuildCandidatePath
        break
    }
}
if ($null -eq $msbuild) {
    throw "MSBuild.exe was not found under $visualStudioInstallation."
}

Write-Host "Visual Studio installation: $visualStudioInstallation"
Write-Host "MSBuild: $msbuild"
# #endregion

if (Test-Path -LiteralPath $normalizedLanguageServerPublishDirectory) {
    Remove-Item -LiteralPath $normalizedLanguageServerPublishDirectory -Recurse -Force
}

Write-Host 'Publishing the Viu language server through the shared MSBuild target'
& dotnet msbuild $extensionProject `
    -target:ViuPublishLanguageServer `
    "-p:Configuration=$Configuration" `
    "-p:ViuLanguageServerPublishPath=$normalizedLanguageServerPublishDirectory" `
    -nologo `
    @versionBuildArguments
if ($LASTEXITCODE -ne 0) {
    throw "Publishing the Viu language server failed with exit code $LASTEXITCODE."
}

# Rebuild rather than Build: the VSIX is assembled from whatever sits in the output directory, so a
# stale artifact from an earlier configuration must not survive into the package.
Write-Host 'Building and packaging the Visual Studio extension'
& $msbuild $extensionProject `
    -restore `
    -target:Rebuild `
    "-p:Configuration=$Configuration" `
    "-p:ViuLanguageServerPublishPath=$normalizedLanguageServerPublishDirectory" `
    -p:ViuSkipLanguageServerPublish=true `
    -nologo `
    -verbosity:minimal `
    @versionBuildArguments
if ($LASTEXITCODE -ne 0) {
    throw "Building the Visual Studio extension failed with exit code $LASTEXITCODE."
}

# The VSSDK names the container itself, so ask it rather than globbing bin\: a target-framework
# change leaves the old framework's .vsix sitting beside the new one, and a glob would have to guess
# between them.
$extensionProjectDirectory = Split-Path -Parent $extensionProject
$targetVsixContainer = & $msbuild $extensionProject `
    -nologo `
    -getProperty:TargetVsixContainer `
    "-p:Configuration=$Configuration"
if ($LASTEXITCODE -ne 0) {
    throw "Reading TargetVsixContainer from the Visual Studio extension project failed with exit code $LASTEXITCODE."
}

$extensionPackagePath = [System.IO.Path]::GetFullPath(
    (Join-Path $extensionProjectDirectory ($targetVsixContainer | Select-Object -First 1).Trim()))
if (-not (Test-Path -LiteralPath $extensionPackagePath)) {
    throw "The Visual Studio extension build did not produce $extensionPackagePath."
}

if (-not (Test-Path -LiteralPath $normalizedExtensionOutputDirectory)) {
    New-Item -ItemType Directory -Path $normalizedExtensionOutputDirectory | Out-Null
}

$packagedExtension = Join-Path $normalizedExtensionOutputDirectory `
    'Assimalign.Viu.VisualStudio.vsix'
Copy-Item -LiteralPath $extensionPackagePath -Destination $packagedExtension -Force

# #region: Verify the package
# The MSBuild backstops assert the payload exists on disk; this asserts it reached the container.
# The two are not the same check - an inclusion rule that silently stops matching would leave the
# publish directory perfectly healthy and the VSIX empty.
$requiredEntries = @(
    'extension.vsixmanifest',
    'Assimalign.Viu.VisualStudio.dll',
    'Assimalign.Viu.VisualStudio.pkgdef',
    'ViuFileIcon.imagemanifest',
    'Branding/on-light/viu-mono-16.png',
    'Branding/on-light/viu-mono-32.png',
    'Branding/on-dark/viu-mono-16.png',
    'Branding/on-dark/viu-mono-32.png',
    'Branding/nuget/viu-nuget-mono-light-32.png',
    'Branding/nuget/viu-nuget-mono-light-256.png',
    'language-server.json',
    'LanguageServer/win-x64/Assimalign.Viu.LanguageServer.exe',
    'LanguageServer/win-arm64/Assimalign.Viu.LanguageServer.exe'
)

Add-Type -AssemblyName System.IO.Compression.FileSystem
$extensionArchive = [System.IO.Compression.ZipFile]::OpenRead($packagedExtension)
try {
    $entryNames = @($extensionArchive.Entries | ForEach-Object FullName)
    $missingEntries = @($requiredEntries | Where-Object { $entryNames -notcontains $_ })
    if ($missingEntries.Count -gt 0) {
        throw "The packaged extension is missing required entries: $($missingEntries -join ', ')."
    }

    $manifestEntry = $extensionArchive.GetEntry('extension.vsixmanifest')
    $manifestReader = [System.IO.StreamReader]::new($manifestEntry.Open())
    try {
        [xml] $manifest = $manifestReader.ReadToEnd()
    }
    finally {
        $manifestReader.Dispose()
    }

    $namespaceManager = [System.Xml.XmlNamespaceManager]::new($manifest.NameTable)
    $namespaceManager.AddNamespace(
        'vsix',
        'http://schemas.microsoft.com/developer/vsx-schema/2011')
    $mefAsset = $manifest.SelectSingleNode(
        "/vsix:PackageManifest/vsix:Assets/vsix:Asset[@Type='Microsoft.VisualStudio.MefComponent']",
        $namespaceManager)
    if ($null -eq $mefAsset) {
        throw 'The packaged extension declares no Microsoft.VisualStudio.MefComponent asset, so its editor parts would never compose.'
    }

    $packageAsset = $manifest.SelectSingleNode(
        "/vsix:PackageManifest/vsix:Assets/vsix:Asset[@Type='Microsoft.VisualStudio.VsPackage']",
        $namespaceManager)
    if ($null -eq $packageAsset) {
        throw 'The packaged extension declares no Microsoft.VisualStudio.VsPackage asset, so the .viu file-extension claim would never be registered.'
    }

    $imageManifestAsset = $manifest.SelectSingleNode(
        "/vsix:PackageManifest/vsix:Assets/vsix:Asset[@Type='Microsoft.VisualStudio.ImageManifest']",
        $namespaceManager)
    if ($null -eq $imageManifestAsset -or
        $imageManifestAsset.Path -ne 'ViuFileIcon.imagemanifest') {
        throw 'The packaged extension does not declare ViuFileIcon.imagemanifest as its Visual Studio Image Manifest asset.'
    }

    $packageDefinitionEntry = $extensionArchive.GetEntry(
        'Assimalign.Viu.VisualStudio.pkgdef')
    $packageDefinitionReader = [System.IO.StreamReader]::new(
        $packageDefinitionEntry.Open())
    try {
        $packageDefinition = $packageDefinitionReader.ReadToEnd()
    }
    finally {
        $packageDefinitionReader.Dispose()
    }

    $expectedFileAssociationKey = '[$RootKey$\ShellFileAssociations\.viu]'
    $expectedFileAssociationValue =
        '"DefaultIconMoniker"="6aa672fb-e43f-4822-9353-30fb3069a6e0:1"'
    if (-not $packageDefinition.Contains(
            $expectedFileAssociationKey,
            [System.StringComparison]::Ordinal) -or
        -not $packageDefinition.Contains(
            $expectedFileAssociationValue,
            [System.StringComparison]::Ordinal)) {
        throw 'The packaged .viu file association does not use the expected Visual Studio GUID:ID image moniker.'
    }

    $imageManifestEntry = $extensionArchive.GetEntry('ViuFileIcon.imagemanifest')
    $imageManifestReader = [System.IO.StreamReader]::new(
        $imageManifestEntry.Open())
    try {
        [xml] $imageManifest = $imageManifestReader.ReadToEnd()
    }
    finally {
        $imageManifestReader.Dispose()
    }

    $imageNamespaceManager = [System.Xml.XmlNamespaceManager]::new(
        $imageManifest.NameTable)
    $imageNamespaceManager.AddNamespace(
        'image',
        'http://schemas.microsoft.com/VisualStudio/ImageManifestSchema/2014')
    $imageGuid = $imageManifest.SelectSingleNode(
        "/image:ImageManifest/image:Symbols/image:Guid[@Name='ViuImageAssets']",
        $imageNamespaceManager)
    $imageId = $imageManifest.SelectSingleNode(
        "/image:ImageManifest/image:Symbols/image:ID[@Name='ViuFile']",
        $imageNamespaceManager)
    $imageResources = $imageManifest.SelectSingleNode(
        "/image:ImageManifest/image:Symbols/image:String[@Name='Resources']",
        $imageNamespaceManager)
    if ($null -eq $imageGuid -or
        $imageGuid.Value -ne '{6aa672fb-e43f-4822-9353-30fb3069a6e0}' -or
        $null -eq $imageId -or
        $imageId.Value -ne '1' -or
        $null -eq $imageResources -or
        $imageResources.Value -ne '/Assimalign.Viu.VisualStudio;Component/Branding') {
        throw 'The packaged image manifest does not match the .viu GUID:ID association or its WPF Branding resource root.'
    }

    $expectedImageSources = @(
        [pscustomobject]@{ Uri = '$(Resources)/on-light/viu-mono-16.png'; Background = 'Light'; Size = '16' },
        [pscustomobject]@{ Uri = '$(Resources)/on-light/viu-mono-32.png'; Background = 'Light'; Size = '32' },
        [pscustomobject]@{ Uri = '$(Resources)/on-dark/viu-mono-16.png'; Background = 'Dark'; Size = '16' },
        [pscustomobject]@{ Uri = '$(Resources)/on-dark/viu-mono-32.png'; Background = 'Dark'; Size = '32' },
        [pscustomobject]@{ Uri = '$(Resources)/on-light/viu-mono-16.png'; Background = 'HighContrastLight'; Size = '16' },
        [pscustomobject]@{ Uri = '$(Resources)/on-light/viu-mono-32.png'; Background = 'HighContrastLight'; Size = '32' },
        [pscustomobject]@{ Uri = '$(Resources)/on-dark/viu-mono-16.png'; Background = 'HighContrastDark'; Size = '16' },
        [pscustomobject]@{ Uri = '$(Resources)/on-dark/viu-mono-32.png'; Background = 'HighContrastDark'; Size = '32' }
    )
    foreach ($expectedImageSource in $expectedImageSources) {
        $source = $imageManifest.SelectSingleNode(
            "/image:ImageManifest/image:Images/image:Image/image:Source[@Uri='$($expectedImageSource.Uri)' and @Background='$($expectedImageSource.Background)']/image:Size[@Value='$($expectedImageSource.Size)']",
            $imageNamespaceManager)
        if ($null -eq $source) {
            throw "The packaged image manifest is missing $($expectedImageSource.Background) $($expectedImageSource.Size)-pixel source $($expectedImageSource.Uri)."
        }
    }

    $extensionAssemblyEntry = $extensionArchive.GetEntry(
        'Assimalign.Viu.VisualStudio.dll')
    $extensionAssemblyBytes = [System.IO.MemoryStream]::new()
    try {
        $extensionAssemblyEntryStream = $extensionAssemblyEntry.Open()
        try {
            $extensionAssemblyEntryStream.CopyTo($extensionAssemblyBytes)
        }
        finally {
            $extensionAssemblyEntryStream.Dispose()
        }
        $extensionAssembly = [System.Reflection.Assembly]::Load(
            $extensionAssemblyBytes.ToArray())
    }
    finally {
        $extensionAssemblyBytes.Dispose()
    }

    $generatedResourceName = $extensionAssembly.GetManifestResourceNames() |
        Where-Object { $_.EndsWith('.g.resources', [System.StringComparison]::Ordinal) } |
        Select-Object -First 1
    if ([string]::IsNullOrWhiteSpace($generatedResourceName)) {
        throw 'The packaged extension assembly has no WPF generated-resource table for the file icon.'
    }

    $generatedResourceStream = $extensionAssembly.GetManifestResourceStream(
        $generatedResourceName)
    $generatedResourceReader = [System.Resources.ResourceReader]::new(
        $generatedResourceStream)
    try {
        $generatedResourceKeys = @()
        $generatedResourceEnumerator = $generatedResourceReader.GetEnumerator()
        while ($generatedResourceEnumerator.MoveNext()) {
            $generatedResourceKeys += [string] $generatedResourceEnumerator.Key
        }
    }
    finally {
        $generatedResourceReader.Dispose()
        $generatedResourceStream.Dispose()
    }

    $expectedGeneratedResourceKeys = @(
        'branding/on-light/viu-mono-16.png',
        'branding/on-light/viu-mono-32.png',
        'branding/on-dark/viu-mono-16.png',
        'branding/on-dark/viu-mono-32.png')
    $missingGeneratedResourceKeys = @(
        $expectedGeneratedResourceKeys |
            Where-Object { $generatedResourceKeys -notcontains $_ })
    if ($missingGeneratedResourceKeys.Count -gt 0) {
        throw "The packaged extension assembly is missing image-manifest resources: $($missingGeneratedResourceKeys -join ', ')."
    }

    $metadata = $manifest.SelectSingleNode(
        '/vsix:PackageManifest/vsix:Metadata',
        $namespaceManager)
    if ($metadata.Icon -ne 'Branding\nuget\viu-nuget-mono-light-256.png') {
        throw "Unexpected Visual Studio extension icon: $($metadata.Icon)."
    }
    if ($metadata.PreviewImage -ne 'Branding\nuget\viu-nuget-mono-light-256.png') {
        throw "Unexpected Visual Studio extension preview image: $($metadata.PreviewImage)."
    }

    $entryCount = $entryNames.Count
}
finally {
    $extensionArchive.Dispose()
}
# #endregion

$packagedExtensionFile = Get-Item -LiteralPath $packagedExtension
$maximumPackageSize = 50MB
if ($packagedExtensionFile.Length -gt $maximumPackageSize) {
    throw "The Visual Studio extension is $($packagedExtensionFile.Length) bytes; Marketplace packages must remain at or below $maximumPackageSize bytes."
}

$packageSizeInMegabytes = $packagedExtensionFile.Length / 1MB
Write-Host "Visual Studio extension: $packagedExtension"
Write-Host ("  {0} entries, {1:N2} MB ({2} bytes)" -f `
    $entryCount,
    $packageSizeInMegabytes,
    $packagedExtensionFile.Length)

if ($DeployExperimental) {
    $visualStudioInstances = @(
        & $visualStudioInstaller `
            -all `
            -products '*' `
            -requires Microsoft.Component.MSBuild `
            -format json |
            ConvertFrom-Json)
    if ($LASTEXITCODE -ne 0) {
        throw "vswhere.exe failed while locating the selected Visual Studio instance with exit code $LASTEXITCODE."
    }

    $visualStudioInstance = $visualStudioInstances |
        Where-Object {
            [System.IO.Path]::GetFullPath($_.installationPath).Equals(
                [System.IO.Path]::GetFullPath($visualStudioInstallation),
                [System.StringComparison]::OrdinalIgnoreCase)
        } |
        Select-Object -First 1
    if ($null -eq $visualStudioInstance) {
        throw "The Visual Studio instance id for $visualStudioInstallation could not be resolved."
    }

    $developmentEnvironment = Join-Path $visualStudioInstallation `
        'Common7\IDE\devenv.exe'
    $extensionInstaller = Join-Path $visualStudioInstallation `
        'Common7\IDE\VSIXInstaller.exe'
    if (-not (Test-Path -LiteralPath $developmentEnvironment) -or
        -not (Test-Path -LiteralPath $extensionInstaller)) {
        throw "Visual Studio's development environment or extension installer is missing under $visualStudioInstallation."
    }

    $runningDevelopmentEnvironments = @(
        Get-CimInstance Win32_Process -Filter "Name = 'devenv.exe'" |
            Where-Object {
                -not [string]::IsNullOrWhiteSpace($_.ExecutablePath) -and
                [System.IO.Path]::GetFullPath($_.ExecutablePath).Equals(
                    [System.IO.Path]::GetFullPath($developmentEnvironment),
                    [System.StringComparison]::OrdinalIgnoreCase)
            })
    if ($runningDevelopmentEnvironments.Count -gt 0) {
        $runningProcessIds = $runningDevelopmentEnvironments.ProcessId -join ', '
        throw "Close the selected Visual Studio instance before experimental deployment. Running process ids: $runningProcessIds."
    }

    Write-Host "Installing into Visual Studio instance $($visualStudioInstance.instanceId), root suffix $ExperimentalRootSuffix"
    # VSIXInstaller is a GUI executable and can hand work to a child process. Start-Process -Wait
    # waits for that process tree, so /UpdateConfiguration cannot race ahead of the file copy.
    $extensionInstallation = Start-Process `
        -FilePath $extensionInstaller `
        -ArgumentList @(
            '/quiet',
            '/force',
            "/instanceIds:$($visualStudioInstance.instanceId)",
            "/rootSuffix:$ExperimentalRootSuffix",
            $packagedExtension) `
        -WindowStyle Hidden `
        -Wait `
        -PassThru
    if ($extensionInstallation.ExitCode -ne 0) {
        throw "Visual Studio extension installation failed with exit code $($extensionInstallation.ExitCode)."
    }

    # DeployVsixExtensionFiles only copies and enables the VSIX. An explicit configuration pass is
    # required to invalidate the root suffix's ImageLibrary cache before the next IDE process reads
    # ShellFileAssociations. Run it hidden because it is a non-interactive maintenance process.
    $configurationUpdate = Start-Process `
        -FilePath $developmentEnvironment `
        -ArgumentList @('/RootSuffix', $ExperimentalRootSuffix, '/UpdateConfiguration') `
        -WindowStyle Hidden `
        -Wait `
        -PassThru
    if ($configurationUpdate.ExitCode -ne 0) {
        throw "Visual Studio experimental configuration update failed with exit code $($configurationUpdate.ExitCode)."
    }

    Write-Host 'Experimental deployment and image-library configuration refresh succeeded.'
}
