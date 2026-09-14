<#
.SYNOPSIS
    Builds the versioned Viu API reference ([V01.01.13.04], #101).
.DESCRIPTION
    Requires an installed .NET SDK/workload and a prior Release solution restore. Only local-tool
    restore may access the network; compilation and docfx generation use restored local inputs.
    All generated inputs, logs, and site files stay in this checkout's ignored _out directory.
#>
[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$repository = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$working = Join-Path $repository '_out/api-reference-work'
$site = Join-Path $repository '_out/api-reference'
$projectDirectory = Join-Path $repository 'docs/api-reference'
Import-Module (Join-Path $PSScriptRoot 'modules/ViuPackaging.psm1') -Force
Import-Module (Join-Path $PSScriptRoot 'modules/ViuApiReference.psm1') -Force

function Invoke-CheckedDotnet {
    param([string[]] $Arguments, [string] $Log)
    & dotnet @Arguments 2>&1 | Tee-Object -FilePath $Log | Out-Host
    if ($LASTEXITCODE -ne 0) {
        throw "dotnet $($Arguments -join ' ') failed ($LASTEXITCODE). See $Log."
    }
}

function Get-ProjectProperties {
    param([string] $Project, [switch] $Documentation)
    $arguments = @('msbuild', $Project, '-nologo', '-p:Configuration=Release',
        '-p:BuildProjectReferences=false', '-target:ResolveReferences',
        '-getProperty:TargetPath,DocumentationFile,ViuVersion,GenerateDocumentationFile,NoWarn', '-getItem:ReferencePath')
    if ($Documentation) { $arguments += '-p:GenerateDocumentationFile=true' }
    $result = & dotnet @arguments
    if ($LASTEXITCODE -ne 0) { throw "Cannot evaluate $Project." }
    return ($result -join "`n" | ConvertFrom-Json)
}

$environmentNames = @('DOTNET_CLI_WORKLOAD_UPDATE_NOTIFY_DISABLE', 'DOTNET_CLI_TELEMETRY_OPTOUT')
$previousEnvironment = @{}
foreach ($name in $environmentNames) {
    $previousEnvironment[$name] = [System.Environment]::GetEnvironmentVariable($name, 'Process')
}
Push-Location $repository
try {
    # --no-restore does not disable the CLI's background workload-manifest downloads.
    foreach ($name in $environmentNames) { [System.Environment]::SetEnvironmentVariable($name, 'true', 'Process') }
    $revision = (& git rev-parse HEAD | Out-String).Trim()
    if ($LASTEXITCODE -ne 0) { throw 'Cannot determine the repository revision for source-directory links.' }
    # Resolve fixed output paths before deleting: never clean a different checkout or output tree.
    foreach ($directory in @($working, $site)) {
        $resolved = [System.IO.Path]::GetFullPath($directory)
        if (-not $resolved.StartsWith((Join-Path $repository '_out') + [System.IO.Path]::DirectorySeparatorChar,
            [System.StringComparison]::OrdinalIgnoreCase)) { throw "Unsafe output path: $resolved" }
        if (Test-Path -LiteralPath $resolved) { Remove-Item -LiteralPath $resolved -Recurse -Force }
        New-Item -ItemType Directory -Path $resolved -Force | Out-Null
    }
    Invoke-CheckedDotnet @('tool', 'restore', '--tool-manifest', '.config/dotnet-tools.json') (Join-Path $working 'tool-restore.log')
    $toolVersion = (& dotnet tool run docfx -- --version | Out-String).Trim()
    if ($LASTEXITCODE -ne 0) { throw 'Could not read docfx version.' }
    $sdkVersion = (& dotnet --version | Out-String).Trim()
    if ($LASTEXITCODE -ne 0) { throw 'Could not read the .NET SDK version.' }

    $shipping = @(Get-ViuLibraryProject -RepositoryDirectory $repository)
    $parsers = @(Get-ChildItem (Join-Path $repository 'libraries/Syntax') -Directory |
        ForEach-Object { Get-ChildItem (Join-Path $_.FullName 'src') -Filter '*.csproj' -File } |
        Sort-Object FullName | ForEach-Object FullName)
    # UtilityCss.Build is a tasks-only package; no lib/ surface is published (see project README).
    $projects = @($shipping | Where-Object { [System.IO.Path]::GetFileNameWithoutExtension($_) -ne 'Assimalign.Viu.UtilityCss.Build' }) + $parsers

    # Refresh an explicit compiler input: TreatWarningsAsErrors alone does not invalidate
    # CoreCompile's cache. Use ordinary Build because solution Rebuild cleans shared SDK staging
    # directories while other projects are still consuming them. These generated targets affect
    # this invocation only; no repository-wide build settings or shipped assemblies are changed.
    $compilationTargets = Join-Path $working 'force-compilation.targets'
    $compilationMarker = Join-Path $working 'force-compilation.marker'
    [System.IO.File]::WriteAllText($compilationTargets, @'
<Project>
  <ItemGroup>
    <CustomAdditionalCompileInputs Include="$(MSBuildThisFileDirectory)force-compilation.marker" />
  </ItemGroup>
</Project>
'@)
    # Packaging already enables XML for shipping libraries. Enabling it solution-wide would wrongly
    # require comments on test fixtures and other non-published implementation projects.
    [System.IO.File]::WriteAllText($compilationMarker, [guid]::NewGuid().ToString('N'))
    Invoke-CheckedDotnet @('build', 'Assimalign.Viu.slnx', '-c', 'Release', '--no-restore',
        "-p:CustomBeforeMicrosoftCommonTargets=$compilationTargets", '-p:TreatWarningsAsErrors=true', '-warnaserror') (Join-Path $working 'release.log')
    foreach ($parser in $parsers) {
        $name = [System.IO.Path]::GetFileNameWithoutExtension($parser)
        [System.IO.File]::WriteAllText($compilationMarker, [guid]::NewGuid().ToString('N'))
        Invoke-CheckedDotnet @('build', $parser, '-c', 'Release', '--no-restore', '-p:BuildProjectReferences=false',
            "-p:CustomBeforeMicrosoftCommonTargets=$compilationTargets",
            '-p:GenerateDocumentationFile=true', '-p:TreatWarningsAsErrors=true', '-warnaserror') (Join-Path $working "$name.log")
    }

    $assemblies = New-Item -ItemType Directory -Path (Join-Path $working 'assemblies')
    $clauses = New-ViuSpecificationMap -SpecificationPath (Join-Path $repository 'docs/SPECIFICATION.md')
    $citationCount = 0
    $version = $null
    $referencePaths = [System.Collections.Generic.HashSet[string]]::new([System.StringComparer]::OrdinalIgnoreCase)
    foreach ($project in @($shipping) + $parsers) {
        Write-Host "Reading XML documentation and references: $([System.IO.Path]::GetFileNameWithoutExtension($project))"
        $evaluation = Get-ProjectProperties $project -Documentation:($project -in $parsers)
        $properties = $evaluation.Properties
        if ($properties.GenerateDocumentationFile -ne 'true' -or $properties.NoWarn -match '(^|[;, ])(?:CS)?1591($|[;, ])') {
            throw "XML documentation/CS1591 is disabled for $project."
        }
        if ($version -and $version -ne $properties.ViuVersion) { throw "Package version differs in $project." }
        $version = $properties.ViuVersion
        if ($project -notin $projects) { continue }
        foreach ($reference in $evaluation.Items.ReferencePath) { [void]$referencePaths.Add($reference.FullPath) }
        $assembly = $properties.TargetPath
        $xml = [System.IO.Path]::GetFullPath((Join-Path (Split-Path $project) $properties.DocumentationFile))
        if (-not (Test-Path -LiteralPath $assembly) -or -not (Test-Path -LiteralPath $xml)) {
            throw "Missing compiled assembly or XML documentation for $project."
        }
        Copy-Item -LiteralPath $assembly -Destination $assemblies.FullName
        $citationCount += Convert-ViuDocumentationXml -SourcePath $xml -DestinationPath (Join-Path $assemblies.FullName ([System.IO.Path]::GetFileName($xml))) -ClauseMap $clauses
    }
    # Docfx rejects different physical copies of the same assembly. Use staged public assemblies,
    # its .NET 10 host's managed runtime dependencies, and restored third-party compile references.
    # Keep docfx's own four core/facade selections: replacing those breaks its mixed net10.0 /
    # netstandard2.0 metadata compilation (CS0518). Never feed native runtime DLLs to Roslyn.
    $canonicalReferences = [ordered]@{}
    foreach ($assembly in Get-ChildItem -LiteralPath $assemblies.FullName -Filter '*.dll' -File | Sort-Object Name) {
        $canonicalReferences[$assembly.Name] = $assembly.FullName
    }
    $runtimes = @(& dotnet --list-runtimes | ForEach-Object {
        if ($_ -match '^Microsoft.NETCore.App (10\.\d+\.\d+) \[(.+)\]$') {
            [pscustomobject]@{ Version = [version]$Matches[1]; Path = Join-Path $Matches[2] $Matches[1] }
        }
    } | Sort-Object Version -Descending)
    if ($LASTEXITCODE -ne 0 -or $runtimes.Count -eq 0) { throw 'The pinned docfx tool requires an installed .NET 10 runtime.' }
    $runtimeNames = [System.Collections.Generic.HashSet[string]]::new([System.StringComparer]::OrdinalIgnoreCase)
    foreach ($assembly in Get-ChildItem -LiteralPath $runtimes[0].Path -Filter '*.dll' -File | Sort-Object Name) {
        try { [void][System.Reflection.AssemblyName]::GetAssemblyName($assembly.FullName) }
        catch [System.BadImageFormatException] { continue }
        [void]$runtimeNames.Add($assembly.Name)
        if ($assembly.Name -notin @('mscorlib.dll', 'netstandard.dll', 'System.Runtime.dll', 'System.Private.CoreLib.dll')) {
            $canonicalReferences[$assembly.Name] = $assembly.FullName
        }
    }
    foreach ($reference in $referencePaths | Sort-Object) {
        $name = [System.IO.Path]::GetFileName($reference)
        if ($runtimeNames.Contains($name)) { continue }
        if (-not $canonicalReferences.Contains($name)) { $canonicalReferences[$name] = $reference }
    }

    # Publish the guide/specification and their directly linked reader documentation. Further
    # repository navigation remains commit-pinned source links, not a crawl of contributor/agent
    # instructions. Preserve repository-relative paths for docfx's local link/fragment validation.
    $articles = New-Item -ItemType Directory -Path (Join-Path $working 'content')
    foreach ($entry in @('index.md', 'toc.yml')) {
        Copy-Item -LiteralPath (Join-Path $projectDirectory $entry) -Destination $articles.FullName
    }
    $queue = [System.Collections.Generic.Queue[string]]::new()
    $primary = @('docs/SPECIFICATION.md', 'docs/guide/getting-started.md', 'docs/DEVELOPER-EXAMPLES.md')
    $published = [System.Collections.Generic.HashSet[string]]::new([System.StringComparer]::OrdinalIgnoreCase)
    $primary | ForEach-Object { $queue.Enqueue($_); [void]$published.Add($_) }
    $visited = [System.Collections.Generic.HashSet[string]]::new([System.StringComparer]::OrdinalIgnoreCase)
    while ($queue.Count -gt 0) {
        $relative = $queue.Dequeue()
        if (-not $visited.Add($relative)) { continue }
        $source = Join-Path $repository $relative
        $destination = Join-Path $articles.FullName $relative
        New-Item -ItemType Directory -Path (Split-Path $destination) -Force | Out-Null
        if ([System.IO.Path]::GetExtension($source) -ne '.md') {
            Copy-Item -LiteralPath $source -Destination $destination
            continue
        }
        $markdown = [System.IO.File]::ReadAllText($source)
        # Ignore fenced code when discovering Markdown links (examples are not navigation).
        $fence = $null
        $lines = foreach ($line in $markdown -split '\r?\n') {
            if ($line -match '^\s*(`{3,}|~{3,})') {
                if ($null -eq $fence) { $fence = $Matches[1][0] }
                elseif ($fence -eq $Matches[1][0]) { $fence = $null }
                $line
                continue
            }
            if ($null -ne $fence) { $line; continue }
            [regex]::Replace($line, '(?<=\]\()(?<path>[^\s)#]+)(?<fragment>#[^\s)]*)?(?=\))', {
                param($match)
                $link = $match.Groups['path'].Value
                if ($link -match '^(?:[a-zA-Z][a-zA-Z0-9+.-]*:|/|#)') { return $match.Value }
                $target = [System.IO.Path]::GetFullPath((Join-Path (Split-Path $source) $link))
                if (-not $target.StartsWith($repository + [System.IO.Path]::DirectorySeparatorChar, [System.StringComparison]::OrdinalIgnoreCase)) {
                    throw "Document link escapes this checkout: $relative -> $link"
                }
                if (-not (Test-Path -LiteralPath $target)) { throw "Missing document link: $relative -> $link" }
                $isDirectory = Test-Path -LiteralPath $target -PathType Container
                if ($isDirectory) {
                    $target = Join-Path $target 'README.md'
                    if (-not (Test-Path -LiteralPath $target)) {
                        # Source-directory links are repository navigation, not site pages.
                        return 'https://github.com/assimalign/viu/tree/' + $revision + '/' +
                            [System.IO.Path]::GetRelativePath($repository, (Split-Path $target)).Replace('\', '/')
                    }
                    $link = $link.TrimEnd('/') + '/README.md'
                }
                $targetRelative = [System.IO.Path]::GetRelativePath($repository, $target).Replace('\', '/')
                if (($relative -in $primary -and -not $targetRelative.StartsWith('.') -and $targetRelative -ne 'README.md') -or $published.Contains($targetRelative)) {
                    [void]$published.Add($targetRelative)
                    $queue.Enqueue($targetRelative)
                    return $link + $match.Groups['fragment'].Value
                }
                return 'https://github.com/assimalign/viu/blob/' + $revision + '/' + $targetRelative + $match.Groups['fragment'].Value
            })
        }
        $markdown = $lines -join "`n"
        if ($relative -eq 'docs/SPECIFICATION.md') { $markdown = Convert-ViuSpecification -Text $markdown -ClauseMap $clauses }
        [System.IO.File]::WriteAllText($destination, $markdown)
    }

    $configuration = Get-Content (Join-Path $projectDirectory 'docfx.json') -Raw | ConvertFrom-Json -AsHashtable
    $configuration.build.globalMetadata._appName = "Viu $version"
    $configuration.build.globalMetadata._appTitle = "Viu $version API reference"
    $configuration.build.globalMetadata._appFooter = "Viu package version $version"
    $configuration.build.globalMetadata.ViuVersion = $version
    # Paths in the checked-in config are relative to docs/api-reference. Keep that base when writing
    # the generated config into _out, without creating generated files beside the source project.
    foreach ($metadata in $configuration.metadata) {
        foreach ($mapping in @($metadata.src) + @($metadata.references)) { $mapping.src = [System.IO.Path]::GetFullPath((Join-Path $projectDirectory $mapping.src)) }
        $metadata.dest = [System.IO.Path]::GetFullPath((Join-Path $projectDirectory $metadata.dest))
        $metadata.filter = Join-Path $projectDirectory $metadata.filter
        # Resolve against the exact locally restored compile references (not a NuGet cache scan).
        # Parser projects do not copy their Roslyn dependency DLLs into bin/.
        $metadata.references += @($canonicalReferences.Values | ForEach-Object {
            @{ src = Split-Path $_; files = @([System.IO.Path]::GetFileName($_)) }
        })
    }
    foreach ($mapping in @($configuration.build.content) + @($configuration.build.resource)) {
        $mapping.src = if ($mapping.ContainsKey('src')) { [System.IO.Path]::GetFullPath((Join-Path $projectDirectory $mapping.src)) } else { $projectDirectory }
    }
    $configuration.build.dest = $site
    $configPath = Join-Path $working 'docfx.json'
    $configuration | ConvertTo-Json -Depth 30 | Set-Content -LiteralPath $configPath
    Invoke-CheckedDotnet @('tool', 'run', 'docfx', '--', 'metadata', $configPath, '--warningsAsErrors', '--log', (Join-Path $working 'metadata.json')) (Join-Path $working 'metadata.log')
    $api = New-Item -ItemType Directory -Path (Join-Path $articles.FullName 'api') -Force
    $tableOfContents = [System.Collections.Generic.List[string]]::new()
    foreach ($group in @('libraries', 'templates')) {
        $metadataDirectory = Join-Path $working "metadata/$group"
        foreach ($file in Get-ChildItem -LiteralPath $metadataDirectory -Filter '*.yml' -File | Sort-Object Name) {
            if ($file.Name -eq 'toc.yml') {
                $tableOfContents.Add([System.IO.File]::ReadAllText($file.FullName))
            } else {
                $target = Join-Path $api.FullName $file.Name
                if (Test-Path -LiteralPath $target) { throw "Duplicate API metadata file: $($file.Name)" }
                Copy-Item -LiteralPath $file.FullName -Destination $target
            }
        }
    }
    [System.IO.File]::WriteAllText((Join-Path $api.FullName 'toc.yml'),
        (Merge-ViuApiReferenceTableOfContents -TablesOfContents $tableOfContents.ToArray()))
    Invoke-CheckedDotnet @('tool', 'run', 'docfx', '--', 'build', $configPath, '--warningsAsErrors', '--log', (Join-Path $working 'build.json')) (Join-Path $working 'build.log')
    # toc.html files are navigation fragments, not reader pages. Check version labels on every
    # complete HTML document and report only those documents in the page count.
    $pages = @(Get-ChildItem -LiteralPath $site -Filter '*.html' -File -Recurse | Where-Object {
        $html = [System.IO.File]::ReadAllText($_.FullName)
        if ($html -notmatch '<html[\s>]') { return $false }
        if ($html -notmatch ('(?s)<footer\b.*?Viu package version ' + [regex]::Escape($version))) {
            throw "Missing package version footer: $($_.FullName)"
        }
        return $true
    })
    if ($pages.Count -eq 0) { throw 'Docfx produced no HTML pages.' }
    $summary = [ordered]@{ docfxVersion = $toolVersion; sdkVersion = $sdkVersion; runtimeVersion = $runtimes[0].Version.ToString(); packageVersion = $version; warnings = 0; pages = $pages.Count; assemblies = $projects.Count; clauses = $clauses.Count; citations = $citationCount; output = $site }
    $summary | ConvertTo-Json | Set-Content (Join-Path $working 'summary.json')
    Write-Host ($summary | ConvertTo-Json)
}
finally {
    foreach ($name in $environmentNames) {
        [System.Environment]::SetEnvironmentVariable($name, $previousEnvironment[$name], 'Process')
    }
    Pop-Location
}
