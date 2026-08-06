# Viu SDK

Viu ships an MSBuild project SDK, **`Assimalign.Viu.Sdk`**, that chains through
`Microsoft.NET.Sdk.WebAssembly` and delivers the whole framework through a single
shared-framework reference — the same packaging model as the sibling Cohesion repo
(`Assimalign.Cohesion.Sdk` / `Assimalign.Cohesion.App`), re-targeted at WASM browser apps.

A complete Viu app project is:

```xml
<Project Sdk="Assimalign.Viu.Sdk">
    <PropertyGroup>
        <TargetFramework>net10.0</TargetFramework>
    </PropertyGroup>
</Project>
```

Pin the SDK version inline (`Sdk="Assimalign.Viu.Sdk/10.0.1-preview.2"`) or in
`global.json`:

```json
{
    "msbuild-sdks": {
        "Assimalign.Viu.Sdk": "10.0.1-preview.2"
    }
}
```

The SDK is resolved by NuGet's built-in MSBuild SDK resolver — the same machinery
that handles `Microsoft.NET.Sdk.Web` — so it works in Visual Studio, Rider, and the
dotnet CLI with no installer and no admin rights.

## What the SDK gives a consumer

| Piece | Mechanism |
| --- | --- |
| WASM browser app model | `Sdk.props`/`Sdk.targets` chain `Microsoft.NET.Sdk.WebAssembly` |
| The framework libraries (`Assimalign.Viu.Shared`, `.Components`, `.Reactivity`, `.State`, `.Core`, `.Browser`) | Implicit `<FrameworkReference Include="Assimalign.Viu.App" />` via the `KnownFrameworkReference` registration in [Targets/Assimalign.Viu.Sdk.FrameworkReference.props](Assimalign.Viu.Sdk/Targets/Assimalign.Viu.Sdk.FrameworkReference.props) |
| The `[Reactive]` and `.viu`/`.vue` source generators | Shipped inside the `Assimalign.Viu.App.Ref` targeting pack at `analyzers/dotnet/cs/` and listed as `<File Type="Analyzer">` in its `data/FrameworkList.xml` |
| `.viu` and tag-based `.vue` single-file component compilation | The generator's AdditionalFiles/CompilerVisibleProperty wiring in [Targets/Assimalign.Viu.Generators.Syntax.props](Assimalign.Viu.Sdk/Targets/Assimalign.Viu.Generators.Syntax.props) and [.targets](Assimalign.Viu.Sdk/Targets/Assimalign.Viu.Generators.Syntax.targets) — the single authoritative copy, packed into the SDK's `Targets/` and also imported directly by in-repo projects. `.vue` scripts require explicit `lang="csharp"`; JavaScript is never executed |
| `.viu`/`.vue` component-style CSS bundling | The `ViuBundleCss` MSBuild task (+ parser closure) in the SDK package's `Tasks/`, driven by the packed `Assimalign.Viu.Sdk.Css.Bundling.targets`. The bundle registers as a **content-fingerprinted** static web asset ([V01.01.12.12.03]) |
| `.viu`/`.vue` component stylesheet `<link>` | **Injected automatically** — no manual link tag. The `ViuInjectCssBundleLink` task (same `Tasks/` assembly) splices `<link rel="stylesheet" href="<AssemblyName>.viu.css" />` into `wwwroot/index.html` at build, *before* the SDK's compression pipeline so gzip/brotli negotiation stays intact ([V01.01.12.12.01]). The href is the stable plain route a static host serves; a fingerprinted route is also registered for manifest-aware hosts. Opt out with `<ViuInjectSingleFileComponentCssLink>false</ViuInjectSingleFileComponentCssLink>` (a hand-authored link also suppresses injection) |
| Standalone utility CSS | `ViuBundleUtilityCss` slices `.viu`/`.vue` template regions, scans host `.html`/`.htm` markup, and compiles the frozen Viu-owned Tailwind CSS v4.3.3-compatible manifest to a separate `<PackageId>.utilities.css` static web asset. It has no Tailwind, Node, CLI, or PostCSS dependency. Component CSS and utility CSS can be enabled independently |
| Utility stylesheet `<link>`, CSS-first configuration, and watch inputs | The utility link is injected through the same compression-safe host-page rewrite. Supported markup sources and the optional singular `@(ViuUtilityCss)` entry flow to `dotnet watch`; the virtual import, source rules, theme, custom utilities, and custom variants configure the same immutable engine used by build generation, completion, and hover. Set `<ViuInjectUtilityCssLink>false</ViuInjectUtilityCssLink>` to author the link manually |
| `viu-dom.js` interop bridge | Packed under `assets/` and copied to the consumer's `wwwroot/_content/Assimalign.Viu.Browser/` at build |

The framework reference resolves to two NuGet packages (the
`Microsoft.AspNetCore.App.Ref` / `.Runtime.<rid>` shape):

