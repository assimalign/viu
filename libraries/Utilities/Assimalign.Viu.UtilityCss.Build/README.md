# Assimalign.Viu.UtilityCss.Build

Add one private build reference to generate utility CSS from `.viu`, `.vue`, `.razor`, `.cshtml`, `.html`, and `.htm` files.

```xml
<PackageReference Include="Assimalign.Viu.UtilityCss.Build" Version="10.0.0-beta.26" PrivateAssets="all" />
```

Build normally; the package emits `obj/.../utilitycss/<AssemblyName>.utilities.css`. Static-web-asset hosts register it as an asset and endpoint; plain projects copy it to `bin`. The host owns its HTML link. A single CSS-first entry stylesheet is optional:

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
| `ViuUtilityCssSourceIdentifier` | Overrides the Static Web Asset source identifier. |
| `@(ViuUtilityCss)` | Supplies zero or one CSS-first entry stylesheet. |
| `@(ViuUtilityCssSource)` | Adds explicit sources. |
| `@(ViuUtilityCssSourceExclude)` | Excludes sources. |

No Viu SDK, Viu runtime, Node, or JavaScript toolchain is involved.
