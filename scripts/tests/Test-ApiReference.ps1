<#
.SYNOPSIS
    Runs offline regression checks for API-reference clause resolution ([V01.01.13.04]).
#>
[CmdletBinding()]
param()

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
Import-Module (Join-Path $PSScriptRoot '../modules/ViuApiReference.psm1') -Force

$repository = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$fixtureRoot = Join-Path $repository ('_out/api-reference-tests/' + [guid]::NewGuid().ToString('N'))
[void][System.IO.Directory]::CreateDirectory($fixtureRoot)
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

try {
    $specification = @'
# Specification

`[RND-FLAGS-1]` Stable flags.

````text
`[FAKE-1]` Not a definition.
```
`[FAKE-2]` Still inside the longer fence.
````

~~~text
`[FAKE-3]` Tilde example.
~~~

`[RTR-12]` Route behavior.
An inline `[FAKE-4]` citation is not a definition.
`[FAKE-5]` Continuation of that paragraph is not a definition.
'@
    $specificationPath = Join-Path $fixtureRoot 'SPECIFICATION.md'
    [System.IO.File]::WriteAllText($specificationPath, $specification)
    $clauses = New-ViuSpecificationMap -SpecificationPath $specificationPath
    Assert-Condition ($clauses.Count -eq 2) 'Only normative paragraph-start definitions enter the map.'
    Assert-Condition ($clauses['RND-FLAGS-1'] -ceq 'clause-rnd-flags-1') 'Clause anchors are stable lowercase identifiers.'
    $converted = Convert-ViuSpecification -Text $specification -ClauseMap $clauses
    Assert-Condition ($converted.Contains('<a id="clause-rnd-flags-1"></a>')) 'Staged specification receives an explicit anchor.'
    Assert-Condition ([regex]::Matches($converted, '<a id="clause-').Count -eq 2) 'Code examples do not receive anchors.'
    Assert-Condition ([System.IO.File]::ReadAllText($specificationPath) -ceq $specification) 'Source specification is unchanged.'

    $duplicatePath = Join-Path $fixtureRoot 'duplicate.md'
    [System.IO.File]::WriteAllText($duplicatePath, $specification + "`n`n" + '`[RTR-12]` Duplicate.')
    Assert-Failure { New-ViuSpecificationMap -SpecificationPath $duplicatePath } '*Duplicate specification clause [RTR-12]*'.Replace('[', '`[').Replace(']', '`]')
    Assert-Failure { Convert-ViuSpecification -Text ($specification + "`n`n" + '`[NEW-1]` New.') -ClauseMap $clauses } '*Unknown specification clause definition*'

    $sourcePath = Join-Path $fixtureRoot 'source.xml'
    $destinationPath = Join-Path $fixtureRoot 'converted.xml'
    $documentation = @'
<?xml version="1.0"?>
<doc><members><member name="T:Example">
<summary>A &amp; B &lt; C: [RND-FLAGS-1], <c>[RTR-12]</c>, <c>before [RTR-12] after</c>. [V01.01.13.04]</summary>
<remarks><see href="https://example.invalid/[ATTR-1]">An external link</see><code>var value = "[RTR-12] &amp; &lt;";</code><pre>[RTR-12]</pre></remarks>
<!-- [COMMENT-1] -->
</member></members></doc>
'@
    [System.IO.File]::WriteAllText($sourcePath, $documentation)
    $count = Convert-ViuDocumentationXml -SourcePath $sourcePath -DestinationPath $destinationPath -ClauseMap $clauses
    $result = [xml][System.IO.File]::ReadAllText($destinationPath)
    Assert-Condition ($count -eq 3) 'Bare, inline-code, and mixed inline-code citations all convert.'
    Assert-Condition ($result.SelectNodes('//summary/see').Count -eq 3) 'Citations render as links outside inline code markup.'
    Assert-Condition ($result.SelectSingleNode('//summary/see').GetAttribute('href') -ceq '../docs/SPECIFICATION.md#clause-rnd-flags-1') 'Links address the published specification.'
    Assert-Condition ($result.SelectSingleNode('//summary').InnerText.StartsWith('A & B < C:')) 'XML escaping survives conversion.'
    Assert-Condition ($result.SelectSingleNode('//code').InnerText -ceq 'var value = "[RTR-12] & <";') 'Code examples are preserved.'
    Assert-Condition ($result.SelectSingleNode('//pre').InnerText -ceq '[RTR-12]') 'Preformatted examples are preserved.'
    Assert-Condition ($result.SelectSingleNode('//summary').InnerText.Contains('[V01.01.13.04]')) 'WBS references remain prose.'
    Assert-Condition ([System.IO.File]::ReadAllText($sourcePath) -ceq $documentation) 'Compiler XML source is unchanged.'

    Assert-Failure { Convert-ViuDocumentationXml -SourcePath $sourcePath -DestinationPath $sourcePath -ClauseMap $clauses } '*staged copy*'
    foreach ($unknown in @('<summary>[UNKNOWN-9]</summary>', '<code>[UNKNOWN-9]</code>')) {
        [System.IO.File]::WriteAllText($sourcePath, '<doc><members><member name="T:Unknown">' + $unknown + '</member></members></doc>')
        Assert-Failure { Convert-ViuDocumentationXml -SourcePath $sourcePath -DestinationPath $destinationPath -ClauseMap $clauses } '*Unknown specification clause*'
    }
    $libraryTable = @'
### YamlMime:TableOfContent
items:
- uid: Assimalign.Viu.Components
  name: Components
  items:
  - uid: Assimalign.Viu.Components.PatchFlags
    name: PatchFlags
memberLayout: SamePage
'@
    $templateTable = @'
### YamlMime:TableOfContent
items:
- uid: Assimalign.Viu.Syntax.Templates
  name: Templates
memberLayout: SamePage
'@
    $merged = Merge-ViuApiReferenceTableOfContents -TablesOfContents @($libraryTable, $templateTable)
    Assert-Condition ($merged.Contains('- uid: Assimalign.Viu.Components') -and $merged.Contains('- uid: Assimalign.Viu.Syntax.Templates')) 'Both metadata groups remain in navigation.'
    Assert-Condition ($merged.Contains('  - uid: Assimalign.Viu.Components.PatchFlags')) 'Nested navigation items are preserved.'
    Assert-Condition ([regex]::Matches($merged, '(?m)^### YamlMime:TableOfContent$').Count -eq 1) 'Merged navigation has one MIME header.'
    Assert-Condition ([regex]::Matches($merged, '(?m)^items:$').Count -eq 1) 'Merged navigation has one root items sequence.'
    Assert-Condition ([regex]::Matches($merged, '(?m)^memberLayout: SamePage$').Count -eq 1) 'Merged navigation has one root member layout.'
    foreach ($unexpected in @(
        $templateTable.Replace('memberLayout: SamePage', 'memberLayout: SeparatePages'),
        $templateTable.Replace('memberLayout: SamePage', "unknown: value`nmemberLayout: SamePage"),
        $templateTable.Replace('### YamlMime:TableOfContent', '### YamlMime:Unexpected'),
        $templateTable.Replace('memberLayout: SamePage', '')
    )) {
        Assert-Failure { Merge-ViuApiReferenceTableOfContents -TablesOfContents @($libraryTable, $unexpected) } '*Unexpected DocFX table-of-contents*'
    }
    Write-Host "API-reference preprocessing checks passed: $checks."
} finally {
    # Delete only this invocation's fixture directory after checking its resolved workspace boundary.
    $expectedParent = [System.IO.Path]::GetFullPath((Join-Path $repository '_out/api-reference-tests'))
    $resolvedFixture = [System.IO.Path]::GetFullPath($fixtureRoot)
    if ([System.IO.Path]::GetDirectoryName($resolvedFixture) -cne $expectedParent) {
        throw "Refusing to clean unexpected fixture path '$resolvedFixture'."
    }
    Remove-Item -LiteralPath $resolvedFixture -Recurse -Force
}
