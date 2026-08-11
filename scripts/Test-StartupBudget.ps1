<#
.SYNOPSIS
    Validates real-browser startup measurements against the reviewed Viu budget.

.DESCRIPTION
    Consumes the stable JSON written by scripts/Test-EndToEnd.ps1 -MeasureStartup,
    independently recomputes the median of at least ten post-warm-up Chromium
    runs, and fails when it exceeds startupBudgetMilliseconds plus the explicit
    noise tolerance in scripts/budgets/PublishBudgets.json. This implements the
    startup contract from [V01.01.12.06.01] (#182) for the budget-gates workflow
    restored by [V01.01.12.26] (#320). It never rewrites the reviewed manifest.

.PARAMETER ResultsPath
    Startup-measurement JSON produced by scripts/Test-EndToEnd.ps1.

.PARAMETER ManifestPath
    Reviewed budget manifest. Defaults to scripts/budgets/PublishBudgets.json.

.PARAMETER BaselineResultsPath
    Optional startup JSON measured from the base revision. Its median is reported
    as an informational delta; the checked-in budget remains the gate.

.PARAMETER BaselineManifestPath
    Optional budget manifest from the base revision. Its
    startupBaselineMilliseconds value supplies the informational base delta when
    a full base-revision browser run is intentionally avoided. Measured baseline
    results take precedence.

.PARAMETER NoGate
    Validate and report the measurement without failing for an over-budget result.
    Configuration and malformed-measurement failures still exit with code 2.

.OUTPUTS
    Exit code 0 on pass (or -NoGate), 1 on budget regression, and 2 when the
    manifest or measurement is missing or malformed.
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory)] [string] $ResultsPath,
    [string] $ManifestPath,
    [string] $BaselineResultsPath,
    [string] $BaselineManifestPath,
    [switch] $NoGate
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

if (-not $ManifestPath) {
    $ManifestPath = Join-Path $PSScriptRoot 'budgets/PublishBudgets.json'
}

function Exit-WithConfigurationError {
    param([string] $Message)
    [Console]::Error.WriteLine("Test-StartupBudget: $Message")
    exit 2
}

function Get-RequiredProperty {
    param(
        [object] $InputObject,
        [string] $PropertyName,
        [string] $SourceName)
    $property = $InputObject.PSObject.Properties[$PropertyName]
    if ($null -eq $property -or $null -eq $property.Value) {
        Exit-WithConfigurationError "$SourceName is missing required property '$PropertyName'."
    }
    return $property.Value
}

function Get-Median {
    param([double[]] $Values)
    $ordered = @($Values | Sort-Object)
    $middle = [int][Math]::Floor($ordered.Count / 2)
    if (($ordered.Count % 2) -eq 1) {
        return [double] $ordered[$middle]
    }
    return [double](($ordered[$middle - 1] + $ordered[$middle]) / 2.0)
}

function Format-Milliseconds {
    param([double] $Milliseconds)
    return ('{0:N1} ms' -f $Milliseconds)
}

function Format-SignedMilliseconds {
    param([double] $Milliseconds)
    $sign = if ($Milliseconds -ge 0) { '+' } else { '-' }
    return $sign + (Format-Milliseconds ([Math]::Abs($Milliseconds)))
}

if (-not (Test-Path -LiteralPath $ManifestPath -PathType Leaf)) {
    Exit-WithConfigurationError "Budget manifest not found: $ManifestPath"
}
if (-not (Test-Path -LiteralPath $ResultsPath -PathType Leaf)) {
    Exit-WithConfigurationError "Startup results not found: $ResultsPath"
}

try {
    $manifest = Get-Content -Raw -LiteralPath $ManifestPath | ConvertFrom-Json
    $measurement = Get-Content -Raw -LiteralPath $ResultsPath | ConvertFrom-Json
}
catch {
    Exit-WithConfigurationError "Could not parse manifest or startup results: $($_.Exception.Message)"
}

$schemaVersion = [int](Get-RequiredProperty $measurement 'schemaVersion' $ResultsPath)
if ($schemaVersion -ne 1) {
    Exit-WithConfigurationError "Unsupported startup result schemaVersion $schemaVersion; expected 1."
}

$measurementName = [string](Get-RequiredProperty $measurement 'measurement' $ResultsPath)
if (-not [string]::Equals(
        $measurementName,
        'boot-to-interactive',
        [System.StringComparison]::Ordinal)) {
    Exit-WithConfigurationError "Startup result measurement must be 'boot-to-interactive'; found '$measurementName'."
}

