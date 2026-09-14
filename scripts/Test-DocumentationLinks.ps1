<#
.SYNOPSIS
    Checks repository documentation links without network access ([V01.01.13.05], #102).
.DESCRIPTION
    Checks Markdown links, images, reference destinations, HTTPS autolinks, heading fragments,
    explicit HTML anchors, and specification citations. Code blocks, code-span links, and HTML
    comments are examples rather than navigation. Specification citations are checked even in code
    examples, as in the API-reference XML gate; only HTML comments are excluded from citations.
    Relative targets must stay in this checkout. Directory targets may link to source directories;
    a directory fragment is checked against its README.md. External destinations must be absolute
    HTTPS URLs; their existence is deliberately not checked. Docfx xref: UID links are internal;
    their resolution is gated by the subsequent warnings-as-errors site build. ADR record numbers
    and WBS work-item numbers are not specification clauses. Files are checked in ordinal path
    order, then source order, and the first failure includes the file, line, and original target.
.PARAMETER RepositoryDirectory
    An optional isolated fixture root beneath this checkout, used by the offline regression tests.
.PARAMETER PassThru
    Also returns the file, link, anchor-reference, and clause-citation counts as an object.
#>
[CmdletBinding()]
param(
    [string] $RepositoryDirectory = (Join-Path $PSScriptRoot '..'),
    [switch] $PassThru
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
Import-Module (Join-Path $PSScriptRoot 'modules/ViuApiReference.psm1') -Force
$checkout = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$repository = [System.IO.Path]::GetFullPath($RepositoryDirectory).TrimEnd([char[]]'\/')
if ($repository -cne $checkout -and -not $repository.StartsWith($checkout + [System.IO.Path]::DirectorySeparatorChar,
        [System.StringComparison]::OrdinalIgnoreCase)) {
    throw "Documentation fixture root must stay inside this checkout: '$repository'."
}
$clauseMap = New-ViuSpecificationMap -SpecificationPath (Join-Path $repository 'docs/SPECIFICATION.md')
$documentCache = @{}
$counts = [ordered]@{ Files = 0; Links = 0; Anchors = 0; Citations = 0 }

function Clear-MarkdownRange {
    param([char[]] $Characters, [int] $Start, [int] $Length)
    for ($position = $Start; $position -lt $Start + $Length; $position++) {
        if ($Characters[$position] -notin @("`r", "`n")) { $Characters[$position] = ' ' }
    }
}

function Test-MarkdownEscape {
    param([string] $Text, [int] $Position)
    $slashes = 0
    for ($previous = $Position - 1; $previous -ge 0 -and $Text[$previous] -eq '\'; $previous--) { $slashes++ }
    return ($slashes % 2) -eq 1
}

function ConvertFrom-MarkdownEscapes {
    param([string] $Text)
    return [System.Net.WebUtility]::HtmlDecode([regex]::Replace($Text, '\\([!"#$%&''()*+,\-./:;<=>?@\[\]\\^_`{|}~])', '$1'))
}

function Get-MarkdownProse {
    param([string] $Text, [switch] $RemoveCodeSpans)
    $characters = $Text.ToCharArray()
    foreach ($comment in [regex]::Matches($Text, '<!--[\s\S]*?(?:-->|\z)')) {
        Clear-MarkdownRange $characters $comment.Index $comment.Length
    }
    $visible = -join $characters
    $fenceCharacter = ''
    $fenceLength = 0
    $paragraphOpen = $false
    $indentedCode = $false
    $listIndentations = [System.Collections.Generic.List[int]]::new()
    foreach ($line in [regex]::Matches($visible, '(?m)^[^\r\n]*(?:\r?\n|\z)')) {
        $value = $line.Value.TrimEnd([char[]]"`r`n")
        # A list item's indentation belongs to its container. Four additional spaces start code
        # only after a paragraph break; lazy paragraph continuations remain ordinary navigation.
        $content = [regex]::Replace($value, '^ {0,3}(?:> ?)+', '')
        $indentation = [regex]::Match($content, '^ *').Length
        if ([string]::IsNullOrWhiteSpace($content)) { $paragraphOpen = $false; continue }
        while ($listIndentations.Count -gt 0 -and $indentation -lt $listIndentations[$listIndentations.Count - 1]) {
            $listIndentations.RemoveAt($listIndentations.Count - 1)
        }
        $baseline = if ($listIndentations.Count -gt 0) { $listIndentations[$listIndentations.Count - 1] } else { 0 }
        $list = [regex]::Match($content.Substring([Math]::Min($baseline, $content.Length)), '^ {0,3}(?:[-+*]|\d+[.)]) +')
        if ($fenceLength -eq 0 -and $list.Success) {
            $baseline += $list.Length
            $listIndentations.Add($baseline)
            $content = $content.Substring($baseline)
            $paragraphOpen = $false
        } else {
            $content = $content.Substring([Math]::Min($baseline, $content.Length))
        }
        $fence = [regex]::Match($content, '^ {0,3}(?<marker>`{3,}|~{3,})(?<suffix>.*)$')
        if ($fenceLength -gt 0) {
            Clear-MarkdownRange $characters $line.Index $line.Length
            if ($fence.Success -and $fence.Groups['marker'].Value[0].ToString() -eq $fenceCharacter -and
                $fence.Groups['marker'].Length -ge $fenceLength -and
                [string]::IsNullOrWhiteSpace($fence.Groups['suffix'].Value)) { $fenceLength = 0; $paragraphOpen = $false }
        } elseif ($fence.Success) {
            $fenceCharacter = $fence.Groups['marker'].Value[0].ToString()
            $fenceLength = $fence.Groups['marker'].Length
            Clear-MarkdownRange $characters $line.Index $line.Length
        } elseif ($content -match '^(?: {4}|\t)' -and (-not $paragraphOpen -or $indentedCode)) {
            Clear-MarkdownRange $characters $line.Index $line.Length
            $indentedCode = $true
        } else {
            $indentedCode = $false
            $paragraphOpen = $content -notmatch '^ {0,3}(?:#{1,6}\s|(?:[-*_]\s*){3,}$)'
        }
    }
    $visible = -join $characters
    if ($RemoveCodeSpans) {
        $position = 0
        while ($position -lt $visible.Length) {
            if ($visible[$position] -ne '`' -or (Test-MarkdownEscape $visible $position)) { $position++; continue }
            $length = 1
            while ($position + $length -lt $visible.Length -and $visible[$position + $length] -eq '`') { $length++ }
            $closing = [regex]::Match($visible.Substring($position + $length), '(?<!`)`{' + $length + '}(?!`)')
            if ($closing.Success) {
                $end = $position + $length + $closing.Index + $length
                Clear-MarkdownRange $characters $position ($end - $position)
                $position = $end
            } else { $position += $length }
        }
    }
    return -join $characters
}

function Get-MarkdownAnchors {
    param([string] $Text)
    $anchors = [System.Collections.Generic.HashSet[string]]::new([System.StringComparer]::Ordinal)
    $headings = [System.Collections.Generic.HashSet[string]]::new([System.StringComparer]::Ordinal)
    foreach ($explicit in [regex]::Matches((Get-MarkdownProse $Text -RemoveCodeSpans),
        '(?i)<[a-z][a-z0-9]*\b[^>]*?\s(?:id|name)\s*=\s*(?:"(?<identifier>[^"]+)"|''(?<identifier>[^'']+)''|(?<identifier>[^\s>]+))')) {
        [void]$anchors.Add([System.Net.WebUtility]::HtmlDecode($explicit.Groups['identifier'].Value))
    }
    $lines = $Text -split '\r?\n'
    for ($position = 0; $position -lt $lines.Count; $position++) {
        $heading = [regex]::Match($lines[$position], '^ *(?:> ?)*#{1,6}\s+(?<text>.*?)(?:\s+#+\s*)?$')
        $value = $null
        if ($heading.Success) { $value = $heading.Groups['text'].Value }
        elseif ($position + 1 -lt $lines.Count -and $lines[$position + 1] -match '^ {0,3}(?:=+|-+)\s*$' -and
                -not [string]::IsNullOrWhiteSpace($lines[$position])) { $value = $lines[$position]; $position++ }
        if ($null -eq $value) { continue }
        $custom = [regex]::Match($value, '\s*\{#(?<identifier>[^\s}]+)\}\s*$')
        if ($custom.Success) { [void]$anchors.Add($custom.Groups['identifier'].Value); continue }
        $value = [regex]::Replace($value, '!?(\[(?<label>[^\]]*)\])(?:\([^)]*\)|\[[^\]]*\])', '${label}')
        $value = [regex]::Replace($value, '(?<!\w)_{1,3}(?<text>\S(?:.*?\S)?)_{1,3}(?!\w)', '${text}')
        $value = ConvertFrom-MarkdownEscapes ([regex]::Replace($value, '<[^>]*>', ''))
        $value = [regex]::Replace($value.ToLowerInvariant(), '[^\p{L}\p{M}\p{N}\s_-]', '')
        $identifier = [regex]::Replace($value, '\s', '-')
        $candidate = $identifier
        $suffix = 0
        while (-not $headings.Add($candidate)) { $suffix++; $candidate = "$identifier-$suffix" }
        [void]$anchors.Add($candidate)
    }
    return ,$anchors
}

function Get-MarkdownDocument {
    param([string] $Path)
    if ($documentCache.ContainsKey($Path)) { return $documentCache[$Path] }
    $text = [System.IO.File]::ReadAllText($Path)
    $prose = Get-MarkdownProse $text
    $anchors = Get-MarkdownAnchors $prose
    if ($Path -eq (Join-Path $repository 'docs/SPECIFICATION.md')) {
        # These anchors are injected into the staged specification by the existing #101 module.
        foreach ($anchor in $clauseMap.Values) { [void]$anchors.Add($anchor) }
    }
    $document = [pscustomobject]@{ Text = $text; Prose = $prose; Navigation = (Get-MarkdownProse $text -RemoveCodeSpans); Anchors = $anchors }
    $documentCache[$Path] = $document
    return $document
}

function Get-MarkdownDestinations {
    param([object] $Document)
    $characters = $Document.Navigation.ToCharArray()
    $references = @{}
    $destinations = [System.Collections.Generic.List[object]]::new()
    $destinationRanges = @{}
    foreach ($definition in [regex]::Matches($Document.Navigation,
        '(?m)^ *(?:> ?)*\[(?<label>(?:\\.|[^\]\\\r\n])+)\]:[ \t]*(?:\r?\n[ \t]*)?(?<target><[^>\r\n]*>|(?:\\.|[^\s\\])+)(?:[^\r\n]*)')) {
        $label = [regex]::Replace((ConvertFrom-MarkdownEscapes $definition.Groups['label'].Value).Trim(), '\s+', ' ').ToLowerInvariant()
        $target = $definition.Groups['target'].Value.Trim([char[]]'<>')
        if (-not $references.ContainsKey($label)) { $references[$label] = $target }
        $destinations.Add([pscustomobject]@{ Index = $definition.Index; Target = $target; Kind = 'Link' })
        Clear-MarkdownRange $characters $definition.Index $definition.Length
    }
    $navigation = -join $characters
    for ($position = 0; $position -lt $navigation.Length; $position++) {
        if ($destinationRanges.ContainsKey($position)) { $position = $destinationRanges[$position] - 1; continue }
        if ($navigation[$position] -ne '[' -or (Test-MarkdownEscape $navigation $position)) { continue }
        $depth = 1
        $closing = $position + 1
        for (; $closing -lt $navigation.Length -and $depth -gt 0; $closing++) {
            if (Test-MarkdownEscape $navigation $closing) { continue }
            if ($navigation[$closing] -eq '[') { $depth++ }
            elseif ($navigation[$closing] -eq ']') { $depth-- }
        }
        if ($depth -ne 0) { continue }
        $label = $Document.Text.Substring($position + 1, $closing - $position - 2)
        $following = $closing
        if ($following -lt $navigation.Length -and $navigation[$following] -eq '(') {
            $destination = $following + 1
            while ($destination -lt $navigation.Length -and [char]::IsWhiteSpace($navigation[$destination])) { $destination++ }
            $end = $destination
            if ($end -lt $navigation.Length -and $navigation[$end] -eq '<') {
                $destination++; $end++
                while ($end -lt $navigation.Length -and ($navigation[$end] -ne '>' -or (Test-MarkdownEscape $navigation $end))) { $end++ }
                $after = $end + 1
            } else {
                $depth = 0
                while ($end -lt $navigation.Length) {
                    $character = $navigation[$end]
                    if (-not (Test-MarkdownEscape $navigation $end)) {
                        if ($character -eq '(') { $depth++ }
                        elseif ($character -eq ')') { if ($depth -eq 0) { break }; $depth-- }
                        elseif ([char]::IsWhiteSpace($character)) { break }
                    }
                    $end++
                }
                $after = $end
            }
            if ($end -ge $navigation.Length) { continue }
            $tail = [regex]::Match($navigation.Substring($after), '^\s*(?:(?:"(?:\\.|[^"\\])*"|''(?:\\.|[^''\\])*''|\((?:\\.|[^)\\])*\))\s*)?\)')
            if (-not $tail.Success) { continue }
            $destinations.Add([pscustomobject]@{ Index = $position; Target = $Document.Text.Substring($destination, $end - $destination); Kind = 'Link' })
            # Continue through the label so a linked image validates both its src and outer href.
            # Skip the destination/title when reached, and exclude angle destinations from autolinks.
            $destinationRanges[$following] = $after + $tail.Length
            continue
        }
        $reference = $label
        $explicit = $false
        if ($following -lt $navigation.Length -and $navigation[$following] -eq '[') {
            $referenceEnd = $navigation.IndexOf(']', $following + 1)
            if ($referenceEnd -ge 0) {
                $explicit = $true
                $reference = $Document.Text.Substring($following + 1, $referenceEnd - $following - 1)
                if ($reference.Length -eq 0) { $reference = $label }
                $closing = $referenceEnd + 1
            }
        }
        $reference = [regex]::Replace((ConvertFrom-MarkdownEscapes $reference).Trim(), '\s+', ' ').ToLowerInvariant()
        if ($references.ContainsKey($reference)) {
            $destinations.Add([pscustomobject]@{ Index = $position; Target = $references[$reference]; Kind = 'Link' })
            if ($explicit) { $destinationRanges[$following] = $closing }
        } elseif ($explicit) {
            $destinations.Add([pscustomobject]@{ Index = $position; Target = "[$reference]"; Kind = 'UndefinedReference' })
            $position = $closing - 1
        }
    }
    foreach ($automatic in [regex]::Matches($navigation, '<(?<target>[a-zA-Z][a-zA-Z0-9+.-]*:[^<>\r\n]*)>')) {
        $insideDestination = $false
        foreach ($start in $destinationRanges.Keys) {
            if ($automatic.Index -ge $start -and $automatic.Index -lt $destinationRanges[$start]) { $insideDestination = $true; break }
        }
        if (-not $insideDestination -and -not (Test-MarkdownEscape $navigation $automatic.Index)) {
            $destinations.Add([pscustomobject]@{ Index = $automatic.Index; Target = $automatic.Groups['target'].Value; Kind = 'Link' })
        }
    }
    $citationText = [regex]::Replace($Document.Text, '<!--[\s\S]*?(?:-->|\z)', {
        param($comment)
        [regex]::Replace($comment.Value, '[^\r\n]', ' ')
    })
    foreach ($citation in [regex]::Matches($citationText, '\[(?<identifier>[A-Z][A-Z0-9]*(?:-[A-Z0-9]+)*-\d+)\]')) {
        if ($citation.Groups['identifier'].Value -notmatch '^ADR-\d{4}$') {
            $destinations.Add([pscustomobject]@{ Index = $citation.Index; Target = $citation.Groups['identifier'].Value; Kind = 'Citation' })
        }
    }
    return $destinations | Sort-Object Index
}

function Test-DocumentationDestination {
    param([string] $Path, [object] $Destination)
    if ($Destination.Kind -eq 'Citation') {
        $counts.Citations++
        if (-not $clauseMap.Contains($Destination.Target)) { throw 'Unknown specification clause.' }
        return
    }
    if ($Destination.Kind -eq 'UndefinedReference') { throw 'Undefined Markdown reference label.' }
    $counts.Links++
    $target = ConvertFrom-MarkdownEscapes $Destination.Target
    if ($target.StartsWith('xref:', [System.StringComparison]::Ordinal)) {
        if ($target -notmatch '^xref:[^\s<>]+$') { throw 'Docfx internal UID links require a nonempty xref: identifier.' }
        return
    }
    if ($target -match '^[a-zA-Z][a-zA-Z0-9+.-]*:' -or $target.StartsWith('//')) {
        $address = $null
        if ($target -cnotmatch '^https://' -or $target -match '\s|\\|%(?![0-9a-fA-F]{2})' -or
            -not [System.Uri]::TryCreate($target, [System.UriKind]::Absolute, [ref]$address) -or
            [string]::IsNullOrWhiteSpace($address.Host) -or $address.UserInfo.Length -gt 0) {
            throw 'External links must be absolute https:// URLs with a host and valid escaping.'
        }
        return
    }
    $fragmentIndex = $target.IndexOf('#')
    $fragment = if ($fragmentIndex -ge 0) { [System.Uri]::UnescapeDataString($target.Substring($fragmentIndex + 1)) } else { '' }
    $relative = if ($fragmentIndex -ge 0) { $target.Substring(0, $fragmentIndex) } else { $target }
    $relative = [System.Uri]::UnescapeDataString(($relative -split '\?', 2)[0])
    if ($relative.StartsWith('/') -or $relative.Contains('\')) { throw 'Local Markdown links must use repository-relative forward-slash paths.' }
    $resolved = if ($relative.Length -eq 0) { $Path } else { [System.IO.Path]::GetFullPath((Join-Path (Split-Path $Path) $relative)) }
    if ($resolved -ne $repository -and -not $resolved.StartsWith($repository + [System.IO.Path]::DirectorySeparatorChar,
            [System.StringComparison]::OrdinalIgnoreCase)) { throw 'Relative link escapes this checkout.' }
    if (-not (Test-Path -LiteralPath $resolved)) { throw 'Relative target does not exist.' }
    if ($fragment.Length -eq 0) { return }
    $counts.Anchors++
    if (Test-Path -LiteralPath $resolved -PathType Container) { $resolved = Join-Path $resolved 'README.md' }
    if (-not (Test-Path -LiteralPath $resolved -PathType Leaf)) { throw 'Fragment target has no readable file.' }
    if ([System.IO.Path]::GetExtension($resolved) -ieq '.md') {
        if (-not (Get-MarkdownDocument $resolved).Anchors.Contains($fragment)) { throw 'Markdown anchor does not exist.' }
    } elseif ([System.IO.Path]::GetExtension($resolved) -in @('.html', '.htm', '.svg')) {
        if (-not (Get-MarkdownDocument $resolved).Anchors.Contains($fragment)) { throw 'HTML anchor does not exist.' }
    } elseif ($fragment -notmatch '^L\d+(?:-L\d+)?$') {
        throw 'Only Markdown/HTML anchors or source line fragments can be checked offline.'
    } else {
        $lineRange = [regex]::Match($fragment, '^L(?<first>\d+)(?:-L(?<last>\d+))?$')
        $firstLine = [int]$lineRange.Groups['first'].Value
        $lastLine = if ($lineRange.Groups['last'].Success) { [int]$lineRange.Groups['last'].Value } else { $firstLine }
        if ($firstLine -lt 1 -or $lastLine -lt $firstLine -or $lastLine -gt [System.IO.File]::ReadAllLines($resolved).Length) {
            throw 'Source line fragment must be an ordered, positive range within the file length.'
        }
    }
}

# Enumerate only named roots, pruning generated directories before descending into them.
$pending = [System.Collections.Generic.Stack[string]]::new()
foreach ($root in @('docs', 'libraries', 'sdks', 'extensions')) {
    $directory = Join-Path $repository $root
    if (Test-Path -LiteralPath $directory -PathType Container) { $pending.Push($directory) }
}
$files = [System.Collections.Generic.List[string]]::new()
if (Test-Path -LiteralPath (Join-Path $repository 'README.md')) { $files.Add((Join-Path $repository 'README.md')) }
while ($pending.Count -gt 0) {
    foreach ($entry in Get-ChildItem -LiteralPath $pending.Pop() -Force) {
        if ($entry.Attributes -band [System.IO.FileAttributes]::ReparsePoint) { continue }
        if ($entry.PSIsContainer) {
            if ($entry.Name -notin @('bin', 'obj', 'node_modules', '_out', '.git')) { $pending.Push($entry.FullName) }
            continue
        }
        $relative = [System.IO.Path]::GetRelativePath($repository, $entry.FullName).Replace('\', '/')
        if ($relative -match '^(?:docs/.+\.md|libraries/(?:[^/]+/)+docs/[^/]+\.md|sdks/.+\.md|extensions/(?:[^/]+/)*README\.md)$') {
            $files.Add($entry.FullName)
        }
    }
}
$files.Sort([System.StringComparer]::Ordinal)
foreach ($file in $files) {
    $counts.Files++
    $document = Get-MarkdownDocument $file
    foreach ($destination in Get-MarkdownDestinations $document) {
        try { Test-DocumentationDestination $file $destination }
        catch {
            $line = 1 + [regex]::Matches($document.Text.Substring(0, $destination.Index), '\n').Count
            $relative = [System.IO.Path]::GetRelativePath($repository, $file).Replace('\', '/')
            throw "${relative}:${line}: '$($destination.Target)' - $($_.Exception.Message)"
        }
    }
}
Write-Host "Documentation links passed: $($counts.Files) files, $($counts.Links) links, $($counts.Anchors) anchors, $($counts.Citations) clause citations."
if ($PassThru) { [pscustomobject]$counts }
