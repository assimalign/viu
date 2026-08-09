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

## Where a .props/.targets file lives — exactly two homes

Every MSBuild props/targets file belongs to **one** of these, and per-project `build/` folders scattered
next to a library's or analyzer's `src/` are not a third option:

1. **The owning SDK segment's `Targets/` folder** —
   `sdks/Assimalign.Viu.Sdk/Targets/` holds host-neutral generator and component-library pack logic;
   `sdks/Assimalign.Viu.Sdk.Browser/Targets/` holds WebAssembly, browser asset, CSS application/hot
   reload, and publish logic. `sdks/Directory.Build.targets` packs each folder into its owning SDK
   nupkg with a `Targets\**\*` glob, and that segment's `Sdk/Sdk.props`/`Sdk.targets` import from there.
   Shared behavior belongs only in the base SDK; the Browser SDK imports it. In-repo projects import
   the *same* base file directly (see `Assimalign.Viu.Generators.Syntax.props|.targets`, imported by
   `build/Targets/Build.References.Analyzers.targets` under `$(ViuUseSingleFileComponents)`), so the
   packaged and dogfooded paths cannot drift.
2. **`build/`** (repository root) — it supports this repository's own pipeline.

A file that must reach consumers under a *different* packaged name stays in `build/` and is packed with
an explicit `PackagePath` (the CSS bundling / utility / hot-reload targets do this). An analyzer project
ships only source and its DLL.

## Reference projects and packages by name

Never write a raw `<ProjectReference Include="..\..\...csproj" />` or `<PackageReference>` in a library,
test, or example csproj. Use the by-name item groups the build system resolves:

- **`<ViuProjectReference Include="Assimalign.Viu.Components" />`** — public project reference (flows as a
  `.nupkg` dependency). Resolved by assembly name against every indexed code root —
  `libraries/`, `tooling/`, `analyzers/`, `sdks/`, `extensions/`, and `benchmarks/` — see
  `build/Targets/Build.References.Projects.targets`.
- **`<ViuPrivateProjectReference Include="..." />`** — private reference (`PrivateAssets=all`; does not
  flow to consumers).
- **`<ViuPackageReference Include="xunit" />`** — package reference with **no `Version` attribute**;
  versions are centralized in `build/Targets/Build.References.Packages.targets`. To add or bump a package,
  edit that central file.
- **`<ViuAnalyzerReference … />`** — for Roslyn analyzers / source generators (see
  `build/Targets/Build.References.Analyzers.targets`).

## Target framework and language

- Opt a project into its TFM via the central alias, never a hardcoded string:
  `<TargetFramework>$(TargetFrameworkForLibraries)</TargetFramework>` (net10.0). Analyzers and
  compiler/build-time tooling that must load inside Roslyn or MSBuild hosts use
  `$(TargetFrameworkForAnalyzers)` (netstandard2.0); their test projects still use the library alias.
- `Nullable`, `LangVersion=preview`, `EnablePreviewFeatures=false`, and `EnforceCodeStyleInBuild` flow
  centrally from `build/Targets/` — do **not** set them per-csproj.
- **C# preview *language* features are on; runtime preview *APIs* are off.** `LangVersion=Preview` and
  `EnablePreviewFeatures` are independent switches. Shipped assemblies must not emit
  `[assembly: RequiresPreviewFeatures]` — it is viral, forcing every consumer to opt in
  ([V01.01.14.07]). A project needing a runtime preview API (`static abstract` interface members,
  `INumber<T>`, `IParsable<T>`) has to justify making that requirement viral for consumers; it is not
  a per-csproj convenience.

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
    <ViuProjectReference Include="Assimalign.Viu.Components" />
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

Sample apps live in `assimalign/viu-examples` and consume the packaged
`Assimalign.Viu.Sdk.Browser`/frameworks from `_out/packages`; they must not use
`ViuProjectReference`. Host-neutral component-library consumers use `Assimalign.Viu.Sdk`.

## Versioning and packaging

- The version is centralized in `build/Targets/Build.Version.props` (`$(ViuVersion)` /
  `ViuVersionPrefix` / `ViuVersionSuffix`). **No per-project `<Version>`** — set `VersionPrefix` /
  `VersionSuffix` only through the central file.
- Package output goes to `$(ViuOutputPathForLibraries)` (`_out/packages`).

## Adding a new library

