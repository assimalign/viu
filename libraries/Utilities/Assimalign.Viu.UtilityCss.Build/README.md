# Assimalign.Viu.UtilityCss.Build

[![NuGet version](https://img.shields.io/nuget/v/Assimalign.Viu.UtilityCss.Build?logo=nuget)](https://www.nuget.org/packages/Assimalign.Viu.UtilityCss.Build) [![NuGet downloads](https://img.shields.io/nuget/dt/Assimalign.Viu.UtilityCss.Build?logo=nuget)](https://www.nuget.org/packages/Assimalign.Viu.UtilityCss.Build)

Add one private build reference to generate utility CSS from `.viu`, `.vue`, `.razor`, `.cshtml`, `.html`, and `.htm` files.

```xml
<PackageReference Include="Assimalign.Viu.UtilityCss.Build" Version="10.0.0-beta.26" PrivateAssets="all" />
```

Build normally; the package emits `obj/.../utilitycss/<AssemblyName>.utilities.css`. By default it also writes the versioned editor-discovery files `utilitycss.manifest.v1.json` and `utilitycss.classcatalog.v1.json` beside the bundle and declares the catalog as `@(ViuClassCatalog)`. Static-web-asset hosts register the CSS bundle as an asset and endpoint; plain projects copy it to `bin`. The host owns its HTML link. A single CSS-first entry stylesheet is optional:

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
| `ViuUtilityCssCatalogMaximumEntries` | Bounds base-class catalog entries; defaults to `50000`. A value below the applicable expansion reduces sidecar size and sets `truncated`; higher values accommodate unusually large project themes. |
| `ViuUtilityCssSourceIdentifier` | Overrides the Static Web Asset source identifier. |
| `@(ViuUtilityCss)` | Supplies zero or one CSS-first entry stylesheet. |
| `@(ViuUtilityCssSource)` | Adds explicit sources. |
| `@(ViuUtilityCssSourceExclude)` | Excludes sources. |

The [editor-sidecar contract](docs/EDITOR-SIDECAR.md) defines the UtilityCss-owned manifest and producer behavior. The catalog payload follows Viu's generic [class-catalog contract](https://github.com/assimalign/viu/blob/main/tooling/Editor/Assimalign.Viu.LanguageService/docs/CLASS-CATALOGS.md).

No Viu SDK, Viu runtime, Node, or JavaScript toolchain is involved.
