# Getting started with Viu

Viu is a standalone C#/.NET user-interface framework that runs in the browser through the .NET
WebAssembly build tools. This guide takes you from an empty folder to a running, publishable Viu app
using the `viu-app` project template and packaged **`Assimalign.Viu.Sdk.Browser`**. Host-neutral
component libraries use the `viu-lib` template and base `Assimalign.Viu.Sdk`; browser applications
use the Browser SDK shown throughout this guide.

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

> **Preview status.** Viu is pre-release. Package versions and the local feed below reflect the
> current preview; once Viu publishes to nuget.org the feed step goes away. The manual project anatomy
> remains after the template walkthrough so every generated file and SDK choice stays explicit.

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

A Viu browser app project uses the Browser MSBuild SDK instead of the host-neutral base SDK:

```xml
<Project Sdk="Assimalign.Viu.Sdk.Browser">
```

That one line imports `Assimalign.Viu.Sdk`, chains `Microsoft.NET.Sdk.WebAssembly`, references the
targeting-only `Assimalign.Viu.App` base plus `Assimalign.Viu.App.Browser`, and turns on `.viu`
single-file-component compilation and Browser CSS delivery with no per-project wiring. The full
component-library and application surfaces, including every opt-out property, are documented in
[`sdks/README.md`](../../sdks/README.md); the packaging model is
[founding decision 8](../PLAN.md#founding-design-decisions-cwasm-divergences).

While Viu is pre-release you consume it from a **repo-local NuGet feed**. From a clone of
[`assimalign/viu`](https://github.com/assimalign/viu), pack the SDK and framework into `_out/packages`:

```sh
pwsh scripts/Install-Local.ps1
```

This produces both project SDKs, `Assimalign.Viu.App.Ref`,
`Assimalign.Viu.App.Browser.Ref`, and
`Assimalign.Viu.App.Browser.Runtime.browser-wasm` (see the
[local development loop](../../sdks/README.md#local-development-loop)). Your app points a
`nuget.config` at that folder, shown below.

## Create the project from the template

Install the template pack from the same feed as the SDK and framework packages. During the preview,
run this from a directory whose `nuget.config` contains the `viu-local` source shown in the manual
walkthrough below:

```sh
dotnet new install Assimalign.Viu.Templates --nuget-source C:\Source\repos\assimalign\viu\_out\packages
dotnet new list viu
```

Create and enter a Browser WebAssembly application:

```sh
dotnet new viu-app --name HelloViu
cd HelloViu
dotnet run
```

The generated application uses `Assimalign.Viu.Sdk.Browser`, pins both Viu project SDKs to the
template package's version, enables nullable reference types and full trimming, and contains a
compiled `.viu` reactive counter. Pass `--nullable false` only when the project deliberately disables
nullable analysis. A host-neutral, packable component library with a passing generated-catalog test
project is equally small:

```sh
dotnet new viu-lib --name Contoso.Components
cd Contoso.Components
dotnet test
dotnet pack -c Release
```

The library template uses `Assimalign.Viu.Sdk` and never pulls in Browser or the WebAssembly workload.
For a Browser application with a server-rendering host, enable the explicit template option:

```sh
dotnet new viu-app --name HelloViu --ssr
dotnet run --project HelloViu/Server/HelloViu.Server.csproj
```

That variant moves the counter into a host-neutral component project, declares
`ViuServerRendering=true`, and adds an ASP.NET host. The Browser and server projects share the same
compiled component assembly; the host explicitly registers `GeneratedViuComponents` and
`GeneratedViuServerRenders`, so server selection performs no reflection or assembly scanning.
Specified by `[SSR-TARGET-1]` through `[SSR-TARGET-3]` and [V01.01.12.28].

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
        "Assimalign.Viu.Sdk": "10.0.1-preview.2",
        "Assimalign.Viu.Sdk.Browser": "10.0.1-preview.2"
    }
}
```

**`nuget.config`** — add the local feed alongside nuget.org (point `value` at your clone's
`_out/packages`; this matches the [`sdks/README.md`](../../sdks/README.md#local-development-loop)
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
<Project Sdk="Assimalign.Viu.Sdk.Browser">

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

A Viu authored component is a plain C# object implementing `IComponent`. Its synchronous `Setup`
method runs **once** per mounted invocation and returns a `ComponentRenderer`. That render function
receives the mount's `ComponentRenderFrame`, produces a fresh immutable `VirtualNode` description,
and re-runs whenever reactive state it read changes. Static identity and input/output declarations
live on `ComponentRegistration`, where Core can read them before activation; live host bookkeeping
stays internal. These four distinct lifetimes keep activation explicit, AOT-safe, and free of
reflection ([ADR-0004](../adr/0004-composition-only-component-model.md)).

**`Program.cs`** — a Viu WASM app's whole bootstrap: compose the app, decorate its live lifetime, and
await that lifetime. Direct construction of `BrowserApplicationBuilder` selects the browser host and
its default `#app` mount target. The `RunAsync` extension starts the application, waits for shutdown,
and stops it; it remains pending across the mounted lifetime, so no artificial infinite delay is
required:

```csharp
using Assimalign.Viu;
using Assimalign.Viu.Browser;
using Assimalign.Viu.Components;

using HelloViu;

ComponentFactory components = new();
components.Register(
    new ComponentRegistration(
        Counter.Reference,
        Counter.Contract,
        static _ => new Counter()));

await new BrowserApplicationBuilder()
    .ConfigureApplication(options =>
    {
        options.RootComponent = new ComponentNode(Counter.Reference);
        options.Components = components;
    })
    .Build()
    .RunAsync();
```

Compose the app exclusively through `ConfigureApplication(options => ...)`: `RootComponent`,
`Components`, `Services`, `State`, `Directives`, and diagnostics all live on
`ApplicationOptions`. `Build()` freezes that composition into a read-only `IApplicationContext`;
later option changes cannot recompose it. The context also exposes the live `IsRunning` state and
`Stopping` token. After `Build()`, `Use` registers asynchronous middleware around the complete live
application lifetime. It does not mutate the component, directive, service, or state composition,
and calling it after execution starts throws ([APP-2]–[APP-4]).

### Dependency injection (`System.IServiceProvider`)

For app-level singletons — a data client, a router, a store registry — Viu integrates
**bring-your-own dependency injection over `System.IServiceProvider`**. Build and own the provider
with the container of your choice, attach its lookup-only interface to Viu, then resolve from a
component's `Setup`:

```csharp
using System;

using Assimalign.Viu;
using Assimalign.Viu.Browser;
using Assimalign.Viu.Components;

IComponentFactory components = BuildComponentFactory();
IServiceProvider services = BuildApplicationServices();

await new BrowserApplicationBuilder()
    .ConfigureApplication(options =>
    {
        options.RootComponent = new ComponentNode(App.Reference);
        options.Components = components;
        options.Services = services;
    })
    .Build()
    .RunAsync();
```

```csharp
// inside a component's Setup:
var api = (ApiClient?)context.Services?.GetService(typeof(ApiClient))
    ?? throw new InvalidOperationException("ApiClient is not registered.");
```

`IServiceProvider` is lookup-only, so Viu cannot offer container-agnostic registration methods or
invent lifetime semantics over it. Register services through your chosen container, then assign the
resulting provider to `ApplicationOptions.Services`. Viu borrows it and never disposes it; the
external composition root retains ownership [APP-6]. Services are nullable and opt-in; a primitive
root needs neither a component registration nor a provider.

This is **app-level** DI. Viu deliberately has **no hierarchical component-tree dependency API** —
no ambient provide/inject walking up the parent chain
([`[CMP-24]`](../SPECIFICATION.md#48-no-component-tree-provideinject)). A component's dependencies are
explicit:

- `ComponentContext.Bindings` carries contract-resolved parameters, slots, and fallthrough values;
- nullable `ComponentContext.Services` carries application services;
- State and other conventions attach through services plus the ambient reactive scope; and
- a parent requests a component explicitly with `ComponentNode` and `ComponentReference`, while
  application composition owns registration resolution.

This is a decision, not a deferral. An ambient hierarchical channel makes a component's contract
invisible at its call site and its behavior dependent on where it happens to be mounted; it also
resists trimming, because nothing static says which values a subtree will ask for. The cost is
visible and accepted elsewhere in the framework — `RouterView` takes its nesting depth as an explicit
argument precisely because no ambient channel exists to carry it.

**`Counter.cs`** — a working counter:

```csharp
using System;

using Assimalign.Viu.Components;
using Assimalign.Viu.Reactivity;

namespace HelloViu;

internal sealed class Counter : IComponent
{
    public static ComponentReference Reference { get; } =
        ComponentReference.ForType(typeof(Counter));

    public static ComponentContract Contract { get; } =
        new(displayName: nameof(Counter));

    public ComponentRenderer Setup(ComponentContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        Reference<int> count = Reactive.Reference(0);
        Computed<string> label = Reactive.Computed(
            () => count.Value == 1 ? "1 click" : $"{count.Value} clicks");

        void Increment() => count.Value++;

        return frame => new ElementNode(
            new QualifiedName("section"),
            bindings:
            [
                ElementBinding.Attribute(new QualifiedName("class"), "counter"),
            ],
            children:
            [
                new ElementNode(
                    new QualifiedName("h1"),
                    children: [new TextNode("Hello from Viu")]),
                new ElementNode(
                    new QualifiedName("p"),
                    bindings:
                    [
                        ElementBinding.Attribute(new QualifiedName("class"), "count"),
                    ],
                    children: [new TextNode(label.Value)]),
                new ElementNode(
                    new QualifiedName("button"),
                    bindings:
                    [
                        ElementBinding.Attribute(new QualifiedName("class"), "primary"),
                        ElementBinding.Attribute(new QualifiedName("type"), "button"),
                        ElementBinding.Event("click", (Action)Increment),
                    ],
                    children: [new TextNode("Increment")]),
            ]);
    }
}
```

The render function builds an immutable virtual-node tree; the runtime diffs it and applies only the
changed nodes to the real DOM. Hand-built output carries `RenderPlan.None` and takes the correct full
diff path. This matters more on WASM than in JavaScript: **every DOM mutation crosses the JS-interop
boundary**, so idiomatic Viu leans on compiled render functions and batched renderer updates rather
than imperative DOM access ([ADR-0003](../adr/0003-batched-interop-dom-operations.md)). `VirtualNode`,
its ten sealed node kinds, `IComponent`, `ComponentContext`, and registration vocabulary live in
[`Assimalign.Viu.Components`](../../libraries/Assimalign.Viu.Components/docs/OVERVIEW.md); the
browser `Application` facade and `BrowserApplication` host live in
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
[#216](https://github.com/assimalign/viu/issues/216)); a `.viu` also serves as a **bundled CSS** unit.
Per-component scope identifiers remain deferred under `[V01.01.06.12]`; their later addition is
additive to `ComponentContract` and does not change the file format.

> **`.vue` files compile too.** Viu ships a `.vue` single-file-component compatibility parser as a
> product feature ([V01.01.06.09], [#250](https://github.com/assimalign/viu/issues/250)). The SDK globs
> `**/*.viu` and `**/*.vue` into the same build graph, and everything downstream of the container
> parse — template code generation, style extraction, CSS Modules, source mapping, hot-reload metadata —
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
> class, **and** implements `IComponent` with a synchronous `Setup(ComponentContext)` returning a
> frame-based `ComponentRenderer`. Its generated registration carries a static `ComponentContract`
> beside an explicit activator. Register that definition in the application `IComponentFactory`,
> request it with `new ComponentNode(ComponentReference.ForType(typeof(Greeting)))`, and assign the factory to `ApplicationOptions.Components`
> before building and invoking the `RunAsync` extension. Reactive `@script` members (a `Reference<T>`, a `[Reactive]` field) drive
> re-render, and a
> template event handler (`@click="Increment"`) calls the like-named `@script` method:
>
> ```
> <template>
>     <button class="counter" @click="Increment">{{ Count }}</button>
> </template>
> @script {
>     using Assimalign.Viu.Reactivity;
>     public readonly Reference<int> Count = Reactive.Reference(0);
>     public void Increment() => Count.Value++;
> }
> ```
>
> The completed `[V01.01.15]` migration changed the generated runtime bridge, not this authoring experience:
> template events still call the named C# member, reactive reads still drive re-render, and parameters,
> events, lifecycle declarations, and fallthrough behavior retain their specified source forms.
> Hand-authored `IComponent` components use the same registration and rendering vocabulary.

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

The component examples in this guide describe the shipping `[V01.01.15]` surface. Remaining
documentation work includes:

- **A template-syntax reference and the API reference site** — the Documentation area
  [V01.01.13](https://github.com/assimalign/viu/issues/97).

---

This guide follows the repo's [documentation conventions](../CONTRIBUTING.md). Its component-model,
packaging, and command examples describe the current Browser SDK workflow.