1. `libraries/Assimalign.Viu.<Name>/{src,test}` with the two csproj shapes above — or
   `tooling/Assimalign.Viu.<Name>/{src,test}` for compiler, build-time, or editor code. The `tooling/`
   location carries that role; do not add a blanket `Tooling.` segment to the assembly id or namespace.
   Use the specific product role instead (`Syntax.*`, `Compiler.*`, `UtilityCss`, `LanguageService`,
   or `LanguageServer`).
2. Add both csprojs to `Assimalign.Viu.slnx`.
3. Wire a CI workflow entry for the area ([V01.01.12.02]).
4. No dangling references — when a project is renamed or moved, update every referrer.
5. If the library is host-neutral framework runtime, add it to `@(ViuFrameworkAssembly)` in
   `frameworks/Assimalign.Viu.App.props`. If it is Browser-only, add it to the Browser segment in
   `frameworks/Assimalign.Viu.App.Browser.props`. Do not make a host-neutral library depend on the
   Browser segment.
6. If the library is packable, add its package id to `$script:ViuLibraryPackageIds` in
   `scripts/modules/ViuPackaging.psm1`; the drift guard scans both `libraries/` and `tooling/` and
   fails the pack when a packable project is missing from the inventory.

## SDK and shared-framework packaging ([V01.01.12.19], #174; [V01.01.12.27], #323)

External consumers use the SDK matching their topology — never `ViuProjectReference`, which is the
**in-repo dogfooding** mechanism:

- **`frameworks/Assimalign.Viu.App.props`** — the authoritative host-neutral
  `@(ViuFrameworkAssembly)` / `@(ViuFrameworkAnalyzer)` manifest: Reactivity, Components, State,
  Core, and the generator/parser closure. `frameworks/Assimalign.Viu.App.Refs/` produces the
  targeting-only `Assimalign.Viu.App.Ref` package with `ref/<tfm>/`, `data/FrameworkList.xml`, the
  four-entry `data/PackageOverrides.txt`, and analyzers under `analyzers/dotnet/cs/`. The base
  framework is targeting-only and has no runtime package.
- **`frameworks/Assimalign.Viu.App.Browser.props`** — the Browser-only manifest adds
  `Assimalign.Viu.Browser`. `frameworks/Assimalign.Viu.App.Browser.Refs/` produces
  `Assimalign.Viu.App.Browser.Ref`, containing the Browser reference and its one override entry;
  `frameworks/Assimalign.Viu.App.Browser.Runtime/` produces
  `Assimalign.Viu.App.Browser.Runtime.browser-wasm`, containing the base-plus-Browser implementation
  closure and `data/RuntimeList.xml`. The runtime pack never carries `PackageOverrides.txt`.
- **`frameworks/Assimalign.Viu.App.targets`** remains the shared manifest writer and pack-layout
  implementation. Segment props select the assemblies, analyzers, framework name, and Ref/Runtime
  kind; do not duplicate the writer in the Browser segment.
- **`sdks/Assimalign.Viu.Sdk/`** — the component-library SDK. Its `Tasks/` project produces
  `Assimalign.Viu.Sdk`, `Sdk.props` chains `Microsoft.NET.Sdk`, and the SDK registers a
  targeting-only `FrameworkReference` to `Assimalign.Viu.App`. It owns `.viu`/`.vue`
  `AdditionalFiles`, Syntax/Reactivity generators, and component-style extraction. Packing a library
  carries its `.viu.css` plus generated `buildTransitive` registration; the base SDK never loads
  Browser, requires a WebAssembly workload, registers browser static assets, or writes `wwwroot`.
- **`sdks/Assimalign.Viu.Sdk.Browser/`** — the application SDK. Its `Tasks/` project produces
  `Assimalign.Viu.Sdk.Browser` with an exact-version dependency on the base SDK. Its SDK imports the
  base, chains `Microsoft.NET.Sdk.WebAssembly`, registers `Assimalign.Viu.App.Browser`, consumes
  transitive component-library style registrations as browser static assets, and owns
  `viu-dom.js`, application CSS/utility bundling and link injection, the CSS hot-reload worker,
  WebAssembly fixes, and publish-budget hooks.
- Both SDKs import the pack-time-frozen `Build.Version.props` snapshot. Shared authoring logic stays
  in the base SDK; browser-only logic stays in the Browser SDK.
- **Local loop**: `scripts/Install-Local.ps1` packs both SDKs, both targeting packs, and the one
  Browser runtime pack into `_out/packages` (gitignored). Consumption docs: `sdks/README.md`.
- The `frameworks/` csprojs carry documented deviations from the no-raw-`ProjectReference` rule
  (build-order edges needing `ReferenceOutputAssembly=false` + `UndefineProperties` metadata the
  `ViuProjectReference` transform does not carry).
