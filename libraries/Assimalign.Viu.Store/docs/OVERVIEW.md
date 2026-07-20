# Assimalign.Viu.Store — overview

State management for Viu — the role [`pinia`](https://pinia.vuejs.org) plays for Vue 3
(https://github.com/vuejs/pinia). This first feature delivers the **store definition API**: the
setup-syntax `DefineStore`/`UseStore` pair, the per-app store registry (Pinia's `createPinia()`
root), and the `EffectScope`-owned lifetime that every later Store feature builds on. It is the C#
port of `defineStore()`, `createPinia()`, and `store.$dispose()` (`packages/pinia/src/store.ts`,
`createPinia.ts`, `rootStore.ts`).

State, getters, and actions as first-class members (`[V01.01.09.02]`), SSR serialization and
hydration (`[V01.01.09.03]`), and the store plugin/persistence system (`[V01.01.09.04]`) are later
features of the Store area (#75) and are not part of this package yet. Here, a store is whatever
object the setup delegate returns — it is expected to hold `Ref<T>` state, `Computed<T>` getters, and
methods, and this feature guarantees that object's identity, single construction, and scope-bound
lifetime.

## What it contains

Public surface (all under namespace `Assimalign.Viu.Store`):

- **`Stores`** (static facade): the module-level entry point. `DefineStore(id, setup)` is the C# port
  of `defineStore()`; `CreateRegistry()` is the port of `createPinia()`; `ActiveRegistry` /
  `SetActiveRegistry(...)` are the port of `activePinia` / `setActivePinia()`.
- **`StoreDefinition<TStore>`**: the C# port of the `useStore` function `defineStore` returns. Carries
  the `Id`, resolves the instance with `UseStore(registry)` (DI-friendly) or `UseStore()` (ambient,
  from a component's app context or the active registry), and disposes a single store with
  `Dispose(registry)` (the port of `store.$dispose()`).
- **`StoreRegistry`**: the per-app store root (the C# port of the `Pinia` instance). Owns a detached
  root `EffectScope` and the id → instance map; `AsPlugin<TNode>()` bridges it onto an app through
  `App.Use(...)`; `Dispose()` stops the whole subsystem; `Count` and `IsDisposed` round out the
  surface. Directly constructible (`new StoreRegistry()`) so a DI container can own it.
- **`StoreSetup<TStore>`** (`Delegates/`): the parameterless setup delegate that constructs the store
  inside its effect scope — the C# port of the setup function passed to `defineStore`.
- **`DuplicateStoreIdException`**: the deterministic failure when two different definitions claim the
  same id in one registry (carries the offending `StoreId`), instead of silently aliasing state.

Internal (`Internal/`, exercised through `InternalsVisibleTo` tests): `StorePlugin<TNode>` (the
`IPlugin<TNode>` adapter that provides the registry app-wide and sets it active on install) and
`StoreEntry` (a registry's per-store record: instance, owning scope, and owning definition).

## Using it

```csharp
using Assimalign.Viu.Reactivity;
using Assimalign.Viu.Store;

// A setup-style store: state is a Ref, the getter is a Computed, the action is a method.
sealed class CounterStore
{
    public Reference<int> Count { get; } = Reactive.Reference(0);
    public Computed<int> Doubled { get; }
    public CounterStore() => Doubled = Reactive.Computed(() => Count.Value * 2);
    public void Increment() => Count.Value++;
}

// Define once (share the returned definition; declare it static readonly).
static readonly StoreDefinition<CounterStore> UseCounter =
    Stores.DefineStore("counter", () => new CounterStore());

// Per app: create a registry and install it.
var pinia = Stores.CreateRegistry();
app.Use(pinia.AsPlugin<TNode>());          // provides the registry app-wide

// Inside a component Setup — resolves the app's registry, no argument needed:
var counter = UseCounter.UseStore();
// From plain C#/DI — pass the registry handle explicitly:
var same = UseCounter.UseStore(pinia);     // ReferenceEquals(counter, same) == true

pinia.Dispose();                            // stops every store's scope
```

## Boundaries

- References **only** `Assimalign.Viu.Reactivity` (for `EffectScope`/`Ref`/`Computed`) and
  `Assimalign.Viu.RuntimeCore` (for `App`, `IPlugin`, provide/inject, and the current component
  instance). It references **no DOM/JavaScript-interop assembly** — stores are platform-agnostic and
  run in a plain .NET test host.
- Trimming- and NativeAOT-safe: store construction goes through the typed setup delegate captured at
  `DefineStore` time — never `Activator.CreateInstance` or attribute scanning — so there is no
  reflection-based activation and no dynamic code generation.
- Not thread-safe (single-threaded JS event-loop model). The registry is per app instance, so
  server-side (multi-request) hosting stays isolated without global mutable state; the ambient
  `ActiveRegistry` is a client/testing convenience — pass the registry explicitly on the server.
- Design rationale, the Pinia counterpart mapping, and the deliberate C#/WASM divergences:
  [DESIGN.md](DESIGN.md).
