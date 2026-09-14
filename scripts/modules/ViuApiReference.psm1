<#
.SYNOPSIS
    Resolves specification citations for the API-reference build ([V01.01.13.04]).

.DESCRIPTION
    The specification is the single source of clause identifiers. Only staged Markdown and
    compiler-produced XML copies are changed; library sources and their build outputs stay intact.
#>

Set-StrictMode -Version Latest

$script:ViuClausePattern = '\[(?<identifier>[A-Z][A-Z0-9]*(?:-[A-Z0-9]+)*-\d+)\]'

function Get-ViuSpecificationClauseDefinition {
    [CmdletBinding()]
    param([Parameter(Mandatory)][AllowEmptyString()][string] $Text)

    $fenceCharacter = ''
    $fenceLength = 0
    $paragraphStart = $true
    foreach ($lineMatch in [regex]::Matches($Text, '(?m)^(?<line>[^\r\n]*)(?:\r?\n|\z)')) {
        $line = $lineMatch.Groups['line'].Value
        $fenceMatch = [regex]::Match($line, '^ {0,3}(?<marker>`{3,}|~{3,})(?<suffix>.*)$')
        if ($fenceLength -gt 0) {
            if ($fenceMatch.Success -and
                $fenceMatch.Groups['marker'].Value[0].ToString() -eq $fenceCharacter -and
                $fenceMatch.Groups['marker'].Length -ge $fenceLength -and
                [string]::IsNullOrWhiteSpace($fenceMatch.Groups['suffix'].Value)) {
                $fenceLength = 0
                $paragraphStart = $true
            }
            continue
        }
        if ($fenceMatch.Success) {
            $fenceCharacter = $fenceMatch.Groups['marker'].Value[0].ToString()
            $fenceLength = $fenceMatch.Groups['marker'].Length
            continue
        }
        if ([string]::IsNullOrWhiteSpace($line)) {
            $paragraphStart = $true
            continue
        }

        $definition = [regex]::Match($line, '^ {0,3}`' + $script:ViuClausePattern + '`(?:\s|$)')
        if ($paragraphStart -and $definition.Success) {
            [pscustomobject]@{
                Identifier = $definition.Groups['identifier'].Value
                Index = $lineMatch.Index
            }
        }
        $paragraphStart = [regex]::IsMatch($line, '^ {0,3}#{1,6}\s')
    }
}

function New-ViuSpecificationMap {
    <#
    .SYNOPSIS
        Returns the ordered mapping from specification clause identifiers to stable site anchors.
    .DESCRIPTION
        A definition begins a paragraph with a backtick-delimited [UPPERCASE-ID-1]. Fenced code
        examples are ignored. Duplicate identifiers and an empty specification fail the build.
        The anchor for RND-FLAGS-1 is clause-rnd-flags-1, independent of heading names or ordering.
    #>
    [CmdletBinding()]
    param([Parameter(Mandatory)][string] $SpecificationPath)

    $clauseMap = [ordered]@{}
    $text = [System.IO.File]::ReadAllText($SpecificationPath)
    foreach ($definition in Get-ViuSpecificationClauseDefinition -Text $text) {
        $identifier = $definition.Identifier
        if ($clauseMap.Contains($identifier)) {
            throw "Duplicate specification clause [$identifier] in '$SpecificationPath'."
        }
        $clauseMap.Add($identifier, 'clause-' + $identifier.ToLowerInvariant())
    }
    if ($clauseMap.Count -eq 0) {
        throw "No specification clause definitions found in '$SpecificationPath'."
    }
    return $clauseMap
}

function Convert-ViuSpecification {
    <#
    .SYNOPSIS
        Adds explicit clause anchors to a staged specification without changing its source.
    .DESCRIPTION
        Uses the same paragraph and code-fence recognition as New-ViuSpecificationMap. A supplied
        map must describe every definition exactly once, so stale maps fail before publication.
    #>
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)][AllowEmptyString()][string] $Text,
        [Parameter(Mandatory)][System.Collections.IDictionary] $ClauseMap
    )

    $newline = if ($Text.Contains("`r`n")) { "`r`n" } else { "`n" }
    $seen = [System.Collections.Generic.HashSet[string]]::new([System.StringComparer]::Ordinal)
    $result = [System.Text.StringBuilder]::new()
    $position = 0
    foreach ($definition in Get-ViuSpecificationClauseDefinition -Text $Text) {
        $identifier = $definition.Identifier
        if (-not $ClauseMap.Contains($identifier)) {
            throw "Unknown specification clause definition [$identifier]; regenerate the clause map."
        }
        if (-not $seen.Add($identifier)) {
            throw "Duplicate specification clause [$identifier]."
        }
        [void]$result.Append($Text, $position, $definition.Index - $position)
        [void]$result.Append('<a id="').Append($ClauseMap[$identifier]).Append('"></a>')
        [void]$result.Append($newline).Append($newline)
        $position = $definition.Index
    }
    if ($seen.Count -ne $ClauseMap.Count) {
        throw 'The specification clause map contains definitions absent from the staged specification.'
    }
    [void]$result.Append($Text, $position, $Text.Length - $position)
    return $result.ToString()
}

