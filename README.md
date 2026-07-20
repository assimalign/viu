# Viu

A faithful re-implementation of [Vue.js 3](https://vuejs.org) in C#/.NET, running in the browser
through the .NET WebAssembly build tools (`Microsoft.NET.Sdk.WebAssembly`, `JSImport`/`JSExport`
interop). Viu mirrors Vue 3's package boundaries as `Assimalign.Viu.*` class libraries and tracks
[vuejs/core](https://github.com/vuejs/core) (v3.5.x) semantics, with three deliberate C#/WASM
divergences at its core:

- **Roslyn source generators** stand in for everything Vue does with the JavaScript `Proxy` and
  runtime `new Function` — WASM is AOT/trimming territory, so reflection-based serialization and
  dynamic code generation are forbidden.
- **Ref-first reactivity** replaces Proxy-based reactive objects; `[Reactive]` partial classes are
  source-generated.
- **Batched JS-interop** is the performance budget: the interop boundary is the dominant cost, so
  DOM mutations batch and static content is stringified aggressively.

These are recorded as architecture decisions in [`docs/adr/`](docs/adr/); the full architecture map,
founding decisions, and wave strategy live in [`docs/PLAN.md`](docs/PLAN.md).

## Status

Early, active development, delivered in waves (see [`docs/PLAN.md`](docs/PLAN.md) and the
[project board](https://github.com/orgs/assimalign/projects/15) for the authoritative status). The
reactive core, the platform-agnostic renderer with scheduler and component model, the browser DOM
bridge, the template compiler front end, the `.viu` single-file-component pipeline, the router's
DOM-free route table and matcher, and the store's setup-style `defineStore`/`createPinia` definition
API are all in the tree at varying maturity; each library's `docs/OVERVIEW.md` states what it
currently provides.
The demo is a reactively re-rendering stopwatch in
[`examples/Assimalign.Viu.WebApp`](examples/Assimalign.Viu.WebApp).

## Repository map

Framework libraries use the inverted layout `libraries/Assimalign.Viu.<Name>/{src,test,docs}` — the
folder name **is** the assembly and package id (no area wrapper folders). Each shipping library
carries a `docs/OVERVIEW.md` (what it is, its public surface, its Vue 3 counterpart) and a
`docs/DESIGN.md` (why it is shaped that way, the vuejs/core module it ports, and known deltas).

### Framework libraries (`libraries/`)

| Library | Vue 3 counterpart | Docs |
| --- | --- | --- |
| [`Assimalign.Viu.Shared`](libraries/Assimalign.Viu.Shared) | [`@vue/shared`](https://github.com/vuejs/core/tree/main/packages/shared) — PatchFlags/ShapeFlags/SlotFlags, class/style normalization, DOM knowledge tables | [OVERVIEW](libraries/Assimalign.Viu.Shared/docs/OVERVIEW.md) · [DESIGN](libraries/Assimalign.Viu.Shared/docs/DESIGN.md) |
| [`Assimalign.Viu.Reactivity`](libraries/Assimalign.Viu.Reactivity) | [`@vue/reactivity`](https://github.com/vuejs/core/tree/main/packages/reactivity) — dependencies, Ref/Computed, effects, scopes, watch, reactive collections | [OVERVIEW](libraries/Assimalign.Viu.Reactivity/docs/OVERVIEW.md) · [DESIGN](libraries/Assimalign.Viu.Reactivity/docs/DESIGN.md) |
| [`Assimalign.Viu.RuntimeCore`](libraries/Assimalign.Viu.RuntimeCore) | [`@vue/runtime-core`](https://github.com/vuejs/core/tree/main/packages/runtime-core) — vnodes, renderer, scheduler, component model, built-ins | [OVERVIEW](libraries/Assimalign.Viu.RuntimeCore/docs/OVERVIEW.md) · [DESIGN](libraries/Assimalign.Viu.RuntimeCore/docs/DESIGN.md) |
| [`Assimalign.Viu.RuntimeDom`](libraries/Assimalign.Viu.RuntimeDom) | [`@vue/runtime-dom`](https://github.com/vuejs/core/tree/main/packages/runtime-dom) — JS-interop DOM bridge, patchProp, events, v-model/v-show | [OVERVIEW](libraries/Assimalign.Viu.RuntimeDom/docs/OVERVIEW.md) · [DESIGN](libraries/Assimalign.Viu.RuntimeDom/docs/DESIGN.md) |
| [`Assimalign.Viu.Router`](libraries/Assimalign.Viu.Router) | [`vue-router`](https://github.com/vuejs/router) — the DOM-free route table and matcher, history integration (memory/web/hash), and the RouterView/RouterLink components today; async navigation guards and lazy routes follow ([V01.01.08]) | [OVERVIEW](libraries/Assimalign.Viu.Router/docs/OVERVIEW.md) · [DESIGN](libraries/Assimalign.Viu.Router/docs/DESIGN.md) |
| [`Assimalign.Viu.Router.RuntimeDom`](libraries/Assimalign.Viu.Router.RuntimeDom) | (no direct Vue peer — vue-router touches the DOM itself) — the browser bridge wiring RuntimeDom's click dispatch into RouterLink navigation; installed at bootstrap by router apps ([V01.01.08]) | [OVERVIEW](libraries/Assimalign.Viu.Router.RuntimeDom/docs/OVERVIEW.md) · [DESIGN](libraries/Assimalign.Viu.Router.RuntimeDom/docs/DESIGN.md) |
| [`Assimalign.Viu.Store`](libraries/Assimalign.Viu.Store) | [`pinia`](https://github.com/vuejs/pinia) — the setup-style `defineStore`/`createPinia` definition API on `EffectScope`, plus the `Store<TState>` member model (reactive state, computed getters, actions) with `Patch`/`Reset`/`Subscribe`/`OnAction`; SSR and plugins follow ([V01.01.09]) | [OVERVIEW](libraries/Assimalign.Viu.Store/docs/OVERVIEW.md) · [DESIGN](libraries/Assimalign.Viu.Store/docs/DESIGN.md) |
| [`Assimalign.Viu.Syntax`](libraries/Assimalign.Viu.Syntax) | (shared base) — the located node/diagnostic primitives and registration-based parser pipeline every language library roots on | [OVERVIEW](libraries/Assimalign.Viu.Syntax/docs/OVERVIEW.md) · [DESIGN](libraries/Assimalign.Viu.Syntax/docs/DESIGN.md) |
| [`Assimalign.Viu.Syntax.Templates`](libraries/Assimalign.Viu.Syntax.Templates) | [`@vue/compiler-core`](https://github.com/vuejs/core/tree/main/packages/compiler-core) + [`compiler-dom`](https://github.com/vuejs/core/tree/main/packages/compiler-dom) — the Vue template language front end and C# render-function codegen | [OVERVIEW](libraries/Assimalign.Viu.Syntax.Templates/docs/OVERVIEW.md) · [DESIGN](libraries/Assimalign.Viu.Syntax.Templates/docs/DESIGN.md) |
| [`Assimalign.Viu.Syntax.SingleFileComponent`](libraries/Assimalign.Viu.Syntax.SingleFileComponent) | [`@vue/compiler-sfc`](https://github.com/vuejs/core/tree/main/packages/compiler-sfc) — the `.viu` `@`-block container parser | [OVERVIEW](libraries/Assimalign.Viu.Syntax.SingleFileComponent/docs/OVERVIEW.md) · [DESIGN](libraries/Assimalign.Viu.Syntax.SingleFileComponent/docs/DESIGN.md) · [FORMAT](libraries/Assimalign.Viu.Syntax.SingleFileComponent/docs/FORMAT.md) |
| [`Assimalign.Viu.Syntax.Css`](libraries/Assimalign.Viu.Syntax.Css) | [`@vue/compiler-sfc`](https://github.com/vuejs/core/tree/main/packages/compiler-sfc) `compileStyle()` — CSS tokenizer, rule parser, and scoped-CSS rewrite | [OVERVIEW](libraries/Assimalign.Viu.Syntax.Css/docs/OVERVIEW.md) · [DESIGN](libraries/Assimalign.Viu.Syntax.Css/docs/DESIGN.md) |
| [`Assimalign.Viu.Syntax.Html`](libraries/Assimalign.Viu.Syntax.Html) | (Vite HTML entry processing) — the `.html` host-page language (scaffold) | [OVERVIEW](libraries/Assimalign.Viu.Syntax.Html/docs/OVERVIEW.md) · [DESIGN](libraries/Assimalign.Viu.Syntax.Html/docs/DESIGN.md) |
| [`Assimalign.Viu.Syntax.JavaScript`](libraries/Assimalign.Viu.Syntax.JavaScript) | (interop-glue JavaScript) — the `.js` language around the interop boundary (scaffold) | [OVERVIEW](libraries/Assimalign.Viu.Syntax.JavaScript/docs/OVERVIEW.md) · [DESIGN](libraries/Assimalign.Viu.Syntax.JavaScript/docs/DESIGN.md) |
| [`Assimalign.Viu.Testing`](libraries/Assimalign.Viu.Testing) | [`@vue/runtime-test`](https://github.com/vuejs/core/tree/main/packages/runtime-test) + [`@vue/test-utils`](https://test-utils.vuejs.org) — the in-memory renderer and component test harness | [OVERVIEW](libraries/Assimalign.Viu.Testing/docs/OVERVIEW.md) · [DESIGN](libraries/Assimalign.Viu.Testing/docs/DESIGN.md) |
| [`Assimalign.Viu.Tooling.Css`](libraries/Assimalign.Viu.Tooling.Css) | (build-time composition core, no direct Vue peer) — shared `.viu` `@style` compilation and bundling used by both build-time hosts | [OVERVIEW](libraries/Assimalign.Viu.Tooling.Css/docs/OVERVIEW.md) · [DESIGN](libraries/Assimalign.Viu.Tooling.Css/docs/DESIGN.md) |

### Source generators and build tasks (`analyzers/`)

These are build-time (netstandard2.0) components — the sanctioned replacement for Vue's `Proxy` and
runtime template compilation. They never ship in the runtime assemblies.

| Project | Role |
| --- | --- |
| `Assimalign.Viu.Reactivity.Generators` | Emits the property wrappers for `[Reactive]`/`[ShallowReactive]` partial classes (Vue's `reactive()`, source-generated). |
| `Assimalign.Viu.Syntax.Generators` | The incremental generator that compiles `.viu` single-file components and templates to C# render methods (the composition root that registers the template and style parsers). |
| `Assimalign.Viu.Tooling.Tasks` | The `ViuBundleCss` MSBuild task that writes the compiled `.viu` `@style` output to a physical stylesheet (outside the analyzer sandbox). |

### Examples (`examples/`)

| Sample | What it shows |
| --- | --- |
| [`Assimalign.Viu.WebApp`](examples/Assimalign.Viu.WebApp) | A browser WASM app: a stopwatch rendered from C# through the handle-based DOM bridge. Its `?diagnostics=1` mode runs the interop marshaling benchmark behind [RuntimeDom ADR-0001](libraries/Assimalign.Viu.RuntimeDom/docs/ADR-0001-interop-marshaling.md). |

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
dotnet test libraries/Assimalign.Viu.Reactivity/test/
dotnet test libraries/Assimalign.Viu.RuntimeCore/test/
```

### Run the demo

```sh
dotnet run --project examples/Assimalign.Viu.WebApp
```

## Plan and tracking

- [Delivery plan](docs/PLAN.md) — architecture mapping (Vue 3 package → Viu library), founding
  design decisions, and the wave strategy.
- [Architecture decisions](docs/adr/) — the append-only decision log (founding C#/WASM divergences).
- [Documentation conventions](docs/CONTRIBUTING.md) — where `OVERVIEW.md`, `DESIGN.md`, and ADRs
  live, what belongs in each, and when they must be updated.
- [Project board](https://github.com/orgs/assimalign/projects/15) — the authoritative backlog
  (`[V01.01.*]` WBS items: program → area epics → features → tasks).
- Work-item intake: [`.claude/skills/viu-work-items`](.claude/skills/viu-work-items/SKILL.md).

## License

See [LICENSE](LICENSE).
