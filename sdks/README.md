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
| The framework libraries (`Assimalign.Viu.Shared`, `.Reactivity`, `.RuntimeCore`, `.RuntimeDom`) | Implicit `<FrameworkReference Include="Assimalign.Viu.App" />` via the `KnownFrameworkReference` registration in [Targets/Assimalign.Viu.Sdk.FrameworkReference.props](Assimalign.Viu.Sdk/Targets/Assimalign.Viu.Sdk.FrameworkReference.props) |
| The `[Reactive]` and `.viu` source generators | Shipped inside the `Assimalign.Viu.App.Ref` targeting pack at `analyzers/dotnet/cs/` and listed as `<File Type="Analyzer">` in its `data/FrameworkList.xml` |
| `.viu` single-file component compilation | The generator's AdditionalFiles/CompilerVisibleProperty wiring, packed into the SDK's `Targets/` from its in-repo source |
| `.viu` `@style` CSS bundling | The `ViuBundleCss` MSBuild task (+ parser closure) in the SDK package's `Tasks/`, driven by the packed `Assimalign.Viu.Sdk.Css.Bundling.targets`. The bundle registers as a **content-fingerprinted** static web asset ([V01.01.12.12.03]) |
| `.viu` `@style` stylesheet `<link>` | **Injected automatically** — no manual link tag. The `ViuInjectCssBundleLink` task (same `Tasks/` assembly) splices `<link rel="stylesheet" href="<AssemblyName>.viu.css" />` into `wwwroot/index.html` at build, *before* the SDK's compression pipeline so gzip/brotli negotiation stays intact ([V01.01.12.12.01]). The href is the stable plain route a static host serves; a fingerprinted route is also registered for manifest-aware hosts. Opt out with `<ViuInjectSingleFileComponentCssLink>false</ViuInjectSingleFileComponentCssLink>` (a hand-authored link also suppresses injection) |
| `viu-dom.js` interop bridge | Packed under `assets/` and copied to the consumer's `wwwroot/_content/Assimalign.Viu.RuntimeDom/` at build |

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
    <!-- Keep .viu compilation but skip CSS bundling / disable .viu entirely. -->
    <ViuBundleSingleFileComponentCss>false</ViuBundleSingleFileComponentCss>
    <EnableSingleFileComponentGeneration>false</EnableSingleFileComponentGeneration>
</PropertyGroup>
```

## Local development loop

`scripts/Install-Local.ps1` packs the full chain into the repo-local feed
`_out/packages/`:

1. `dotnet pack sdks/Assimalign.Viu.Sdk/Tasks` → `Assimalign.Viu.Sdk.<ver>.nupkg`
2. `dotnet pack frameworks/Assimalign.Viu.App.Runtime/src -p:RuntimeIdentifier=browser-wasm` → `Assimalign.Viu.App.Runtime.browser-wasm.<ver>.nupkg`
3. `dotnet pack frameworks/Assimalign.Viu.App.Refs/src` → `Assimalign.Viu.App.Ref.<ver>.nupkg`

It prunes `~/.nuget/packages/` extracts for the same versions first, so
same-version repacks always pick up fresh content.

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