function Convert-ViuDocumentationXml {
    <#
    .SYNOPSIS
        Validates all XML text citations and writes a copy with links to specification anchors.
    .DESCRIPTION
        Returns the number of converted citations. Unknown identifiers fail even inside code
        examples. Code/pre blocks, existing links, attributes, comments, and WBS references stay
        unchanged. Inline c citations become see links outside code markup so DocFX renders links.
        XML parsing and writing preserve escaping; no textual replacement is performed on XML.
    #>
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)][string] $SourcePath,
        [Parameter(Mandatory)][string] $DestinationPath,
        [Parameter(Mandatory)][System.Collections.IDictionary] $ClauseMap
    )

    if ([string]::Equals([System.IO.Path]::GetFullPath($SourcePath),
            [System.IO.Path]::GetFullPath($DestinationPath), [System.StringComparison]::OrdinalIgnoreCase)) {
        throw 'Documentation XML must be written to a staged copy, not its source path.'
    }

    $document = [System.Xml.XmlDocument]::new()
    $document.PreserveWhitespace = $true
    $document.XmlResolver = $null
    $readerSettings = [System.Xml.XmlReaderSettings]::new()
    $readerSettings.DtdProcessing = [System.Xml.DtdProcessing]::Prohibit
    $readerSettings.XmlResolver = $null
    $reader = [System.Xml.XmlReader]::Create($SourcePath, $readerSettings)
    try { $document.Load($reader) } finally { $reader.Dispose() }

    $textNodes = @($document.SelectNodes('//text()'))
    # Validate before changing anything, including prose that lives inside preserved code blocks.
    foreach ($textNode in $textNodes) {
        foreach ($citation in [regex]::Matches($textNode.Value, $script:ViuClausePattern)) {
            $identifier = $citation.Groups['identifier'].Value
            if (-not $ClauseMap.Contains($identifier)) {
                $member = $textNode.SelectSingleNode('ancestor::member')
                $location = if ($null -ne $member) { $member.GetAttribute('name') } else { 'document' }
                throw "Unknown specification clause [$identifier] in '$SourcePath' ($location)."
            }
        }
    }

    $convertedCount = 0
    foreach ($textNode in $textNodes) {
        $citations = [regex]::Matches($textNode.Value, $script:ViuClausePattern)
        if ($citations.Count -eq 0 -or
            $null -ne $textNode.SelectSingleNode('ancestor::code | ancestor::pre | ancestor::see | ancestor::seealso | ancestor::a')) {
            continue
        }

        $target = $textNode
        $inlineCode = $textNode.ParentNode.Name -eq 'c' -and $textNode.ParentNode.ChildNodes.Count -eq 1
        if ($inlineCode) { $target = $textNode.ParentNode }
        $replacement = $document.CreateDocumentFragment()
        $position = 0
        foreach ($citation in $citations) {
            if ($citation.Index -gt $position) {
                $prefix = $document.CreateTextNode($textNode.Value.Substring($position, $citation.Index - $position))
                if ($inlineCode -and -not [string]::IsNullOrWhiteSpace($prefix.Value)) {
                    $code = $document.CreateElement('c')
                    [void]$code.AppendChild($prefix)
                    [void]$replacement.AppendChild($code)
                } else {
                    [void]$replacement.AppendChild($prefix)
                }
            }
            $identifier = $citation.Groups['identifier'].Value
            $link = $document.CreateElement('see')
            $link.SetAttribute('href', '../docs/SPECIFICATION.md#' + $ClauseMap[$identifier])
            $link.InnerText = $citation.Value
            [void]$replacement.AppendChild($link)
            $position = $citation.Index + $citation.Length
            $convertedCount++
        }
        if ($position -lt $textNode.Value.Length) {
            $suffix = $document.CreateTextNode($textNode.Value.Substring($position))
            if ($inlineCode -and -not [string]::IsNullOrWhiteSpace($suffix.Value)) {
                $code = $document.CreateElement('c')
                [void]$code.AppendChild($suffix)
                [void]$replacement.AppendChild($code)
            } else {
                [void]$replacement.AppendChild($suffix)
            }
        }
        [void]$target.ParentNode.ReplaceChild($replacement, $target)
    }

    [void][System.IO.Directory]::CreateDirectory([System.IO.Path]::GetDirectoryName([System.IO.Path]::GetFullPath($DestinationPath)))
    $writerSettings = [System.Xml.XmlWriterSettings]::new()
    $writerSettings.Encoding = [System.Text.UTF8Encoding]::new($false)
    $writerSettings.NewLineHandling = [System.Xml.NewLineHandling]::None
    $writerSettings.Indent = $false
    $writer = [System.Xml.XmlWriter]::Create($DestinationPath, $writerSettings)
    try { $document.Save($writer) } finally { $writer.Dispose() }
    return $convertedCount
}