$browserEngine = [string](Get-RequiredProperty $measurement 'browserEngine' $ResultsPath)
if (-not [string]::Equals(
        $browserEngine,
        'Chromium',
        [System.StringComparison]::Ordinal)) {
    Exit-WithConfigurationError "Startup measurement browserEngine must be 'Chromium'; found '$browserEngine'."
}

$sampleName = [string](Get-RequiredProperty $measurement 'sample' $ResultsPath)
$warmupRuns = [int](Get-RequiredProperty $measurement 'warmupRuns' $ResultsPath)
$measuredRuns = [int](Get-RequiredProperty $measurement 'measuredRuns' $ResultsPath)
$reportedMedian = [double](Get-RequiredProperty $measurement 'medianStartupMilliseconds' $ResultsPath)
$startupValues = @(
    Get-RequiredProperty $measurement 'startupMilliseconds' $ResultsPath |
        ForEach-Object { [double] $_ }
)

if ($warmupRuns -lt 1) {
    Exit-WithConfigurationError 'Startup measurement must include at least one warm-up run.'
}
if ($measuredRuns -lt 10) {
    Exit-WithConfigurationError "Startup measurement has $measuredRuns measured run(s); at least 10 post-warm-up runs are required."
}
if ($startupValues.Count -ne $measuredRuns) {
    Exit-WithConfigurationError "startupMilliseconds contains $($startupValues.Count) value(s), but measuredRuns is $measuredRuns."
}
if (@($startupValues | Where-Object { $_ -lt 0 -or [double]::IsNaN($_) -or [double]::IsInfinity($_) }).Count -gt 0) {
    Exit-WithConfigurationError 'startupMilliseconds must contain only finite, non-negative values.'
}

$calculatedMedian = Get-Median $startupValues
if ([Math]::Abs($calculatedMedian - $reportedMedian) -gt 0.05) {
    Exit-WithConfigurationError ("Reported median {0} does not match the calculated median {1}." -f `
            (Format-Milliseconds $reportedMedian),
            (Format-Milliseconds $calculatedMedian))
}

$samples = @(Get-RequiredProperty $manifest 'samples' $ManifestPath)
$sample = $samples | Where-Object { $_.name -eq $sampleName } | Select-Object -First 1
if ($null -eq $sample) {
    Exit-WithConfigurationError "No manifest sample named '$sampleName'."
}
$budgetMilliseconds = [double](Get-RequiredProperty $sample 'startupBudgetMilliseconds' $ManifestPath)
$toleranceProperty = $sample.PSObject.Properties['startupToleranceMilliseconds']
$toleranceMilliseconds = if ($null -eq $toleranceProperty) { 0.0 } else { [double] $toleranceProperty.Value }
if ($budgetMilliseconds -le 0 -or $toleranceMilliseconds -lt 0) {
    Exit-WithConfigurationError 'startupBudgetMilliseconds must be positive and startupToleranceMilliseconds cannot be negative.'
}

$baselineProperty = $sample.PSObject.Properties['startupBaselineMilliseconds']
$baselineMilliseconds = if ($null -eq $baselineProperty -or $null -eq $baselineProperty.Value) {
    $null
}
else {
    [double] $baselineProperty.Value
}
if ($BaselineManifestPath) {
    if (-not (Test-Path -LiteralPath $BaselineManifestPath -PathType Leaf)) {
        Exit-WithConfigurationError "Baseline manifest not found: $BaselineManifestPath"
    }
    try {
        $baselineManifest = Get-Content -Raw -LiteralPath $BaselineManifestPath | ConvertFrom-Json
        $baselineManifestSamples = @(Get-RequiredProperty `
            $baselineManifest `
            'samples' `
            $BaselineManifestPath)
        $baselineManifestSample = $baselineManifestSamples |
            Where-Object { $_.name -eq $sampleName } |
            Select-Object -First 1
        $baselineManifestProperty = if ($null -ne $baselineManifestSample) {
            $baselineManifestSample.PSObject.Properties['startupBaselineMilliseconds']
        }
        else {
            $null
        }
        if ($null -ne $baselineManifestProperty -and $null -ne $baselineManifestProperty.Value) {
            $baselineMilliseconds = [double] $baselineManifestSample.startupBaselineMilliseconds
        }
    }
    catch {
        Exit-WithConfigurationError "Could not parse baseline manifest: $($_.Exception.Message)"
    }
}
if ($BaselineResultsPath) {
    if (-not (Test-Path -LiteralPath $BaselineResultsPath -PathType Leaf)) {
        Exit-WithConfigurationError "Baseline startup results not found: $BaselineResultsPath"
    }
    try {
        $baseline = Get-Content -Raw -LiteralPath $BaselineResultsPath | ConvertFrom-Json
        $baselineSchemaVersion = [int](Get-RequiredProperty `
            $baseline `
            'schemaVersion' `
            $BaselineResultsPath)
        if ($baselineSchemaVersion -ne 1) {
            Exit-WithConfigurationError "Unsupported baseline startup result schemaVersion $baselineSchemaVersion; expected 1."
        }
        $baselineMeasurementName = [string](Get-RequiredProperty `
            $baseline `
            'measurement' `
            $BaselineResultsPath)
        if (-not [string]::Equals(
                $baselineMeasurementName,
                $measurementName,
                [System.StringComparison]::Ordinal)) {
            Exit-WithConfigurationError "Baseline measurement '$baselineMeasurementName' does not match '$measurementName'."
        }
        $baselineBrowserEngine = [string](Get-RequiredProperty `
            $baseline `
            'browserEngine' `
            $BaselineResultsPath)
        if (-not [string]::Equals(
                $baselineBrowserEngine,
                $browserEngine,
                [System.StringComparison]::Ordinal)) {
            Exit-WithConfigurationError "Baseline browserEngine '$baselineBrowserEngine' does not match '$browserEngine'."
        }
        $baselineSample = [string](Get-RequiredProperty $baseline 'sample' $BaselineResultsPath)
        if ($baselineSample -ne $sampleName) {
            Exit-WithConfigurationError "Baseline sample '$baselineSample' does not match '$sampleName'."
        }
        $baselineMilliseconds = [double](Get-RequiredProperty `
            $baseline `
            'medianStartupMilliseconds' `
            $BaselineResultsPath)
        if ($baselineMilliseconds -lt 0 -or
            [double]::IsNaN($baselineMilliseconds) -or
            [double]::IsInfinity($baselineMilliseconds)) {
            Exit-WithConfigurationError 'Baseline medianStartupMilliseconds must be finite and non-negative.'
        }
    }
    catch {
        Exit-WithConfigurationError "Could not parse baseline startup results: $($_.Exception.Message)"
    }
}

