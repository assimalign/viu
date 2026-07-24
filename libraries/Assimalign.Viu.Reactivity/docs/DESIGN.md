# Assimalign.Viu.Reactivity design

**Status:** Implemented independent engine and public API boundary.

## Historical baseline

The requested commit `80bb967` finalized documentation after the consolidation. Its relevant
ancestry is:

1. `470142e` — last standalone Reactivity package snapshot.
2. `0fe2d9c` — moves Reactivity into RuntimeCore.
3. `fcc3d84` — renames RuntimeCore to Core and moves the namespace to `Assimalign.Viu`.
4. `80bb967` — cascades the consolidation through documentation and rules.

The redesign uses `470142e` to recover package responsibilities and uses the current Core code as
the implementation baseline. This avoids losing fixes and the later class-based hot-path model.

## Vue-shaped public API

The `Reactive` facade remains the discoverable C# counterpart of `@vue/reactivity`.

| Vue 3.5 concept | Viu surface to preserve |
| --- | --- |
| `ref()` | `Reactive.Reference<T>(value)`, `Reference<T>`, and `IReactiveReference<T>` |
| `shallowRef()` | `Reactive.ShallowReference<T>(value)`, `ShallowReference<T>`, and `IReactiveReference<T>` |
| `customRef()` | `Reactive.CustomReference<T>(factory)`, `CustomReference<T>`, and `IReactiveReference<T>` |
| `computed()` | `Reactive.Computed<T>(getter, setter)`, `Computed<T>`, and `IReactiveReference<T>` |
| `effect()` | `Reactive.Effect(...)` and `ReactiveEffect` |
| `effectScope()` / `getCurrentScope()` / `onScopeDispose()` | `Reactive.EffectScope(...)`, `Reactive.CurrentScope`, `Reactive.OnScopeDispose(...)`, and `EffectScope` |
| `watch()` / `watchEffect()` | `Reactive.Watch(...)`, `Reactive.WatchEffect(...)`, `WatchOptions`, `WatchHandle`, and scheduler/delegate contracts |
| `triggerRef()` | `Reactive.TriggerReference(...)` |
| `isRef()` / `unref()` | `Reactive.IsRef(...)` / `Reactive.Unref(...)` |
| `toRef()` / `toRefs()` | `Reactive.ToRef(...)` and generated `ToReferences()` members |
| `isReactive()` / `isReadonly()` | matching `Reactive` inspection methods |
| `toRaw()` / `markRaw()` | matching `Reactive` escape hatches |
| tracking and batching controls | `PauseTracking`, `ResetTracking`, `StartBatch`, and `EndBatch` |

C# cannot reproduce JavaScript `Proxy` safely under trimming and AOT. Vue's `reactive()` family
therefore remains represented by `[Reactive]` and `[ShallowReactive]` source generation plus
`ReactiveList<T>`, `ReactiveDictionary<TKey, TValue>`, and `ReactiveSet<T>`. This is a platform
adaptation of the API, not an attempt to emulate `Proxy` through reflection.

`isProxy()` has no literal C# counterpart because the design creates no proxy object. Callers use
`IsReactive` and `IsReadonly` to inspect generated reactive objects and reactive collections.

## Type model

The restored design deliberately uses both public interfaces and an engine base class:

- `IReactiveReference` and `IReactiveReference<T>` are the public, substitutable contracts.
- `ReactiveValue` and `ReactiveValue<T>` remain the first-party implementation backbone.
- `Reference<T>`, `ShallowReference<T>`, `CustomReference<T>`, projected references, and
  `Computed<T>` derive from the base and implement the corresponding interfaces.

This hybrid preserves the useful parts of both historical designs. Public APIs such as `IsRef`,
`Unref`, watchers, and component-facing state can accept `IReactiveReference` without knowing a
concrete class. The dependency engine still stores the dependency cell on `ReactiveValue` and uses
abstract-class dispatch between first-party subscribers and values. Interface dispatch remains on
cold API boundaries rather than per-trigger hot paths.

`ReactiveValue` therefore remains valuable, but it is no longer the only public abstraction.
Removing it would force shared engine state behind interface properties or duplicate it across each
reference implementation.

The interface restores extensibility, but it cannot enforce correct tracking. An external
`IReactiveReference<T>` implementation is responsible for tracking reads and triggering changed
writes. `Reactive.CustomReference(...)` remains the preferred extension point. Operations that
need direct dependency access, such as forced triggering and graph inspection, additionally require
`IReactiveTrackedReference`; a reference-only implementation does not gain a dependency cell
implicitly.

## Interface naming

Every public interface owned by the Reactivity package starts with `IReactive`. Restored historical
names map as follows:

| Historical name | Proposed name |
| --- | --- |
| `IReference` | `IReactiveReference` |
| `IReference<T>` | `IReactiveReference<T>` |
| `ITrackedReference` | `IReactiveTrackedReference` |
| `IReadonlyReactive` | `IReactiveReadOnly` |
| `IReactiveObject` | `IReactiveObject` |
| `IReactiveTraversable` | `IReactiveTraversable` |
| `IWatchScheduler` | `IReactiveWatchScheduler` |

`IReactiveEffectScope` and `IReactiveEffectScopeFactory` make their effect-lifetime role explicit.
`EffectScope` implements the former, while `ReactiveEffectScopeFactory` is the first-party adapter
for consumers such as State.
`ReadOnly` uses the repository's whole-word .NET spelling rather than retaining
`IReadonlyReactive`.

## Engine and generator boundary

The package owns the versioned dependency graph, linked subscriber edges, batching, effect scopes,
watch traversal, and reactive collections. It remains single-threaded for the browser event-loop
model.

Reactive-object generation ships through the
`Assimalign.Viu.Generators.Reactivity` analyzer assembly. The analyzer recognizes attributes and
emits runtime contracts in `Assimalign.Viu.Reactivity`; diagnostic identifiers remain stable.
Generated `ToReferences()` members expose `IReactiveReference<T>`.

Before promotion into `libraries/` or publication, the analyzer must be atomically renamed back to
`Assimalign.Viu.Reactivity.Generators` together with every solution, framework-manifest, package,
test, and shipping reference. Keeping both analyzer identities active is not a compatibility
strategy: both would emit the same partial members. The Core-named identity is therefore a staging
compatibility seam, not a separately deferrable package rename.

## Namespace migration

The original package used `Assimalign.Viu.Reactivity`; the consolidated implementation uses
`Assimalign.Viu`. Restoring the former follows the repository's namespace-equals-assembly rule but
is source-breaking. The recommended migration is:

1. Move the implementation to `Assimalign.Viu.Reactivity`.
2. Keep a temporary Core compatibility facade for static entry points where practical.
3. Publish analyzer diagnostics or code fixes for namespace migration.
4. Remove the compatibility facade on a declared major-version boundary.

Concrete type forwarding cannot preserve a type whose namespace changes, so this must be planned as
an API migration rather than hidden behind assembly forwarding.

## Runtime integration

Standalone watches run synchronously unless a caller supplies an
`IReactiveWatchScheduler`. Core owns the renderer scheduler adapter, component error routing, and
the runtime-bound `ViuWatch` facade; none of those concerns introduce a Reactivity dependency on
Core or Components.

`EffectScope` and `Reactive.EffectScope(...)` remain the Vue-shaped developer APIs.
Abstraction-facing consumers use `IReactiveEffectScopeFactory`, normally through
`ReactiveEffectScopeFactory`, when they should not depend on the concrete scope type.
