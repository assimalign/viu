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

## Teleport is a special vnode type, not a component

`Teleport` ([V01.01.03.17], upstream `components/Teleport.ts`) mirrors upstream by being a distinct
`VirtualNodeType.Teleport` (carrying `ShapeFlags.Teleport`) that the renderer branches on in
*patch / move / unmount* — **not** an `IComponentDefinition` like `BaseTransition`. It cannot be an
ordinary component: it frames its own tree position with a main-tree anchor pair (reusing the vnode's
`El`/`Anchor`) while mounting its children into a *different* container, and it moves those children
between containers when `disabled`/`to` change — behavior with no place in the component render model.
Target-side state (the resolved target and its anchor pair, plus the deferred-mount job) hangs off a
single internal `TeleportState` reference so a non-Teleport vnode pays only one null field.

Target lookup is the one new platform seam: the `to` prop is either a direct platform-node target or a
selector resolved through `RendererOptions<TNode>.QuerySelector` — the browser adapter's DOM
`querySelector`, the test adapter's registered-root search. The renderer never touches a node except
through node-ops, so no DOM/JS access leaks in. `defer` resolves the target in a post-flush job
(so a Teleport can target an element rendered later in the same tree); toggling `disabled` and changing
`to` relocate the existing nodes with `insert` (no unmount), preserving subtree state.

Deliberate divergences from upstream `Teleport.ts`, each documented at its call site:

- **No SVG/MathML target-namespace sniff.** Upstream inspects the resolved target's `namespaceURI` to
  switch the children's namespace; Viu's node-ops expose no element-namespace query, so the ambient
  compiled namespace is threaded through instead.
- **Target anchors are created only when the target resolves.** Upstream's `prepareAnchor` always
  creates the `targetStart`/`targetAnchor` pair (for its hydration path) and inserts them only when a
  target exists; Viu creates none for an unresolved target, so a missing/disabled Teleport never leaks
  two never-inserted platform-node handles. The `TeleportEndKey` sibling-skip property is likewise
  hydration-only and omitted.
