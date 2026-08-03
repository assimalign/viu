# Viu

A standalone C#/.NET user-interface framework, running in the browser through the .NET WebAssembly
build tools (`Microsoft.NET.Sdk.WebAssembly`, `JSImport`/`JSExport` interop). Viu renders through a
**hierarchical virtual-node tree with compiler-informed diffing**: an application describes its UI as
an immutable tree of node descriptions, a build-time compiler annotates that tree with what can
change, and the runtime patches only the annotated parts.

[`docs/SPECIFICATION.md`](docs/SPECIFICATION.md) is the authoritative statement of what Viu is and
what it guarantees. Three constraints shape everything below it:

- **Roslyn source generators are the sanctioned metaprogramming mechanism.** WASM is AOT/trimming
  territory, so reflection-based serialization and runtime code generation are forbidden. Templates,
  `[Reactive]` property bodies, and activation paths are all emitted at build time; there is no
  runtime compiler.
- **Reactivity is explicit reference cells.** `Reference<T>` and `Computed<T>` are read and written
  through `.Value`, so a dependency is established by an ordinary property read rather than by
  interception — nothing is tracked invisibly and nothing needs a runtime object proxy.
- **The JS-interop boundary is the performance budget.** Crossing it is the dominant runtime cost, so
  DOM mutations batch into as few crossings as possible and static content is stringified
  aggressively.

Those three are recorded as architecture decisions in [`docs/adr/`](docs/adr/); the delivery
narrative — waves, the WBS map, and the founding decisions — lives in
[`docs/PLAN.md`](docs/PLAN.md).

