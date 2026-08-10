# Viu Delivery Plan

Viu is a standalone C#/.NET user-interface framework running in the browser through the .NET
WebAssembly build tools (`Microsoft.NET.Sdk.WebAssembly`, `JSImport`/`JSExport` interop). This
document is the narrative companion to the executable backlog in the org GitHub Project
[**#15 "Viu"**](https://github.com/orgs/assimalign/projects/15) — the board is the authoritative,
living plan; this file records the architecture mapping, the founding design decisions, and the wave
strategy behind it.

## Where Viu stands

The proof-of-concept stage is complete. On 2026-08-09 this checkout built with **0 warnings and
0 errors**, and its 27 solution test assemblies passed **2,717 tests with 0 failures**. The tree now
contains the host-neutral Application Model and renderer, explicit reactivity, the authored
component model and closed virtual-node algebra, compiler-informed block patching, keyed
reconciliation, browser/server/testing hosts, routing and state conventions, the build-time
template and single-file-component pipeline, packaging, and editor tooling.

A developer can create a host-neutral component library with
`<Project Sdk="Assimalign.Viu.Sdk">`.
A browser application uses
`<Project Sdk="Assimalign.Viu.Sdk.Browser">`. Both paths support code-first, `.viu`, and compatible
`.vue` component authoring. Browser applications can compose services, state, registrations, and
lifetime middleware; run reactively; use the router and state libraries; and publish a trimmed static
WebAssembly site. Components can also be rendered to HTML or tested without a browser. The
component-library path needs neither Browser nor the WebAssembly workload.
The sibling [`assimalign/viu-examples`](https://github.com/assimalign/viu-examples) repository is the
packaged-consumer showcase for those paths.

The Browser SDK also has a real Debug `dotnet watch` path: stylesheet changes regenerate CSS and refresh
links without discarding browser state, while generated metadata remounts affected components for
template or C# changes. That path is not yet a separate Viu development-server command, and Visual
Studio's ordinary Hot Reload command does not invoke the SDK watch-list contract.

The remaining developer-experience gaps are concrete: there is no `Assimalign.Viu.DevTools` library
or inspection user interface, and no generated API reference or template-language reference site.
The current behavioral limits are maintained in
[`SPECIFICATION.md` §17](SPECIFICATION.md#17-non-goals-and-current-limits); the board remains the
authority for delivery status.

## Architecture: Viu library map

Package boundaries map 1:1 to .NET class libraries using the inverted layout
`{libraries,tooling}/Assimalign.Viu.<Name>/{src|test}`. Runtime projects live in `libraries/`,
compiler/editor projects live in `tooling/`, and the folder name is the assembly/package identifier
with no area wrapper folders. The following table is the exact shipping-library set under
`libraries/`; the final column lists each consumer's direct Viu project dependencies.

| Area | Shipping library | Role | Direct Viu dependencies |
| --- | --- | --- | --- |
| Reactivity (`V01.01.02`) | `Assimalign.Viu.Reactivity` | Single-threaded dependency tracking, reference cells, effects, scopes, watch, and reactive collections; the runtime leaf | None |
| Components (`V01.01.15`) | `Assimalign.Viu.Components` | The closed `VirtualNode` algebra, compiler/runtime flags, authored-component model, contracts, bindings, and activation registrations | Reactivity |
| State (`V01.01.09`) | `Assimalign.Viu.State` | State-management convention above the component model, attached through its public seams | Components, Reactivity |
| Core (`V01.01.03`) | `Assimalign.Viu.Core` | The Application Model: composition root, lifetime and middleware, renderer/scheduler engine, mounted bookkeeping, and public operations; its root namespace is `Assimalign.Viu` | Components, Reactivity, State |
| Browser (`V01.01.04`) | `Assimalign.Viu.Browser` | Browser host: batched DOM interop, bindings, events, directives, transitions, state restore, lazy-hydration triggers, and application bootstrap | Components, Core, Reactivity, State |
| Server rendering (`V01.01.07`) | `Assimalign.Viu.ServerRenderer` | WHATWG HTML serialization, direct compiled markup, hydration markers, state islands, and host-neutral request adaptation | Components, Core, State |
| Testing (`V01.01.11`) | `Assimalign.Viu.Testing` | DOM-free in-memory host and component test surface over the production renderer | Components, Core |
| Router (`V01.01.08`) | `Assimalign.Viu.Router` | Host-free navigation convention: matching, history, route components, and guard pipeline | Components, Reactivity |
| Browser router (`V01.01.08`) | `Assimalign.Viu.Browser.Router` | Leaf integration between Browser click dispatch and Router navigation | Core, Router, Browser |

Vocabulary lives low, composition lives high, and optional conventions attach through designed
seams. Reactivity is independent; Components owns descriptions and authored behavior; State and
Router are conventions; Core composes and executes applications; Browser, ServerRenderer, and
Testing adapt that model to hosts. The adopted rationale and complete dependency graph are recorded
in [`COMPONENT-MODEL-PLAN.md`](COMPONENT-MODEL-PLAN.md).

The build/editor side contains exactly ten projects under `tooling/`: the `Assimalign.Viu.Syntax`
base; its Templates, SingleFileComponent, Css, and Html language libraries; the Css and
SingleFileComponent compiler composition roots; UtilityCss; LanguageService; and LanguageServer.
The Reactivity and Syntax generators live under `analyzers/`. The single-file-component compiler is
the shared `.viu`/`.vue` to C# projection used by both build and editor hosts, preserving the
shipping `.vue` compatibility input while keeping Viu's runtime independent of JavaScript execution.

## Founding design decisions (C#/WASM divergences)

> **Historical scope note (2026-08-02).** The area map records how the July 2026 backlog was
> initially divided. Since 2026-08-02, [`SPECIFICATION.md`](SPECIFICATION.md) and repository tests
> have been the authority for Viu semantics; the eight dated decisions below remain Viu-owned
> architecture decisions. The shipping `.vue` input remains an external container-format
> compatibility target under [V01.01.06.09], and performance research remains non-normative in
> [`PERFORMANCE-RESEARCH.md`](PERFORMANCE-RESEARCH.md).

These eight dated decisions define Viu's C# and WebAssembly architecture:

1. **Compiler-informed VDOM is the defining idea to keep.** The compiler and runtime share the
   PatchFlags/ShapeFlags bitmask and SlotStability vocabulary; the runtime patches only what flags say can
   change, and the block tree flattens dynamic nodes. On WASM this matters *more* than in JS: every
   DOM mutation crosses the JS-interop boundary, so every skipped patch visit is a marshaling
   round-trip avoided.
2. **No JS `Proxy` → reference-first reactivity + source generators.** `Reference<T>` and
   `Computed<T>` getter/setter cells are the primary primitives. `[Reactive]` partial classes receive
   source-generated property wrappers; reactive collections use dedicated types instead of proxied
   BCL collections. The dependency engine uses version counters and doubly linked dependency lists.
3. **No runtime template compilation.** Templates and single-file components compile at build time
   through Roslyn source generators; that
   is the only path, and it is also how the tooling story (diagnostics, IDE integration) gets
   Razor-grade. The canonical `.viu` container is the **hybrid** form decided 2026-08-02
   (`V01.01.06.10`, #257): tag-based `<template>`/`<style>` blocks, with the
   component's C# kept in an `@script { }` block and custom blocks staying @-syntax. That decision
   partially reverses the 2026-07-17 `V01.01.06.01` decision, which made `@template`/`@script`/
   `@style` @-block syntax canonical for every block — the earlier decision happened and is
   superseded, not erased: the legacy `@template`/`@style` containers still parse during a
   migration window with a Warning-severity diagnostic (the decision record and rules live in
   `tooling/Assimalign.Viu.Syntax.SingleFileComponent/docs/FORMAT.md`). `V01.01.06.09` adds an
   explicitly scoped tag-based `.vue` compatibility input. Both containers feed the same Viu
   template compiler.
4. **The interop boundary is the performance budget.** Patch operations batch into a command buffer
   applied by one JS call per flush; events use one delegated JS listener forwarding into .NET;
   static content is stringified aggressively into `innerHTML` inserts.
5. **Composition-only component model.** No options-object authoring, mixins, or global-properties
   bag. Components use setup closures; conventions attach through services and the ambient reactive
   scope. Recorded as a founding ADR and refined by `[V01.01.15]`.
6. **Trimming/AOT-safe everywhere.** No reflection-based serialization, no dynamic codegen, and no
   linker-unfriendly activation. The publish budgets are live CI gates in
   [`budget-gates.yml`](../.github/workflows/budget-gates.yml): trimmed payload size and trim
   warnings on relevant pull requests, WebAssembly AOT publication and real-browser
   `boot-to-interactive` startup on scheduled/on-demand lanes — all enforced against the measured
   `EndToEndBrowserApp` baselines and reviewed ceilings in `scripts/budgets/PublishBudgets.json`
   (re-baselined 2026-08-09 under `[V01.01.12.26]`/#320 and `[V01.01.12.06.01]`/#182; CI never
   rewrites the manifest). Deterministic interop counts remain a per-PR gate in `benchmarks.yml`
   under [RND-IO-5].
7. **Cohesion integration at MVP.** Viu will integrate with the Cohesion platform
   (`assimalign/cohesion`) as MVP approaches — apps served by Cohesion Web, SSR hosted in-process
   (tracked as `V01.01.12.08`, #104, now narrowed to the hosting integration — the packaging half
   landed as decision 8). Consequence now: hosting and server-rendering seams stay host-agnostic —
   [V01.01.07.04] ships a server adaptor contract that any web framework implements as a thin
   downstream adapter (Cohesion Web first; ASP.NET Core only if ever wanted), and no
   Assimalign.Viu.* library may reference a web framework (decision reaffirmed and made binding
   2026-07-17).
8. **SDK-first packaging with explicit base/Browser segments (packaging foundation landed 2026-07-19 under
   `V01.01.12.19`/#174; segmented 2026-08-09 under `V01.01.12.27`/#323).** Viu ships two
   compositional MSBuild project SDKs. `<Project Sdk="Assimalign.Viu.Sdk">` chains
   `Microsoft.NET.Sdk` for host-neutral component libraries; it supplies `.viu`/`.vue` and
   reactivity generators, carries component styles through library packing, and references the
   targeting-only `Assimalign.Viu.App` framework (Reactivity, Components, State, and Core).
   `<Project Sdk="Assimalign.Viu.Sdk.Browser">` imports that base, chains
   `Microsoft.NET.Sdk.WebAssembly`, and references `Assimalign.Viu.App.Browser`, whose Browser-only
   targeting pack is composed with the base and whose
   `Assimalign.Viu.App.Browser.Runtime.browser-wasm` package owns the runtime payload. Browser assets,
   CSS bundling into `wwwroot`, hot reload, and publish budgets belong only to the Browser SDK;
   ServerRenderer remains an opt-in package. This implements API-hardening decision D12: component
   libraries need build-time component authoring without loading browser payload, which superseded
   the former second-host trigger. **Codegen placement decision:** the source generators stay Roslyn
   incremental generators (moving them into MSBuild tasks would forfeit IDE integration and
   incrementality) and are delivered through `Assimalign.Viu.App.Ref` at `analyzers/dotnet/cs` with
   `<File Type="Analyzer">` manifest entries. The local loop is `scripts/Install-Local.ps1` →
   `_out/packages` (see `sdks/README.md`). In-repo projects keep dogfooding via
   `ViuProjectReference`; the two SDKs are external-consumer surfaces. The packaging model retains
   the `V01.01.12.03` (#92) feed/release scope and delivers `V01.01.12.12.02` (#168) through the
   segmented SDK payload.

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

| Wave | Delivered position | Remaining board work |
| --- | --- | --- |
| **W01** | Rendering, reactivity, browser-host, testing, solution, and CI foundations are delivered; every planned feature row is closed | No planned feature row remains |
| **W02** | The component/application foundation, watch/reactive collections, keyed reconciliation, browser bootstrap, and test utilities are delivered; every planned feature row is closed | No planned feature row remains |
| **W03** | The primary compiler, single-file-component, block-patching, directive, and interop-batching paths are delivered; the deferred compiler optimization set has explicit implemented-or-dropped outcomes, and the size/startup budget gates are live against measured `EndToEndBrowserApp` baselines | Complete diagnostic source attribution |
| **W04** | Router, State, built-ins, CSS compilation/modules, samples, and the getting-started path are delivered at their main feature boundaries; generated trees and compiled SSR share static scoped-style attributes; hosted fingerprint selection and deterministic component-library/application CSS delivery are complete | Close built-in edge cases, generated State/source-map work, and the deferred reactive scoped-CSS runtime |
| **W05** | Host-neutral component-library and Browser SDK/framework segments, validated release packaging/staging, hydration foundations plus lazy activation, direct server compiler output, host-neutral SSR adaptation, SSR state round-tripping, explicit server-profile selection with reflection-free registration, installable `dotnet new` templates with an optional server host, editor hot-reload metadata, a working package-only `dotnet watch` CSS/component path with connected-browser conformance, the ordinary real-browser end-to-end harness, and live size/startup budget gates exist | Finish lazy routing, runtime inspection, compatibility/conformance gates, and Cohesion hosting integration |
| **W06** | Utility composition and semantic `@script` language-server work have begun | Deliver complete Suspense, custom elements, static prerendering, persistent State extensions, the DevTools timeline/user interface, remaining editor support, generated API reference, and the documentation site |

This snapshot reconciles the plan with live issue state on 2026-08-10. The budget-gate activation and
scoped-CSS follow-ups are grouped with their closest delivery themes here; Project #15 remains
authoritative for their Wave custom fields.

### [V01.01.14] API hardening — complete

`[V01.01.14]` ([epic #284](https://github.com/assimalign/viu/issues/284)) hardened the package-visible
surface after the framework reshape: public naming and visibility, application lifetime and
middleware, safe batching and reactive inspection, typed Router and Testing contracts, parser
closure, package overrides, XML documentation enforcement, and PublicAPI baselines. All eleven
child features are closed, and every row in the
[`API-HARDENING-PLAN.md`](API-HARDENING-PLAN.md) completion record is terminal.

The D6 SDK/framework platform segmentation decision was delivered after the arc by
`[V01.01.12.27]` (#323). API-hardening decision D12 superseded the second-host trigger because
component-library authoring already supplies the second real consumer topology: generator and style
extraction without Browser, WebAssembly, browser assets, or a runtime pack.

### [V01.01.15] Component model — complete

`[V01.01.15]` ([epic #313](https://github.com/assimalign/viu/issues/313)) delivered the adopted
four-lifetime model: immutable `VirtualNode` descriptions, static registration identity and
contracts, activated authored `IComponent` instances, and Core-owned mounted bookkeeping. The arc
moved the closed virtual-node algebra and authored model into Components, established frame-based
render emission and explicit AOT-safe activation, dissolved the common-primitives package into
purpose-owned homes, and closed the host, service/reactive, generated-code, and application seams.

The migration and all four child features are closed. The design rationale, type disposition,
completed P0–P6 sequence, and verification record remain in
[`COMPONENT-MODEL-PLAN.md`](COMPONENT-MODEL-PLAN.md) rather than being duplicated here.

## The planned backlog

### [V01.01.01] Framework - Shared (W01, P001)

| Code | Feature | Wave | Priority |
| --- | --- | --- | --- |
| `V01.01.01.01` | Implement the PatchFlags/ShapeFlags bitmask and SlotStability model | W01 | P001 |
| `V01.01.01.02` | Implement class, style, and display-string normalization helpers | W01 | P002 |
| `V01.01.01.03` | Implement HTML, SVG, and MathML tag and attribute knowledge tables | W01 | P002 |

### [V01.01.02] Framework - Reactivity (W01, P001)

> The .NET reshape briefly consolidated this area, but that decision was superseded on 2026-08-02.
> `Assimalign.Viu.Reactivity` is a separate shipping leaf library; the dated reconciliation note in
> [`NET-RESHAPE-PLAN.md`](NET-RESHAPE-PLAN.md) preserves the intervening history.

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

### [V01.01.03] Framework - Core (W01, P001)

> The area now ships as `Assimalign.Viu.Core`, rooted at namespace `Assimalign.Viu`. Reactivity and
> Components are separate lower libraries; Core owns the Application Model, engine, and public
> operations. The epic and its feature history stay under their original WBS codes.

| Code | Feature | Wave | Priority |
| --- | --- | --- | --- |
| `V01.01.03.01` | Redesign the VNode model with shape flags and dynamic-children support | W01 | P001 |
| `V01.01.03.02` | Implement the renderer factory with injected platform node-ops | W01 | P001 |
| `V01.01.03.03` | Implement keyed children diffing with LIS minimal moves | W02 | P002 |
| `V01.01.03.04` | Implement the scheduler with batched flush phases and NextTickAsync | W01 | P001 |
| `V01.01.03.05` | Integrate render effects for reactive re-rendering | W01 | P001 |
| `V01.01.03.06` | Implement the component instance and Setup model | W02 | P001 |
| `V01.01.03.07` | Implement props declaration, validation, and attrs fallthrough | W02 | P001 |
| `V01.01.03.08` | Implement emits and the component event contract | W02 | P001 |
| `V01.01.03.09` | Implement slots with stability flags | W02 | P002 |
| `V01.01.03.10` | ~~Implement Provide and Inject~~ — superseded by the explicit dependency seams in `[CMP-24]` and `[CMP-33]` | W02 | P002 |
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

### [V01.01.04] Framework - Browser (W01, P001)

> Renamed to `Assimalign.Viu.Browser` by the .NET reshape (R3, [V01.01.12.22], `docs/NET-RESHAPE-PLAN.md`);
> the navigation bridge is `Assimalign.Viu.Browser.Router` after [V01.01.14.09]. The epic and its
> feature history stay; the shipping code lives in the Browser host.

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
| `V01.01.07.03.01` | Implement lazy hydration strategies for idle, visibility, media, and interaction triggers | W05 | P005 |
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

### [V01.01.09] Framework - State (W04, P003)

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
| `V01.01.12.27` | Segment the SDK and shared framework into base and Browser packages | W05 | P003 |

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
- Performance research policy: [PERFORMANCE-RESEARCH.md](PERFORMANCE-RESEARCH.md)


