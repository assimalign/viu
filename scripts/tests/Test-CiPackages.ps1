<#
.SYNOPSIS
    Runs offline checks for the CI package channel: version derivation, version parsing, prune
    selection, and a prune run against a recorded GitHub API.

.DESCRIPTION
    .github/workflows/ci-packages.yml derives every CI version with Get-ViuCiPackageVersion, and
    .github/workflows/ci-packages-prune.yml deletes from the shared feed with
    Invoke-ViuCiPackagePrune. These checks pin the two promises the channel makes: a CI build always
    sorts below the release it builds toward, and the pruner never selects a release version,
    whatever it is asked to do. Nothing here touches the network; the prune run replaces
    Invoke-WebRequest inside the feed module with an in-memory feed.
#>
[CmdletBinding()]
param()

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
Import-Module (Join-Path $PSScriptRoot '../modules/ViuPackaging.psm1') -Force
Import-Module (Join-Path $PSScriptRoot '../modules/ViuPackageFeed.psm1') -Force

$checks = 0

function Assert-Condition {
    param([bool] $Condition, [string] $Message)
    if (-not $Condition) { throw "FAIL: $Message" }
    $script:checks++
}

function Assert-Failure {
    param([scriptblock] $Action, [string] $MessagePattern)
    $failed = $false
    try { & $Action } catch {
        if ($_.Exception.Message -notlike $MessagePattern) { throw }
        $failed = $true
    }
    Assert-Condition $failed "Expected failure matching '$MessagePattern'."
}

function Compare-NuGetVersion {
    # SemVer 2.0 precedence for the shapes used here: numeric identifiers compare as numbers,
    # alphanumeric ones ordinally ignoring case (as NuGet does), and a longer list wins a tie.
    param([string] $Left, [string] $Right)
    $leftCore, $leftSuffix = $Left.Split('-', 2)
    $rightCore, $rightSuffix = $Right.Split('-', 2)
    $coreOrder = ([version] $leftCore).CompareTo([version] $rightCore)
    if ($coreOrder -ne 0) { return $coreOrder }
    if (-not $leftSuffix -and -not $rightSuffix) { return 0 }
    if (-not $leftSuffix) { return 1 }
    if (-not $rightSuffix) { return -1 }
    $leftPart = $leftSuffix.Split('.')
    $rightPart = $rightSuffix.Split('.')
    for ($index = 0; $index -lt [Math]::Min($leftPart.Count, $rightPart.Count); $index++) {
        $leftNumeric = $leftPart[$index] -match '^[0-9]+$'
        $rightNumeric = $rightPart[$index] -match '^[0-9]+$'
        if ($leftNumeric -and $rightNumeric) {
            $order = ([int] $leftPart[$index]).CompareTo([int] $rightPart[$index])
        }
        elseif ($leftNumeric) { $order = -1 }
        elseif ($rightNumeric) { $order = 1 }
        else { $order = [string]::Compare($leftPart[$index], $rightPart[$index], [StringComparison]::OrdinalIgnoreCase) }
        if ($order -ne 0) { return [Math]::Sign($order) }
    }
    return $leftPart.Count.CompareTo($rightPart.Count)
}

# --- Get-ViuCiPackageVersion -------------------------------------------------------------------

$main = Get-ViuCiPackageVersion -Version '10.0.0-preview.1' -BuildNumber 57
Assert-Condition ($main.Version -eq '10.0.0-preview.0.ci.57') "main build version, got $($main.Version)"
Assert-Condition ($main.BaseVersion -eq '10.0.0-preview.0') 'main build base'
Assert-Condition ($main.Channel -eq 'ci') 'main build channel'
Assert-Condition ($main.VersionPrefix -eq '10.0.0' -and $main.VersionSuffix -eq 'preview.0.ci.57') 'main build prefix and suffix'

$pullRequest = Get-ViuCiPackageVersion -Version '10.0.0-preview.1' -BuildNumber 58 -PullRequestNumber 380
Assert-Condition ($pullRequest.Version -eq '10.0.0-preview.0.pr.380.58') "pull request version, got $($pullRequest.Version)"
Assert-Condition ($pullRequest.Channel -eq 'pr') 'pull request channel'

