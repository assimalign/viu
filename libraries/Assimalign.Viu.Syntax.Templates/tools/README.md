# Compiler code-generation tools

## `Generate-HtmlEntities.ps1`

Regenerates [`src/Internal/HtmlNamedCharacterReferences.g.cs`](../src/Internal/HtmlNamedCharacterReferences.g.cs)
— the embedded WHATWG named character reference table the tokenizer uses to decode entities
(`&amp;`, `&copy;`, `&notin;`, …). The table is compile-time data so the parser needs no runtime DOM
or network access (the [V01.01.05.01] AOT boundary).

Regenerate only when the WHATWG list changes:

```pwsh
# 1. Download the authoritative list (do NOT commit the .json — it is a build input, not source):
curl -fsSL https://html.spec.whatwg.org/entities.json -o entities.json
# 2. Regenerate the committed .cs (writes ../src/Internal/HtmlNamedCharacterReferences.g.cs):
./Generate-HtmlEntities.ps1 -EntitiesJsonPath ./entities.json
```

The generator uses `System.Text.Json` (the list has keys differing only in case, e.g. `&DownArrow;`
vs `&Downarrow;`, which the PowerShell JSON object model cannot represent). Values are emitted with
`\uXXXX` escapes so the generated source contains no literal non-ASCII or control characters.