- **`default(TNode)` is honored as the "no node" sentinel** when resolving a string target: a value-type
  handle renderer (the browser's `0`) returning it means "not found", exactly as a null reference-type
  handle does — so a missing selector never teleports into node `0`.

## KeepAlive is a component with renderer-internal reach

`KeepAlive` ([V01.01.03.18], upstream `components/KeepAlive.ts`) is — unlike Teleport — a real
`IComponentDefinition` (`KeepAlive.Instance`, the singleton `RenderHelpers._KeepAlive` resolves to). It
wraps one dynamic child and, when the view switches, has the renderer **move the outgoing child's subtree
into a hidden storage container instead of unmounting it** (`deactivate`) and move it home on return
(`activate`), so the child's `Setup` runs once and all its state survives. The two paths are driven by
`ShapeFlags.ComponentShouldKeepAlive` / `ComponentKeptAlive` (bit-for-bit with `@vue/shared`): the
renderer's `processComponent` short-circuits a kept-alive vnode into `ActivateComponent` instead of a
fresh mount, and its `unmount` short-circuits a should-keep-alive vnode into `DeactivateComponent`
(move-to-storage) instead of teardown.

Upstream injects the renderer internals onto `instance.ctx.renderer` in `mountComponent`; Viu mirrors the
split without leaking `TNode` into the component. The renderer owns activate/deactivate and, at mount,
attaches an internal `KeepAliveContext` (a node-op-created storage container plus a real-unmount delegate)
to the KeepAlive instance **before** `Setup` runs. `KeepAlive` itself owns the cache and the render/prune
logic: because `KeepAlive.Instance` is a shared singleton, every per-mount value (the cache, the
least-recently-used key order, the current/pending keys) is **closure** state created fresh per `Setup`,
never an instance field. The cache key is the child's vnode key when present, else the component
definition reference — a typed key, never a reflected type name (AOT/trimming). `Max` caps the cache with
LRU eviction (a `LinkedList<object>` orders keys oldest-first); `Include`/`Exclude` match the child's
declared `IComponentDefinition.Name`, and a `flush: 'post'` watch prunes newly excluded entries when the
props change.

`Activated`/`Deactivated` hooks mirror upstream's `registerKeepAliveHook`: a KeepAlive activates only its
*direct* child, so a nested descendant's hook is prepended onto every ancestor KeepAlive-root instance's
hook list, and one `InvokeHooks` on that root fires the whole subtree child-before-parent. A
deactivation-branch check (upstream's `__wdc`) skips a hook whose owning branch is already deactivated so
nested KeepAlives do not double-fire.

Deliberate divergences from upstream `KeepAlive.ts`, each documented at its call site:

- **No Suspense unwrapping.** Upstream's `getInnerChild` unwraps a Suspense child; Viu treats the child as
  its own inner child until Suspense lands ([V01.01.03.20]).
- **No mount-invalidation of a pending activated/mounted hook.** Upstream 3.5 calls `invalidateMount` when
  deactivating so a queued-but-unrun mounted/activated hook is cancelled; Viu's synchronous per-render
  flush drains a newly-mounted subtree's post-flush hooks before any later render can deactivate it, so
  `mounted` still fires exactly once and the guard is unnecessary for discrete render cycles.
- **Aggregated hooks fire with the KeepAlive-root as the ambient instance.** Upstream's `invokeArrayFns`
  sets no current instance; Viu fires the aggregated list through the root's `InvokeHooks`, so a
  descendant hook that reads `ComponentInstance.Current` sees the root — a minor divergence pinned by test.

## Async components define the runtime contract, not lazy download

`AsyncComponents.DefineAsyncComponent` ([V01.01.03.16], upstream `apiAsyncComponent.ts`,
https://vuejs.org/guide/components/async.html) returns an internal `AsyncComponentWrapper`
(`IComponentDefinition`): a loader (`Func<Task<IComponentDefinition>>`) resolves the real component
asynchronously; a loading component shows after `Delay`, an error component on failure or `Timeout`,
and the resolved component renders in place. Unlike `KeepAlive` (one shared singleton whose per-mount
state must be closure-local), each `DefineAsyncComponent` call yields a *fresh* wrapper, so the
**load state** — the in-flight request, the cached resolved definition, the retry count — lives as
wrapper **instance fields** shared across every mount of that async component (upstream's
`pendingRequest`/`resolvedComp` module-closure). The **per-mount UI state** (the reactive
`loaded`/`error`/`delayed` refs and the timers) is closure-local to each `Setup`, so two simultaneous
mounts each show their own loading UI while sharing one load. A resolved wrapper's later mounts take a
fast path that renders the cached inner component without re-invoking the loader; concurrent mounts
share one in-flight `Load()` (deduplicated on `pendingRequest`).

This ticket is the **runtime contract only**. The loader holds a static reference to (or awaits) the
real definition; there is no reflection or assembly-download machinery — true lazy-download of
component assemblies is a WASM lazy-loading concern layered on top later. The Router's guard pipeline
([V01.01.08.04]) left an async-component-resolution no-op seam for lazy routes; that is
[V01.01.08.05]'s concern and is deliberately **not** wired here — nothing in this design precludes it.

Resolution drives a reactive re-render, never a poll: settling flips the `loaded`/`error` refs (and
the delay/timeout timers flip `delayed`/`error`), which the render function reads, so the component's
render effect re-runs through the scheduler. Unmounting before resolution disposes the timers and
stops the render effect's scope, so a late resolution or a late timer touches nothing (the pending
render is discarded cleanly).

### Timers flow through an injected delay seam

Vue schedules the `delay` and `timeout` with `setTimeout`; the Viu `Scheduler` is a *microtask* queue
with no macrotask timer to reuse, so async components schedule through `AsyncComponentDelay` — the
injected clock/delay seam the ticket calls for. Its default runs a real `Task.Delay` whose
continuation resumes on the captured single-threaded synchronization context (the WASM main thread),
never off-context; a `FlushDispatcher`-style test seam lets a manual controller drive virtual time so
"the loading component appears only after `Delay`" and the timeout path are pinned deterministically,
with no wall-clock waits.

Task continuations follow the same rule as upstream (issue #32): they resume on the single-threaded
WASM `SynchronizationContext` — no `ConfigureAwait(false)` off-context resumption into render code.
Production always has that context; a plain xUnit host has none, so the tests install a single-threaded
`SynchronizationContext` that runs continuations inline on the test thread (mirroring WASM) — otherwise
a shared load with multiple awaiters could hop a continuation to the thread pool non-deterministically.

### KeepAlive interplay and the Suspense seam

A kept-alive async component that resolves forces its `KeepAlive` parent to re-render (upstream:
mark the parent dirty and `queueJob(parent.update)`) so the parent caches the now-resolved subtree.
Because `KeepAlive` preserves the *wrapper instance* itself (cached by the child vnode's key / the
wrapper definition reference), the wrapper's cached `resolvedComp` and `loaded` ref survive a
deactivate/activate cycle — the resolved inner component keeps its state and the loader never re-runs,
mirroring upstream and pinned by test.

`Suspensible` (default true, upstream parity) is exposed with a **boundary-registration hook
contract**: when the flag is set and an enclosing boundary is present (`ComponentInstance.SuspenseBoundary`,
inherited from the parent or the ambient `SuspenseBoundaryContext` a future Suspense sets while
mounting its subtree), the async component hands the boundary its in-flight load through
`ISuspenseBoundary.RegisterAsyncDependency` instead of rendering its own loading UI. The real
enclosing-Suspense integration completes in [V01.01.03.20] (W06); the contract exists now and is
validated against a fake boundary in tests. With no boundary present (the case until Suspense lands)
the flag has no effect and the component renders its own loading/error UI.

Deliberate divergences from upstream `apiAsyncComponent.ts`, each documented at its call site:

- **A synchronously-completing loader resolves on the first render** (no forced placeholder frame).
  Upstream's loader is always a promise, so it renders the placeholder once before the microtask
  resolves; a C# `Task` may already be complete, in which case `loaded` flips during `Setup` and the
  first render is the resolved component. Tests that need the placeholder frame use a
  `TaskCompletionSource` resolved after mount.
- **Loader-error routing gates the crash-loudly default on the error component.** Upstream's
  `handleError` always runs the capture chain and only differs on dev throwing; Viu routes the error
  through the capture chain / app handler and rethrows to the host only when there is *no* error
  component to display it (`ComponentErrorHandling.Handle(..., rethrowIfUnhandled: ErrorComponent is
  null)`), so an async component with an error component never aborts the flush.
- **The `useId` `markAsyncBoundary` id-stability hook is omitted** — it belongs to SSR id generation,
  not this client-only runtime contract.

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