$afterBump = Get-ViuCiPackageVersion -Version '10.0.0-preview.2' -BuildNumber 90
Assert-Condition ($afterBump.Version -eq '10.0.0-preview.1.ci.90') 'post-bump base decrements the new line'

$releaseCandidate = Get-ViuCiPackageVersion -Version '10.0.1-rc.3' -BuildNumber 4
Assert-Condition ($releaseCandidate.Version -eq '10.0.1-rc.2.ci.4') 'release-candidate lines derive the same way'

foreach ($case in @(
        @{ Canonical = '10.0.0-preview.1'; Build = 57 },
        @{ Canonical = '10.0.0-preview.2'; Build = 2147483647 },
        @{ Canonical = '10.0.1-rc.1'; Build = 1 })) {
    $ci = Get-ViuCiPackageVersion -Version $case.Canonical -BuildNumber $case.Build
    $pr = Get-ViuCiPackageVersion -Version $case.Canonical -BuildNumber $case.Build -PullRequestNumber 1
    Assert-Condition ((Compare-NuGetVersion $ci.Version $case.Canonical) -lt 0) "$($ci.Version) sorts below $($case.Canonical)"
    Assert-Condition ((Compare-NuGetVersion $pr.Version $case.Canonical) -lt 0) "$($pr.Version) sorts below $($case.Canonical)"
}
Assert-Condition ((Compare-NuGetVersion '10.0.0-preview.0.ci.10' '10.0.0-preview.0.ci.9') -gt 0) 'builds order numerically within a channel'
Assert-Condition ((Compare-NuGetVersion '10.0.0-preview.1.ci.1' '10.0.0-preview.1') -gt 0) 'post-bump builds sort above the released line'

Assert-Failure { Get-ViuCiPackageVersion -Version '10.0.0' -BuildNumber 1 } '*is stable*'
Assert-Failure { Get-ViuCiPackageVersion -Version '10.0.0-preview' -BuildNumber 1 } '*positive integer prerelease identifier*'
Assert-Failure { Get-ViuCiPackageVersion -Version '10.0.0-preview.0' -BuildNumber 1 } '*positive integer prerelease identifier*'
Assert-Failure { Get-ViuCiPackageVersion -Version '10.0.0-preview.01' -BuildNumber 1 } '*leading zero*'
Assert-Failure { Get-ViuCiPackageVersion -Version '10.0.0-ci.1' -BuildNumber 1 } "*reserved 'ci'*"
Assert-Failure { Get-ViuCiPackageVersion -Version '10.0.0-preview.1.local.1' -BuildNumber 1 } "*reserved 'local'*"
Assert-Failure { Get-ViuCiPackageVersion -Version 'v10.0.0-preview.1' -BuildNumber 1 } '*must be SemVer*'
Assert-Failure { Get-ViuCiPackageVersion -Version '10.0.0-preview.1' -BuildNumber 0 } '*BuildNumber*'
Assert-Failure { Get-ViuCiPackageVersion -Version '10.0.0-preview.1' -BuildNumber 1 -PullRequestNumber 0 } '*PullRequestNumber*'

# --- ConvertFrom-ViuCiPackageVersion -----------------------------------------------------------

$parsedMain = ConvertFrom-ViuCiPackageVersion -Version $main.Version
Assert-Condition ($parsedMain.Channel -eq 'ci' -and $parsedMain.BuildNumber -eq 57 -and $null -eq $parsedMain.PullRequestNumber) 'parses a main build'
Assert-Condition ($parsedMain.BaseVersion -eq '10.0.0-preview.0') 'parses a main build base'
$parsedPullRequest = ConvertFrom-ViuCiPackageVersion -Version $pullRequest.Version
Assert-Condition ($parsedPullRequest.Channel -eq 'pr' -and $parsedPullRequest.PullRequestNumber -eq 380 -and $parsedPullRequest.BuildNumber -eq 58) 'parses a pull request build'
$upperCase = ConvertFrom-ViuCiPackageVersion -Version '10.0.0-preview.0.CI.5'
Assert-Condition ($null -ne $upperCase -and $upperCase.Channel -eq 'ci') 'reads the channel label case-insensitively'

