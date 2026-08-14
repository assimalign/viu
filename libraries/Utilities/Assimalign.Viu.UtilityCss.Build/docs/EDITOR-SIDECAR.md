# Utility CSS editor sidecar

`Assimalign.Viu.UtilityCss.Build` writes two UTF-8 JSON files beside a successfully generated utility CSS bundle. Both files use stable ordinal ordering and byte-compare no-op writes. If the bundle becomes empty, or `ViuUtilityCssEmitEditorSidecar` is `false`, stale sidecars are removed.

The files are a discovery and class-data contract for project systems and language services. Additive fields may be introduced within version 1. Removing a field, changing a field's meaning, or otherwise making a breaking format change requires a version 2 filename rather than rewriting the version 1 contract.

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

`utilitycss.catalog.v1.json` has this shape:

```json
{
  "schemaVersion": 1,
  "truncated": true,
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

The task uses the engine's project-aware bounded completion query against the same registry, resolved theme, references, `@utility` definitions, and `@variant` definitions used for the bundle. The default budget is `500` and can be changed with `ViuUtilityCssEditorCatalogMaximumItems`.

Rules actually generated into the bundle receive priority within the budget. When those rules fit, completion and hover metadata for a source-used class is not displaced by a large themed catalog. If generated rules alone exceed the budget, the first engine-ordered generated rules are retained and `truncated` is `true`. Remaining slots are filled from the bounded project-aware completion result, de-duplicated by exact class text. Selected entries are finally ordered by engine `SortOrder` and then ordinal class text. `truncated` is also `true` when available completions were omitted.

`colorValue` is present only when structured engine metadata identifies a color-bearing class. Consumers must not infer colors by parsing `css`. Version 1 does not emit the optional `sortText` field; array order is authoritative.

When sidecars are enabled, the packaged targets declare the absolute catalog path as `@(ViuClassCatalog)` during MSBuild project evaluation, before generation runs. A project-system host can therefore discover the contract without importing Viu editor tooling.