function Merge-ViuApiReferenceTableOfContents {
    <#
    .SYNOPSIS
        Combines the item sequences produced by the pinned DocFX metadata groups.
    .DESCRIPTION
        DocFX 2.78.5 emits a TableOfContent mapping with items followed by memberLayout: SamePage.
        Preserve every item line and emit shared root properties once. Unexpected root properties,
        layouts, or document shapes fail explicitly instead of silently discarding metadata.
    #>
    [CmdletBinding()]
    param([Parameter(Mandatory)][ValidateNotNullOrEmpty()][string[]] $TablesOfContents)

    $itemGroups = [System.Collections.Generic.List[string]]::new()
    foreach ($table in $TablesOfContents) {
        $normalized = $table.Replace("`r`n", "`n").TrimEnd([char[]]"`n")
        $shape = [regex]::Match($normalized,
            '\A### YamlMime:TableOfContent\nitems:\n(?<items>[\s\S]+)\nmemberLayout: SamePage\z')
        if (-not $shape.Success) {
            throw 'Unexpected DocFX table-of-contents shape; expected TableOfContent, items, and memberLayout: SamePage.'
        }
        $items = $shape.Groups['items'].Value
        if (-not $items.StartsWith('- uid: ', [System.StringComparison]::Ordinal)) {
            throw 'Unexpected DocFX table-of-contents shape; items must begin with a namespace UID.'
        }
        foreach ($line in $items -split "`n") {
            if (-not [string]::IsNullOrWhiteSpace($line) -and
                -not $line.StartsWith('- uid: ', [System.StringComparison]::Ordinal) -and
                -not $line.StartsWith('  ', [System.StringComparison]::Ordinal)) {
                throw "Unexpected DocFX table-of-contents root content: $line"
            }
        }
        $itemGroups.Add($items)
    }
    return "### YamlMime:TableOfContent`nitems:`n" + ($itemGroups -join "`n") + "`nmemberLayout: SamePage`n"
}

