<#
.SYNOPSIS
    Runs isolated offline documentation-link regressions ([V01.01.13.05], #102).
#>
[CmdletBinding()]
param()

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$repository = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$fixtureParent = Join-Path $repository '_out/documentation-link-tests'
$fixtureRoot = Join-Path $fixtureParent ([guid]::NewGuid().ToString('N'))
$checker = Join-Path $PSScriptRoot '../Test-DocumentationLinks.ps1'
$checks = 0

function Set-FixtureFile {
    param([string] $RelativePath, [string] $Text)
    $path = Join-Path $fixtureRoot $RelativePath
    [void][System.IO.Directory]::CreateDirectory((Split-Path $path))
    [System.IO.File]::WriteAllText($path, $Text)
}

function Assert-Condition {
    param([bool] $Condition, [string] $Message)
    if (-not $Condition) { throw "FAIL: $Message" }
    $script:checks++
}

function Assert-DocumentationFailure {
    param([string] $Markdown, [string] $Expected)
    Set-FixtureFile 'README.md' $Markdown
    $failed = $false
    try { & $checker -RepositoryDirectory $fixtureRoot } catch {
        if (-not $_.Exception.Message.Contains($Expected)) { throw }
        $failed = $true
    }
    Assert-Condition $failed "Expected diagnostic containing '$Expected'."
}

try {
    Set-FixtureFile 'docs/SPECIFICATION.md' @'
# Specification

`[DOC-1]` The fixture contract.
'@
    Set-FixtureFile 'docs/reader.md' @'
# Reader

## A `code` heading &amp; punctuation!

## Repeated

## Repeated

## Repeated-1

Setext heading
--------------

<a id="stable-anchor"></a>
<a name='legacy-anchor'></a>

## Named {#custom-anchor}

````md
# Hidden heading
``` 
# Still hidden
````
'@
    Set-FixtureFile 'docs/a (b).md' '# Parentheses'
    Set-FixtureFile 'docs/picture.svg' '<svg id="drawing"></svg>'
    Set-FixtureFile 'docs/sample.txt' "First`nSecond`nThird"
    Set-FixtureFile 'libraries/Runtime/Example/docs/OVERVIEW.md' '[Specification](../../../../docs/SPECIFICATION.md)'
    Set-FixtureFile 'libraries/Runtime/Example/docs/DESIGN.md' '[Overview](OVERVIEW.md)'
    Set-FixtureFile 'sdks/Example/docs/README.md' '[Specification](../../../docs/SPECIFICATION.md)'
    Set-FixtureFile 'extensions/Example/README.md' '[Specification](../../docs/SPECIFICATION.md)'
    foreach ($generated in @('bin', 'obj', 'node_modules', '_out')) {
        Set-FixtureFile "docs/$generated/broken.md" '[Ignored](absent.md)'
    }
    Set-FixtureFile 'extensions/Example/NOT-README.md' '[Outside the requested audit](absent.md)'
    Set-FixtureFile 'libraries/Runtime/Example/README.md' '[Outside the requested audit](absent.md)'
    Set-FixtureFile 'README.md' @'
# Home

[A heading](docs/reader.md#a-code-heading--punctuation)
[Second duplicate](docs/reader.md#repeated-1)
[Collision](docs/reader.md#repeated-1-1)
[Setext](docs/reader.md#setext-heading)
[Explicit](docs/reader.md#stable-anchor)
[Named](docs/reader.md#legacy-anchor)
[Custom](docs/reader.md#custom-anchor)
[Clause](docs/SPECIFICATION.md#clause-doc-1); `[DOC-1]`; [V01.01.13.05].
[Parentheses](docs/a%20(b).md "A title")
[Escaped parentheses](docs/a%20\(b\).md)
[Angle destination](<docs/a (b).md> 'A title')
![Image](docs/picture.svg#drawing)
[Reader][reader]
[reader][]
[reader]

[reader]: docs/reader.md
[unused]: https://example.invalid/unused

<https://example.invalid/search?q=a&amp;b=c>
[Nested [label]](docs/reader.md)
[Multiline](
docs/reader.md
"title"
)
[Empty]()
[Directory](docs)

`[Code](absent.md)` and ``[Long code](absent.md) ` more``.
\[Escaped](absent.md)
<!-- [Comment](absent.md) [UNKNOWN-1] -->

    [Indented code](absent.md)

````markdown
[Fenced](absent.md) [DOC-1]
```
[Still fenced](absent.md)
````

~~~markdown
[Tilde fence](absent.md)
~~~

> ```markdown
> [Quoted fence](absent.md)
> ```
'@
    $result = & $checker -RepositoryDirectory $fixtureRoot -PassThru
    Assert-Condition ($result.Files -eq 8) 'All requested roots are audited and generated directories are excluded.'
    Assert-Condition ($result.Links -eq 26) 'Inline, image, reference, autolink, empty, and directory destinations are counted.'
    Assert-Condition ($result.Anchors -eq 9) 'Heading, duplicate, explicit, and specification fragments are validated.'
    Assert-Condition ($result.Citations -eq 3) 'Citations are checked in prose, inline code, and fenced examples.'

    Assert-DocumentationFailure "# Home`n`n[Broken](docs/absent.md)`n[Also broken](other.md)" "README.md:3: 'docs/absent.md'"
    Assert-DocumentationFailure '[Broken](docs/reader.md#missing)' "'docs/reader.md#missing' - Markdown anchor does not exist."
    Assert-DocumentationFailure '[Hidden](docs/reader.md#hidden-heading)' 'Markdown anchor does not exist.'
    Assert-DocumentationFailure '[Hidden](docs/reader.md#still-hidden)' 'Markdown anchor does not exist.'
    Assert-DocumentationFailure '[Unknown][missing]' "'[missing]' - Undefined Markdown reference label."
    Assert-DocumentationFailure '[Unused]: missing.md' "'missing.md' - Relative target does not exist."
    Assert-DocumentationFailure 'A `[UNKNOWN-3]` citation.' "'UNKNOWN-3' - Unknown specification clause."
    Assert-DocumentationFailure ('```text' + "`n[UNKNOWN-4]`n" + '```') "'UNKNOWN-4' - Unknown specification clause."
    Assert-DocumentationFailure "    [UNKNOWN-5]" "'UNKNOWN-5' - Unknown specification clause."
    Assert-DocumentationFailure '[Escape](../outside.md)' 'Relative link escapes this checkout.'
    Assert-DocumentationFailure '[Root](/docs/reader.md)' 'repository-relative forward-slash paths'
    foreach ($invalid in @('http://example.invalid', '//example.invalid', 'mailto:reader@example.invalid',
            'https:///missing-host', 'https://example.invalid/%wrong', 'https://user:password@example.invalid')) {
        Assert-DocumentationFailure "[External](<$invalid>)" 'External links must be absolute https:// URLs'
    }
    Assert-DocumentationFailure '<http://example.invalid>' 'External links must be absolute https:// URLs'
    Assert-DocumentationFailure '[Missing image](docs/missing.svg)' 'Relative target does not exist.'
    Assert-DocumentationFailure '[Wrong case](docs/reader.md#Reader)' 'Markdown anchor does not exist.'
    Assert-DocumentationFailure '[Directory fragment](docs#missing)' 'Fragment target has no readable file.'
    Assert-DocumentationFailure "- Parent`n    - [Nested missing](missing.md)" "README.md:2: 'missing.md'"
    Assert-DocumentationFailure "A paragraph`n    [Continued missing](missing.md)" "README.md:2: 'missing.md'"
    Assert-DocumentationFailure "- Parent`n`n    [Indented continuation](missing.md)" "README.md:3: 'missing.md'"
    Assert-DocumentationFailure '[![Missing image](missing.svg)](README.md)' "'missing.svg' - Relative target does not exist."
    Assert-DocumentationFailure '[![Insecure image](http://example.invalid/image.png)](README.md)' 'External links must be absolute https:// URLs'
    Assert-DocumentationFailure "[![Image](missing.svg)][reader]`n`n[reader]: README.md" "'missing.svg' - Relative target does not exist."
    Assert-DocumentationFailure '[Source](docs/sample.txt#L0)' 'ordered, positive range'
    Assert-DocumentationFailure '[Source](docs/sample.txt#L3-L2)' 'ordered, positive range'
    Assert-DocumentationFailure '[Source](docs/sample.txt#L1-L4)' 'ordered, positive range'
    Set-FixtureFile 'README.md' @'
# Navigation

> ## Quoted title

## _Emphasized_ title

[Quote](#quoted-title)
[Emphasis](#emphasized-title)
[Angle URL](<https://example.invalid>)
[![Badge](docs/picture.svg)](docs/reader.md)
[API reference](xref:Assimalign.Viu)
[Source](docs/sample.txt#L1-L3)
[ADR-0001] and [V01.01.13.05] are other numbering systems.

- Parent

      [List code](absent.md)

- Links
    [Reader][reader]

    [reader]: docs/reader.md
'@
    $result = & $checker -RepositoryDirectory $fixtureRoot -PassThru
    Assert-Condition ($result.Links -eq 13) 'Linked images, list references, and angle URLs are counted once per destination; xref stays internal.'
    Assert-Condition ($result.Anchors -eq 3) 'Quoted and emphasized headings plus positive source-line ranges resolve.'
    Assert-Condition ($result.Citations -eq 1) 'ADR and WBS numbers are not mistaken for specification clauses.'
    Write-Host "Documentation-link regression checks passed: $checks."
} finally {
    # The resolved invocation directory must be an immediate child of this checkout's test output.
    $resolvedFixture = [System.IO.Path]::GetFullPath($fixtureRoot)
    if ([System.IO.Path]::GetDirectoryName($resolvedFixture) -cne [System.IO.Path]::GetFullPath($fixtureParent)) {
        throw "Refusing to clean unexpected fixture path '$resolvedFixture'."
    }
    if (Test-Path -LiteralPath $resolvedFixture) { Remove-Item -LiteralPath $resolvedFixture -Recurse -Force }
}
