# Viu Delivery Plan

Viu is a faithful re-implementation of Vue.js 3 in C#/.NET, running in the browser through the
.NET WebAssembly build tools (`Microsoft.NET.Sdk.WebAssembly`, `JSImport`/`JSExport` interop). This
document is the narrative companion to the executable backlog in the org GitHub Project
[**#15 "Viu"**](https://github.com/orgs/assimalign/projects/15) — the board is the authoritative,
living plan; this file records the architecture mapping, the founding design decisions, and the wave
strategy behind it.

## Where the POC stands

The current code proves the rendering seam end to end and nothing more:

- `Assimalign.Viu.CoreLib/VirtualDom` — an immutable VNode tree (`VElement`/`VText`/`VFragment`,
  keyed), a generic `VirtualDomRenderer<TNode>` over an `IVirtualDomAdapter<TNode>` (mount /
  in-place patch / unmount, fragments via comment markers), a standalone diff producing patch
  records, and an HTML string renderer.
- The original in-repository browser WASM stopwatch proved the handle-based DOM bridge
  (`JSImport` node operations and callbacks dispatched through `JSExport`). Current packaged SDK
  behavior is exercised by the separate
  [`assimalign/viu-examples`](https://github.com/assimalign/viu-examples) showcase.

What it validates: the adapter-injected renderer design (identical in spirit to Vue's
`createRenderer(RendererOptions)`), and that C#-driven DOM patching through interop works. What it
lacks: reactivity (the demo polls every 100 ms), a component model, a scheduler, keyed diffing with
minimal moves, compiler-informed patching, and every ecosystem piece. The child reconciliation is
index-based (no LIS), the diff/patch path is duplicated in two implementations, and naming was split
between `Assimalign.Vue.*` and `Assimalign.Vuecs.*` (standardized on `Assimalign.Vue.*` 2026-07-16,
then renamed to `Assimalign.Viu.*` with the product rename — see the architecture note below).

## Architecture: Vue 3 package → Viu library

Package boundaries map 1:1 to .NET class libraries using the inverted layout
`libraries/Assimalign.Viu.<Name>/{src|test}` — the folder name is the assembly/package id, with no
area wrapper folders (project decision, 2026-07-16). The product name is **Viu** and the package
root is **`Assimalign.Viu.*`** (renamed from Vue/Vuecs 2026-07-19, `V01.01.12.18`/#173, aligning
the brand with the `.viu` SFC extension; the GitHub repo slug is now `assimalign/viu` (renamed
from `assimalign/vuecs`), and upstream Vue.js references — `@vue/*` names, vuejs.org links, the
`vue:` template prefix — are deliberately untouched):

| Area (WBS) | Viu library | Vue 3 counterpart |
| --- | --- | --- |
| Shared (`V01.01.01`) | `Assimalign.Viu.Shared` | `@vue/shared` — PatchFlags/ShapeFlags/SlotFlags, normalization, DOM tables |
| Reactivity (`V01.01.02`) | `Assimalign.Viu.Reactivity` → merged into `Assimalign.Viu.Core` ([V01.01.12.21]) | `@vue/reactivity` — deps, Ref/Computed, effects, scopes, watch |
| RuntimeCore (`V01.01.03`) | `Assimalign.Viu.RuntimeCore` → renamed `Assimalign.Viu.Core`, root namespace `Assimalign.Viu` ([V01.01.12.21]) | `@vue/runtime-core` — vnodes, renderer, scheduler, component model, built-ins |
| RuntimeDom (`V01.01.04`) | `Assimalign.Viu.RuntimeDom` → renamed `Assimalign.Viu.Browser` ([V01.01.12.22]) | `@vue/runtime-dom` — JS-interop DOM bridge, patchProp, events, v-model/v-show |
| Compiler (`V01.01.05`) | `Assimalign.Viu.Syntax.Templates` (+ source generators) | `@vue/compiler-core` + `compiler-dom` (roots on the shared `Assimalign.Viu.Syntax` base) |
| SingleFileComponent (`V01.01.06`) | `Assimalign.Viu.Syntax.SingleFileComponent` (+ `Assimalign.Viu.Tooling.SingleFileComponent`, the shared build/editor projection core extracted with [V01.01.06.11]) | `@vue/compiler-sfc` — `.viu` single-file components (hybrid container syntax since `V01.01.06.10`: `<template>`/`<style>` tags + the `@script` block; the inner template language stays Vue markup; roots on the shared `Assimalign.Viu.Syntax` base) |
| ServerRenderer (`V01.01.07`) | `Assimalign.Viu.ServerRenderer` | `@vue/server-renderer` + `compiler-ssr` — SSR, hydration, SSG |
| Router (`V01.01.08`) | `Assimalign.Viu.Router` (+ `Assimalign.Viu.Router.Browser`, formerly `Assimalign.Viu.Router.RuntimeDom` — renamed [V01.01.12.22] — the browser click-dispatch bridge — vue-router touches the DOM directly; Viu's DOM-free Router cannot, so the glue is its own leaf package outside the shared framework) | `vue-router` |
| Store (`V01.01.09`) | `Assimalign.Viu.Store` | `pinia` |
| DevTools (`V01.01.10`) | `Assimalign.Viu.DevTools` | `vue-devtools` protocol + UI |
| Testing (`V01.01.11`) | `Assimalign.Viu.Testing` | `@vue/runtime-test` + `@vue/test-utils` |
| Tooling (`V01.01.12`) | build/CI/templates/dev loop | Vite + `create-vue` + monorepo infra |
| Documentation (`V01.01.13`) | docs + samples | vuejs.org + examples |

The parsing side of the map is the **`Assimalign.Viu.Syntax` cluster**: the shared base defines the
node/diagnostic primitives and a registration-based parser pipeline (`SyntaxParser`,
`AggregateSyntaxParser` — the seam build tooling uses to attach a parser to a block name, `lang`
option, or file type, the role Vite plugins play in a Vue build), and one library per language roots
on it: `Assimalign.Viu.Syntax.Templates` (the Vue template language), `.SingleFileComponent` (the
`.viu` container), and the browser-language scaffolds `.Css`, `.Html`, and `.JavaScript` (raw-root
parsers today; rule/element/statement-level parsing lands with their work items, starting with scoped
CSS [V01.01.06.04]). Composition roots sit beside the cluster as `Tooling` libraries:
`Assimalign.Viu.Tooling.Css` ([V01.01.12.12]) is the shared style compilation/bundling both
build-time hosts run, and `Assimalign.Viu.Tooling.SingleFileComponent` ([V01.01.06.11]) is the ONE
`.viu`/`.vue` → C# projection pipeline the `Assimalign.Viu.Generators.Syntax` source generator and
the Visual Studio language service both consume, so build output and editor understanding cannot
drift (the `@vue/compiler-sfc`-consumed-by-Vite-and-Volar shape).

## Founding design decisions (C#/WASM divergences)

> **Superseded framing (2026-08-02) — the decisions stand, the framing does not.**
>
> On 2026-08-02 the user directed that **Viu is a standalone framework, not a port of Vue.js**. Vue
> is no longer a normative authority for Viu's semantics; the adopted idea was the **render
> architecture** — hierarchical virtual-node-tree rendering with compiler-informed diffing — which
> Viu now owns and specifies on its own terms.
> [**`docs/SPECIFICATION.md`**](SPECIFICATION.md) is the authority for Viu's semantics, and behavior
> is pinned by this repository's tests.
>
> Three consequences for reading this section and the architecture map above it:
>
> 1. **The framing sentence immediately below is superseded.** "Everything else tracks Vue 3
>    semantics (vuejs/core v3.5.x) as the reference implementation" is no longer the policy. There is
>    no reference implementation; there is a specification. The eight decisions themselves are
>    unaffected and remain accurate — each is carried forward as normative clauses in
>    `SPECIFICATION.md` (§3 constraints, §5 reactivity, §6 rendering, §8 compilation, §11 the
>    host-agnostic rule, §15 packaging) and, for the five recorded as ADRs, in
>    [`docs/adr/`](adr/README.md), where each carries its own dated framing note.
> 2. **The 2026-07-19 packaging note above ("upstream Vue.js references — `@vue/*` names, vuejs.org
>    links, the `vue:` template prefix — are deliberately untouched") is the written form of the
>    policy this decision reverses**, and is superseded for `@vue/*` naming and vuejs.org links used
>    as behavioral authority. Two parts of that sentence remain **true and deliberate**: the `vue:`
>    template prefix is live shipping template syntax, and the "Vue 3 counterpart" column of the
>    architecture table above is preserved as the historical record of how the areas were originally
>    scoped — it is not a parity contract.
> 3. **The `.vue` compatibility surface is unaffected and is a shipping product feature.**
>    [V01.01.06.09] (#250) targets the Vue single-file-component container specification because
>    compatibility with a documented external format *is* the requirement — the same category as Viu
>    Utilities' Tailwind CSS v4.3.3 target. It is specified as `SPECIFICATION.md` §9.
>
> Continued evaluation of Vue's (and other frameworks') **performance** work is explicitly retained,
> through [`docs/PERFORMANCE-RESEARCH.md`](PERFORMANCE-RESEARCH.md) — a non-normative ledger in which
> nothing becomes binding on Viu until it lands in the specification or an ADR.
>
> This section is preserved, not rewritten: it records what was decided and when.

These are deliberate, recorded divergences from upstream — everything else tracks Vue 3 semantics
(vuejs/core v3.5.x) as the reference implementation:

1. **Compiler-informed VDOM is the defining idea to keep.** The compiler and runtime share the
   PatchFlags/ShapeFlags/SlotFlags bitmask vocabulary; the runtime patches only what flags say can
   change, and the block tree flattens dynamic nodes. On WASM this matters *more* than in JS: every
   DOM mutation crosses the JS-interop boundary, so every skipped patch visit is a marshaling
   round-trip avoided.
2. **No JS `Proxy` → Ref-first reactivity + source generators.** `Ref<T>`/`Computed<T>` (plain
   getter/setter with track/trigger — Vue 3.5's own ref internals port directly) are the primary
   primitives. `reactive(obj)` becomes `[Reactive]` partial classes whose property wrappers are
   emitted by a Roslyn source generator; reactive collections are dedicated `ReactiveList<T>`-style
   types, not proxied BCL types. The dependency engine ports Vue 3.5's version-counter +
   doubly-linked-list design.
3. **No runtime template compilation.** Vue's full build compiles templates with `new Function` —
   impossible in WASM. Templates and SFCs compile at build time via Roslyn source generators; that
   is the only path, and it is also how the tooling story (diagnostics, IDE integration) gets
   Razor-grade. The canonical `.viu` container is the **hybrid** form decided 2026-08-02
   (`V01.01.06.10`, #257): tag-based `<template>`/`<style>` blocks exactly as in Vue, with the
   component's C# kept in an `@script { }` block and custom blocks staying @-syntax. That decision
   partially reverses the 2026-07-17 `V01.01.06.01` decision, which made `@template`/`@script`/
   `@style` @-block syntax canonical for every block — the earlier decision happened and is
   superseded, not erased: the legacy `@template`/`@style` containers still parse during a
   migration window with a Warning-severity diagnostic (the decision record and rules live in
   `libraries/Assimalign.Viu.Syntax.SingleFileComponent/docs/FORMAT.md`). `V01.01.06.09` adds an
   explicitly scoped tag-based `.vue` compatibility input. Template markup remains standard Vue
   template syntax in both containers.
4. **The interop boundary is the performance budget.** Patch operations batch into a command buffer
   applied by one JS call per flush; events use one delegated JS listener forwarding into .NET;
   static content is stringified aggressively into `innerHTML` inserts.
5. **Composition-only component model.** No Options API, no mixins, no
   `app.config.globalProperties` — typed provide/inject and composition functions instead. Recorded
   as a founding ADR.
6. **Trimming/AOT-safe everywhere.** No reflection-based serialization, no dynamic codegen, no
   linker-unfriendly activation. Every area publishes a representative WASM consumer with trimming
   validation; size and startup budgets gate CI from W03.
7. **Cohesion integration at MVP.** Viu will integrate with the Cohesion platform
   (`assimalign/cohesion`) as MVP approaches — apps served by Cohesion Web, SSR hosted in-process
   (tracked as `V01.01.12.08`, #104, now narrowed to the hosting integration — the packaging half
   landed as decision 8). Consequence now: hosting and server-rendering seams stay host-agnostic —
   [V01.01.07.04] ships a server adaptor contract that any web framework implements as a thin
   downstream adapter (Cohesion Web first; ASP.NET Core only if ever wanted), and no
   Assimalign.Viu.* library may reference a web framework (decision reaffirmed and made binding
   2026-07-17).
8. **SDK-first packaging on Cohesion's shared-framework model (landed 2026-07-19,
   `V01.01.12.19`/#174).** Viu ships as an MSBuild project SDK — a consumer csproj is just
   `<Project Sdk="Assimalign.Viu.Sdk">` — chaining `Microsoft.NET.Sdk.WebAssembly`, with the
   framework delivered as the `Assimalign.Viu.App` shared framework: a `KnownFrameworkReference`
   registration resolving to the `Assimalign.Viu.App.Ref` targeting pack (compile references +
   `data/FrameworkList.xml`) and per-RID `Assimalign.Viu.App.Runtime.<rid>` runtime packs
   (`browser-wasm` today) — the `Microsoft.AspNetCore.App.Ref`/`.Runtime.<rid>` shape, mirrored
   from `assimalign/cohesion`'s `sdks/` + `frameworks/`. **Codegen placement decision:** the source
   generators stay Roslyn incremental generators (moving them into MSBuild tasks would forfeit IDE
   integration and incrementality) but are *delivered* through the Ref pack's `analyzers/dotnet/cs`
   with `<File Type="Analyzer">` manifest entries, so SDK consumers get `[Reactive]` and `.viu`
   compilation with zero wiring; the `ViuBundleCss` MSBuild task and the `.viu`
   AdditionalFiles/CSS-bundling MSBuild logic ship inside the SDK package (`Tasks/` + `Targets/`,
   packed from their in-repo sources so they cannot drift). The local loop is
   `scripts/Install-Local.ps1` → `_out/packages` (see `sdks/README.md`). In-repo projects keep
   dogfooding via `ViuProjectReference` — the SDK is the *external consumer* surface. This
   re-scopes `V01.01.12.03` (#92: library packages + feed publishing on top of the pack loop) and
   delivers `V01.01.12.12.02` (#168: the task now ships in the SDK rather than a standalone
   package).

## Delivery model

Work is tracked exactly like the sibling Cohesion repo:

- **WBS-coded items** — `[V01.01.NN]` area epics → `[V01.01.NN.MM]` features → `[V01.01.NN.MM.PP]`
  tasks, held together by native GitHub sub-issue links, all on Project #15. Program root:
  `[V01.01.00] Viu - Framework Libraries`.
- **Waves (W01–W06)** phase delivery; **Priority (P001–P007)** orders work within and across waves
  (lower = first). Tasks are created iteratively as features start — the feature list below is the
  planned scope; tasks are intentionally not pre-generated.
- **Scope creep is captured, not absorbed**: the `viu-work-items` skill
  (`.claude/skills/viu-work-items/`) files discovered work as its own item with
  `Origin=DiscoveredTask|DiscoveredFeature` and the `scope-creep` label, so one PR closes everything
  it actually resolved and creep stays measurable.
- Project #15 carries viu work only (`V`-prefixed WBS codes, repo `assimalign/viu`).

### Wave narrative

| Wave | Theme | Exit demo |
| --- | --- | --- |
| **W01** | Rendering foundation — shared contracts, reactivity core (deps/Ref/effect/computed/scope), VNode v2 + renderer + scheduler + render effects, hardened DOM bridge + patchProp + events, in-memory test renderer, solution restructure + CI | The stopwatch re-renders reactively (no polling) through the new pipeline, tested DOM-free |
| **W02** | Component model — instance/setup, props, emits, slots, provide/inject, lifecycle, app API, directives, refs, dynamic components, watch, `[Reactive]` source-gen, reactive collections, LIS keyed diff, browser bootstrap, test utils | TodoMVC built from components with `h()` render functions |
| **W03** | Compiler — template parser → transforms → C# codegen source generator, patch flags + block tree end-to-end, static hoisting, diagnostics, `.viu` SFC format + MSBuild, interop command buffer, v-model/v-show, size budgets | TodoMVC rewritten as `.viu` components; interop calls measurably collapse |
| **W04** | Ecosystem — router, store, built-ins (Teleport/KeepAlive/Transition/async), scoped CSS + CSS modules, HackerNews sample, getting-started guide | HackerNews client: routed, stored, styled |
| **W05** | Server + DX — SSR renderer + SSR codegen + hydration + the host-agnostic server adaptor, packaging/NuGet, `dotnet new` templates, dev loop, e2e harness, benchmarks, devtools protocol, SFC hot-reload metadata | Server-rendered, hydrated sample; `dotnet new viu-app` works from NuGet |
| **W06** | Enterprise polish — Suspense, devtools UI + reactivity timeline, store plugins, custom elements, prerendering (SSG), API reference, docs site, `.viu` editor support | Docs site built by Viu itself |

## The planned backlog

### [V01.01.01] Framework - Shared (W01, P001)

| Code | Feature | Wave | Priority |
| --- | --- | --- | --- |
| `V01.01.01.01` | Implement the PatchFlags, ShapeFlags, and SlotFlags bitmask model | W01 | P001 |
| `V01.01.01.02` | Implement class, style, and display-string normalization helpers | W01 | P002 |
| `V01.01.01.03` | Implement HTML, SVG, and MathML tag and attribute knowledge tables | W01 | P002 |

### [V01.01.02] Framework - Reactivity (W01, P001)

> Merged into `Assimalign.Viu.Core` by the .NET reshape (R2, [V01.01.12.21], `docs/NET-RESHAPE-PLAN.md`).
> The epic and its feature history stay; the shipping code now lives in the consolidated core library.

| Code | Feature | Wave | Priority |
| --- | --- | --- | --- |
| `V01.01.02.01` | Implement the dependency tracking engine | W01 | P001 |
| `V01.01.02.02` | Implement Ref primitives (Ref<T>, ShallowRef<T>, CustomRef<T>) | W01 | P001 |
| `V01.01.02.03` | Implement ReactiveEffect with scheduler injection | W01 | P001 |
| `V01.01.02.04` | Implement Computed<T> with lazy versioned caching | W01 | P001 |
| `V01.01.02.05` | Implement EffectScope and hierarchical disposal | W01 | P001 |
| `V01.01.02.06` | Implement Watch and WatchEffect semantics | W02 | P002 |
| `V01.01.02.07` | Implement source-generated reactive objects ([Reactive] partial classes) | W02 | P002 |
| `V01.01.02.08` | Implement reactive collection types (ReactiveList, ReactiveDictionary, ReactiveSet) | W02 | P002 |
| `V01.01.02.09` | Implement reactivity escape hatches and introspection | W02 | P003 |

### [V01.01.03] Framework - RuntimeCore (W01, P001)

> Renamed to `Assimalign.Viu.Core` (root namespace `Assimalign.Viu`), with the Reactivity area merged in,
> by the .NET reshape (R2, [V01.01.12.21], `docs/NET-RESHAPE-PLAN.md`). The epic and its feature history stay.

| Code | Feature | Wave | Priority |
| --- | --- | --- | --- |
| `V01.01.03.01` | Redesign the VNode model with shape flags and dynamic-children support | W01 | P001 |
| `V01.01.03.02` | Implement the renderer factory with injected platform node-ops | W01 | P001 |
| `V01.01.03.03` | Implement keyed children diffing with LIS minimal moves | W02 | P002 |
| `V01.01.03.04` | Implement the scheduler with batched flush phases and NextTick | W01 | P001 |
| `V01.01.03.05` | Integrate render effects for reactive re-rendering | W01 | P001 |
| `V01.01.03.06` | Implement the component instance and Setup model | W02 | P001 |
| `V01.01.03.07` | Implement props declaration, validation, and attrs fallthrough | W02 | P001 |
| `V01.01.03.08` | Implement emits and the component event contract | W02 | P001 |
| `V01.01.03.09` | Implement slots with stability flags | W02 | P002 |
| `V01.01.03.10` | Implement Provide and Inject | W02 | P002 |
| `V01.01.03.11` | Implement lifecycle hooks | W02 | P001 |
| `V01.01.03.12` | Implement the App API with plugins and global error handling | W02 | P002 |
| `V01.01.03.13` | Implement the runtime directive system | W02 | P002 |
| `V01.01.03.14` | Implement template refs | W02 | P003 |
| `V01.01.03.15` | Implement block-tree fast paths in the patch engine | W03 | P002 |
| `V01.01.03.16` | Implement async components | W04 | P004 |
| `V01.01.03.17` | Implement Teleport | W04 | P004 |
| `V01.01.03.18` | Implement KeepAlive | W04 | P004 |
| `V01.01.03.19` | Implement the BaseTransition state machine | W04 | P004 |
| `V01.01.03.20` | Implement Suspense | W06 | P006 |
| `V01.01.03.21` | Implement dynamic component resolution | W02 | P002 |

### [V01.01.04] Framework - RuntimeDom (W01, P001)

> Renamed to `Assimalign.Viu.Browser` by the .NET reshape (R3, [V01.01.12.22], `docs/NET-RESHAPE-PLAN.md`);
> the consequence bridge `Assimalign.Viu.Router.RuntimeDom` became `Assimalign.Viu.Router.Browser`. The epic
> and its feature history stay; the shipping code now lives in the browser library.

| Code | Feature | Wave | Priority |
| --- | --- | --- | --- |
| `V01.01.04.01` | Harden the DOM interop bridge and node-handle lifecycle | W01 | P001 |
| `V01.01.04.02` | Implement the patchProp engine | W01 | P001 |
| `V01.01.04.03` | Implement the event system with invoker pattern and modifiers | W01 | P002 |
| `V01.01.04.04` | Implement browser app bootstrap and mounting | W02 | P001 |
| `V01.01.04.05` | Implement interop command-buffer batching | W03 | P002 |
| `V01.01.04.06` | Implement the v-model and v-show runtime directives | W03 | P002 |
| `V01.01.04.07` | Implement DOM Transition and TransitionGroup | W04 | P004 |
| `V01.01.04.08` | Implement custom element support | W06 | P007 |

### [V01.01.05] Framework - Compiler (W03, P002)

| Code | Feature | Wave | Priority |
| --- | --- | --- | --- |
| `V01.01.05.01` | Implement the template tokenizer and parser producing a located AST | W03 | P002 |
| `V01.01.05.02` | Implement the transform pipeline with structural directives | W03 | P002 |
| `V01.01.05.03` | Implement directive transforms for bind, on, model, slot, show, html, and text | W03 | P002 |
| `V01.01.05.04` | Implement C# expression binding and scope analysis in templates | W03 | P002 |
| `V01.01.05.05` | Implement render-function codegen as a Roslyn source generator | W03 | P002 |
| `V01.01.05.06` | Implement patch-flag inference and block emission | W03 | P002 |
| `V01.01.05.07` | Implement static hoisting and stringification | W03 | P003 |
| `V01.01.05.08` | Implement compiler diagnostics with template source mapping | W03 | P003 |

### [V01.01.06] Framework - SingleFileComponent (W03, P003)

| Code | Feature | Wave | Priority |
| --- | --- | --- | --- |
| `V01.01.06.01` | Define the .viu SFC file format (@-block syntax) and block parser | W03 | P003 |
| `V01.01.06.02` | Integrate SFC compilation into MSBuild and the source generator | W03 | P003 |
| `V01.01.06.03` | Implement script-block integration with partial classes | W03 | P003 |
| `V01.01.06.04` | Implement scoped CSS compilation | W04 | P004 |
| `V01.01.06.05` | Emit hot-reload metadata for per-block updates | W05 | P005 |
| `V01.01.06.06` | Implement CSS Modules and v-bind() in CSS | W04 | P004 |
| `V01.01.06.09` | Add tag-based .vue single-file-component compatibility | W05 | P005 |
| `V01.01.06.10` | Adopt the hybrid tag-based .viu container: template and style tags with the @script block | W05 | P002 |
| `V01.01.06.11` | Extract the shared single-file-component projection with bidirectional source maps | W05 | P004 |

### [V01.01.07] Framework - ServerRenderer (W05, P004)

| Code | Feature | Wave | Priority |
| --- | --- | --- | --- |
| `V01.01.07.01` | Implement the SSR string renderer and helper library | W05 | P004 |
| `V01.01.07.02` | Implement SSR compiler transforms for string-concatenation codegen | W05 | P005 |
| `V01.01.07.03` | Implement the hydration walker | W05 | P004 |
| `V01.01.07.04` | Implement the host-agnostic server adaptor for SSR hosting | W05 | P005 |
| `V01.01.07.05` | Implement static prerendering (SSG) | W06 | P006 |

### [V01.01.08] Framework - Router (W04, P003)

| Code | Feature | Wave | Priority |
| --- | --- | --- | --- |
| `V01.01.08.01` | Implement the route table and matcher | W04 | P003 |
| `V01.01.08.02` | Implement history integration | W04 | P003 |
| `V01.01.08.03` | Implement RouterView and RouterLink components | W04 | P003 |
| `V01.01.08.04` | Implement navigation guards and async navigation flows | W04 | P004 |
| `V01.01.08.05` | Implement lazy route components and scroll behavior | W05 | P005 |

### [V01.01.09] Framework - Store (W04, P003)

| Code | Feature | Wave | Priority |
| --- | --- | --- | --- |
| `V01.01.09.01` | Implement the store definition API on EffectScope | W04 | P003 |
| `V01.01.09.02` | Implement state, getters, and actions with reactivity integration | W04 | P003 |
| `V01.01.09.03` | Implement SSR state serialization and client hydration | W05 | P005 |
| `V01.01.09.04` | Implement the store plugin system and persistence | W06 | P006 |

### [V01.01.10] Framework - DevTools (W05, P005)

| Code | Feature | Wave | Priority |
| --- | --- | --- | --- |
| `V01.01.10.01` | Implement the runtime inspection protocol | W05 | P005 |
| `V01.01.10.02` | Implement reactivity timeline and dependency inspection events | W06 | P006 |
| `V01.01.10.03` | Build the devtools inspection UI | W06 | P006 |

### [V01.01.11] Framework - Testing (W01, P001)

| Code | Feature | Wave | Priority |
| --- | --- | --- | --- |
| `V01.01.11.01` | Implement the in-memory test renderer | W01 | P001 |
| `V01.01.11.02` | Implement component test utilities | W02 | P002 |
| `V01.01.11.03` | Build the end-to-end browser test harness | W05 | P005 |
| `V01.01.11.04` | Build the performance benchmark suite | W05 | P004 |

### [V01.01.12] Framework - Tooling (W01, P001)

| Code | Feature | Wave | Priority |
| --- | --- | --- | --- |
| `V01.01.12.01` | Restructure the solution to the Assimalign.Viu library layout | W01 | P001 |
| `V01.01.12.02` | Set up CI workflows with per-area path filtering | W01 | P002 |
| `V01.01.12.03` | Implement NuGet packaging and the release pipeline | W05 | P004 |
| `V01.01.12.04` | Create dotnet new project templates | W05 | P005 |
| `V01.01.12.05` | Build the dev-loop experience | W05 | P005 |
| `V01.01.12.06` | Establish WASM size and AOT budget gates | W03 | P003 |
| `V01.01.12.07` | Build .viu editor support | W06 | P007 |
| `V01.01.12.07.03` | Recolor .viu classification with vibrant built-in categories and distinct component tags | W05 | P005 |
| `V01.01.12.07.04` | Offer declared @script members in completion through Roslyn syntax parsing | W05 | P005 |
| `V01.01.12.07.05` | Implement the tier-2 language-server surface: resolve, symbols, folding, honest hover format | W05 | P005 |
| `V01.01.12.08` | Integrate Viu with the Cohesion platform (hosting; packaging landed via `.19`) | W05 | P004 |
| `V01.01.12.09` | Modularize the library folder structure, whole-word naming | W03 | P002 |
| `V01.01.12.10` | Scope the build-time utility-first CSS engine | W04 | P005 |
| `V01.01.12.11` | CSS construction/emission surface in Assimalign.Viu.Syntax.Css | W04 | P004 |
| `V01.01.12.12` | ViuBundleCss MSBuild task for CSS bundling | W04 | P004 |
| `V01.01.12.13` | Utility-class candidate grammar and variant model | W05 | P005 |
| `V01.01.12.14` | CSS-first utility theme and design-token model | W05 | P005 |
| `V01.01.12.15` | Plain-text utility source detection and extraction | W05 | P005 |
| `V01.01.12.16` | Utility-to-CSS resolver and incremental pipeline | W05 | P005 |
| `V01.01.12.17` | CSS-first utility directives and style composition | W06 | P006 |
| `V01.01.12.18` | Rename product naming from Vue/Vuecs to Viu repo-wide | W04 | P002 |
| `V01.01.12.19` | Adopt Cohesion SDK/shared-framework packaging (Assimalign.Viu.Sdk + Assimalign.Viu.App) | W05 | P003 |
| `V01.01.12.23` | Provide semantic C# IntelliSense in the @script block through a Roslyn workspace in the language server | W06 | P004 |

### [V01.01.13] Framework - Documentation (W02, P003)

| Code | Feature | Wave | Priority |
| --- | --- | --- | --- |
| `V01.01.13.01` | Establish the repo documentation skeleton | W02 | P003 |
| `V01.01.13.02` | Build the sample application gallery | W03 | P004 |
| `V01.01.13.03` | Write the getting-started guide | W04 | P004 |
| `V01.01.13.04` | Generate the API reference from XML docs | W06 | P006 |
| `V01.01.13.05` | Build the documentation site | W06 | P006 |
| `V01.01.13.06` | Build the HackerNews-style sample application | W04 | P004 |

## Operating references

- Work-item intake: [.claude/skills/viu-work-items/SKILL.md](../.claude/skills/viu-work-items/SKILL.md)
- Project schema + manual recipes: [.claude/skills/viu-work-items/reference/project-schema.md](../.claude/skills/viu-work-items/reference/project-schema.md)
- Working conventions: [.claude/rules/workflow.md](../.claude/rules/workflow.md)
- Upstream reference: [vuejs/core](https://github.com/vuejs/core) (v3.5.x), [vuejs.org](https://vuejs.org)


