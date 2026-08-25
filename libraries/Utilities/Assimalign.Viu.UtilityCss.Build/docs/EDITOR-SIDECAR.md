# Utility CSS editor sidecar

`Assimalign.Viu.UtilityCss.Build` writes two UTF-8 JSON files beside a successfully generated utility CSS bundle. Both files use stable ordinal ordering and byte-compare no-op writes. If the bundle becomes empty, or `ViuUtilityCssEmitEditorSidecar` is `false`, stale sidecars are removed.

The manifest is the UtilityCss-owned discovery contract. Additive manifest fields may be introduced within version 1. Removing a manifest field, changing a field's meaning, or otherwise making a breaking manifest change requires a version 2 filename rather than rewriting the version 1 contract. The class catalog follows Viu's generic [class-catalog contract](https://github.com/assimalign/viu/blob/main/tooling/Editor/Assimalign.Viu.LanguageService/docs/CLASS-CATALOGS.md); that document is the format authority for the catalog payload.

## Manifest

`utilitycss.manifest.v1.json` has this shape:

```json
{
  "schemaVersion": 1,
  "engineVersion": "10.0.0.0",
  "entryStylesheetPath": "C:\\project\\utilities.css",
  "sourceFiles": ["C:\\project\\index.html"],
  "themeContentHash": "0123456789abcdef...",
  "bundle": {
    "path": "C:\\project\\obj\\utilitycss\\Project.utilities.css",
    "name": "Project.utilities.css"
  }
}
```

- `entryStylesheetPath` is an absolute path, or `null` when the project has no entry stylesheet.
- `sourceFiles` contains the absolute resolved markup inputs in ordinal path order.
- `themeContentHash` is lowercase SHA-256 over a canonical UTF-8 JSON snapshot of the resolved theme. The snapshot contains the prefix, important mode, properties ordered by name with their values and numeric options, and keyframes ordered by name with their bodies. It therefore covers referenced and entry-stylesheet theme content after resolution rather than hashing machine-dependent source traversal.
- `bundle.path` is absolute and names an existing bundle. `bundle.name` is its file name.
- `engineVersion` is the Utility CSS engine assembly's numeric version.

## Class catalog

`utilitycss.classcatalog.v1.json` has this shape:

```json
{
  "version": 1,
  "truncated": false,
  "entries": [
    {
      "class": "bg-brand",
      "css": ".bg-brand { ... }",
      "colorValue": "#123456"
    },
    {
      "class": "flex",
      "css": ".flex { ... }"
    }
  ]
}
```

The task uses the engine's project-aware bounded completion query against the same registry, resolved theme, references, and `@utility` definitions used for the bundle. It emits the complete finite base-name expansion: static and named built-ins, every theme-backed family, and negative theme values only where the registry's utility definition declares negative-value support. Variant-prefixed composites such as `hover:bg-brand` are excluded; live language-server completion owns variant composition. Catalog v1 also does not form the Cartesian product of slash modifiers such as every color with every opacity. A slash-modified class used in source still receives source-used priority, while live completion owns unused modifier composition.

Source-used base classes receive priority within the budget and retain deterministic engine order. The remaining base expansion follows in ordinal class-name order, de-duplicated by exact class text across both groups. If source-used classes alone exceed the budget, the first engine-ordered classes are retained. `truncated` is `true` whenever the configured budget omits an applicable entry.

`ViuUtilityCssCatalogMaximumEntries` controls the budget and defaults to `50000`. The v4.3.3 default theme currently expands to 24,087 base entries, leaving headroom for realistic project-defined theme tokens. Consumers can lower the property to trade completion breadth for a smaller sidecar or raise it for an unusually large theme; the `truncated` signal remains authoritative.

`colorValue` is present only when structured engine metadata identifies a color-bearing class. Consumers must not infer colors by parsing `css`. Version 1 does not emit the optional `sortText` field; array order is authoritative.

When sidecars are enabled, the packaged targets declare the absolute catalog path as `@(ViuClassCatalog)` during MSBuild project evaluation, before generation runs. A project-system host can therefore discover the contract without importing Viu editor tooling.

## Generated-asset dependency manifest

The editor sidecars above are not hot-reload inputs: they describe a non-empty generated bundle and are intentionally removed when that bundle becomes empty. For the Browser SDK generated-asset seam, UtilityCss instead writes `utilitycss.generated-asset-dependencies.v1` whenever `ViuGeneratedAssetSeamVersion` is exactly `1`. The file persists across empty output and uses the generic seam's line-oriented dependency-manifest format:

```text
viu-generated-asset-dependencies-v1
file:<base64-encoded absolute UTF-8 path>
root:<base64-encoded absolute UTF-8 path>
```

`file:` records include the resolved source set, the entry stylesheet, and every resolved or currently missing path in its recursive `@reference` closure. A direct source or entry item with `Watch="false"` is omitted while remaining available to generation; referenced files retain their own dependency records. A markup path below an independently declared automatic-discovery root remains covered by that root. `root:` records include directories or glob roots introduced by `@source`. Records use deterministic platform path order, are duplicate-free, and are byte-compared before writing so an unchanged dependency graph retains its timestamp. The manifest is declared through the public `DependencyManifestPath` metadata on `@(ViuGeneratedAsset)`; it is neither an editor-discovery contract nor an invitation for consumers to import UtilityCss-private MSBuild names.

UtilityCss registers its bundle only when the host advertises generated-asset seam version `1`. Absent and incompatible versions remain silently build-triggered. This keeps independently versioned packages inert across unknown seam revisions while allowing a matching Browser SDK to regenerate and live-swap the bundle under `dotnet watch`. Specified by [V01.01.12.30.04], issue [#355](https://github.com/assimalign/viu/issues/355).