Viu also ships a **`.vue` single-file-component compatibility parser** as a product feature
([V01.01.06.09], [#250](https://github.com/assimalign/viu/issues/250)): tag-based `.vue` files
compile under Viu alongside the canonical `.viu` container. That is a compatibility target on a
documented external format — the same category as Viu Utilities' Tailwind CSS v4.3.3 target — and is
specified in [§9 of the specification](docs/SPECIFICATION.md#9-vue-compatibility--a-shipping-feature).

## Status

Early, active development, delivered in waves (see [`docs/PLAN.md`](docs/PLAN.md) and the
[project board](https://github.com/orgs/assimalign/projects/15) for the authoritative status). The
reactive core, the host-neutral renderer with its scheduler and component model, the browser DOM
bridge, the template compiler front end, the `.viu`/`.vue` single-file-component pipeline, the
router's DOM-free route table and matcher, and the state package's `StateStoreDefinition` /
`StateStoreRegistry` API are all in the tree at varying maturity; each library's `docs/OVERVIEW.md`
states what it currently provides, and the specification describes implemented behavior only (its
[§17](docs/SPECIFICATION.md#17-non-goals-and-current-limits) carries the non-goals and the current
limits). The packaged-consumer showcase lives in the separate
[`assimalign/viu-examples`](https://github.com/assimalign/viu-examples) repository.

## Repository map

Framework libraries use the inverted layout `libraries/Assimalign.Viu.<Name>/{src,test,docs}` — the
folder name **is** the assembly and package id (no area wrapper folders). Each shipping library
carries a `docs/OVERVIEW.md` (what it is and its public surface) and, where the shape needs
justifying, a `docs/DESIGN.md` (why it is built that way, the WASM/AOT constraints, and its
non-goals). Neither may contradict [`docs/SPECIFICATION.md`](docs/SPECIFICATION.md).

### Framework libraries (`libraries/`)

| Library | Responsibility | Docs |
| --- | --- | --- |
| [`Assimalign.Viu.Shared`](libraries/Assimalign.Viu.Shared) | The compiler↔runtime flag vocabulary (`PatchFlags`, `ShapeFlags`, `SlotFlags`), class/style normalization, form-binding value matching, and the HTML/SVG/MathML knowledge tables | [OVERVIEW](libraries/Assimalign.Viu.Shared/docs/OVERVIEW.md) · [DESIGN](libraries/Assimalign.Viu.Shared/docs/DESIGN.md) |
| [`Assimalign.Viu.Components`](libraries/Assimalign.Viu.Components) | The immutable component-tree vocabulary — `IComponent` and the element, template, text, comment, static, fragment, and teleport shapes — plus the activation and component-resolution contracts | [OVERVIEW](libraries/Assimalign.Viu.Components/docs/OVERVIEW.md) |
| [`Assimalign.Viu.Reactivity`](libraries/Assimalign.Viu.Reactivity) | The dependency engine and the reference primitives: `Reference<T>`, `ShallowReference<T>`, `CustomReference<T>`, `Computed<T>`, effects, effect scopes, `Watch`, and the reactive collections | [OVERVIEW](libraries/Assimalign.Viu.Reactivity/docs/OVERVIEW.md) · [DESIGN](libraries/Assimalign.Viu.Reactivity/docs/DESIGN.md) |
| [`Assimalign.Viu.State`](libraries/Assimalign.Viu.State) | Store definitions (`StateStoreDefinition<TStore>`) and the `StateStoreRegistry` that owns their reactive lifetimes, plus the optional `StateStore<TState>` member model with `Patch`/`Reset`/`Subscribe`/`OnAction` ([V01.01.09]) | [OVERVIEW](libraries/Assimalign.Viu.State/docs/OVERVIEW.md) · [DESIGN](libraries/Assimalign.Viu.State/docs/DESIGN.md) |
| [`Assimalign.Viu.Core`](libraries/Assimalign.Viu.Core) | The host-neutral application, renderer, and scheduler — block-aware patch dispatch, keyed reconciliation, hydration — and the built-in components (Teleport, KeepAlive, Suspense, transitions, asynchronous and dynamic components). Rooted at the `Assimalign.Viu` namespace, because the core *is* the product | [OVERVIEW](libraries/Assimalign.Viu.Core/docs/OVERVIEW.md) · [KEEP-ALIVE](libraries/Assimalign.Viu.Core/docs/KEEP-ALIVE.md) · [ASYNC/DYNAMIC](libraries/Assimalign.Viu.Core/docs/ASYNCHRONOUS-AND-DYNAMIC-COMPONENTS.md) |
| [`Assimalign.Viu.Browser`](libraries/Assimalign.Viu.Browser) | The browser host adapter: the batched JS-interop DOM bridge, attribute/property patching, event wiring, the `v-model`/`v-show` directives, and CSS transitions | [OVERVIEW](libraries/Assimalign.Viu.Browser/docs/OVERVIEW.md) · [DESIGN](libraries/Assimalign.Viu.Browser/docs/DESIGN.md) · [ADR-0001](libraries/Assimalign.Viu.Browser/docs/ADR-0001-interop-marshaling.md) |
| [`Assimalign.Viu.ServerRenderer`](libraries/Assimalign.Viu.ServerRenderer) | The DOM-free string/stream HTML renderer (WHATWG-exact escaping, attributes, class/style, slots, teleport buffering, `serverPrefetch`) and the hydration marker protocol; the compiler's server code generation and the server adaptor follow ([V01.01.07]) | [OVERVIEW](libraries/Assimalign.Viu.ServerRenderer/docs/OVERVIEW.md) · [DESIGN](libraries/Assimalign.Viu.ServerRenderer/docs/DESIGN.md) |
| [`Assimalign.Viu.Router`](libraries/Assimalign.Viu.Router) | The DOM-free route table and matcher, history integration (memory/web/hash), the `RouterView`/`RouterLink` components, and the asynchronous navigation-guard pipeline; lazy routes and scroll behavior follow ([V01.01.08]) | [OVERVIEW](libraries/Assimalign.Viu.Router/docs/OVERVIEW.md) · [DESIGN](libraries/Assimalign.Viu.Router/docs/DESIGN.md) |
| [`Assimalign.Viu.Router.Browser`](libraries/Assimalign.Viu.Router.Browser) | The browser bridge wiring the Browser host's click dispatch into `RouterLink` navigation, so the router core stays DOM-free; installed at bootstrap by router apps ([V01.01.08]) | [OVERVIEW](libraries/Assimalign.Viu.Router.Browser/docs/OVERVIEW.md) · [DESIGN](libraries/Assimalign.Viu.Router.Browser/docs/DESIGN.md) |
| [`Assimalign.Viu.Syntax`](libraries/Assimalign.Viu.Syntax) | The shared parser base: located node and diagnostic primitives, and the registration-based pipeline every language library roots on | [OVERVIEW](libraries/Assimalign.Viu.Syntax/docs/OVERVIEW.md) · [DESIGN](libraries/Assimalign.Viu.Syntax/docs/DESIGN.md) |
| [`Assimalign.Viu.Syntax.Templates`](libraries/Assimalign.Viu.Syntax.Templates) | The template language front end — parse, transform, static analysis, patch-flag inference — and the C# render-method code generator | [OVERVIEW](libraries/Assimalign.Viu.Syntax.Templates/docs/OVERVIEW.md) · [DESIGN](libraries/Assimalign.Viu.Syntax.Templates/docs/DESIGN.md) |
| [`Assimalign.Viu.Syntax.SingleFileComponent`](libraries/Assimalign.Viu.Syntax.SingleFileComponent) | Both container parsers over one shared tag scanner: the canonical `.viu` container, and the `.vue` compatibility parser that is a shipping feature ([V01.01.06.09], [#250](https://github.com/assimalign/viu/issues/250)) | [OVERVIEW](libraries/Assimalign.Viu.Syntax.SingleFileComponent/docs/OVERVIEW.md) · [DESIGN](libraries/Assimalign.Viu.Syntax.SingleFileComponent/docs/DESIGN.md) · [FORMAT](libraries/Assimalign.Viu.Syntax.SingleFileComponent/docs/FORMAT.md) |
| [`Assimalign.Viu.Syntax.Css`](libraries/Assimalign.Viu.Syntax.Css) | The CSS tokenizer, rule parser, and scoped-CSS rewrite behind `<style>` block compilation | [OVERVIEW](libraries/Assimalign.Viu.Syntax.Css/docs/OVERVIEW.md) · [DESIGN](libraries/Assimalign.Viu.Syntax.Css/docs/DESIGN.md) |
| [`Assimalign.Viu.Syntax.Html`](libraries/Assimalign.Viu.Syntax.Html) | The `.html` host-page language, for build-time rewriting of the boot page (scaffold) | [OVERVIEW](libraries/Assimalign.Viu.Syntax.Html/docs/OVERVIEW.md) · [DESIGN](libraries/Assimalign.Viu.Syntax.Html/docs/DESIGN.md) |
| [`Assimalign.Viu.Syntax.JavaScript`](libraries/Assimalign.Viu.Syntax.JavaScript) | The `.js` language around the interop boundary (scaffold) | [OVERVIEW](libraries/Assimalign.Viu.Syntax.JavaScript/docs/OVERVIEW.md) · [DESIGN](libraries/Assimalign.Viu.Syntax.JavaScript/docs/DESIGN.md) |
| [`Assimalign.Viu.Testing`](libraries/Assimalign.Viu.Testing) | The in-memory host (`TNode = TestNode`) and the component test wrappers, so the runtime is exercised without a browser | [OVERVIEW](libraries/Assimalign.Viu.Testing/docs/OVERVIEW.md) · [DESIGN](libraries/Assimalign.Viu.Testing/docs/DESIGN.md) |
| [`Assimalign.Viu.Tooling.Css`](libraries/Assimalign.Viu.Tooling.Css) | The build-time composition root for `<style>` compilation and bundling that both build-time hosts share | [OVERVIEW](libraries/Assimalign.Viu.Tooling.Css/docs/OVERVIEW.md) · [DESIGN](libraries/Assimalign.Viu.Tooling.Css/docs/DESIGN.md) |
| [`Assimalign.Viu.Tooling.SingleFileComponent`](libraries/Assimalign.Viu.Tooling.SingleFileComponent) | The ONE `.viu`/`.vue` → C# projection (parse, `@script` analysis, render and source maps, diagnostics) that the source generator and the language service both run, so build output and editor understanding cannot drift ([V01.01.06.11]) | [OVERVIEW](libraries/Assimalign.Viu.Tooling.SingleFileComponent/docs/OVERVIEW.md) · [DESIGN](libraries/Assimalign.Viu.Tooling.SingleFileComponent/docs/DESIGN.md) |
| [`Assimalign.Viu.Tooling.UtilityCss`](libraries/Assimalign.Viu.Tooling.UtilityCss) | The build-time engine for **Viu Utilities** — candidate scanning, the project candidate index, and utility generation — an independent C# implementation pinned to the Tailwind CSS v4.3.3 compatibility target | [OVERVIEW](libraries/Assimalign.Viu.Tooling.UtilityCss/docs/OVERVIEW.md) · [DESIGN](libraries/Assimalign.Viu.Tooling.UtilityCss/docs/DESIGN.md) · [THIRD-PARTY-NOTICES](libraries/Assimalign.Viu.Tooling.UtilityCss/docs/THIRD-PARTY-NOTICES.md) |

### Source generators and build tasks (`analyzers/`)

These are build-time (netstandard2.0) components. They are the sanctioned metaprogramming mechanism:
because WASM forbids runtime code generation, everything a dynamic language would do at run time
happens here instead. They never ship in the runtime assemblies.

| Project | Role |
| --- | --- |
| `Assimalign.Viu.Generators.Reactivity` | Emits the tracking/triggering property bodies for `[Reactive]`/`[ShallowReactive]` partial classes, so a plain object becomes reactive with no reflection and no runtime interception. |
| `Assimalign.Viu.Generators.Syntax` | The incremental generator that compiles `.viu` single-file components and templates to C# render methods (the composition root that registers the template and style parsers). |
| `Assimalign.Viu.Sdk.Tasks` | The SDK's MSBuild tasks, including `ViuBundleCss`, which writes compiled `.viu` `<style>` output to a physical stylesheet outside the analyzer sandbox. |

### Packaged SDK showcase

[`assimalign/viu-examples`](https://github.com/assimalign/viu-examples) contains the complete
browser showcase. It consumes `Assimalign.Viu.Sdk`, `Assimalign.Viu.Router`, and
`Assimalign.Viu.Router.Browser` from a local NuGet feed, so it exercises the same package boundary
as an external application rather than relying on project references into this repository.

### Packaging (`sdks/`, `frameworks/`)

External apps consume Viu through an MSBuild project SDK, not project references — a complete app
csproj is `<Project Sdk="Assimalign.Viu.Sdk">`. The SDK chains `Microsoft.NET.Sdk.WebAssembly` and
delivers the framework as the `Assimalign.Viu.App` shared framework (the
`Microsoft.AspNetCore.App.Ref`/`.Runtime.<rid>` model, mirrored from `assimalign/cohesion`). See
[`sdks/README.md`](sdks/README.md) for the full consumer surface and the local development loop.

| Path | Produces | Role |
| --- | --- | --- |
| `sdks/Assimalign.Viu.Sdk` | `Assimalign.Viu.Sdk` | The project SDK: chains the WebAssembly SDK, registers the `Assimalign.Viu.App` framework reference, and ships the `.viu`/CSS build wiring and the `viu-dom.js` bridge. |
| `frameworks/Assimalign.Viu.App.Refs` | `Assimalign.Viu.App.Ref` | The targeting pack: reference assemblies, `FrameworkList.xml`, and the generators (delivered as analyzers). |
| `frameworks/Assimalign.Viu.App.Runtime` | `Assimalign.Viu.App.Runtime.browser-wasm` | The per-RID runtime pack: implementation assemblies for `browser-wasm`. |

In-repo projects dogfood the framework through `ViuProjectReference` (see
[`.claude/rules/build-system.md`](.claude/rules/build-system.md)); the SDK is the external-consumer
surface.

## Getting started

### Prerequisites

- The [.NET SDK](https://dotnet.microsoft.com/download) pinned in [`global.json`](global.json)
  (currently `10.0.301`).
- The WebAssembly tools workload, needed to build and run the browser sample:
  ```sh
  dotnet workload install wasm-tools
  ```

### Clone and build

```sh
git clone https://github.com/assimalign/viu.git
cd viu
dotnet build Assimalign.Viu.slnx
```

### Test

Each library's tests live beside it under `test/`:

```sh
dotnet test libraries/Assimalign.Viu.Core/test/
```

### Run the showcase

Pack the local SDK and framework, then follow the
[`viu-examples` README](https://github.com/assimalign/viu-examples#run-locally).

## Plan and tracking

- [Specification](docs/SPECIFICATION.md) — the authoritative statement of Viu's semantics: the
  execution model, the component model, reactivity, the rendering architecture, compilation,
  styling, server rendering, routing, state, tooling, and packaging. Clauses carry stable ids
  (`RND-BLOCK-2`, `SCH-4`, …) that code, tests, and issues cite as text.
- [Delivery plan](docs/PLAN.md) — the wave strategy, the WBS map, and the founding design decisions
  (with the historical record of how the areas were originally scoped).
- [Architecture decisions](docs/adr/) — the append-only decision log for repo-wide, cross-cutting
  decisions.
- [Performance research](docs/PERFORMANCE-RESEARCH.md) — the explicitly non-normative ledger for
  optimization techniques observed elsewhere, measured against Viu's benchmark baselines before any
  are adopted.
- [Documentation conventions](docs/CONTRIBUTING.md) — where `OVERVIEW.md`, `DESIGN.md`, and ADRs
  live, what belongs in each, and when they must be updated.
- [Getting started guide](docs/guide/getting-started.md) — build, run, and publish a Viu app with the
  packaged `Assimalign.Viu.Sdk` (prerequisites → first component → reactivity → publish).
- [Release guide](docs/RELEASING.md) — beta and stable package channels, NuGet trusted publishing,
  GitHub Packages, and the Visual Studio Marketplace preview.
- [Project board](https://github.com/orgs/assimalign/projects/15) — the authoritative backlog
  (`[V01.01.*]` WBS items: program → area epics → features → tasks).
- Work-item intake: [`.claude/skills/viu-work-items`](.claude/skills/viu-work-items/SKILL.md).

## License

See [LICENSE](LICENSE).