| Package | Contents | When restored |
| --- | --- | --- |
| `Assimalign.Viu.App.Ref` | `ref/net10.0/` reference assemblies, `data/FrameworkList.xml`, `analyzers/dotnet/cs/` generators | Compile time |
| `Assimalign.Viu.App.Runtime.browser-wasm` | `runtimes/browser-wasm/lib/net10.0/` implementation assemblies, `data/RuntimeList.xml` | App build/publish |

Opt out / pin independently:

```xml
<PropertyGroup>
    <!-- Skip the implicit FrameworkReference (explicit ones keep working). -->
    <ViuAutoIncludeAppFramework>false</ViuAutoIncludeAppFramework>
    <!-- Pin the App framework independently of the SDK version. -->
    <ViuAppFrameworkVersion>10.0.2</ViuAppFrameworkVersion>
    <!-- Keep component compilation but skip CSS bundling / disable component generation entirely. -->
    <ViuBundleSingleFileComponentCss>false</ViuBundleSingleFileComponentCss>
    <EnableSingleFileComponentGeneration>false</EnableSingleFileComponentGeneration>
    <!-- Utility CSS is independent from both switches above. -->
    <ViuBundleUtilityCss>false</ViuBundleUtilityCss>
    <!-- Keep explicit @(ViuUtilityCssSource) items but disable automatic globs. -->
    <ViuUtilityCssAutomaticSourceDiscovery>false</ViuUtilityCssAutomaticSourceDiscovery>
</PropertyGroup>
```

Utility source inclusion and exclusion stay in MSBuild:

```xml
<ItemGroup>
    <!-- Optional singular CSS-first configuration entry; it is not scanned for class candidates. -->
    <ViuUtilityCss Include="Utilities.css" />
    <ViuUtilityCssSource Include="..\shared-markup\**\*.html" />
    <ViuUtilityCssSourceExclude Include="Legacy\**\*" />
</ItemGroup>
```

## Viu Utilities: frozen v4.3.3 surface

Viu Utilities is pinned to Tailwind CSS **v4.3.3** behavior. The machine-readable
[`compatibility-v4.3.3.json`](../tooling/Assimalign.Viu.UtilityCss/conformance/compatibility-v4.3.3.json)
is the authority: it freezes 382 utility roots, 88 variants, 21 theme namespaces, the supported
value and modifier modes, eight source forms, and the executable customization directives and
functions. Independently authored
[`golden-vectors-v4.3.3.json`](../tooling/Assimalign.Viu.UtilityCss/conformance/golden-vectors-v4.3.3.json)
pins generated CSS and metadata ordering.

`Utilities.css` is the singular CSS-first project entry. A complete entry can use the Viu-owned
virtual import and configuration surface without installing Tailwind:

```css
@import "viu-utilities" source("../Components") prefix(vu) theme(inline) important;

@source "../Shared";
@source not "../Shared/Legacy";
@source inline("vu:{block,grid}");
@source not inline("vu:hidden");

@theme {
  --color-brand: oklch(0.63 0.19 260);
  --spacing-18: 4.5rem;
  --breakpoint-tablet: 52rem;
}

@reference "./shared-utilities.css";

@utility content-auto {
  content-visibility: auto;
}

@utility tab-* {
  tab-size: --value(--tab-size-*, integer, [integer], --default(4));
}

@custom-variant theme-midnight (&:where([data-theme="midnight"] *));

.card {
  @apply vu:p-4 vu:bg-brand;

  @variant hover {
    outline: 2px solid currentColor;
  }
}
```

`"viu-utilities"` is a compiler sentinel, not a package name. Import modifiers support
`source(<path>)`, `source(none)`, `prefix(...)`, `theme(inline)`, `theme(static)`, and `important`.
The prefix is the first class variant (`vu:bg-brand`, `vu:tablet:p-18`), while theme custom
properties remain unprefixed in authored `@theme` declarations.

Source paths are resolved relative to `Utilities.css`. `@source` supports include and `not`
exclusion paths plus inline inclusion/exclusion with brace expansion and numeric ranges.
`@theme` supports normal, `inline`, `static`, `reference`, and `default` modes; overrides,
namespace resets, full reset, and custom-property references use the same immutable theme as
generation and editor resolution. The registry's default keyframes remain available, but
project-authored `@keyframes` nested inside `@theme` are not yet parsed.

The executable customization surface is:

- `@utility` for static, nested, negative, and functional utilities;
- `@custom-variant` for selector or `@slot` variants;
- `@variant` for applying one or more variants inside authored CSS;
- `@apply` for built-in or custom utility composition;
- `@reference` for importing utility and variant definitions without copying the referenced
  stylesheet's ordinary CSS;
- `--value()`, `--modifier()`, `--default()`, `--spacing()`, and `--alpha()` inside functional
  definitions and authored CSS.

