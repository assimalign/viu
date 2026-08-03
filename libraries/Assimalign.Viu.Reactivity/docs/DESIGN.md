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

## The ratified public surface

`Reactive` is the single static facade; it is the discoverable entry point for everything the
package offers. The surface below is **ratified and stable** — it is the normative list in
`[RCT-5]`, and a member is added to it, never quietly renamed.

| Capability | Viu surface |
| --- | --- |
| A tracked reference cell | `Reactive.Reference<T>(value)`, `Reference<T>`, and `IReactiveReference<T>` |
| A cell that notifies only on assignment, not on mutation of what it holds | `Reactive.ShallowReference<T>(value)`, `ShallowReference<T>`, and `IReactiveReference<T>` |
| A cell with caller-supplied track/trigger control | `Reactive.CustomReference<T>(factory)`, `CustomReference<T>`, and `IReactiveReference<T>` |
| Lazily evaluated, version-cached derived state | `Reactive.Computed<T>(getter, setter)`, `Computed<T>`, and `IReactiveReference<T>` |
| A tracked side effect | `Reactive.Effect(...)` and `ReactiveEffect` |
| A lifetime boundary owning effects | `Reactive.EffectScope(...)`, `Reactive.CurrentScope`, `Reactive.OnScopeDispose(...)`, and `EffectScope` |
| Change observation with an explicit callback | `Reactive.Watch(...)`, `Reactive.WatchEffect(...)`, `WatchOptions`, `WatchHandle`, and scheduler/delegate contracts |
| Forced notification regardless of value equality | `Reactive.TriggerReference(...)` |
| Reference inspection and unwrapping | `Reactive.IsRef(...)` / `Reactive.Unref(...)` |
| Projected references | `Reactive.ToRef(...)` and generated `ToReferences()` members |
| Reactive-object inspection | `Reactive.IsReactive(...)` / `Reactive.IsReadonly(...)` |
| Escape hatches out of reactivity | `Reactive.ToRaw(...)` / `Reactive.MarkRaw(...)` |
| Tracking and batching controls | `PauseTracking`, `ResetTracking`, `StartBatch`, and `EndBatch` |

**`IsRef`, `Unref`, and `ToRef` are ratified short forms** and a recorded exception to the
repository's whole-word naming rule. The whole-word spellings collide with the surface they operate
on: `Reactive.ToReference(...)` is indistinguishable at a call site from the `Reference<T>` factory
`Reactive.Reference(...)`, and `Unreference` reads as *releasing* a reference rather than reading
through one. These three names are frozen; they are not an oversight to be corrected.

### There is no proxy

Reactive *objects* are not produced by intercepting member access at runtime. Trimming and
NativeAOT forbid the dynamic interception a proxy-based design needs, so an object opts in through
`[Reactive]` / `[ShallowReactive]` and a source generator emits its per-property reactive wrappers,
while reactive collections are dedicated types — `ReactiveList<T>`,
`ReactiveDictionary<TKey, TValue>`, and `ReactiveSet<T>` — that implement the BCL collection
interfaces rather than wrapping BCL types (`[RCT-6]`).

The consequence is deliberate: **there is no implicit deep reactivity** (`[RCT-7]`). An author opts
in per class or per collection. This is more predictable and less magical, and it is what makes the
model trimming-safe. There is likewise nothing to ask "is this a proxy?" about — callers use
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

`EffectScope` and `Reactive.EffectScope(...)` are the developer-facing scope APIs.
Abstraction-facing consumers use `IReactiveEffectScopeFactory`, normally through
`ReactiveEffectScopeFactory`, when they should not depend on the concrete scope type.