$effectiveCeiling = $budgetMilliseconds + $toleranceMilliseconds
$deltaOverCeiling = $calculatedMedian - $effectiveCeiling
$withinBudget = $deltaOverCeiling -le 0
$baseDeltaText = 'n/a'
if ($null -ne $baselineMilliseconds) {
    $baseDeltaText = Format-SignedMilliseconds ($calculatedMedian - $baselineMilliseconds)
}

Write-Host ''
Write-Host 'Viu startup-time budget report (headless Chromium)'
Write-Host '==================================================='
Write-Host "Sample:             $sampleName"
Write-Host "Browser engine:     $browserEngine"
Write-Host "Runs:               $warmupRuns warm-up + $measuredRuns measured"
Write-Host "Actual median:      $(Format-Milliseconds $calculatedMedian)"
Write-Host "Budget:             $(Format-Milliseconds $budgetMilliseconds)"
Write-Host "Noise tolerance:    $(Format-Milliseconds $toleranceMilliseconds)"
Write-Host "Effective ceiling:  $(Format-Milliseconds $effectiveCeiling)"
Write-Host "Delta vs base:      $baseDeltaText"
Write-Host "Result:             $(if ($withinBudget) { 'PASS' } else { 'FAIL' })"

if (-not $withinBudget) {
    $failure = "${sampleName} startupBudgetMilliseconds exceeded by $(Format-Milliseconds $deltaOverCeiling) " +
        "(actual $(Format-Milliseconds $calculatedMedian); budget $(Format-Milliseconds $budgetMilliseconds); " +
        "tolerance $(Format-Milliseconds $toleranceMilliseconds); delta vs base $baseDeltaText)"
    Write-Host "::error title=Startup budget exceeded::$failure"
    Write-Host "ERROR: $failure"
    Write-Host "Reduce startup work or explicitly update $ManifestPath with reviewed measurement provenance."
}

if ($NoGate) {
    exit 0
}
exit ($withinBudget ? 0 : 1)
