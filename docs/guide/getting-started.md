# Getting started with Viu

Viu is a standalone C#/.NET user-interface framework that runs in the browser through the .NET
WebAssembly build tools. This guide takes you from an empty folder to a running, publishable Viu app
using the packaged **`Assimalign.Viu.Sdk`** — the surface an external consumer uses.

Three decisions shape everything below (they are the
[founding design decisions](../PLAN.md#founding-design-decisions-cwasm-divergences), and
[`docs/SPECIFICATION.md`](../SPECIFICATION.md) is the authoritative statement of what Viu
guarantees):

- **Reactive state is an explicit cell.** A `Reference<T>` is an object you read and write through
  `.Value`. Reading it inside a render function or a `Computed` subscribes to it; assigning to it
  notifies every subscriber. Nothing is intercepted and nothing is tracked invisibly — the
  subscription happens because you performed a property read.
- **Reactive objects are source-generated, not intercepted.** A `[Reactive]` partial class has its
  tracking/triggering property bodies emitted at build time by a Roslyn source generator, so an
  ordinary C# object becomes reactive with no reflection and no runtime proxy.
- **Templates compile at build time.** There is no runtime template compiler and no runtime code
  generation — WASM is AOT and trimming territory. `.viu` and `.vue` single-file components and their
  templates are compiled by source generators during the build
  ([ADR-0005](../adr/0005-no-runtime-template-compilation.md)).

> **Preview status.** Viu is pre-release. There is **no `dotnet new` template yet** — that lands with
> [V01.01.12.04] (wave W05), so this guide creates the project by hand. Package versions and the local
> feed below reflect the current preview; once Viu publishes to nuget.org the feed step goes away.

## Prerequisites

- **The .NET SDK** pinned in the repo's [`global.json`](../../global.json) — currently **`10.0.301`**
  (any `10.0.3xx` SDK in the same feature band works). Check with:

  ```sh
  dotnet --version
  ```

- **The WebAssembly tools workload**, which supplies the browser runtime pack, trimming, and the
  ahead-of-time (AOT) native toolchain:

  ```sh
  dotnet workload install wasm-tools
  ```

  Confirm it is present with `dotnet workload list` (look for `wasm-tools`).

## How a Viu app is packaged

A Viu app project uses the Viu MSBuild SDK instead of a plain `Microsoft.NET.Sdk`:

```xml
<Project Sdk="Assimalign.Viu.Sdk">
```

That one line chains `Microsoft.NET.Sdk.WebAssembly`, references the `Assimalign.Viu.App` shared
framework (the core and runtime-dom libraries), and turns on `.viu` single-file
component compilation and CSS bundling — with no per-project wiring. The full consumer surface,
including every opt-out property, is documented in [`sdks/README.md`](../../sdks/README.md); the
packaging model is [founding decision 8](../PLAN.md#founding-design-decisions-cwasm-divergences).

While Viu is pre-release you consume it from a **repo-local NuGet feed**. From a clone of
[`assimalign/viu`](https://github.com/assimalign/viu), pack the SDK and framework into `_out/packages`:

```sh
pwsh scripts/Install-Local.ps1
```

This produces `Assimalign.Viu.Sdk`, `Assimalign.Viu.App.Ref`, and
`Assimalign.Viu.App.Runtime.browser-wasm` packages (see the
[local development loop](../../sdks/README.md#local-development-loop)). Your app points a
`nuget.config` at that folder, shown below.

## Create the project by hand

Make a new folder (for example `HelloViu`) and add these files.

**`global.json`** — pin the Viu SDK version so restore resolves it from the local feed. Match the
package version produced by `Install-Local.ps1` (it tracks the repo's
[`build/Targets/Build.Version.props`](../../build/Targets/Build.Version.props)):

```json
{
    "sdk": {
        "version": "10.0.300",
        "rollForward": "latestFeature"
    },
    "msbuild-sdks": {
        "Assimalign.Viu.Sdk": "10.0.1-preview.2"
    }
}
```

**`nuget.config`** — add the local feed alongside nuget.org (point `value` at your clone's
`_out/packages`; this mirrors the [`sdks/README.md`](../../sdks/README.md#local-development-loop)
pattern):

```xml
<?xml version="1.0" encoding="utf-8"?>
<configuration>
    <packageSources>
        <clear />
        <add key="viu-local" value="C:\Source\repos\assimalign\viu\_out\packages" />
        <add key="nuget.org" value="https://api.nuget.org/v3/index.json" />
    </packageSources>
</configuration>
```

**`HelloViu.csproj`** — the whole project file:

```xml
<Project Sdk="Assimalign.Viu.Sdk">

    <PropertyGroup>
        <TargetFramework>net10.0</TargetFramework>
        <!-- Let the WebAssembly SDK resolve the index.html boot placeholders
             (the importmap and the main#[.{fingerprint}].js reference) at build
             and publish. The .viu CSS-bundle <link> injection rides the same
             host-page rewrite. -->
        <OverrideHtmlAssetPlaceholders>true</OverrideHtmlAssetPlaceholders>
    </PropertyGroup>

    <ItemGroup>
        <StaticWebAssetFingerprintPattern Include="JS" Pattern="*.js" Expression="#[.{fingerprint}]!" />
    </ItemGroup>

</Project>
```

> **Why `OverrideHtmlAssetPlaceholders`?** It tells the WebAssembly SDK to statically resolve the
> boot placeholders in `index.html` (the import map and the fingerprinted `main.js` reference) at
> build and publish. The automatic `.viu` stylesheet `<link>` injection rides that same host-page
> rewrite, so today the property is required for both to work. Making the SDK default it is tracked in
> [#215](https://github.com/assimalign/viu/issues/215); until then, set it explicitly.

**`wwwroot/index.html`** — the host page. It has a mount target (`#app`), the WebAssembly boot
placeholders, and — importantly — **no manual stylesheet `<link>`**: the build injects the `.viu` CSS
bundle link for you (see [Styling](#styling-with-viu-single-file-components) below).

```html
<!DOCTYPE html>
<html lang="en">
<head>
    <meta charset="UTF-8" />
    <meta name="viewport" content="width=device-width, initial-scale=1.0" />
    <title>Hello Viu</title>
    <link rel="preload" id="webassembly" />
    <script type="importmap"></script>
    <script type="module" src="main#[.{fingerprint}].js"></script>
</head>
<body>
    <main id="app"></main>
</body>
</html>
```

**`wwwroot/main.js`** — the standard .NET WebAssembly boot:

```js
import { dotnet } from './_framework/dotnet.js'

const { runMain } = await dotnet.create()

await runMain()
```

## Your first component

A Viu component is a plain C# object implementing `IComponent`. Its `Setup` method runs **once** per
instance, declares the component's reactive state as ordinary locals, and returns a **render
function** that re-runs whenever the state it read changes. The closure *is* the component's state:
there is no separate state object to declare, no name-based lookup between state and template, and
no instance to reflect over at run time — which is exactly what makes the model AOT- and
trimming-safe ([ADR-0004](../adr/0004-composition-only-component-model.md): a component is a setup
function returning a render function, and there is no second, declarative authoring style).

**`Program.cs`** — a Viu WASM app's whole bootstrap: build the app from a root component and mount it
by selector, then keep the WASM main loop alive (rendering is reactive from there on). The builder is
the .NET-idiomatic shape (compare `WebApplication.CreateBuilder`), and `MountAsync` loads the browser
bridge inside the mount path — there is no separate initialization call:

```csharp
using System.Threading;
using System.Threading.Tasks;

using Assimalign.Viu.Browser;

using HelloViu;

await BrowserApplication.CreateBuilder(new Counter()).Build().MountAsync("#app");

await Task.Delay(Timeout.Infinite);
```

Configure the app on the builder before `Build()` — `UseRootComponent`, `UseComponentFactory`,
`UseServiceProvider`, `UseStateRegistry`, `UseDirectiveResolver`, and plugins with
`Use(IApplicationPlugin)`. Configuration is complete by the time `Build()` returns; a plugin is an
awaited pre-mount initialization hook over an already-composed application, not a mutable registry
you can extend afterwards.

### Dependency injection (`System.IServiceProvider`)

For app-level singletons — a data client, a router, a store registry — Viu integrates
**bring-your-own dependency injection over `System.IServiceProvider`**. Register services on the
builder (the shape a .NET developer expects, compare `WebApplicationBuilder.Services`), then resolve
them from a component's `Setup`:

```csharp
using Assimalign.Viu;
using Assimalign.Viu.Browser;

var builder = BrowserApplication.CreateBuilder(new App());
builder.Services.AddSingleton(new ApiClient(baseAddress));         // an app-level singleton
builder.Services.AddTransient<RequestId>(_ => new RequestId());    // a fresh instance per resolution
await builder.Build().MountAsync("#app");
```

```csharp
// inside a component's Setup:
var api = DependencyInjection.GetRequiredService<ApiClient>();      // resolves from the app service provider
```

The default provider is **AOT-safe**: every service is created by a factory delegate — there is no
reflection, no constructor discovery, and no `Microsoft.Extensions.DependencyInjection` dependency. It
supports `Singleton`, `Scoped` (per application — the app is the root scope), and `Transient`
lifetimes; two applications get isolated providers, and disposing an application disposes its owned
singleton/scoped services. To use a full container (`Microsoft.Extensions.DependencyInjection`,
Autofac, …), implement the small `IServiceContainer` over it and pass it to
`builder.UseServiceContainer(...)`.

This is **app-level** DI, and it is the *only* ambient channel: Viu deliberately has **no hierarchical
component-tree dependency API** — no ambient provide/inject walking up the parent chain
([`[CMP-24]`](../SPECIFICATION.md#48-no-component-tree-provideinject)). A component's dependencies are
explicit, and there are exactly four ways to get one:

- **parameters and slots** for parent-to-child data;
- **`IComponentContext.Services`** for application services (what you registered on the builder above);
- **a State definition** resolved through the application's store registry, for shared mutable state; and
- **`IComponentContext.Components`** for deliberate component resolution by name or type.

This is a decision, not a deferral. An ambient hierarchical channel makes a component's contract
invisible at its call site and its behavior dependent on where it happens to be mounted; it also
resists trimming, because nothing static says which values a subtree will ask for. The cost is
visible and accepted elsewhere in the framework — `RouterView` takes its nesting depth as an explicit
argument precisely because no ambient channel exists to carry it.

**`Counter.cs`** — a working counter:

```csharp
using System;

using Assimalign.Viu;

namespace HelloViu;

internal sealed class Counter : IComponent
{
    public string? Name => "Counter";

    public ComponentSetup Setup(ComponentProperties properties, ComponentSetupContext context)
    {
        // ref() -> Reactive.Reference: a reactive box read and written through .Value.
        var count = Reactive.Reference(0);

        // computed(): a cached, lazily recomputed derived value.
        var label = Reactive.Computed(() => count.Value == 1 ? "1 click" : $"{count.Value} clicks");

        void Increment() => count.Value++;

        return () => VirtualNodeFactory.Element(
            "section",
            VirtualNodeFactory.Properties(("class", "counter")),
            VirtualNodeFactory.Element("h1", "Hello from Viu"),
            VirtualNodeFactory.Element(
                "p",
                VirtualNodeFactory.Properties(("class", "count")),
                VirtualNodeFactory.Text(label.Value)),
            VirtualNodeFactory.Element(
                "button",
                VirtualNodeFactory.Properties(
                    ("class", "primary"),
                    ("type", "button"),
                    ("onClick", (Action)Increment)),
                VirtualNodeFactory.Text("Increment")));
    }
}
```

The render function builds a **virtual node** tree with `VirtualNodeFactory`; the runtime diffs it and
applies only the changed nodes to the real DOM. This matters more on WASM than in JavaScript: **every
DOM mutation crosses the JS-interop boundary**, so idiomatic Viu leans on the compiled render function
and the renderer's batched updates rather than imperative DOM access
([ADR-0003](../adr/0003-batched-interop-dom-operations.md)). `VirtualNodeFactory`, `IComponent`,
`ComponentProperties`, and `ComponentSetupContext` all live in
[`Assimalign.Viu.Core`](../../libraries/Assimalign.Viu.Core/docs/OVERVIEW.md); the
browser entry point `BrowserApplication` lives in
[`Assimalign.Viu.Browser`](../../libraries/Assimalign.Viu.Browser/docs/OVERVIEW.md). For a complete
application with props, emitted events, lifecycle hooks, routing, forms, state, and built-ins, read
the external [`viu-examples` showcase](https://github.com/assimalign/viu-examples).

## Reactivity basics

Viu's reactivity is a dependency graph of explicit cells. The API surface is the static `Reactive`
facade:

| Primitive | What it does |
| --- | --- |
| `Reactive.Reference(0)` → `Reference<T>` | A tracked cell. Reading `.Value` inside an effect subscribes to it; assigning a different value notifies every subscriber. |
| `Reactive.ShallowReference(obj)` → `ShallowReference<T>` | A cell that notifies only when you assign a *new instance*, never when you mutate the instance it already holds. |
| `Reactive.Computed(() => …)` → `Computed<T>` | Derived state. Evaluated lazily on first read, cached until a dependency's version changes, then recomputed on the next read — not eagerly on invalidation. |
| `Reactive.Watch(source, callback)` | Runs a callback when a source changes, delivered through the scheduler so several writes in one turn coalesce into one invocation. |
| `[Reactive]` **partial class** | A reactive object. The generator emits the tracking/triggering bodies for each `partial` property. |
| `ReactiveList<T>`, `ReactiveDictionary<TKey,TValue>`, `ReactiveSet<T>` | Reactive collections implementing the BCL collection interfaces, tracking reads and triggering on writes. |

Three consequences worth internalizing up front:

- **`.Value` is always explicit in C#.** `count.Value++`, never `count++`. Templates compiled from a
  single-file component do unwrap reference cells for you, so `{{ Count }}` in a `<template>` reads the
  underlying value; hand-written C# never does.
- **A `Reference<T>` survives being passed around.** It is a reference type, so handing it to a method
  or capturing it in a closure keeps the reactive connection intact — there is no wrapper to
  accidentally unwrap and no interception layer to fall out of.
- **Collections are dedicated types, not wrapped instances.** Use `ReactiveList<T>` instead of trying
  to make a `List<T>` reactive; the tracking is in the type, not layered over it at run time.

`[Reactive]` is what makes an ordinary object's properties participate in that graph, and it does so
at build time (see [ADR-0002](../adr/0002-ref-first-reactivity.md) and the
[Reactivity overview](../../libraries/Assimalign.Viu.Reactivity/docs/OVERVIEW.md)). The class must be
`partial`, and every reactive property is declared `partial`:

```csharp
using Assimalign.Viu;

namespace HelloViu;

[Reactive]
internal partial class TodoItem
{
    public partial string Title { get; set; }
    public partial bool Done { get; set; }
}
```

The generator emits the tracking/triggering bodies and makes the class implement `IReactiveObject`, so
reading `todo.Title` inside a render function or `Computed` establishes a dependency, and assigning it
schedules a re-render — no reflection, fully trimming- and AOT-safe.

## Styling with .viu single-file components

Viu's canonical single-file component is the `.viu` file, using the hybrid container
([V01.01.06.10], #257): `<template>`/`<style>` tags plus the C# `@script { }` block (the exact grammar
is in [`FORMAT.md`](../../tooling/Assimalign.Viu.Syntax.SingleFileComponent/docs/FORMAT.md); legacy
`@template`/`@style` `@`-blocks still parse with a migration warning). A `.viu` with a
`<template>`/`@script` compiles to a **mountable component** (see the note below,
[#216](https://github.com/assimalign/viu/issues/216)); a `.viu` also serves as a **scoped, bundled CSS**
unit.

> **`.vue` files compile too.** Viu ships a `.vue` single-file-component compatibility parser as a
> product feature ([V01.01.06.09], [#250](https://github.com/assimalign/viu/issues/250)). The SDK globs
> `**/*.viu` and `**/*.vue` into the same build graph, and everything downstream of the container
> parse — template code generation, scoped styles, CSS Modules, source mapping, hot-reload metadata —
> is shared. A `.vue` script merges into the generated component only when it declares
> `lang="csharp"`, because Viu executes no JavaScript. The rules are specified in
> [§9 of the specification](../SPECIFICATION.md#9-vue-compatibility--a-shipping-feature).

Add a `.viu` file with a `<style>` block:

**`AppStyles.viu`**:

```
<style>
    .counter {
        display: grid;
        gap: 1rem;
        max-width: 24rem;
        margin: 4rem auto;
        padding: 2rem;
        border-radius: 1rem;
        font-family: system-ui, sans-serif;
        background: #f2f4f7;
        color: #10243b;
    }

    .count {
        font-size: 2rem;
        font-weight: 700;
    }

    .primary {
        padding: 0.75rem 1.25rem;
        border: none;
        border-radius: 999px;
        background: #f05a28;
        color: white;
        font-weight: 700;
        cursor: pointer;
    }
</style>
```

At build the SDK extracts every `<style>` block, bundles it into a **content-fingerprinted** static web
asset (`<AssemblyName>.viu.css`), and **injects the `<link rel="stylesheet">` into `index.html`
automatically** — before the SDK's gzip/brotli compression pipeline, so compression negotiation stays
intact. You write no manual link tag. This is why `index.html` above has none; the details are in
[`sdks/README.md`](../../sdks/README.md) and the injection mechanism is
[V01.01.12.12.01](https://github.com/assimalign/viu/issues/167).

> **`.viu` `<template>`/`@script` components are mountable ([#216](https://github.com/assimalign/viu/issues/216)).**
> A `.viu` with a `<template>` (Viu template syntax) and an `@script` (C#) block now compiles to a
> **mountable component**: the generator emits the render function, merges the script into the partial
> class, **and** generates the `IComponent` bridge (a `Setup` that returns the render delegate),
> so you mount it exactly like a hand-written component — `BrowserApplication.CreateBuilder(new Greeting()).Build().MountAsync("#app")`
> or `VirtualNodeFactory.Component(new Greeting(), props)` — with no manual wiring beyond the package
> reference. Reactive `@script` members (a `Reference<T>`, a `[Reactive]` field) drive re-render, and a
> template event handler (`@click="Increment"`) calls the like-named `@script` method:
>
> ```
> <template>
>     <button class="counter" @click="Increment">{{ Count }}</button>
> </template>
> @script {
>     using Assimalign.Viu;
>     public readonly Reference<int> Count = Reactive.Reference(0);
>     public void Increment() => Count.Value++;
> }
> ```
>
> Still in progress ([#227](https://github.com/assimalign/viu/issues/227)): parameters and events
> declared inside `@script`, and `@script`-authored lifecycle hooks. Until those land,
> undeclared attributes fall through to the component's root element, and a parent composes a child `.viu`
> by explicit instantiation (`new Greeting()`). Hand-authored `IComponent` components (as above)
> remain fully supported.

## Run and publish

**Build:**

```sh
dotnet build
```

**Run** the dev server (it serves the app and resolves the boot placeholders; it prints the localhost
URL it binds — the port is chosen for you):

```sh
dotnet run
```

Open the printed URL and you will see the counter; clicking **Increment** updates the rendered count
reactively, and the styles from `AppStyles.viu` are applied through the auto-injected link.

**Publish** a trimmed, statically hostable build:

```sh
dotnet publish -c Release
```

The published `bin/Release/net10.0/publish/wwwroot` contains:

- your compiled component and the Viu framework assemblies, fingerprinted and trimmed, under
  `_framework/` (e.g. `HelloViu.<hash>.wasm`, `Assimalign.Viu.Core.<hash>.wasm`, …);
- the CSS bundle `HelloViu.viu.css` — registered as a content-fingerprinted static web asset, though
  a standalone publish ships the stable plain-named file that any static host can serve — with its
  `.gz` and `.br` variants;
- `index.html` with the injected `<link rel="stylesheet" href="HelloViu.viu.css" />` (carried into the
  compressed `index.html.gz` / `index.html.br` too);
- the `viu-dom.js` interop bridge under `_content/Assimalign.Viu.Browser/`.

That folder is a static site — host it on any static web host.

## Where to go next

- **The repository map** in the [root `README.md`](../../README.md) — every library, generator, sample,
  and packaging project.
- **Per-library overviews** — each library documents itself in `docs/OVERVIEW.md` (what it is) and
  `docs/DESIGN.md` (why it is shaped that way). Start with
  [Core](../../libraries/Assimalign.Viu.Core/docs/OVERVIEW.md) and
  [Browser](../../libraries/Assimalign.Viu.Browser/docs/OVERVIEW.md).
- **The sample app** — the external
  [`viu-examples` showcase](https://github.com/assimalign/viu-examples) consumes the packaged SDK
  and demonstrates components, routing, reactivity, state, browser directives, built-ins, and the
  `.viu` CSS pipeline.
- **The specification** — [`docs/SPECIFICATION.md`](../SPECIFICATION.md) states what Viu guarantees,
  clause by clause: the execution model, the component model, reactivity, the rendering architecture,
  compilation, styling, server rendering, routing, and state. It is the place to go when the guide
  says *what* and you need *exactly what*.
- **The delivery plan** — [`docs/PLAN.md`](../PLAN.md) records the wave strategy and the founding
  decisions; the [architecture decisions](../adr/) log the repo-wide C#/WASM choices behind them.

## Not yet available

This guide is intentionally scoped to what a consumer can build and publish today. Coming later:

- **A `dotnet new` project template** — [V01.01.12.04] (W05); until then, create the project by hand as
  above.
- **Parameters, events, and lifecycle hooks declared inside a `.viu` `@script`** — still in progress
  ([#227](https://github.com/assimalign/viu/issues/227)). Mountable `.viu` `<template>`/`@script` components
  themselves already work ([#216](https://github.com/assimalign/viu/issues/216), see the note above).
- **A template-syntax reference and the API reference site** — the Documentation area
  [V01.01.13](https://github.com/assimalign/viu/issues/97).

---

This guide follows the repo's [documentation conventions](../CONTRIBUTING.md); every code block is
extracted verbatim from a Viu app that was built, run, and published against the packaged SDK.
