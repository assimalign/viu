---
paths:
  - "**/*.csproj"
  - "**/*.props"
  - "**/*.targets"
  - "build/**"
  - "Directory.Build.props"
  - "Directory.Build.targets"
  - "global.json"
  - "**/*.slnx"
---

# Build system

Shared build logic is centralized under `build/` and imported repo-wide via `Directory.Build.props`
(→ `build/Build.props`) and `Directory.Build.targets` (→ `build/Build.targets`). **Shared build logic
belongs in `build/`, never duplicated in individual csprojs** — this is the most drift-prone area.

## Reference projects and packages by name

Never write a raw `<ProjectReference Include="..\..\...csproj" />` or `<PackageReference>` in a library,
test, or example csproj. Use the by-name item groups the build system resolves:

- **`<ViuProjectReference Include="Assimalign.Viu.Shared" />`** — public project reference (flows as a
  `.nupkg` dependency). Resolved by assembly name against `libraries/**/*.csproj`.
- **`<ViuPrivateProjectReference Include="..." />`** — private reference (`PrivateAssets=all`; does not
  flow to consumers).
- **`<ViuPackageReference Include="xunit" />`** — package reference with **no `Version` attribute**;
  versions are centralized in `build/Targets/Build.References.Packages.targets`. To add or bump a package,
  edit that central file.
- **`<ViuAnalyzerReference … />`** — for Roslyn analyzers / source generators (see
  `build/Targets/Build.References.Analyzers.targets`).

## Target framework and language

- Opt a project into its TFM via the central alias, never a hardcoded string:
  `<TargetFramework>$(TargetFrameworkForLibraries)</TargetFramework>` (net10.0). Analyzers use
  `$(TargetFrameworkForAnalyzers)` (netstandard2.0).
- `Nullable`, `LangVersion=preview`, `EnablePreviewFeatures=true`, and `EnforceCodeStyleInBuild` flow
  centrally from `build/Targets/` — do **not** set them per-csproj.

## csproj shapes

Shipping library (`src/`):

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>$(TargetFrameworkForLibraries)</TargetFramework>
    <IsAotCompatible>true</IsAotCompatible>
  </PropertyGroup>
  <!-- optional -->
  <ItemGroup>
    <ViuProjectReference Include="Assimalign.Viu.Shared" />
  </ItemGroup>
</Project>
```

Test project (`test/`):

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>$(TargetFrameworkForLibraries)</TargetFramework>
    <IsPackable>false</IsPackable>
  </PropertyGroup>
  <ItemGroup>
    <ViuPackageReference Include="Microsoft.NET.Test.Sdk" />
    <ViuPackageReference Include="xunit" />
    <ViuPackageReference Include="xunit.runner.visualstudio" />
    <ViuPackageReference Include="Shouldly" />
  </ItemGroup>
  <ItemGroup>
    <ViuProjectReference Include="Assimalign.Viu.<Name>" />
  </ItemGroup>
</Project>
```

Sample apps (`examples/`) keep their own SDK (e.g. `Microsoft.NET.Sdk.WebAssembly`), set
`<TargetFramework>$(TargetFrameworkLatest)</TargetFramework>`, use `ViuProjectReference`, and do **not**
set `IsAotCompatible` (they are not shipping libraries).

## Versioning and packaging

- The version is centralized in `build/Targets/Build.Version.props` (`$(ViuVersion)` /
  `ViuVersionPrefix` / `ViuVersionSuffix`). **No per-project `<Version>`** — set `VersionPrefix` /
  `VersionSuffix` only through the central file.
- Package output goes to `$(ViuOutputPathForLibraries)` (`_out/packages`).

## Adding a new library

1. `libraries/Assimalign.Viu.<Name>/{src,test}` with the two csproj shapes above.
2. Add both csprojs to `Assimalign.Viu.slnx`.
3. Wire a CI workflow entry for the area ([V01.01.12.02]).
4. No dangling references — when a project is renamed or moved, update every referrer.
5. If the library is a runtime framework member (ships in every Viu app), add it to
   `@(ViuFrameworkAssembly)` in `frameworks/Assimalign.Viu.App.props` so the framework packs
   deliver it.

## SDK and shared-framework packaging ([V01.01.12.19], #174)

External consumers use `<Project Sdk="Assimalign.Viu.Sdk">` — never `ViuProjectReference`, which is
the **in-repo dogfooding** mechanism. The packaging layer mirrors `assimalign/cohesion`:

- **`frameworks/Assimalign.Viu.App.props`** — the authoritative `@(ViuFrameworkAssembly)` /
  `@(ViuFrameworkAnalyzer)` manifest, gated on `$(ViuFrameworkName)`.
- **`frameworks/Assimalign.Viu.App.targets`** — the `ViuWriteFrameworkList` manifest writer and
  pack layout, branching on `$(ViuFrameworkKind)` = `Ref` (targeting pack: `ref/<tfm>/` +
  `data/FrameworkList.xml` + the generators and their parser closure at `analyzers/dotnet/cs/`,
  every DLL listed as an `Analyzer` entry) | `Runtime` (per-RID runtime pack: `runtimes/<rid>/lib/`
  + `data/RuntimeList.xml`).
- **`sdks/Assimalign.Viu.Sdk/`** — the SDK package (packable unit: `Tasks/…Tasks.csproj` with
  `PackageId=Assimalign.Viu.Sdk`). `Sdk.props` chains `Microsoft.NET.Sdk.WebAssembly`, imports a
  pack-time-frozen `Build.Version.props` snapshot, and registers the `KnownFrameworkReference` for
  `Assimalign.Viu.App` (`browser-wasm`). The `.viu` AdditionalFiles wiring and
  `Build.Css.Bundling.targets` are packed **from their in-repo source files**; the `ViuBundleCss`
  task ships under `Tasks/`; `viu-dom.js` ships under `assets/` and flows into consumer
  `wwwroot/_content/`.
- **Local loop**: `scripts/Install-Local.ps1` packs SDK → runtime pack(s) → ref pack into
  `_out/packages` (gitignored). Consumption docs: `sdks/README.md`.
- The `frameworks/` csprojs carry documented deviations from the no-raw-`ProjectReference` rule
  (build-order edges needing `ReferenceOutputAssembly=false` + `UndefineProperties` metadata the
  `ViuProjectReference` transform does not carry).
