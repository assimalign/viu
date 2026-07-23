# Assimalign.Viu.State design

## Pinia-shaped surface

The State package preserves the behavior previously implemented by `Assimalign.Viu.Store` while
renaming the public surface consistently:

| Previous Store name | State name |
| --- | --- |
| `Stores` | `StateStores` |
| `StoreDefinition<TStore>` | `StateStoreDefinition<TStore>` |
| `StoreRegistry` | `StateStoreRegistry` |
| `Store<TState>` | `StateStore<TState>` |
| `StoreMutation` | `StateStoreMutation` |
| `StorePatchKind` | `StateStorePatchKind` |
| `StoreSubscription` | `StateStoreSubscription` |
| `StoreActionContext` | `StateStoreActionContext` |
| `DuplicateStoreIdException` | `DuplicateStateStoreKeyException` |

Definitions use `Key` rather than `Id`. A shared definition is reusable metadata; mutable state is
always registry-owned.

## Registry lifetime topology

The registry creates one detached root `IReactiveEffectScope` during construction. Resolving a
definition creates a non-detached scope while the root is current, making it a child of that root:

```text
StateStoreRegistry
  -> detached root reactive scope
       -> state store A child scope
       -> state store B child scope
```

The caller's ambient component scope is never the store scope's parent. Removing one definition
stops only its child scope. Disposing the registry stops the root, which cascades through all child
scopes, clears the instance map, and clears `StateStores.ActiveRegistry` when it points to that
registry.

Setup failure stops the newly created child scope and does not add a partial registry entry.
Resolving the same definition again is a cache hit by reference identity. Resolving a different
definition under an owned key raises `DuplicateStateStoreKeyException`.

## Component and service composition

`IStateContext` exposes:

- the store's child `IReactiveEffectScope`;
- the independently selected `IComponentFactory`;
- the independently selected `IServiceProvider`;
- the optional `IReactiveWatchScheduler`; and
- an optional component owner for explicitly scoped feature registries.

The ordinary application-global `definition.Use(componentContext)` path uses
`IStateStoreContext` only to locate the registry and deliberately records no owner. Otherwise the
first component to resolve a global store would become its owner, making setup behavior depend on
mount order. A caller that creates an isolated feature registry can pass an owner explicitly.

## State member model

`StateStore<TState>` is optional. `TState` implements `IReactiveObject`, normally through the
Reactivity source generator. The live `State` object is never replaced.

- `Patch(Action<TState>)` batches a typed group of writes and reports `PatchFunction`.
- `Patch(TState)` invokes a typed author-supplied state applier and reports `PatchObject`.
- `Reset()` creates a fresh factory state and applies it to the live object in place.
- A store constructed without a factory/applier supports mutator patches but rejects object patch
  and reset with `NotSupportedException`.
- `Subscribe` observes reactive state changes.
- `OnAction` observes methods that opt in through `RunAction` or `RunActionAsync`.

The applier is a typed delegate because State cannot enumerate a state shape with reflection under
Viu's trimming and NativeAOT constraints.

## Scheduler behavior

Reactivity owns `IReactiveWatchScheduler`; State consumes it without depending on Core.

When a scheduler is supplied, the store's single deep state watcher uses pre-flush delivery.
Several direct writes before the application flush deduplicate into one callback. Grouped patches
also produce one callback, and multiple queued mutations carry the latest patch kind, matching the
previous Store behavior.

State wraps the scheduler only to observe whether a watch job was actually scheduled. That signal
prevents a no-op patch from leaking its patch kind onto a later direct write without requiring
State to construct renderer jobs or depend on Core.

When no scheduler is supplied, Reactivity's documented synchronous fallback is used. Each direct
write notifies immediately; `Patch` remains a single notification because it wraps all writes in
`Reactive.StartBatch()` / `Reactive.EndBatch()`.

## Subscription and action lifetime

The shared state watcher is created lazily inside the state store's own child scope, not the scope
of the first subscriber. A subscription created in an active caller scope removes itself when that
scope stops unless `detached: true` is selected. Removing one callback never tears down the shared
watcher needed by other callbacks.

State and action callback lists are snapshotted before iteration so callbacks may add or remove
subscriptions without corrupting the current pass.

There is no proxy interception in .NET. Actions are observable only when their implementation
uses a protected action helper. Async helpers await the task before running `After`, so it receives
the resolved value; faults run `OnError` hooks and then propagate.

## AOT and host boundaries

- Store construction invokes `StateStoreSetup<TStore>` directly.
- Component activation remains the responsibility of the supplied `IComponentFactory`.
- Services are resolved only through the supplied `IServiceProvider`.
- State copying uses an explicit typed delegate.
- There is no `Activator.CreateInstance`, runtime constructor inspection, reflection-based state
  traversal, dynamic code generation, DOM dependency, or browser interop dependency.