foreach ($notCi in @(
        '',
        '10.0.0',
        '10.0.0-preview.1',
        '10.0.0-preview.1.local',
        '10.0.0-ci.5',
        '10.0.0-preview.ci.5',
        '10.0.0-preview.0.ci.05',
        '10.0.0-preview.0.ci.0',
        '10.0.0-preview.0.ci',
        '10.0.0-preview.0.pr.15',
        '10.0.0-preview.0.pr.0.3',
        '10.0.0-preview.0.pr.015.3',
        '10.0.0-preview.0.ci.2147483648',
        '10.0.0-local.0.ci.4',
        '10.0.0-preview.0.ci.4+metadata')) {
    Assert-Condition ($null -eq (ConvertFrom-ViuCiPackageVersion -Version $notCi)) "'$notCi' is not a CI build"
}

# --- Select-ViuCiPackageVersionToPrune ---------------------------------------------------------

function New-Version {
    param([string] $Package, [string] $Version, [int] $Id, [string] $CreatedAt = '2026-10-01T00:00:00Z')
    [pscustomobject]@{ PackageName = $Package; VersionId = $Id; Version = $Version; CreatedAt = $CreatedAt }
}

$feed = @(
    New-Version 'Assimalign.Viu.Core' '10.0.0-preview.1' 1
    New-Version 'Assimalign.Viu.Core' '10.0.0-preview.0.pr.15.3' 2
    New-Version 'Assimalign.Viu.Core' '10.0.0-preview.0.pr.152.4' 3
    New-Version 'Assimalign.Viu.Sdk' '10.0.0-preview.0.pr.15.3' 4
    New-Version 'Assimalign.Viu.Core' '10.0.0-preview.0.ci.1' 5 '2026-10-01T01:00:00Z'
    New-Version 'Assimalign.Viu.Sdk' '10.0.0-preview.0.ci.1' 6 '2026-10-01T01:00:00Z'
    New-Version 'Assimalign.Viu.Core' '10.0.0-preview.0.ci.2' 7 '2026-10-01T02:00:00Z'
    New-Version 'Assimalign.Viu.Core' '10.0.0-preview.0.ci.3' 8 '2026-10-01T03:00:00Z'
    New-Version 'Assimalign.Viu.Core' '10.0.0-preview.1.ci.9' 9 '2026-09-01T00:00:00Z'
)

$closedOnly = Select-ViuCiPackageVersionToPrune -PackageVersion $feed -ClosedPullRequestNumber @(15)
Assert-Condition ($closedOnly.Count -eq 2) "closing #15 selects its two versions, got $($closedOnly.Count)"
Assert-Condition (@($closedOnly | Where-Object { $_.Version -like '*.pr.152.*' }).Count -eq 0) 'closing #15 never selects #152'
Assert-Condition ($closedOnly[0].PackageName -eq 'Assimalign.Viu.Sdk') 'the SDK is deleted first within a build'

$retention = Select-ViuCiPackageVersionToPrune -PackageVersion $feed -MainBuildCount 2
Assert-Condition ($retention.Count -eq 2) "keeping two main builds of preview.0 selects build 1 on both packages, got $($retention.Count)"
Assert-Condition (@($retention | Where-Object { $_.BuildNumber -ne 1 }).Count -eq 0) 'only the oldest main build is selected'
Assert-Condition ($retention[0].PackageName -eq 'Assimalign.Viu.Sdk') 'the SDK leads the expired build'
Assert-Condition (@($retention | Where-Object { $_.BaseVersion -eq '10.0.0-preview.1' }).Count -eq 0) 'each base keeps its own builds'

