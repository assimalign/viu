# Assimalign.Viu.UtilityCss.Build

Add one private build reference to generate utility CSS from `.viu`, `.vue`, `.razor`, `.cshtml`, `.html`, and `.htm` files.

```xml
<PackageReference Include="Assimalign.Viu.UtilityCss.Build" Version="10.0.0-beta.26" PrivateAssets="all" />
```

Build normally; the package emits `obj/.../utilitycss/<AssemblyName>.utilities.css`. By default it also writes the versioned editor-discovery files `utilitycss.manifest.v1.json` and `utilitycss.catalog.v1.json` beside the bundle and declares the catalog as `@(ViuClassCatalog)`. Static-web-asset hosts register the CSS bundle as an asset and endpoint; plain projects copy it to `bin`. The host owns its HTML link. A single CSS-first entry stylesheet is optional:

```xml
<ItemGroup><ViuUtilityCss Include="utilities.css" /></ItemGroup>
```

| Property or item | Purpose |
|---|---|
| `ViuUtilityCssEnabled` | Opts out when set to `false`; defaults to `true`. |
| `ViuUtilityCssAutomaticSourceDiscovery` | Controls default recursive discovery. |
| `ViuUtilityCssSourcePatterns` | Overrides the default extension patterns. |
| `ViuUtilityCssBundleName` | Overrides `<AssemblyName>.utilities.css`. |
| `ViuUtilityCssCopyToOutput` | Controls plain-host output copying. |
| `ViuUtilityCssEmitEditorSidecar` | Emits the manifest and class catalog; defaults to `true`. |
| `ViuUtilityCssEditorCatalogMaximumItems` | Bounds catalog entries; defaults to the engine completion budget of `500`. |
| `ViuUtilityCssSourceIdentifier` | Overrides the Static Web Asset source identifier. |
| `@(ViuUtilityCss)` | Supplies zero or one CSS-first entry stylesheet. |
| `@(ViuUtilityCssSource)` | Adds explicit sources. |
| `@(ViuUtilityCssSourceExclude)` | Excludes sources. |

The [editor-sidecar contract](docs/EDITOR-SIDECAR.md) defines both versioned JSON formats, deterministic-write behavior, catalog priority, and compatibility policy.

No Viu SDK, Viu runtime, Node, or JavaScript toolchain is involved.
