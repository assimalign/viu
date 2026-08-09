<#
.SYNOPSIS
    Publishes Viu sample applications with trimming enabled, measures the
    brotli-compressed size of the published browser payload, and fails when a
    sample exceeds the budget recorded in the checked-in manifest.

.DESCRIPTION
    Every published byte is network payload for a WebAssembly app, so a
    per-sample brotli budget is the enforcement mechanism for the framework's
    source-generator-first, reflection-free architecture ([V01.01.12.06], #95;
    restored by [V01.01.12.26], #320).

    The gate is deterministic and cross-platform (Windows local + GitHub Actions
    ubuntu runners): it uses the sample's isolated publish script when the manifest
    declares one, otherwise it shells out to `dotnet publish`, then computes brotli
    sizes with the in-box .NET compression APIs. It needs no external tooling beyond
    the .NET SDK and the wasm-tools workload.

    Trimming is configured by each packaged-consumer sample project rather than a
    global publish property, because a global -p:PublishTrimmed=true can flow to
    netstandard2.0 generator projects and fail with NETSDK1124. The gate publishes
    with -warnaserror, so any ILLink/trim-analyzer warning fails the publish across
    the sample and its package closure -- this is also the trimming-validation gate.

    Budgets only ever change through an explicit edit to the manifest
    (scripts/budgets/PublishBudgets.json) in the pull request under review. This
    script never rewrites the manifest, so size growth is always a reviewed
    decision -- there is no silent ratcheting.

.PARAMETER ManifestPath
    Path to the budget manifest. Defaults to scripts/budgets/PublishBudgets.json
    alongside this script (i.e. the manifest of the tree that owns this script,
    even when -RepositoryRoot points at a different checkout).

.PARAMETER SampleName
    One or more sample names (matching the manifest) to measure. Defaults to every
    sample in the manifest.

.PARAMETER Configuration
    Build configuration to publish. Defaults to Release; Debug builds are never a
    valid measurement subject.

.PARAMETER PublishDirectory
    When supplied, the script measures this already-published directory instead of
    invoking `dotnet publish`. Used by the tests (to feed deterministic fixtures)
    and to re-measure a payload without republishing. Single-sample mode: combine
    with -SampleName when the manifest holds more than one sample.

.PARAMETER RepositoryRoot
    Root against which project paths in the manifest are resolved. Defaults to the
    parent of this script's directory. A project may live in a sibling repository
    (for example ../viu-examples) while publish output remains under this root.

.PARAMETER ResultsPath
    When supplied, writes the machine-readable measurement results as JSON for CI
    artifacts or later comparison.

.PARAMETER BaselineResultsPath
    When supplied, a prior results JSON (typically the base branch) whose per-sample
    sizes are shown as a delta column in the report.

.PARAMETER BaselineManifestPath
    When supplied, reads baselineCompressedPublishSizeBytes from another revision's
    budget manifest. This lets pull-request CI report a base-branch delta without
    publishing the base revision a second time. A measured BaselineResultsPath takes
    precedence. The current manifest's reviewed baseline is the fallback.

.PARAMETER NoGate
    Measure and report only; never set a failing exit code. Used for the base-branch
    measurement, which is informational, not a gate.

.PARAMETER PublishOutputRoot
    Directory that holds per-sample publish output. Defaults to a folder under the
    repository's gitignored _out. Cleared per sample before publishing.

.OUTPUTS
    Exit code 0 when every measured sample is within budget (or -NoGate), 1 when any
    sample exceeds its budget, 2 on a configuration/tooling error.
#>
[CmdletBinding()]
param(
    [string] $ManifestPath,
    [string[]] $SampleName,
    [string] $Configuration = 'Release',
    [string] $PublishDirectory,
    [string] $RepositoryRoot,
    [string] $ResultsPath,
    [string] $BaselineResultsPath,
    [string] $BaselineManifestPath,
    [switch] $NoGate,
    [string] $PublishOutputRoot
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$scriptRoot = $PSScriptRoot
if (-not $RepositoryRoot) {
    $RepositoryRoot = Split-Path $scriptRoot -Parent
}
$RepositoryRoot = (Resolve-Path -LiteralPath $RepositoryRoot).Path
if (-not $ManifestPath) {
    $ManifestPath = Join-Path $scriptRoot 'budgets/PublishBudgets.json'
}
if (-not $PublishOutputRoot) {
    $PublishOutputRoot = Join-Path $RepositoryRoot '_out/budgets'
}
$repositoryOutputRoot = [System.IO.Path]::GetFullPath(
    (Join-Path $RepositoryRoot '_out'))
$pathComparison = if ([System.OperatingSystem]::IsWindows()) {
    [System.StringComparison]::OrdinalIgnoreCase
}
else {
    [System.StringComparison]::Ordinal
}

# Files the browser never downloads on its own: pre-compressed duplicates the SDK
# may emit (compressing a *.br/*.gz copy would double count) and source maps.
$excludedPayloadExtensions = @('.br', '.gz', '.map')

function Write-Report {
    param([string] $Text)
    # Report goes to the host so it stays visible in CI logs regardless of the
    # object stream; measurement objects are returned separately.
    Write-Host $Text
}

function Exit-WithError {
    # Configuration / tooling failures exit with code 2 (distinct from an
    # over-budget gate failure, which is code 1). Write straight to stderr rather
    # than Write-Error so $ErrorActionPreference='Stop' cannot pre-empt the exit
    # code with an unhandled terminating error.
    param([string] $Message, [int] $Code = 2)
    [Console]::Error.WriteLine("Measure-PublishBudget: $Message")
    exit $Code
}

function Format-Bytes {
    param([long] $Bytes)
    if ($Bytes -ge 1MB) { return ('{0:N2} MB' -f ($Bytes / 1MB)) }
    if ($Bytes -ge 1KB) { return ('{0:N2} KB' -f ($Bytes / 1KB)) }
    return ("$Bytes B")
}

function Format-SignedBytes {
    param([long] $Bytes)
    $sign = if ($Bytes -ge 0) { '+' } else { '-' }
    return ($sign + (Format-Bytes ([Math]::Abs($Bytes))))
}

function Test-IsPathChildOf {
    param(
        [string] $Path,
        [string] $ParentPath)
    $parentPrefix = $ParentPath.TrimEnd(
        [System.IO.Path]::DirectorySeparatorChar,
        [System.IO.Path]::AltDirectorySeparatorChar) +
        [System.IO.Path]::DirectorySeparatorChar
    return $Path.StartsWith(
        $parentPrefix,
        $pathComparison)
}

function Assert-NoReparsePointInDirectoryTree {
    param([string] $Path)
    if (-not (Test-Path -LiteralPath $Path)) {
        return
    }

    $root = Get-Item -LiteralPath $Path -Force
    if (-not $root.PSIsContainer) {
        Exit-WithError "Publish output target is not a directory: $Path"
    }
    $pending = [System.Collections.Generic.Stack[System.IO.DirectoryInfo]]::new()
    $pending.Push($root)
    while ($pending.Count -gt 0) {
        $directory = $pending.Pop()
        if (($directory.Attributes -band [System.IO.FileAttributes]::ReparsePoint) -ne 0) {
            Exit-WithError "Publish output cannot contain a reparse point: $($directory.FullName)"
        }
        foreach ($child in Get-ChildItem -LiteralPath $directory.FullName -Force) {
            if (($child.Attributes -band [System.IO.FileAttributes]::ReparsePoint) -ne 0) {
                Exit-WithError "Publish output cannot contain a reparse point: $($child.FullName)"
            }
            if ($child.PSIsContainer) {
                $pending.Push($child)
            }
        }
    }
}

function Resolve-SafePublishOutputDirectory {
    param([string] $SampleName)
    $repositoryRootItem = Get-Item -LiteralPath $RepositoryRoot -Force
    if (-not $repositoryRootItem.PSIsContainer -or
        ($repositoryRootItem.Attributes -band [System.IO.FileAttributes]::ReparsePoint) -ne 0) {
        Exit-WithError "RepositoryRoot must be a normal directory: $RepositoryRoot"
    }

    $publishRootPath = [System.IO.Path]::GetFullPath($PublishOutputRoot)
    if (-not (Test-IsPathChildOf -Path $publishRootPath -ParentPath $repositoryOutputRoot)) {
        Exit-WithError "PublishOutputRoot must be a child of repository _out: $publishRootPath"
    }

    $currentPath = $publishRootPath
    while ($currentPath -and
        (Test-IsPathChildOf -Path $currentPath -ParentPath $repositoryOutputRoot)) {
        if (Test-Path -LiteralPath $currentPath) {
            $currentItem = Get-Item -LiteralPath $currentPath -Force
            if (-not $currentItem.PSIsContainer) {
                Exit-WithError "Publish output ancestor is not a directory: $currentPath"
            }
            if (($currentItem.Attributes -band [System.IO.FileAttributes]::ReparsePoint) -ne 0) {
                Exit-WithError "Publish output ancestor cannot be a reparse point: $currentPath"
            }
        }
        $currentPath = [System.IO.Path]::GetDirectoryName($currentPath)
    }
    if (Test-Path -LiteralPath $repositoryOutputRoot) {
        $repositoryOutputItem = Get-Item -LiteralPath $repositoryOutputRoot -Force
        if (-not $repositoryOutputItem.PSIsContainer -or
            ($repositoryOutputItem.Attributes -band [System.IO.FileAttributes]::ReparsePoint) -ne 0) {
            Exit-WithError "Repository _out must be a normal directory: $repositoryOutputRoot"
        }
    }

    $outputDirectory = [System.IO.Path]::GetFullPath(
        (Join-Path $publishRootPath $SampleName))
    if (-not (Test-IsPathChildOf -Path $outputDirectory -ParentPath $publishRootPath)) {
        Exit-WithError "Sample name resolves outside PublishOutputRoot: $SampleName"
    }
    return $outputDirectory
}

function Resolve-SafeFileWritePath {
    param(
        [string] $Path,
        [string] $ParameterName)
    $resolvedPath = [System.IO.Path]::GetFullPath($Path)
    if (Test-Path -LiteralPath $resolvedPath) {
        $targetItem = Get-Item -LiteralPath $resolvedPath -Force
        if ($targetItem.PSIsContainer) {
            Exit-WithError "$ParameterName is a directory, not a file: $resolvedPath"
        }
        if (($targetItem.Attributes -band [System.IO.FileAttributes]::ReparsePoint) -ne 0) {
            Exit-WithError "$ParameterName cannot be a reparse point: $resolvedPath"
        }
    }

    $currentDirectory = [System.IO.Path]::GetDirectoryName($resolvedPath)
    while (-not [string]::IsNullOrWhiteSpace($currentDirectory)) {
        if (Test-Path -LiteralPath $currentDirectory) {
            $directoryItem = Get-Item -LiteralPath $currentDirectory -Force
            if (-not $directoryItem.PSIsContainer) {
                Exit-WithError "$ParameterName ancestor is not a directory: $currentDirectory"
            }
            if (($directoryItem.Attributes -band [System.IO.FileAttributes]::ReparsePoint) -ne 0) {
                Exit-WithError "$ParameterName ancestor cannot be a reparse point: $currentDirectory"
            }
        }
        $parentDirectory = [System.IO.Path]::GetDirectoryName($currentDirectory)
        if ([string]::Equals(
                $parentDirectory,
                $currentDirectory,
                $pathComparison)) {
            break
        }
        $currentDirectory = $parentDirectory
    }
    return $resolvedPath
}

function Get-BrotliByteCount {
    param([string] $Path)
    # Model the bytes on the wire: each asset is compressed independently, at the
    # smallest-size brotli quality a production static host would serve.
    $bytes = [System.IO.File]::ReadAllBytes($Path)
    $buffer = [System.IO.MemoryStream]::new()
    $brotli = [System.IO.Compression.BrotliStream]::new(
        $buffer,
        [System.IO.Compression.CompressionLevel]::SmallestSize,
        $true)
    try {
        $brotli.Write($bytes, 0, $bytes.Length)
    }
    finally {
        $brotli.Dispose()
    }
    $count = $buffer.Length
    $buffer.Dispose()
    return [long] $count
}

function Measure-PayloadDirectory {
    param([string] $PayloadRoot)
    # The published browser payload is everything under the app's wwwroot: the
    # _framework runtime + assemblies, index.html, JS glue, and _content assets.
    $compressed = [long] 0
    $raw = [long] 0
    $fileCount = 0
    $files = Get-ChildItem -LiteralPath $PayloadRoot -Recurse -File
    foreach ($file in $files) {
        if ($excludedPayloadExtensions -contains $file.Extension.ToLowerInvariant()) {
            continue
        }
        $compressed += Get-BrotliByteCount $file.FullName
        $raw += $file.Length
        $fileCount++
    }
    return [pscustomobject]@{
        CompressedPublishSizeBytes = $compressed
        RawPublishSizeBytes        = $raw
        FileCount                  = $fileCount
    }
}

function Resolve-PayloadRoot {
    param([string] $PublishRoot)
    # WebAssembly publish output places the browser payload under wwwroot; if a
    # sample ever publishes a flat payload we fall back to the publish root.
    $wwwroot = Join-Path $PublishRoot 'wwwroot'
    if (Test-Path -LiteralPath $wwwroot -PathType Container) {
        return $wwwroot
    }
    return $PublishRoot
}

function Invoke-SamplePublish {
    param(
        [string] $ProjectPath,
        [string] $OutputDirectory,
        [string] $PublishScriptPath)

    if ($PublishScriptPath) {
        # Browser SDK fixtures need an isolated global.json, NuGet configuration,
        # and package cache. Their tracked project must not be evaluated under the
        # repository's build root, so the end-to-end harness owns that boundary.
        Write-Report "  publishing $ProjectPath through $PublishScriptPath (trimmed, $Configuration)"
        & $PublishScriptPath `
            -PublishOnly `
            -PublishDirectory $OutputDirectory `
            -Configuration $Configuration
        return
    }

    if (Test-Path -LiteralPath $OutputDirectory) {
        Assert-NoReparsePointInDirectoryTree -Path $OutputDirectory
        Write-Report "  clearing validated publish output $OutputDirectory"
        Remove-Item -LiteralPath $OutputDirectory -Recurse -Force
    }
    [System.IO.Directory]::CreateDirectory($OutputDirectory) | Out-Null

    # Trimming is enabled by the packaged-consumer project. -warnaserror doubles
    # as the trimming-validation gate: any ILLink/trim-analyzer warning fails here.
    Write-Report "  publishing $ProjectPath (trimmed, $Configuration)"
    & dotnet publish $ProjectPath `
        --configuration $Configuration `
        -warnaserror `
        --output $OutputDirectory
    if ($LASTEXITCODE -ne 0) {
        throw "Publish failed for $ProjectPath (exit $LASTEXITCODE). Trim warnings are treated as errors."
    }
}

# --- Load the manifest ----------------------------------------------------------

if (-not (Test-Path -LiteralPath $ManifestPath -PathType Leaf)) {
    Exit-WithError "Budget manifest not found: $ManifestPath"
}
$manifest = Get-Content -Raw -LiteralPath $ManifestPath | ConvertFrom-Json
$samples = @($manifest.samples)
if ($SampleName) {
    $samples = @($samples | Where-Object { $SampleName -contains $_.name })
    if ($samples.Count -eq 0) {
        Exit-WithError "No manifest sample matched -SampleName: $($SampleName -join ', ')"
    }
}
if ($samples.Count -eq 0) {
    Exit-WithError "Budget manifest defines no samples: $ManifestPath"
}
if ($PublishDirectory -and $samples.Count -gt 1) {
    Exit-WithError '-PublishDirectory measures a single payload; narrow to one sample with -SampleName.'
}

$baseline = $null
if ($BaselineResultsPath) {
    if (-not (Test-Path -LiteralPath $BaselineResultsPath -PathType Leaf)) {
        Exit-WithError "Baseline results not found: $BaselineResultsPath"
    }
    $baseline = Get-Content -Raw -LiteralPath $BaselineResultsPath | ConvertFrom-Json
}
$baselineManifest = $null
if ($BaselineManifestPath) {
    if (-not (Test-Path -LiteralPath $BaselineManifestPath -PathType Leaf)) {
        Exit-WithError "Baseline manifest not found: $BaselineManifestPath"
    }
    $baselineManifest = Get-Content -Raw -LiteralPath $BaselineManifestPath | ConvertFrom-Json
}

# --- Measure --------------------------------------------------------------------

$results = New-Object System.Collections.Generic.List[object]
foreach ($sample in $samples) {
    $budgetBytes = [long] $sample.compressedPublishSizeBytes
    if ($PublishDirectory) {
        $payloadRoot = Resolve-PayloadRoot $PublishDirectory
    }
    else {
        $projectPath = Join-Path $RepositoryRoot $sample.project
        if (-not (Test-Path -LiteralPath $projectPath -PathType Leaf)) {
            Exit-WithError "Sample project not found: $projectPath"
        }
        $publishScriptPath = $null
        $publishScriptProperty = $sample.PSObject.Properties['publishScript']
        if ($publishScriptProperty -and
            -not [string]::IsNullOrWhiteSpace([string] $publishScriptProperty.Value)) {
            $publishScriptPath = Join-Path $RepositoryRoot ([string] $publishScriptProperty.Value)
            if (-not (Test-Path -LiteralPath $publishScriptPath -PathType Leaf)) {
                Exit-WithError "Sample publish script not found: $publishScriptPath"
            }
        }
        $outputDirectory = Resolve-SafePublishOutputDirectory -SampleName $sample.name
        try {
            Invoke-SamplePublish `
                -ProjectPath $projectPath `
                -OutputDirectory $outputDirectory `
                -PublishScriptPath $publishScriptPath
        }
        catch {
            Exit-WithError "$($_.Exception.Message)"
        }
        $payloadRoot = Resolve-PayloadRoot $outputDirectory
    }

    if (-not (Test-Path -LiteralPath $payloadRoot -PathType Container)) {
        Exit-WithError "Published payload directory not found: $payloadRoot"
    }
    $measurement = Measure-PayloadDirectory $payloadRoot
    $actualBytes = [long] $measurement.CompressedPublishSizeBytes
    $withinBudget = $actualBytes -le $budgetBytes

    $sampleBaselineProperty = $sample.PSObject.Properties['baselineCompressedPublishSizeBytes']
    $baselineBytes = if ($sampleBaselineProperty -and $null -ne $sampleBaselineProperty.Value) {
        [long] $sample.baselineCompressedPublishSizeBytes
    }
    else {
        $null
    }
    if ($baselineManifest) {
        $baselineManifestSample = $baselineManifest.samples |
            Where-Object { $_.name -eq $sample.name } |
            Select-Object -First 1
        $baselineManifestProperty = if ($baselineManifestSample) {
            $baselineManifestSample.PSObject.Properties['baselineCompressedPublishSizeBytes']
        }
        else {
            $null
        }
        if ($baselineManifestProperty -and $null -ne $baselineManifestProperty.Value) {
            $baselineBytes = [long] $baselineManifestSample.baselineCompressedPublishSizeBytes
        }
    }
    if ($baseline) {
        $baselineSample = $baseline.samples | Where-Object { $_.name -eq $sample.name } | Select-Object -First 1
        if ($baselineSample) {
            $baselineBytes = [long] $baselineSample.compressedPublishSizeBytes
        }
    }

    $results.Add([pscustomobject]@{
            name                       = $sample.name
            compressedPublishSizeBytes = $actualBytes
            rawPublishSizeBytes        = [long] $measurement.RawPublishSizeBytes
            budgetBytes                = $budgetBytes
            withinBudget               = $withinBudget
            headroomBytes              = ($budgetBytes - $actualBytes)
            deltaOverBudgetBytes       = [Math]::Max(0, $actualBytes - $budgetBytes)
            fileCount                  = $measurement.FileCount
            baselineBytes              = $baselineBytes
        }) | Out-Null
}

# --- Report ---------------------------------------------------------------------

Write-Report ''
Write-Report "Viu publish-size budget report (brotli, $Configuration, trimmed)"
Write-Report '================================================================'
$header = '{0,-32} {1,12} {2,12} {3,10} {4,14}  {5}' -f 'Sample', 'Actual', 'Budget', 'Headroom', 'Delta vs base', 'Status'
Write-Report $header
Write-Report ('-' * $header.Length)

$overCount = 0
foreach ($result in $results) {
    if (-not $result.withinBudget) { $overCount++ }
    $headroomPercent = if ($result.budgetBytes -gt 0) { 100.0 * $result.headroomBytes / $result.budgetBytes } else { 0 }
    $deltaText = 'n/a'
    if ($null -ne $result.baselineBytes) {
        $deltaText = Format-SignedBytes ($result.compressedPublishSizeBytes - $result.baselineBytes)
    }
    $status = if ($result.withinBudget) { 'PASS' } else { 'OVER BUDGET' }
    Write-Report ('{0,-32} {1,12} {2,12} {3,9:N1}% {4,14}  {5}' -f `
            $result.name,
        (Format-Bytes $result.compressedPublishSizeBytes),
        (Format-Bytes $result.budgetBytes),
        $headroomPercent,
        $deltaText,
        $status)
}
Write-Report ('-' * $header.Length)
$measuredCount = $results.Count
$passedCount = $measuredCount - $overCount
Write-Report ("Result: {0} ({1}/{2} within budget)" -f (($overCount -eq 0) ? 'PASS' : 'FAIL'), $passedCount, $measuredCount)
if ($overCount -gt 0) {
    Write-Report ''
    foreach ($result in $results | Where-Object { -not $_.withinBudget }) {
        $baselineDelta = 'n/a'
        if ($null -ne $result.baselineBytes) {
            $baselineDelta = Format-SignedBytes `
                ($result.compressedPublishSizeBytes - $result.baselineBytes)
        }
        $failure = '{0} compressedPublishSizeBytes exceeded by {1} (actual {2}; budget {3}; delta vs base {4})' -f `
            $result.name,
            (Format-Bytes $result.deltaOverBudgetBytes),
            (Format-Bytes $result.compressedPublishSizeBytes),
            (Format-Bytes $result.budgetBytes),
            $baselineDelta
        Write-Report "::error title=Publish budget exceeded::$failure"
        Write-Report "ERROR: $failure"
    }
    Write-Report ''
    Write-Report 'A sample exceeded its reviewed budget. This is a deliberate-decision gate:'
    Write-Report '  - reduce the payload, or'
    Write-Report "  - raise the budget in $ManifestPath as a reviewed change (no silent ratcheting)."
}

# --- Machine-readable results ---------------------------------------------------

if ($ResultsPath) {
    $ResultsPath = Resolve-SafeFileWritePath `
        -Path $ResultsPath `
        -ParameterName 'ResultsPath'
    $resultsDirectory = [System.IO.Path]::GetDirectoryName($ResultsPath)
    if ($resultsDirectory -and -not (Test-Path -LiteralPath $resultsDirectory)) {
        [System.IO.Directory]::CreateDirectory($resultsDirectory) | Out-Null
    }
    [pscustomobject]@{
        configuration = $Configuration
        generatedUtc  = (Get-Date).ToUniversalTime().ToString('o')
        samples       = $results
    } | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath $ResultsPath -Encoding utf8
    Write-Report "Wrote results: $ResultsPath"
}

if ($NoGate) {
    exit 0
}
exit ($overCount -eq 0 ? 0 : 1)
