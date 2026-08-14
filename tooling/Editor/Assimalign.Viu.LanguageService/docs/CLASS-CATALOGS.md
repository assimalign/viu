# Build-contributed class catalogs

A class catalog is a build-produced JSON file that contributes CSS class completion and hover data
to Viu documents without linking the language service to the producing tool. This is the generic
editor contract for [V01.01.12.30] (#346): the producer owns how it derives classes, while Viu owns
only discovery, validation, and editor precedence.

## Version 1 format

A version-one catalog has a filename ending in `*.classcatalog.v1.json` and this shape:

```json
{
  "version": 1,
  "entries": [
    {
      "class": "surface-accent",
      "css": ".surface-accent { background: #123456; }",
      "colorValue": "#123456",
      "sortText": "0010:surface-accent"
    }
  ],
  "truncated": false
}
```

`version`, `entries`, and `truncated` are required. Each entry requires non-empty `class` and `css`
strings. `colorValue` and `sortText` are optional non-empty strings:

- `colorValue` is the computed CSS color represented by the class. Viu transports it directly and
  gives the completion the Language Server Protocol `Color` kind; consumers do not infer a color by
  parsing `css`.
- `sortText` is the producer's stable ordering hint within the build-contributed completion band.
  When absent, the entry's merged source order is used.
- `truncated` is `true` when the producer had applicable entries that are absent from this file. A
  consumer must report completion from that catalog as incomplete, including when prefix filtering
  returns fewer than the language-service item limit.

Version 1 is additive: consumers ignore unknown root and entry properties. A malformed catalog, an
unsupported `version`, or an entry missing a required field invalidates that file but does not hide
other valid files. A breaking format uses a new suffix and matching root version, beginning with
`*.classcatalog.v2.json`; a version-one consumer does not interpret it as version 1.

## Discovery and refresh

For a document owned by a project, the host recursively enumerates the project's `obj` directory for
`*.classcatalog.v1.json`. When more than one path has the same filename, the newest last-write time
wins; an ordinal full-path comparison breaks an exact timestamp tie. Distinct filenames are retained
and merged in ordinal filename order.

Discovery runs at completion and hover request boundaries. The host compares each selected file's
absolute path, existence, last-write time, and length with its previous snapshot. Unchanged files
reuse the same immutable configuration instance; any discovery or stamp change causes every selected
file to be read again and a new configuration to replace it. An unreadable file is omitted for that
request, and a later request can recover without restarting the server. Overlapping feature requests
retain concurrent computation, while an independently ordered class-catalog discovery generation
prevents an older catalog snapshot from replacing one already applied by a later catalog discovery.

## Completion and hover precedence

Component `<style>` classes have absolute precedence. Viu collects them first in the reserved
component-style sort band, then drops every catalog entry whose exact ordinal class name is already
authored in the component. Duplicate catalog names are first-wins in the deterministic catalog and
entry order above.

The shared `LanguageCompletionLimits.MaximumItems` bound is applied once to that merged sequence, not
once per source. Reaching the bound or reading any catalog with `truncated: true` makes the completion
list incomplete. A catalog entry with `colorValue` produces a color completion and carries that value
through the existing editor-neutral completion transport.

Hover follows the same ownership rule. An authored component-style declaration is returned first. If
none owns the class-value token, an exact catalog match returns the entry's `css` in a fenced CSS body.