Candidate detection remains intentionally plain-text and markup-only. Automatic discovery scans
`.viu` and `.vue` `<template>` content, and `.html`/`.htm`; it never scans
`.viu`/`.vue` scripts, component style blocks, ordinary `.css`, or `.cs`. Runtime interpolation
cannot create a discoverable class, so place every complete alternative in template text or add it
with `@source inline(...)`. Code-first C# utility discovery is deferred to a separate feature.

Viu Utilities is an independent Viu feature compatible with documented Tailwind CSS v4.3.3
behavior. It is not affiliated with or endorsed by Tailwind Labs.

### Stylesheet hot reload

In a Debug `dotnet watch` session, the SDK starts one project-scoped CSS regeneration worker.
It observes `.viu`, `.vue`, the utility entry, explicit `@(ViuUtilityCssSource)` inputs, supported
automatically discovered utility markup, transitive `@reference` stylesheets, and markup below
CSS-first `source(...)`/`@source` roots, including external roots and add, delete, and rename
changes. The compiler writes that resolved dependency graph to an intermediate manifest after
each regeneration, so later configuration edits update the live watch set without scanning
unrelated parent directories. It coalesces an editor save and invokes the deterministic component
and utility bundlers once. The generated `<PackageId>.viu.css` and
`<PackageId>.utilities.css` files are registered with the .NET watch host as static web assets.
The host's browser-refresh client replaces their `<link>` elements with cache-busted URLs, so this
CSS-only update does not remount the Viu application or discard browser state. A semantic no-op
does not touch the bundle.

Starting a watch session with no resolved utilities, or removing the final rule, emits one marked,
zero-byte development bundle. This pre-registers the stable asset and link before the first utility
appears and lets the browser unload the old stylesheet after the last utility disappears. A
subsequent ordinary build or publish deletes that tombstone. The worker is
an SDK build tool only: it is disabled by default outside Debug and is never copied into the
application, runtime framework, or publish output. Set
`<ViuCssHotReloadEnabled>false</ViuCssHotReloadEnabled>` to disable it, or adjust the default
100-millisecond quiet period with `<ViuCssHotReloadDebounceMilliseconds>...</ViuCssHotReloadDebounceMilliseconds>`.

This integration targets the `dotnet watch` watch-list contract. Visual Studio's ordinary Hot
Reload command does not currently invoke that contract, so automatic Viu stylesheet regeneration
there requires launching the project through `dotnet watch`. The state-preserving guarantee covers
stylesheet-link replacement only. Generated marker types classify managed component deltas:
template and C# script edits remount the affected component so .NET 10 browser WebAssembly executes
the updated generated code, while style-only edits remain mounted.

## Local development loop

`scripts/Install-Local.ps1` packs the complete package set into the repo-local
feed `_out/packages/`:

1. every independently published library → `Assimalign.Viu.<Name>.<ver>.nupkg` (19 packages)
2. `dotnet pack sdks/Assimalign.Viu.Sdk/Tasks` → `Assimalign.Viu.Sdk.<ver>.nupkg`
3. `dotnet pack frameworks/Assimalign.Viu.App.Runtime/src -p:RuntimeIdentifier=browser-wasm` → `Assimalign.Viu.App.Runtime.browser-wasm.<ver>.nupkg`
4. `dotnet pack frameworks/Assimalign.Viu.App.Refs/src` → `Assimalign.Viu.App.Ref.<ver>.nupkg`

The library set comes from `scripts/modules/ViuPackaging.psm1`, shared with
`scripts/Pack-Release.ps1`, so the local feed and the release set cannot
disagree about what ships — and a library added under `libraries/` but missing
from the inventory fails the pack instead of silently producing a short feed.
Use `-SkipLibraries`, `-SkipSdk`, or `-SkipFramework` to pack a subset.

Cached extracts for the same version are pruned first, so same-version repacks
always pick up fresh content. Pruning covers the machine-global cache **and any
repo-local `globalPackagesFolder`** a consumer declares in its own
`nuget.config` — the sibling `viu-examples` repository does exactly that, and a
prune that missed it left consumers compiling against a previous build's
analyzer while the feed itself looked current. Discovery only touches caches
that already hold `Assimalign.Viu.*` extracts; pass `-ConsumerRoot` for a
consumer outside this repository's sibling directories, or `-SkipCachePrune`
when the version was bumped and no prune is needed.

A consumer outside this repo points a `nuget.config` at the feed:

```xml
<configuration>
    <packageSources>
        <add key="viu-local" value="C:\Source\repos\assimalign\viu\_out\packages" />
        <add key="nuget.org" value="https://api.nuget.org/v3/index.json" />
    </packageSources>
</configuration>
```

The in-repo build does **not** consume the SDK — repo projects stay on
`ViuProjectReference` dogfooding (see `.claude/rules/build-system.md`) so the
framework can be developed without a pack/restore cycle in the loop.
