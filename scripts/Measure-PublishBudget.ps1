<#
.SYNOPSIS
    Publishes Viu sample applications with trimming enabled, measures the
    brotli-compressed size of the published browser payload, and fails when a
    sample exceeds the budget recorded in the checked-in manifest.

.DESCRIPTION
    The Viu analog of vuejs/core's size-check CI job
    (https://github.com/vuejs/core/blob/main/.github/workflows/size-data.yml).
    Every published byte is network payload for a WebAssembly app, so a
    per-sample brotli budget is the enforcement mechanism for the framework's
    source-generator-first, reflection-free architecture ([V01.01.12.06], #95).

    The gate is deterministic and cross-platform (Windows local + GitHub Actions
    ubuntu runners): it shells out to `dotnet publish` and computes brotli sizes
    with the in-box .NET compression APIs, so it needs no external tooling beyond
    the .NET SDK and the wasm-tools workload.

    Trimming is configured by the samples themselves (examples/Directory.Build.props)
    rather than a global publish property, because a global -p:PublishTrimmed=true
    flows to the netstandard2.0 generator projects in the build graph and fails with
    NETSDK1124. The gate publishes with -warnaserror, so any ILLink/trim-analyzer
    warning fails the publish across the sample and every shipping library it pulls
    in -- this is also the trimming-validation gate.

    Budgets only ever change through an explicit edit to the manifest
    (scripts/budgets/PublishBudgets.json) in the pull request under review. This
    script never rewrites the manifest, so size growth is always a reviewed
    decision -- there is no silent ratcheting.

.PARAMETER ManifestPath
    Path to the budget manifest. Defaults to scripts/budgets/PublishBudgets.json
    alongside this script (i.e. the manifest of the tree that owns this script,
    even when -RepositoryRoot points at a different checkout for base-branch delta
    measurement).

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
    Root of the source tree whose sample projects are published. Defaults to the
    parent of this script's directory. Overridden by CI to point at a base-branch
    checkout so the head manifest/method can measure base sources for the delta.

.PARAMETER ResultsPath
    When supplied, writes the machine-readable measurement results as JSON. CI uses
    this to hand a base-branch measurement to a later head-branch run.

.PARAMETER BaselineResultsPath
    When supplied, a prior results JSON (typically the base branch) whose per-sample
    sizes are shown as a delta column in the report.

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
    [switch] $NoGate,
    [string] $PublishOutputRoot
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$scriptRoot = $PSScriptRoot
if (-not $RepositoryRoot) {
    $RepositoryRoot = Split-Path $scriptRoot -Parent
}
$RepositoryRoot = (Resolve-Path $RepositoryRoot).Path
if (-not $ManifestPath) {
    $ManifestPath = Join-Path $scriptRoot 'budgets/PublishBudgets.json'
}
if (-not $PublishOutputRoot) {
    $PublishOutputRoot = Join-Path $RepositoryRoot '_out/budgets'
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
    $files = Get-ChildItem -Path $PayloadRoot -Recurse -File
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
    if (Test-Path $wwwroot) {
        return $wwwroot
    }
    return $PublishRoot
}

function Invoke-SamplePublish {
    param(
        [string] $ProjectPath,
        [string] $OutputDirectory)
    if (Test-Path $OutputDirectory) {
        Remove-Item -Recurse -Force $OutputDirectory
    }
    New-Item -ItemType Directory -Force $OutputDirectory | Out-Null

    # Trimming is enabled by examples/Directory.Build.props. -warnaserror doubles as
    # the trimming-validation gate: any ILLink/trim-analyzer warning fails here.
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

if (-not (Test-Path $ManifestPath)) {
    Exit-WithError "Budget manifest not found: $ManifestPath"
}
$manifest = Get-Content -Raw $ManifestPath | ConvertFrom-Json
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
if ($BaselineResultsPath -and (Test-Path $BaselineResultsPath)) {
    $baseline = Get-Content -Raw $BaselineResultsPath | ConvertFrom-Json
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
        if (-not (Test-Path $projectPath)) {
            Write-Report "  skipping $($sample.name): project not present in this tree ($projectPath)"
            continue
        }
        $outputDirectory = Join-Path $PublishOutputRoot $sample.name
        try {
            Invoke-SamplePublish -ProjectPath $projectPath -OutputDirectory $outputDirectory
        }
        catch {
            Exit-WithError "$($_.Exception.Message)"
        }
        $payloadRoot = Resolve-PayloadRoot $outputDirectory
    }

    $measurement = Measure-PayloadDirectory $payloadRoot
    $actualBytes = [long] $measurement.CompressedPublishSizeBytes
    $withinBudget = $actualBytes -le $budgetBytes

    $baselineBytes = $null
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
    Write-Report 'A sample exceeded its budget. This is a deliberate-decision gate:'
    Write-Report '  - reduce the payload, or'
    Write-Report "  - raise the budget in $ManifestPath as a reviewed change (no silent ratcheting)."
}

# --- Machine-readable results ---------------------------------------------------

if ($ResultsPath) {
    $resultsDirectory = Split-Path $ResultsPath -Parent
    if ($resultsDirectory -and -not (Test-Path $resultsDirectory)) {
        New-Item -ItemType Directory -Force $resultsDirectory | Out-Null
    }
    [pscustomobject]@{
        configuration = $Configuration
        generatedUtc  = (Get-Date).ToUniversalTime().ToString('o')
        samples       = $results
    } | ConvertTo-Json -Depth 6 | Set-Content -Path $ResultsPath -Encoding utf8
    Write-Report "Wrote results: $ResultsPath"
}

if ($NoGate) {
    exit 0
}
exit ($overCount -eq 0 ? 0 : 1)
