# Viu SDKs

[![Assimalign.Viu.Sdk version](https://img.shields.io/nuget/v/Assimalign.Viu.Sdk?logo=nuget)](https://www.nuget.org/packages/Assimalign.Viu.Sdk) [![Assimalign.Viu.Sdk downloads](https://img.shields.io/nuget/dt/Assimalign.Viu.Sdk?logo=nuget)](https://www.nuget.org/packages/Assimalign.Viu.Sdk) [![Assimalign.Viu.Sdk.Browser version](https://img.shields.io/nuget/v/Assimalign.Viu.Sdk.Browser?logo=nuget)](https://www.nuget.org/packages/Assimalign.Viu.Sdk.Browser) [![Assimalign.Viu.Sdk.Browser downloads](https://img.shields.io/nuget/dt/Assimalign.Viu.Sdk.Browser?logo=nuget)](https://www.nuget.org/packages/Assimalign.Viu.Sdk.Browser)

Viu ships two compositional MSBuild project SDKs. Choose by consumer shape:

| Project SDK | Consumer | SDK chain | Viu framework |
| --- | --- | --- | --- |
| `Assimalign.Viu.Sdk` | Host-neutral component library | `Microsoft.NET.Sdk` | Targeting-only `Assimalign.Viu.App` |
| `Assimalign.Viu.Sdk.Browser` | Browser WebAssembly application | Imports `Assimalign.Viu.Sdk`, then chains `Microsoft.NET.Sdk.WebAssembly` | Base `Assimalign.Viu.App` plus `Assimalign.Viu.App.Browser` |

The split exists because component-library authors need the `.viu`/`.vue` and `[Reactive]` source
generators without loading browser build payload or installing the WebAssembly workload. The base SDK
therefore has no browser assets, `wwwroot` bundling, hot-reload worker, publish-budget hooks, or
runtime pack. The Browser SDK owns those application behaviors and reuses the base authoring graph.

## Host-neutral component library

A component library uses the base SDK:

```xml
<Project Sdk="Assimalign.Viu.Sdk">
    <PropertyGroup>
        <TargetFramework>net10.0</TargetFramework>
    </PropertyGroup>
</Project>
```

It can contain real `.viu` or compatible `.vue` components and `[Reactive]` types. Building runs the
generators; packing carries the library's extracted `.viu.css` plus generated `buildTransitive`
registration so a consuming Browser application can register it as a static web asset. The library
restore/build closure contains no `Assimalign.Viu.Browser`, does not resolve a Viu runtime pack, and
does not require `wasm-tools`.

## Browser WebAssembly application

A browser application uses the Browser SDK directly; using the bare base SDK is not an application
compatibility path:

```xml
<Project Sdk="Assimalign.Viu.Sdk.Browser">
    <PropertyGroup>
        <TargetFramework>net10.0</TargetFramework>
    </PropertyGroup>
</Project>
```

The Browser SDK imports and declares an exact-version dependency on the base SDK, so application
projects receive the same generators and can consume packed base-SDK component libraries. It adds
the WebAssembly build, Browser framework, `viu-dom.js`, application CSS bundling/link injection,
transitive component-library style flow, generated-asset hot reload, and publish-budget hooks.
The complete route, ordering, fingerprint, and compression contract is documented in
[Browser SDK CSS delivery](Assimalign.Viu.Sdk.Browser/docs/CSS-DELIVERY.md).

`ViuBrowserDevServer` defaults to `WasmAppHost`; set it to `Custom` with
`ViuBrowserDevServerCommand` and `ViuBrowserDevServerArguments` to replace the child server while
retaining the SDK RunHost. The readiness and HTTP-host obligations are documented in the
[Browser development loop](Assimalign.Viu.Sdk.Browser/docs/DEVELOPMENT-LOOP.md#replace-the-development-server).

Pin the SDK selected by the project inline, for example
`Sdk="Assimalign.Viu.Sdk.Browser/10.0.1-preview.2"`, or in `global.json`:

```json
{
    "msbuild-sdks": {
        "Assimalign.Viu.Sdk": "10.0.1-preview.2",
        "Assimalign.Viu.Sdk.Browser": "10.0.1-preview.2"
    }
}
```

The Browser entry point imports the separately packaged base SDK, so pin both to the same version.
A component-library-only repository pins only `Assimalign.Viu.Sdk`. Both SDKs are resolved by NuGet's
built-in MSBuild SDK resolver — the same machinery that handles `Microsoft.NET.Sdk.Web` — so they
work in Visual Studio, Rider, and the dotnet CLI with no installer and no administrator rights.

## What each segment gives a consumer

| Piece | Owner and mechanism |
| --- | --- |
| Host-neutral runtime references | The base SDK adds targeting-only `<FrameworkReference Include="Assimalign.Viu.App" />`, resolving Reactivity, Components, State, and Core from `Assimalign.Viu.App.Ref` |
| The `[Reactive]` and `.viu`/`.vue` source generators | The base `Assimalign.Viu.App.Ref` carries the analyzer and parser closure under `analyzers/dotnet/cs/`, with `<File Type="Analyzer">` entries in `data/FrameworkList.xml` |
| `.viu` and tag-based `.vue` compilation | The base SDK owns the shared AdditionalFiles/CompilerVisibleProperty wiring in [Targets/Assimalign.Viu.Generators.Syntax.props](Assimalign.Viu.Sdk/Targets/Assimalign.Viu.Generators.Syntax.props) and [.targets](Assimalign.Viu.Sdk/Targets/Assimalign.Viu.Generators.Syntax.targets). `.vue` scripts require explicit `lang="csharp"`; JavaScript is never executed |
| Component-library styles | The base SDK extracts `.viu.css` during pack and carries it with generated `buildTransitive` registration in the library package; it never registers browser static assets or writes `wwwroot` itself |
| WASM browser app model and Browser runtime | The Browser SDK chains `Microsoft.NET.Sdk.WebAssembly` and adds `Assimalign.Viu.App.Browser`; its targeting and runtime packs add `Assimalign.Viu.Browser` |
| Application and component-library CSS | The Browser SDK bundles the app's styles through `ViuBundleCss`, flows packed library styles to `_content/<PackageId>/`, and injects all links in deterministic library-before-application order before WebAssembly compression. Injection works with either value of `OverrideHtmlAssetPlaceholders`; the stable route is default and a labeled fingerprinted endpoint is opt-in ([V01.01.12.12.01], [V01.01.12.12.03], [V01.01.12.12.06]) |
| Generated-asset watch inputs and hot reload | The Browser SDK owns the project-scoped worker and the versioned `@(ViuGeneratedAsset)` seam. Component CSS registers directly; compatible independent build packages can register without consuming SDK-private names ([V01.01.12.30.04], #355) |
| `viu-dom.js` interop bridge | The Browser SDK packs the asset and copies it to `wwwroot/_content/Assimalign.Viu.Browser/` at build |
| Runtime inspection | An application explicitly references `Assimalign.Viu.DevTools` and sets `ViuEnableDevTools=true`; the base SDK fixes Core's linker feature switch and the package conditionally flows its postMessage asset |
| Publish budgets | Browser-only publish hooks measure trimmed/AOT payload; base component libraries do not load them |

The two framework references resolve through three framework packages:

| Package | Contents | When restored |
| --- | --- | --- |
| `Assimalign.Viu.App.Ref` | Reactivity, Components, State, and Core reference assemblies; `data/FrameworkList.xml`; four-entry `data/PackageOverrides.txt`; `analyzers/dotnet/cs/` generator closure | Base and Browser compile time |
| `Assimalign.Viu.App.Browser.Ref` | Browser reference assembly, `data/FrameworkList.xml`, and the Browser package-override entry | Browser compile time |
| `Assimalign.Viu.App.Browser.Runtime.browser-wasm` | Base-plus-Browser implementation closure under `runtimes/browser-wasm/lib/net10.0/` plus `data/RuntimeList.xml` | Browser build/publish |

The base framework is deliberately targeting-only and has no runtime package.
`Assimalign.Viu.ServerRenderer` remains an ordinary opt-in package, not another framework segment.

### Runtime-inspection opt-in

Runtime inspection is an ordinary package, not part of either shared framework. Enable it only in
the application that owns the diagnostic session:

```xml
<PropertyGroup>
    <ViuEnableDevTools>true</ViuEnableDevTools>
</PropertyGroup>
<ItemGroup>
    <PackageReference Include="Assimalign.Viu.DevTools" Version="10.0.1-preview.2" />
</ItemGroup>
```

`ViuEnableDevTools` defaults to `false`. In that state the SDK writes a trim-time feature-switch
value that folds Core's guarded inspection hooks away; even if the package remains referenced, a
trimmed Browser publish contains no DevTools assembly or browser asset. Setting it to `true` keeps
the hooks and conditionally flows `viu-devtools.js` to
`_content/Assimalign.Viu.DevTools/viu-devtools.js`. DevTools stays outside
`Assimalign.Viu.App` and `Assimalign.Viu.App.Browser` in both modes.

### Server-targeted and dual-target projects

Set one project-level property when the assembly must expose compiler-produced server renders:

```xml
<PropertyGroup>
    <ViuServerRendering>true</ViuServerRendering>
</PropertyGroup>
```

The property works with the base SDK and with the derived Browser SDK. The SDK adds the exact-version
ordinary `Assimalign.Viu.ServerRenderer` package and the generator emits both the normal client
virtual-node body and the direct server-markup body. It also emits the reflection-free assembly
catalog `GeneratedViuServerRenders`. A host composes the two generated catalogs explicitly:

```csharp
ComponentFactory components = new();
GeneratedViuComponents.Register(components);

ServerRenderRegistry serverRenders = new();
GeneratedViuServerRenders.Register(serverRenders);

ServerRenderAdaptor<RequestContext> adaptor = new(requestScopeFactory, serverRenders);
```

The declaration is deliberately not a per-file item and does not create another shared-framework
segment. Leaving it unset is the client-only path: generated sources contain no ServerRenderer type,
method, delegate, or registration. A Browser project may set it to produce a deterministic dual-target
assembly while retaining the ordinary Browser profile. Specified by `[SSR-TARGET-1]` through
`[SSR-TARGET-3]`.

Opt out / pin independently:

```xml
<PropertyGroup>
    <!-- Skip the implicit base FrameworkReference (explicit ones keep working). -->
    <ViuAutoIncludeAppFramework>false</ViuAutoIncludeAppFramework>
    <!-- Browser SDK only: independently skip its Browser FrameworkReference. -->
    <ViuAutoIncludeBrowserAppFramework>false</ViuAutoIncludeBrowserAppFramework>
    <!-- Pin base and Browser framework versions independently of the SDK versions. -->
    <ViuAppFrameworkVersion>10.0.2</ViuAppFrameworkVersion>
    <ViuBrowserAppFrameworkVersion>10.0.2</ViuBrowserAppFrameworkVersion>
    <!-- Keep component compilation but skip CSS bundling / disable component generation entirely. -->
    <ViuBundleSingleFileComponentCss>false</ViuBundleSingleFileComponentCss>
    <!-- Browser SDK: author every component-bundle link in the host page. -->
    <ViuInjectSingleFileComponentCssLink>false</ViuInjectSingleFileComponentCssLink>
    <!-- Browser SDK: use the labeled immutable endpoint (manifest-aware hosts only). -->
    <ViuUseFingerprintedSingleFileComponentCssBundleLink>true</ViuUseFingerprintedSingleFileComponentCssBundleLink>
    <EnableSingleFileComponentGeneration>false</EnableSingleFileComponentGeneration>
</PropertyGroup>
```

`ViuBrowserAppFrameworkVersion` defaults to `ViuAppFrameworkVersion`, so the normal path pins only
the base property and keeps both segments coherent. The Browser auto-include property exists only on
the Browser SDK path; disabling it does not disable the inherited base framework reference.

## Standalone UtilityCss add-on

Viu Utilities was removed from the Viu SDK, its private build targets, and editor stack on
2026-08-13. Its engine is now independently published from
[`libraries/Utilities/Assimalign.Viu.UtilityCss`](../libraries/Utilities/Assimalign.Viu.UtilityCss),
and its independently versioned build integration is
`Assimalign.Viu.UtilityCss.Build`. Both remain outside every Viu SDK and framework surface. The
build package can register its generated bundle with a compatible Browser SDK through the public,
versioned generated-asset hot-reload seam; this is an add-on contract, not SDK ownership. Tailwind
CSS v4.3.3 is a compatibility target of the add-on, not of Viu core; the retained
[utility-CSS design](../docs/UTILITY-CSS-DESIGN.md) remains non-normative there.

## Generated stylesheet hot reload

In a Debug `dotnet watch` session, the Browser SDK starts one project-scoped generated-asset
worker. It collects the documented
[`@(ViuGeneratedAsset)` contract](Assimalign.Viu.Sdk.Browser/docs/GENERATED-ASSETS.md), coalesces an
editor save, and invokes the declared deterministic regeneration targets once. The SDK's own
`<PackageId>.viu.css` component bundle uses the contract. A compatible standalone UtilityCss.Build
package can register `<AssemblyName>.utilities.css`, so a new utility class also regenerates and
live-swaps without an application restart. The .NET watch browser-refresh client replaces each
changed `<link>` with a cache-busted URL; a semantic no-op does not touch the corresponding bundle.

Removing the final style from a `PreserveEmpty` generated asset during a watch session emits one
zero-byte development bundle so the browser can unload the old stylesheet. A subsequent ordinary
build or publish deletes that transport asset and any provider-owned cleanup state. The worker is a
Browser SDK build tool only: it is disabled by default
outside Debug and is never copied into the application, runtime framework, or publish output. Set
`<ViuCssHotReloadEnabled>false</ViuCssHotReloadEnabled>` to disable it, or adjust the default
100-millisecond quiet period with
`<ViuCssHotReloadDebounceMilliseconds>...</ViuCssHotReloadDebounceMilliseconds>`.

This integration targets the `dotnet watch` watch-list contract. Visual Studio's ordinary Hot
Reload command does not currently invoke that contract; component and utility styles remain
build-triggered there until a Visual Studio-side driver exists. Live regeneration requires launching
the project through `dotnet watch`. The state-preserving guarantee covers stylesheet-link replacement
only. Generated marker types classify managed component deltas:
template and C# script edits remount the affected component so .NET 10 browser WebAssembly executes
the updated generated code, while style-only edits remain mounted.

## Local development loop

`scripts/Install-Local.ps1` packs the complete package set into the repo-local
feed `_out/packages/`:

1. every independently published library → `Assimalign.Viu.<Name>.<ver>.nupkg`;
2. the base and Browser project SDKs → `Assimalign.Viu.Sdk.<ver>.nupkg` and
   `Assimalign.Viu.Sdk.Browser.<ver>.nupkg`;
3. the targeting-only base framework → `Assimalign.Viu.App.Ref.<ver>.nupkg`;
4. the Browser targeting framework → `Assimalign.Viu.App.Browser.Ref.<ver>.nupkg`; and
5. the Browser runtime framework for `browser-wasm` →
   `Assimalign.Viu.App.Browser.Runtime.browser-wasm.<ver>.nupkg`.

For component-library work on a machine without the WebAssembly workload, run
`pwsh scripts/Install-Local.ps1 -BaseOnly`. That packs standalone libraries, the base SDK, and
`Assimalign.Viu.App.Ref` only; it does not evaluate or pack the Browser SDK/framework path.

The library set comes from `scripts/modules/ViuPackaging.psm1`, shared with
`scripts/Pack-Release.ps1`, so the local feed and the release set cannot
disagree about what ships — and a library added under `libraries/` but missing
from the inventory fails the pack instead of silently producing a short feed.
Use `-SkipLibraries`, `-SkipSdk`, or `-SkipFramework` to pack a subset. The packaging module and
release packer share the same SDK/framework inventory, so neither segment can silently disappear
from one path.

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

The in-repo build does **not** consume either SDK — repo projects stay on
`ViuProjectReference` dogfooding (see `.claude/rules/build-system.md`) so the
framework can be developed without a pack/restore cycle in the loop.
