# Assimalign.Viu.UtilityCss

A standalone .NET utility CSS engine for programmatic build and editor integrations.

```xml
<PackageReference Include="Assimalign.Viu.UtilityCss" Version="10.0.0-beta.26" />
```

Call `UtilityCandidateScanner.Scan(...)` to discover candidates and `UtilityCssCompiler.Compile(...)` to emit deterministic CSS. For automatic project builds, reference `Assimalign.Viu.UtilityCss.Build` instead:

```xml
<PackageReference Include="Assimalign.Viu.UtilityCss.Build" Version="10.0.0-beta.26" PrivateAssets="all" />
```

That build package needs no Viu SDK, runtime, Node, or JavaScript toolchain. An entry stylesheet is optional.

| Build property or item | Purpose |
|---|---|
| `ViuUtilityCssEnabled` | Enables generation; defaults to `true`. |
| `ViuUtilityCssAutomaticSourceDiscovery` | Controls default recursive discovery. |
| `ViuUtilityCssSourcePatterns` | Overrides the default extension patterns. |
| `ViuUtilityCssBundleName` | Selects the generated CSS file name. |
| `ViuUtilityCssCopyToOutput` | Controls plain-host output copying. |
| `ViuUtilityCssSourceIdentifier` | Overrides the Static Web Asset source identifier. |
| `@(ViuUtilityCss)` | Supplies the optional single CSS-first entry stylesheet. |
| `@(ViuUtilityCssSource)` | Adds explicit markup sources. |
| `@(ViuUtilityCssSourceExclude)` | Removes sources from discovery. |