function New-ViuDocumentationNavigation {
    <#
    .SYNOPSIS
        Discovers reader documentation and fills the single documentation navigation tree.
    .DESCRIPTION
        Every guide and library OVERVIEW enters the primary publication set. State is presented as
        its own reader area while retaining its Runtime source path. SDK reader pages are included
        from sdks/README.md and each SDK's docs folder. Unknown library areas fail explicitly, so a
        new area cannot silently disappear from the documentation home ([V01.01.13.05]).
    #>
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)][string] $RepositoryDirectory,
        [Parameter(Mandatory)][string] $TemplatePath
    )

    $primaryDocuments = [System.Collections.Generic.List[string]]::new()
    foreach ($path in @('docs/api-reference/index.md', 'docs/guide/getting-started.md',
        'docs/DEVELOPER-EXAMPLES.md', 'docs/SPECIFICATION.md', 'sdks/README.md')) {
        if (-not (Test-Path -LiteralPath (Join-Path $RepositoryDirectory $path) -PathType Leaf)) {
            throw "Missing primary documentation page: $path"
        }
        $primaryDocuments.Add($path)
    }
    $guideItems = [System.Collections.Generic.List[string]]::new()
    $guideItems.Add('  - name: Developer examples')
    $guideItems.Add('    href: docs/DEVELOPER-EXAMPLES.md')
    foreach ($guide in Get-ChildItem -LiteralPath (Join-Path $RepositoryDirectory 'docs/guide') -Filter '*.md' -Recurse -File | Sort-Object FullName) {
        $relative = [System.IO.Path]::GetRelativePath($RepositoryDirectory, $guide.FullName).Replace('\', '/')
        if ($relative -match '/(?:bin|obj|node_modules|_out)/' -or $relative -eq 'docs/guide/getting-started.md') { continue }
        $title = [regex]::Match([System.IO.File]::ReadAllText($guide.FullName), '(?m)^#\s+(.+?)\s*#*\s*$')
        if (-not $title.Success) { throw "Reader guide requires a level-one title: $relative" }
        $guideItems.Add('  - name: ' + (ConvertTo-Json $title.Groups[1].Value -Compress))
        $guideItems.Add('    href: ' + $relative)
        $primaryDocuments.Add($relative)
    }

    $areas = [ordered]@{}
    foreach ($name in @('Runtime', 'Browser', 'Router', 'State', 'ServerRenderer', 'DevTools', 'Syntax', 'Utilities')) {
        $areas[$name] = [System.Collections.Generic.List[object]]::new()
    }
    $overviewCount = 0
    foreach ($overview in Get-ChildItem -LiteralPath (Join-Path $RepositoryDirectory 'libraries') -Filter 'OVERVIEW.md' -Recurse -File | Sort-Object FullName) {
        $relative = [System.IO.Path]::GetRelativePath($RepositoryDirectory, $overview.FullName).Replace('\', '/')
        if ($relative -notmatch '^libraries/([^/]+)/([^/]+)/docs/OVERVIEW\.md$') { continue }
        $area = $Matches[1]
        $assemblyName = $Matches[2]
        if ($assemblyName -eq 'Assimalign.Viu.State') { $area = 'State' }
        if (-not $areas.Contains($area)) { throw "Unmapped library documentation area '$area': $relative" }
        $areas[$area].Add(@{ Name = $assemblyName; Path = $relative })
        $primaryDocuments.Add($relative)
        $overviewCount++
    }
    $libraryItems = [System.Collections.Generic.List[string]]::new()
    foreach ($area in $areas.Keys) {
        if ($areas[$area].Count -eq 0) { continue }
        $libraryItems.Add('  - name: ' + $area)
        $libraryItems.Add('    items:')
        foreach ($entry in $areas[$area]) {
            $libraryItems.Add('    - name: ' + $entry.Name)
            $libraryItems.Add('      href: ' + $entry.Path)
        }
    }
    $libraryItems.Add('  - name: SDKs')
    $libraryItems.Add('    items:')
    $libraryItems.Add('    - name: SDK overview')
    $libraryItems.Add('      href: sdks/README.md')
    $sdkCount = 1
    foreach ($document in Get-ChildItem -LiteralPath (Join-Path $RepositoryDirectory 'sdks') -Filter '*.md' -Recurse -File | Sort-Object FullName) {
        $relative = [System.IO.Path]::GetRelativePath($RepositoryDirectory, $document.FullName).Replace('\', '/')
        if ($relative -match '/(?:bin|obj|node_modules|_out)/' -or $relative -notmatch '^sdks/[^/]+/docs/.+\.md$') { continue }
        $title = [regex]::Match([System.IO.File]::ReadAllText($document.FullName), '(?m)^#\s+(.+?)\s*#*\s*$')
        if (-not $title.Success) { throw "SDK reader document requires a level-one title: $relative" }
        $libraryItems.Add('    - name: ' + (ConvertTo-Json $title.Groups[1].Value -Compress))
        $libraryItems.Add('      href: ' + $relative)
        $primaryDocuments.Add($relative)
        $sdkCount++
    }
    $template = [System.IO.File]::ReadAllText($TemplatePath)
    foreach ($placeholder in @('{{ViuGuideItems}}', '{{ViuLibraryItems}}')) {
        if ([regex]::Matches($template, [regex]::Escape($placeholder)).Count -ne 1) {
            throw "The documentation navigation template must contain exactly one $placeholder."
        }
    }
    [pscustomobject]@{
        TableOfContents = $template.Replace('{{ViuGuideItems}}', ($guideItems -join "`n")).Replace('{{ViuLibraryItems}}', ($libraryItems -join "`n"))
        PrimaryDocuments = $primaryDocuments.ToArray()
        Sections = @('Overview', 'Getting started', 'Guides', 'Libraries', 'Specification', 'API reference')
        LibraryOverviews = $overviewCount
        SdkDocuments = $sdkCount
    }
}

Export-ModuleMember -Function New-ViuSpecificationMap, Convert-ViuSpecification, Convert-ViuDocumentationXml, Merge-ViuApiReferenceTableOfContents, New-ViuDocumentationNavigation
