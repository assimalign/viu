# Assimalign.Viu.Core — overview

The platform-agnostic runtime — the C# port of
[`@vue/runtime-core`](https://github.com/vuejs/core/tree/main/packages/runtime-core): the virtual
DOM model, the renderer factory, the scheduler, the component model, provide/inject, the runtime
directive system, and the built-in component scaffolding. It performs no DOM or JS interop itself —
a platform package supplies the node-ops (the browser's `Assimalign.Viu.Browser`, tests'
`Assimalign.Viu.Testing`). Area: `V01.01.03`.

## Public surface

- **Virtual DOM model** (`src/` root) — `VirtualNode` (the unified vnode: element / component /
  text / comment / static / fragment / teleport via `VirtualNodeType` + a shape-flag bitmask),
  `VirtualNodeFactory` (including `Teleport`/`TeleportBlock`), `VirtualNodeProperties`.
- **Renderer** (`Rendering/`) — `RendererFactory.CreateRenderer(options)` (Vue's `createRenderer`),
  `Renderer<TNode>` (the mount/patch/unmount pipeline, including the `Teleport` built-in as a special
  vnode type in the patch/move/unmount paths — [V01.01.03.17], and the `KeepAlive` activate/deactivate
  operations its `processComponent`/`unmount` shape-flag branches short-circuit into — [V01.01.03.18]),
  `RendererOptions<TNode>` (the injected platform node-ops, whose optional `QuerySelector` resolves a
  Teleport string target), `RenderEffect<TNode>` (reactive re-render integration), and `BlockToken`.
- **Scheduler** (`Scheduling/`) — `Scheduler` (batched flush phases and `NextTick`) and
  `SchedulerJob`.
- **Component model** (`Components/`) — `ComponentInstance`, `ComponentSetupContext`,
  `ComponentProperties` / `ComponentPropertyDefinition` (props declaration and validation),
  `ComponentAttributes` (attrs fallthrough), `ComponentEmitDefinition` (emits), `ComponentSlots`,
  `Lifecycle` (the hook registration facade, including `OnActivated`/`OnDeactivated`),
  `DynamicComponents`, the `KeepAlive` caching built-in ([V01.01.03.18]), `AsyncComponents`
  (`DefineAsyncComponent`) with `AsyncComponentOptions` and the `AsyncComponentLoader` /
  `AsyncComponentErrorHandler` delegates ([V01.01.03.16]), and the transition scaffolding
  (`BaseTransition`, `BaseTransitionProperties`, `TransitionState`).
- **Application / plugins / builder** — `Application<TNode>` (Vue's `createApp` shell: one root
  mounted into one container), the `IApplication` contract, `IApplicationBuilder` +
  `ApplicationBuilder` (the `WebApplication`-style bootstrap seam), `ApplicationConfiguration`
  (error/warn handlers, performance flag), `IComponentDefinition`, `IPlugin`, and `ISuspenseBoundary`
  (the async-component / Suspense registration seam completed by [V01.01.03.20]).
- **Provide / inject** (`DependencyInjection/`) — `DependencyInjection` and the typed
  `InjectionKey<T>` (the C# port of Vue's component-tree provide/inject).
- **Dependency injection over `System.IServiceProvider`** (`DependencyInjection/`, [V01.01.03.24]) —
  bring-your-own app-level DI: the `IServiceProviderBuilder` bridge (implement over any container),
  the default AOT-safe factory-delegate `ServiceProviderBuilder` (Singleton / Scoped-per-app /
  Transient, no reflection, no `Microsoft.Extensions.DependencyInjection` dependency), the
  `ServiceLifetime`/`ServiceRegistration` descriptors, the `AddSingleton`/`AddScoped`/`AddTransient`
  and `GetService<T>`/`GetRequiredService<T>` extensions, and the `DependencyInjection.GetService<T>()`
  composition functions. The built provider hangs off `IApplication.Services` and is reachable from
  `Setup` via `ComponentInstance.Services`. This is **app-level** wiring — distinct from, and layered
  beside, the Vue-semantic provide/inject above.
- **Directives** (`Directives/`) — `IDirective`, `Directive`, `Directives`, `DirectiveBinding`,
  `DirectiveArgument`, plus the `DirectiveHook` delegate.
- **Watch** (`Watch/`) — `ViuWatch`, the runtime-scheduler-integrated `watch`/`watchEffect` over the
  reactivity primitives.
- **`RenderHelpers`** — the static helper surface (`_openBlock`, `_createElementBlock`,
  `_toDisplayString`, `_renderList`, …) that compiler-generated render methods bind to **by name**.

## Boundaries

- References **`Assimalign.Viu.Shared`** (the flag vocabulary)
  only; it never references a platform/DOM package (the reactivity engine is part of this assembly). Ships as a net10.0 runtime library with
  `IsAotCompatible=true`.
- **Composition-only** component model — no Options API, no mixins, no `app.config.globalProperties`
  (see [ADR-0004](../../../docs/adr/0004-composition-only-component-model.md)).
- Design rationale, the renderer seam, and the render-helper contract: [DESIGN.md](DESIGN.md).

---

## Reactivity — the merged reactive core (area V01.01.02)

Merged into this library from the former reactivity package ([V01.01.12.21]).

The reactive core of Viu — the C# port of
[`@vue/reactivity`](https://github.com/vuejs/core/tree/main/packages/reactivity): dependencies,
refs, computeds, effects, effect scopes, watch, reactive collections, and source-generated reactive
objects. It is Ref-first — there is no JavaScript `Proxy` (see
[ADR-0002](../../../docs/adr/0002-ref-first-reactivity.md)). Area: `V01.01.02`.

## Public surface

- **`Reactive`** (static facade) — the `@vue/reactivity` API surface: `Reference`/`ShallowReference`/
  `CustomReference`/`Computed`, `Effect`, `EffectScope`, `OnScopeDispose`, `TriggerReference`,
  `PauseTracking`/`ResetTracking`, and `StartBatch`/`EndBatch`.
- **References** — `Reference<T>` (Vue's `ref()`), `ShallowReference<T>` (`shallowRef()`),
  `CustomReference<T>` (`customRef()`), and `Computed<T>` (`computed()`, lazy versioned caching).
  All expose a settable `Value` and implement `IReference` / `IReference<T>`.
- **Effects and scopes** — `ReactiveEffect` (the effect runner with scheduler injection),
  `EffectScope` (hierarchical disposal, `effectScope()`), `Dependency` (the tracked-dependency
  primitive), and `Subscriber` (the opaque `public abstract` base for effect-like subscribers,
  exposing its dependency chain read-only via `FirstDependency`).
- **Dependency-graph inspection** — `SubscriberLink` (the read-only edge node between a `Dependency`
  and a `Subscriber`; walk it from `Subscriber.FirstDependency`) and `ITrackedReference` (reaches the
  `Dependency` behind a ref/computed). The whole graph is publicly readable but only the engine can
  mutate it.
- **Watch** — `WatchOptions`, `WatchHandle`, `WatchJob`, `WatchFlushMode`, the `WatchCallback<T>` and
  `OnCleanup` delegates, and the `IWatchScheduler` seam a host (the runtime scheduler) plugs into.
- **Source-generated reactive objects** — the `[Reactive]` / `[ShallowReactive]` attributes
  (`ReactiveAttribute` / `ShallowReactiveAttribute`); a companion source generator emits the
  reactive property wrappers for the annotated partial class (Vue's `reactive()`).
- **Reactive collections** — `ReactiveList<T>`, `ReactiveDictionary<TKey,TValue>`, `ReactiveSet<T>`
  (dedicated reactive types implementing the BCL collection interfaces).
- **Traversal and introspection** — `ReactiveTraversal`, `IReactiveTraversable`, `IReactiveObject`,
  `IReadonlyReactive`.

## Boundaries

- **No `Assimalign.Viu.*` project references** — a standalone base alongside `Assimalign.Viu.Shared`.
  Ships as a net10.0 runtime library with `IsAotCompatible=true`, and **carries the `[Reactive]`
  source generator as an analyzer** so any consumer that references this library gets `[Reactive]`
  support with no extra wiring.
- **Single-threaded** (the JS event-loop model): ambient tracking state is `static` and not
  thread-safe by design.
- Design rationale, the dependency-engine port, and the compiler contract: [DESIGN.md](DESIGN.md).
