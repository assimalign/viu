# Assimalign.Viu.RuntimeCore — design

Why the platform-agnostic runtime is shaped the way it is. What it is: see [OVERVIEW.md](OVERVIEW.md).
Upstream counterpart: [`@vue/runtime-core`](https://github.com/vuejs/core/tree/main/packages/runtime-core).

## The renderer is platform-agnostic by construction

`RendererFactory.CreateRenderer(options)` (Vue's `createRenderer(RendererOptions)`) builds the
mount/patch/unmount pipeline over an injected `RendererOptions<TNode>` — the platform node-ops
(create/insert/remove/text, `patchProp`, static-content insert). **The core never performs interop
itself.** `TNode` is the platform node type: `int` handles for the browser
(`Assimalign.Viu.RuntimeDom`), `TestNode` for the in-memory renderer (`Assimalign.Viu.Testing`).
This single seam is what lets the browser and the DOM-free test host share one renderer, and it is
the exact shape upstream's `createRenderer` takes.

## Compiler-informed patching, decoupled from the compiler

The renderer reads the `PatchFlags`/`ShapeFlags` a vnode carries (from `Assimalign.Viu.Shared`) and
patches only what the flags mark dynamic; the block tree (`BlockToken`) flattens dynamic descendants
so the static structure is skipped. On WASM this is the interop budget in action (see
[ADR-0003](../../../docs/adr/0003-batched-interop-dom-operations.md)).

Compiler-generated render methods bind to `RenderHelpers` **by name** (`_openBlock`,
`_createElementBlock`, …) through a `using static`, so this library and the template compiler share a
contract without a project reference in either direction. The helper members deliberately carry the
upstream-aliased names (a generated-code-only exception to the whole-word naming rule — the names
*are* the contract). The counterpart contract lives in
[`Assimalign.Viu.Syntax.Templates/docs/DESIGN.md`](../../Assimalign.Viu.Syntax.Templates/docs/DESIGN.md);
build-time compilation is [ADR-0005](../../../docs/adr/0005-no-runtime-template-compilation.md).

## Scheduler and reactive re-render

A component's render is a `RenderEffect<TNode>` — a `ReactiveEffect` (from
`Assimalign.Viu.Reactivity`) whose scheduler enqueues the component's update job on the `Scheduler`.
The `Scheduler` batches jobs into flush phases with `NextTick`; the internal `RuntimeWatchScheduler`
bridges Reactivity's `IWatchScheduler` seam to the same queue so `ViuWatch` flushes with rendering.
This is how the stopwatch re-renders reactively instead of polling.

## Composition-only component model

Per [ADR-0004](../../../docs/adr/0004-composition-only-component-model.md), the component model is
composition-only: a `ComponentInstance` runs a setup function; props/emits/slots/lifecycle are
typed; cross-cutting values flow through typed provide/inject (`InjectionKey<T>`) and plugins
(`IPlugin<TNode>`). `Application<TNode>` and `ApplicationConfiguration` deliberately omit an
`app.config.globalProperties` bag.

## Deltas from Vue 3

- **DOM directives live one layer up.** `v-show` and `v-model` and the DOM transitions are *not*
  members of `RenderHelpers`; they ship as `DomRenderHelpers` in `Assimalign.Viu.RuntimeDom`, so
  runtime-core stays DOM-free and a real DOM directive can never mis-bind onto a runtime-core marker
  (see [`Assimalign.Viu.RuntimeDom/docs/OVERVIEW.md`](../../Assimalign.Viu.RuntimeDom/docs/OVERVIEW.md)).
- **`RenderHelpers` uses upstream-aliased member names** in generated code — a documented,
  generated-code-only naming deviation.
- **Hot-path dispatch** favors sealed types and abstract bases over interfaces where the JIT's
  devirtualization matters on mono-wasm — a C#/WASM performance shape with no JS analogue.
- Not thread-safe (single-threaded JS event-loop model).

## Non-goals (sequenced work)

- `Suspense` — [V01.01.03.20] (W06).
- DOM `Transition`/`TransitionGroup` and custom elements — `Assimalign.Viu.RuntimeDom`.
- Server-side rendering and hydration — `Assimalign.Viu.ServerRenderer` (a later area).
