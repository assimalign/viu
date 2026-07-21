# Assimalign.Viu.Reactivity — overview

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