$restarted = @(
    New-Version 'Assimalign.Viu.Core' '10.0.0-preview.0.ci.40' 1 '2026-10-01T00:00:00Z'
    New-Version 'Assimalign.Viu.Core' '10.0.0-preview.0.ci.41' 2 '2026-10-02T00:00:00Z'
    New-Version 'Assimalign.Viu.Core' '10.0.0-preview.0.ci.1' 3 '2026-10-03T00:00:00Z'
)
$restartedSelection = Select-ViuCiPackageVersionToPrune -PackageVersion $restarted -MainBuildCount 2
Assert-Condition ($restartedSelection.Count -eq 1 -and $restartedSelection[0].BuildNumber -eq 40) 'retention ranks builds by publish time, not run number'

$everything = Select-ViuCiPackageVersionToPrune `
    -PackageVersion $feed `
    -ClosedPullRequestNumber @(1..200) `
    -MainBuildCount 1
Assert-Condition (@($everything | Where-Object { $_.Version -eq '10.0.0-preview.1' }).Count -eq 0) 'the most aggressive selection never touches a release version'
Assert-Condition (@($everything | Where-Object { $null -eq (ConvertFrom-ViuCiPackageVersion -Version $_.Version) }).Count -eq 0) 'every selected version is a CI build'

Assert-Failure { Select-ViuCiPackageVersionToPrune -PackageVersion @([pscustomobject]@{ PackageName = 'Assimalign.Viu.Core'; Version = '10.0.0-preview.0.ci.1' }) } "*missing 'VersionId'*"
Assert-Failure {
    Select-ViuCiPackageVersionToPrune -PackageVersion @(
        New-Version 'Assimalign.Viu.Core' '10.0.0-preview.0.ci.1' 1 'not a date'
        New-Version 'Assimalign.Viu.Core' '10.0.0-preview.0.ci.2' 2
    ) -MainBuildCount 1
} '*no parseable CreatedAt*'
Assert-Condition ((Select-ViuCiPackageVersionToPrune -PackageVersion @()).Count -eq 0) 'an empty feed selects nothing'

# --- Get-ViuPackageId is what ci-packages.yml pushes -------------------------------------------

$packageId = @(Get-ViuPackageId)
Assert-Condition ($packageId.Count -eq 19) "the release inventory is 19 packages, got $($packageId.Count)"
Assert-Condition ($packageId -contains 'Assimalign.Viu.Sdk' -and $packageId -contains 'Assimalign.Viu.Sdk.Browser') 'the inventory names both SDKs'
Assert-Condition ($packageId -contains 'Assimalign.Viu.App.Browser.Runtime.browser-wasm') 'the inventory names the Browser runtime pack'

# --- Invoke-ViuCiPackagePrune against a recorded feed ------------------------------------------

$feedModule = Get-Module ViuPackageFeed
& $feedModule {
    $script:RecordedRequest = [System.Collections.Generic.List[string]]::new()
    $script:RecordedFeed = @{}
    $script:RecordedPullRequestState = @{}
    $script:RecordedRemaining = 900

    function script:Invoke-WebRequest {
        [CmdletBinding()]
        param([string] $Uri, [string] $Method, [hashtable] $Headers, [switch] $SkipHttpErrorCheck, [int] $TimeoutSec)

        $path = $Uri.Substring('https://api.github.com'.Length)
        $script:RecordedRequest.Add("$Method $path")
        $script:RecordedRemaining--
        $respond = {
            param([int] $Status, [object] $Body)
            [pscustomobject]@{
                StatusCode = $Status
                Headers    = @{ 'x-ratelimit-remaining' = [string] $script:RecordedRemaining; 'x-ratelimit-reset' = '1792000000' }
                # -InputObject keeps a one-element array an array, as the API returns it.
                Content    = if ($null -eq $Body) { '' } else { ConvertTo-Json -InputObject $Body -Depth 8 }
            }
        }

        if ($path -match '^/orgs/assimalign/packages\?') {
            return & $respond 403 @{ message = 'Resource not accessible by integration' }
        }

        if ($path -match '^/orgs/assimalign/packages/nuget/(?<name>[^/?]+)/versions\?') {
            $name = [System.Uri]::UnescapeDataString($Matches['name'])
            if (-not $script:RecordedFeed.ContainsKey($name)) {
                return & $respond 404 @{ message = 'Package not found.' }
            }

            return & $respond 200 @($script:RecordedFeed[$name] | ForEach-Object { @{ id = $_.Id; name = $_.Version; created_at = $_.CreatedAt } })
        }

        if ($path -match '^/repos/assimalign/viu/pulls/(?<number>[0-9]+)$') {
            return & $respond 200 @{ state = $script:RecordedPullRequestState[[int] $Matches['number']] }
        }

        if ($Method -eq 'DELETE' -and $path -match '^/orgs/assimalign/packages/nuget/(?<name>[^/]+)/versions/(?<id>[0-9]+)$') {
            $name = [System.Uri]::UnescapeDataString($Matches['name'])
            $script:RecordedFeed[$name] = @($script:RecordedFeed[$name] | Where-Object { $_.Id -ne [int] $Matches['id'] })
            return & $respond 204 $null
        }

        if ($Method -eq 'DELETE' -and $path -match '^/orgs/assimalign/packages/nuget/(?<name>[^/]+)$') {
            $script:RecordedFeed.Remove([System.Uri]::UnescapeDataString($Matches['name']))
            return & $respond 204 $null
        }

        throw "Unexpected request: $Method $path"
    }
}

function Reset-RecordedFeed {
    & $feedModule {
        $script:RecordedRequest.Clear()
        $script:RecordedRemaining = 900
        $script:RecordedPullRequestState = @{ 7 = 'closed'; 8 = 'open' }
        $script:RecordedFeed = @{
            'Assimalign.Viu.Sdk'       = @(
                @{ Id = 101; Version = '10.0.0-preview.1'; CreatedAt = '2026-09-30T00:00:00Z' }
                @{ Id = 102; Version = '10.0.0-preview.0.ci.1'; CreatedAt = '2026-10-01T01:00:00Z' }
                @{ Id = 103; Version = '10.0.0-preview.0.ci.2'; CreatedAt = '2026-10-01T02:00:00Z' }
                @{ Id = 104; Version = '10.0.0-preview.0.ci.3'; CreatedAt = '2026-10-01T03:00:00Z' }
                @{ Id = 105; Version = '10.0.0-preview.0.pr.7.4'; CreatedAt = '2026-10-01T04:00:00Z' }
                @{ Id = 106; Version = '10.0.0-preview.0.pr.8.5'; CreatedAt = '2026-10-01T05:00:00Z' }
            )
            'Assimalign.Viu.Core'      = @(
                @{ Id = 201; Version = '10.0.0-preview.1'; CreatedAt = '2026-09-30T00:00:00Z' }
                @{ Id = 202; Version = '10.0.0-preview.0.ci.1'; CreatedAt = '2026-10-01T01:00:00Z' }
                @{ Id = 203; Version = '10.0.0-preview.0.ci.2'; CreatedAt = '2026-10-01T02:00:00Z' }
                @{ Id = 204; Version = '10.0.0-preview.0.ci.3'; CreatedAt = '2026-10-01T03:00:00Z' }
                @{ Id = 205; Version = '10.0.0-preview.0.pr.7.4'; CreatedAt = '2026-10-01T04:00:00Z' }
            )
            # Only closed pull request 7 ever published this package, so it goes as a whole.
            'Assimalign.Viu.Templates' = @(
                @{ Id = 301; Version = '10.0.0-preview.0.pr.7.4'; CreatedAt = '2026-10-01T04:00:00Z' }
            )
        }
    }
}

$pruneArguments = @{
    Organization               = 'assimalign'
    Repository                 = 'assimalign/viu'
    PackageName                = @('Assimalign.Viu.Sdk', 'Assimalign.Viu.Core', 'Assimalign.Viu.Templates', 'Assimalign.Viu.Router')
    Token                      = 'offline-test-token'
    MainBuildCount             = 2
    DeleteIntervalMilliseconds = 0
}

Reset-RecordedFeed
$summaryPath = Join-Path ([System.IO.Path]::GetTempPath()) "viu-ci-prune-$([guid]::NewGuid().ToString('N')).md"
try {
    $dryRun = Invoke-ViuCiPackagePrune @pruneArguments -SummaryPath $summaryPath -WhatIf
    $dryRequest = & $feedModule { $script:RecordedRequest.ToArray() }
    Assert-Condition (@($dryRequest | Where-Object { $_ -like 'DELETE *' }).Count -eq 0) 'a dry run sends no DELETE'
    Assert-Condition ($dryRun.WouldDelete -eq 5) "a dry run reports five versions it would delete, got $($dryRun.WouldDelete)"
    Assert-Condition ((Get-Content -LiteralPath $summaryPath -Raw) -match 'dry run, nothing deleted') 'a dry run writes its summary'
}
finally {
    Remove-Item -LiteralPath $summaryPath -Force -ErrorAction SilentlyContinue
}

Reset-RecordedFeed
$run = Invoke-ViuCiPackagePrune @pruneArguments
$deleteRequest = @(& $feedModule { $script:RecordedRequest.ToArray() } | Where-Object { $_ -like 'DELETE *' })
Assert-Condition ($run.Selected -eq 5) "selects pr.7 on three packages and ci.1 on two, got $($run.Selected)"
Assert-Condition ($run.Deleted -eq 5 -and $run.Deferred -eq 0 -and $run.Failed.Count -eq 0) 'deletes the whole selection'
Assert-Condition ($run.ClosedPullRequests.Count -eq 1 -and $run.ClosedPullRequests[0] -eq 7) 'only closed pull request 7 is pruned'
Assert-Condition ($deleteRequest -contains 'DELETE /orgs/assimalign/packages/nuget/Assimalign.Viu.Templates') 'a package with only selected versions is deleted whole'
Assert-Condition ($deleteRequest.Count -eq 5) "four version deletes plus one package delete, got $($deleteRequest.Count)"
Assert-Condition (@($deleteRequest | Where-Object { $_ -match '/versions/(101|201)$' }).Count -eq 0) 'release versions are never deleted'
Assert-Condition (@($deleteRequest | Where-Object { $_ -match '/versions/106$' }).Count -eq 0) 'an open pull request keeps its builds'
$sdkPullRequestIndex = [array]::IndexOf($deleteRequest, 'DELETE /orgs/assimalign/packages/nuget/Assimalign.Viu.Sdk/versions/105')
$corePullRequestIndex = [array]::IndexOf($deleteRequest, 'DELETE /orgs/assimalign/packages/nuget/Assimalign.Viu.Core/versions/205')
Assert-Condition ($sdkPullRequestIndex -ge 0 -and $sdkPullRequestIndex -lt $corePullRequestIndex) 'the SDK version goes before the library version of the same build'
$remainingSdk = & $feedModule { @($script:RecordedFeed['Assimalign.Viu.Sdk'] | ForEach-Object { $_.Version }) }
Assert-Condition (($remainingSdk -join ',') -eq '10.0.0-preview.1,10.0.0-preview.0.ci.2,10.0.0-preview.0.ci.3,10.0.0-preview.0.pr.8.5') "the SDK keeps its release, two main builds, and the open pull request, got $($remainingSdk -join ',')"

Reset-RecordedFeed
& $feedModule { $script:RecordedFeed = @{} }
Assert-Failure { Invoke-ViuCiPackagePrune @pruneArguments } '*None of the 4 Viu packages is visible*'

Reset-RecordedFeed
& $feedModule { $script:RecordedRemaining = 110 }
$budget = Invoke-ViuCiPackagePrune @pruneArguments
Assert-Condition ($budget.Deleted -eq 0 -and $null -ne $budget.DeferredReason) 'a run without the budget to finish listing defers instead of starting'

Write-Host "CI package policy: $checks checks passed." -ForegroundColor Green
