# Assimalign.Viu.Core — design

Why the platform-agnostic runtime is shaped the way it is. What it is: see [OVERVIEW.md](OVERVIEW.md).
Upstream counterpart: [`@vue/runtime-core`](https://github.com/vuejs/core/tree/main/packages/runtime-core).

## The renderer is platform-agnostic by construction

`RendererFactory.CreateRenderer(options)` (Vue's `createRenderer(RendererOptions)`) builds the
mount/patch/unmount pipeline over an injected `RendererOptions<TNode>` — the platform node-ops
(create/insert/remove/text, `patchProp`, static-content insert). **The core never performs interop
itself.** `TNode` is the platform node type: `int` handles for the browser
(`Assimalign.Viu.Browser`), `TestNode` for the in-memory renderer (`Assimalign.Viu.Testing`).
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

A component's render is a `RenderEffect<TNode>` — a `ReactiveEffect`
whose scheduler enqueues the component's update job on the `Scheduler`.
The `Scheduler` batches jobs into flush phases with `NextTick`; the internal `RuntimeWatchScheduler`
bridges Reactivity's `IWatchScheduler` seam to the same queue so `ViuWatch` flushes with rendering.
This is how the stopwatch re-renders reactively instead of polling.

## Composition-only component model

Per [ADR-0004](../../../docs/adr/0004-composition-only-component-model.md), the component model is
composition-only: a `ComponentInstance` runs a setup function; props/emits/slots/lifecycle are
typed; cross-cutting values flow through typed provide/inject (`InjectionKey<T>`) and plugins
(`IPlugin`). `Application<TNode>` and `ApplicationConfiguration` deliberately omit an
`app.config.globalProperties` bag.

## App-level dependency injection over `System.IServiceProvider` ([V01.01.03.24])

App-level singleton wiring — a data client, a router, a store registry — resolves through
`System.IServiceProvider`, **not** the component-tree provide/inject chain. The two are deliberately
separate: provide/inject with `InjectionKey<T>` is the Vue-semantic feature for passing a value down a
subtree (an ancestor provides, a descendant injects, nearer shadows farther); the service provider is
the .NET-idiomatic home for one-per-application singletons. Keeping them distinct means each stays
simple and neither has to grow the other's semantics.

The bridge is our own `IServiceProviderBuilder` (registration intake + `IServiceProvider Build()`), so
users bring any container without Core taking a `Microsoft.Extensions.DependencyInjection` dependency.
The **default** provider is a factory-delegate registry (`ServiceProviderBuilder` →
`FactoryServiceProvider`): every service is created by a `Func<IServiceProvider, object>`, so there is
**no reflection activation, no constructor discovery, no assembly scanning** — the only path that is
trimming- and WASM/NativeAOT-safe. The application is the single root scope, so `Singleton` and
`Scoped` both cache once per app (isolated across apps) and `Transient` runs its factory per
resolution; disposing the app disposes its owned singleton/scoped disposables. Deliberately **not**
supported in the default provider (documented; bring an MS.Ext.DI adapter for them): child
`IServiceScope`s, open generics, decorators, `IEnumerable<T>` multi-registration, keyed services, and
transient-disposal tracking. The built provider is attached to `ApplicationContext.Services` by the
`ApplicationBuilder` before plugins install and inherited by every `ComponentInstance`
(`ComponentInstance.Services`), so `Setup` resolves through `DependencyInjection.GetService<T>()`.
Router/Store gain additive `AddRouter`/`AddStore` builder extensions that register into services while
keeping their existing provide-based paths (resolution is service-first-then-provide), so no existing
behavior changes. An MS.Ext.DI adapter package is possible future work, out of scope here.

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

## Client hydration adopts the SSR output ([V01.01.07.03])

Hydration lives here, not in `Assimalign.Viu.ServerRenderer` — the walker is the C# port of
`@vue/runtime-core`'s `createHydrationFunctions` (`packages/runtime-core/src/hydration.ts`), and like
upstream it is bound to the renderer internals, so it ships as a `partial` of `Renderer<TNode>`
(`Renderer.Hydration.cs`) that closes over the same `_options`, `Patch`, `MountComponent`, and `Unmount`
the mount path uses. `Application<TNode>.Hydrate` (surfaced as each platform's `CreateSSRApp(...).Mount`)
enters it instead of `Render`.

- **Adopt, don't create.** The walk matches the client vnode tree against the existing server nodes:
  elements are matched by tag and have only their dynamic bindings reconciled (event listeners are
  *always* attached), text is adopted with its content asserted, fragments are consumed between their
  `[`/`]` comment anchors, components hydrate their subtree over the existing nodes, and teleports resolve
  their target and adopt the content there. A clean hydration performs **zero** node-creating or
  structural operations.
- **Reads go through an injected reader; writes through the node-ops.** The walk never touches a platform
  node except through a `HydrationNodeReader<TNode>` (kind, firstChild, nextSibling, parentNode, tag,
  data, attribute) and the existing `RendererOptions<TNode>` write ops. That keeps it platform-agnostic —
  tests read the in-memory tree directly, the browser reads a single batched snapshot — and is why the
  contract added exactly one node-op (`CreateHydrationReader`), not a chatty per-read surface.
- **The SSR markers are a shared convention, not shared code.** `HydrationMarkers` duplicates the marker
  content (`[`, `]`, `teleport start`/`end`/`anchor`) the server renderer's `SsrMarkers` emits, pinned to
  the same upstream reference. Hydration must **not** take a code dependency on `Assimalign.Viu.ServerRenderer`
  (issue #66 boundary); only the emitted byte sequences couple the two ends (pinned by the SSR→hydrate
  round-trip tests in the ServerRenderer suite).
- **PatchFlag fast paths carry over.** A `CACHED` (hoisted / `v-once`) element is adopted verbatim without
  inspecting its children or props. Otherwise the walk actively patches only the exact set v3.5 does
  (`ShouldHydrateProperty`): event listeners (always attached), `.`-prop bindings, forced `input`/`option`
  values, and every non-reserved prop on a custom element. Server-rendered attributes — static **and**
  compiler-dynamic — are adopted, not re-patched: hydration trusts the SSR output for them, so it spends no
  interop per attribute, and the next reactive update reconciles a dynamic attribute through the normal diff.
- **Mismatch never crashes; it recovers per subtree.** A node-type/structure mismatch logs a recoverable
  warning (naming the offending node's path, suppressible per node via `data-allow-mismatch`) and falls
  back to a client render of just that subtree via `Patch(null, …)` — the rest of the tree is still
  adopted, the tree converges, and every listener ends up attached exactly once. A **text** mismatch warns
  and corrects the content in place; a **class/style/attribute** mismatch is dev-warned but **left as the
  server rendered it** (v3.5 `propHasMismatch` warns without patching), suppressible via `data-allow-mismatch`
  (where a `children` allowance also covers `text`, "text being a subset of children"). Because the walk
  reads structure from a possibly-immutable snapshot (the browser reader is a batched pre-walk), the mismatch
  and fragment-teardown paths read the whole affected range **before** removing anything — never re-reading a
  sibling they just mutated.
- **The component bridge mirrors upstream's `componentUpdateFn`.** Before mounting a hydrated component the
  walker stamps the server node onto its vnode's `El` and arms `_componentHydrationReader`; the first
  render effect then adopts the subtree (`HydrateNode(el, subTree)`) instead of `Patch(null, …)`. After
  hydration the `.El` back-pointers are exactly what a mount would set, so reactive re-renders patch the
  adopted nodes with **no remount**.

Deliberate divergences: **Static** vnodes adopt a single node run (the multi-node `staticCount` the SSR
compiler records arrives with [V01.01.07.02]); **lazy hydration strategies** (`hydrateOnIdle`/`Visible`/
`MediaQuery`/`Interaction`) are a browser-API-bound follow-up (see Non-goals) — the async-component
adoption seam is in place, but the concrete triggers are not.

## Deltas from Vue 3

- **DOM directives live one layer up.** `v-show` and `v-model` and the DOM transitions are *not*
  members of `RenderHelpers`; they ship as `DomRenderHelpers` in `Assimalign.Viu.Browser`, so
  runtime-core stays DOM-free and a real DOM directive can never mis-bind onto a runtime-core marker
  (see [`Assimalign.Viu.Browser/docs/OVERVIEW.md`](../../Assimalign.Viu.Browser/docs/OVERVIEW.md)).
- **`RenderHelpers` uses upstream-aliased member names** in generated code — a documented,
  generated-code-only naming deviation.
- **Hot-path dispatch** favors sealed types and abstract bases over interfaces where the JIT's
  devirtualization matters on mono-wasm — a C#/WASM performance shape with no JS analogue.
- Not thread-safe (single-threaded JS event-loop model).

## Non-goals (sequenced work)

- `Suspense` — [V01.01.03.20] (W06).
- DOM `Transition`/`TransitionGroup` and custom elements — `Assimalign.Viu.Browser`.
- Server-side rendering (the string/stream renderer) — `Assimalign.Viu.ServerRenderer`. Client
  **hydration** of that output lives here (above, [V01.01.07.03]); only the SSR renderer itself is out.
- **Lazy hydration strategies** (`hydrateOnIdle`/`hydrateOnVisible`/`hydrateOnMediaQuery`/
  `hydrateOnInteraction`) — a browser-API-bound follow-up ([V01.01.07.03.01]): they need
  `requestIdleCallback`/`IntersectionObserver`/`matchMedia`/interaction listeners in
  `Assimalign.Viu.Browser` and cannot be exercised by the DOM-free suite. The walker already routes
  async-component subtrees through the scheduler's post-flush queue, which is where the triggers will hook.

---

# Reactive core (merged reactivity area)

The reactivity engine, merged into this library ([V01.01.12.21]). What it is: see [OVERVIEW.md](OVERVIEW.md).

Why the reactive core is shaped the way it is. What it is: see [OVERVIEW.md](OVERVIEW.md). Upstream
counterpart: [`@vue/reactivity`](https://github.com/vuejs/core/tree/main/packages/reactivity) (v3.5).

## Ref-first, no `Proxy`

The founding divergence is [ADR-0002](../../../docs/adr/0002-ref-first-reactivity.md): Vue's public
lead is the deep `Proxy` (`reactive(obj)`), which is not AOT/trimming-safe in WASM. Viu leads with
references instead. `Reference<T>` and `Computed<T>` are plain getter/setter types that track on read
and trigger on write — Vue 3.5's own ref internals port directly. `reactive(obj)` becomes a
`[Reactive]` partial class whose property wrappers a source generator emits, and reactive collections
are dedicated types rather than proxied BCL collections.

## The reactive value class model ([V01.01.03.25], reshape arc 2 R6)

The ref family is an **abstract class hierarchy**, not a set of interfaces. `public abstract class
ReactiveValue` owns a single `Dependency` cell inline (a `private protected` field surfaced through a
public, never-tracking `Dependency` property) plus an abstract `object? BoxedValue` (reading tracks)
and a virtual `bool IsReadOnly => false`; `public abstract class ReactiveValue<T> : ReactiveValue`
adds the typed `T Value { get; set; }` and seals `BoxedValue => Value`. `Reference<T>`,
`ShallowReference<T>`, `CustomReference<T>` (and the internal `AccessorReference<T>` behind `ToRef`)
are `sealed` leaves over `ReactiveValue<T>`.

A class, not an interface, for three reasons that all trace to the repo's *dispatch-on-hot-paths*
rule: the `is`-a-ref / `unref` introspection collapses to an O(1), reflection-free `is ReactiveValue`
type test; the shared dependency cell is a **field on the base** (a direct load, no interface-property
stub); and a `private protected` constructor closes the reactive-value set to external subclassing.
This subsumes the arc-1 `IReference`/`IReference<T>`/`IDependencyReference`/`IReadonlyReactive`
interfaces, which are deleted — `Reactive.TriggerReference(ReactiveValue)` force-notifies through the
inherited `Dependency` with no silent no-op branch, and `IsRef`/`Unref`/`IsReadonly` pattern-match the
class.

**`Computed<T>` is composition, not inheritance** (ratified arc-2 decision 1). A computed is *both* a
`Dependency` to its readers and a `Subscriber` to its sources; since it must now be a `ReactiveValue<T>`
it can no longer *be* a `Subscriber`, so it **owns** an internal `sealed ComputedSubscriber : Subscriber`
(a nested type, reaching the owner's private getter/value/version state without widening it). The
readers-facing `Dependency` (inherited from `ReactiveValue`) points its `Computed` back-reference at
the `ComputedSubscriber` so the computed still does not self-track. The `refreshComputed` hot path is
carried over verbatim onto the nested subscriber; the composition is gated by `ReactivityBenchmarks`
(`ComputedChainRecompute`/`DependencyTrackTrigger` stay within noise of the pre-change baseline, zero
new allocation).

**Read-only writes warn, never throw** (Vue 3.5 parity, `packages/reactivity/src/computed.ts`):
writing a getter-only `Computed<T>` or a setter-less projected ref routes through the runtime warning
sink (`RuntimeWarnings`) and leaves the value unchanged. This aligns `Computed<T>` from its earlier
`NotSupportedException` to the upstream warn-and-no-op behavior.

**Recorded exception — `IReactiveObject` stays an interface.** Unlike the ref family, the source-generated
`[Reactive]` contract is an interface, because `[Reactive]` partials attach to *user* classes that
already have their own base types — a base class is structurally impossible there. The readonly flag
that the deleted `IReadonlyReactive` carried folds into `IReactiveObject` as a default-interface member
(`bool IsReadOnly => false`) that a readonly variant overrides to `true`. This is the reactive-area
analogue of the general-rules **namespace-vs-assembly exception** recorded for R2: a deliberate,
scoped deviation from the otherwise class-based ref surface.

## The dependency engine

The engine ports Vue 3.5's **version-counter + doubly-linked-list** design: a `Dependency` holds
subscribers, a `Subscriber` holds its dependencies, and the `SubscriberLink` nodes between them are
reused across runs so a stable dependency set allocates nothing on re-track. Batching
(`StartBatch`/`EndBatch`) coalesces triggers so multiple writes produce at most one run per effect;
`PauseTracking`/`ResetTracking` gate collection.

`Subscriber` is a **`public abstract` class with `internal` members and a `private protected`
constructor** — opaque and un-subclassable externally, but a real base so the engine's hot path
(per-trigger notification) dispatches through a vtable virtual call rather than interface dispatch,
which is measurably costlier on mono-wasm / NativeAOT (the repo's "dispatch on hot paths" rule).
Concrete leaves (`ReactiveEffect`, `Computed<T>`) are `sealed` so the JIT can devirtualize.

## Public engine surface (read-only)

The dependency graph is part of the public API — but only for *reading*. `SubscriberLink` (the port
of Vue's `Link`), `Subscriber.FirstDependency`, and the public, never-tracking `ReactiveValue.Dependency`
property let a .NET developer inspect what depends on what: walk a subscriber's
`FirstDependency` → `NextDependency` chain, reach the `Dependency` behind a ref via its
`Dependency` property, and read each edge's observed `Version`. This mirrors how .NET developers expect
to introspect a framework's object graph, and Vue itself keeps the same structures in `dep.ts`. The
`Dependency` property has no public setter and the cell's own mutating members stay `internal`, so a
reactive value's dependency wiring cannot be swapped or corrupted through the public surface.

Every state-mutating member — link construction, list splicing, version bookkeeping, the flags word —
stays `internal`, so external code can observe the graph but cannot desynchronize the engine. Two
consequences of the hot-path rule shape *how* the surface is exposed: `SubscriberLink` is a `sealed`
class whose observable fields become `{ get; internal set; }` auto-properties (the JIT inlines them to
direct field access), while `Subscriber` — the `private protected` vtable base — keeps its list
head/tail and flags as **internal fields** and surfaces only the head through a separate read-only
`FirstDependency` property, so the per-trigger hot path never pays property-getter dispatch on the
base. The public accessors are for cold inspection only.

## The compiler contract

Because there is no runtime `Proxy` to auto-unwrap refs, the *compiler* decides every access form,
and `Ref<T>.Value` being a **settable property** is what makes it work: `count.Value` serves both
reads and writes, so `count++` and `count += 1` rewrite cleanly. This library owns the `Value`
semantics; the template compiler's expression-binding table
([`Assimalign.Viu.Syntax.Templates/docs/DESIGN.md`](../../Assimalign.Viu.Syntax.Templates/docs/DESIGN.md))
is pinned to it — a change to `Value` requires a matching change there.

## Deltas from Vue 3

- **`.Value` in read and write positions** replaces Vue's read-time `unref` plus `isRef`-guarded
  assignment (forced by, and enabled by, C# properties).
- **`effect()` stops the effect if its first run throws** before rethrowing (upstream `effect()`
  parity), so a failed effect leaves no live subscriptions.
- **The abstract-base hot-path model** (over interfaces) is a deliberate C#/WASM performance shape
  with no upstream analogue — JavaScript has no equivalent dispatch cost.
- **Reactive collections are concrete types**, not proxied BCL types; behavior tracks the mutation
  triggers Vue's collection handlers fire.

## Non-goals

- No deep implicit reactivity without `[Reactive]` or a reactive collection — reactivity is opted
  into explicitly.
- Deeper reactivity escape hatches beyond the current surface remain sequenced work. The public
  dependency-graph surface ([V01.01.02.10]) is deliberately **read-only** — writable graph
  manipulation from outside the engine is a non-goal.
