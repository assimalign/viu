# Assimalign.Viu.Store — overview

State management for Viu — the role [`pinia`](https://pinia.vuejs.org) plays for Vue 3
(https://github.com/vuejs/pinia). Two features have landed. The **store definition API**
(`[V01.01.09.01]`): the setup-syntax `DefineStore`/`UseStore` pair, the per-app store registry
(Pinia's `createPinia()` root), and the `EffectScope`-owned lifetime everything else builds on — the
C# port of `defineStore()`, `createPinia()`, and `store.$dispose()` (`packages/pinia/src/store.ts`,
`createPinia.ts`, `rootStore.ts`). And the **state/getters/actions member model** (`[V01.01.09.02]`):
the optional `Store<TState>` base class that gives a store Pinia's instance API — `Patch` (`$patch`),
`Reset` (`$reset`), `Subscribe` (`$subscribe`), and `OnAction` (`$onAction`) — over a source-generated
`[Reactive]` state object.

SSR serialization and hydration (`[V01.01.09.03]`) and the store plugin/persistence system
(`[V01.01.09.04]`) are later features of the Store area (#75) and are not part of this package yet.

A store can still be **any** object the setup delegate returns (holding `Reference<T>` state,
`Computed<T>` getters, and methods) when it only needs reactive state and getters; the definition API
guarantees that object's identity, single construction, and scope-bound lifetime. Deriving from
`Store<TState>` adds the batched instance API on top.

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
  root `EffectScope` and the id → instance map; `AsPlugin()` bridges it onto an app through
  `App.Use(...)`; `Dispose()` stops the whole subsystem; `Count` and `IsDisposed` round out the
  surface. Directly constructible (`new StoreRegistry()`) so a DI container can own it.
- **`StoreSetup<TStore>`** (`Delegates/`): the parameterless setup delegate that constructs the store
  inside its effect scope — the C# port of the setup function passed to `defineStore`.
- **`DuplicateStoreIdException`**: the deterministic failure when two different definitions claim the
  same id in one registry (carries the offending `StoreId`), instead of silently aliasing state.

Member model (`[V01.01.09.02]`, the C# port of Pinia's store instance API):

- **`Store<TState>`**: the optional base class for a store whose reactive state is a source-generated
  `[Reactive]` object (`TState : IReactiveObject`). `State` is the port of `$state`; `Patch` is
  `$patch` (a mutator-delegate form and a partial-state form); `Reset` is `$reset` (needs an
  initial-state factory, else it throws — Pinia setup-store parity); `Subscribe` is `$subscribe`; and
  `OnAction` is `$onAction`. Actions route their body through the protected `RunAction`/`RunActionAsync`
  helpers so `OnAction` can observe them (there is no `Proxy` to intercept the call).
- **`StoreMutation`** + **`StorePatchKind`**: the metadata a `Subscribe` callback receives — the store
  id and how the change arrived (`Direct` member write, `PatchFunction`, or `PatchObject`), the port of
  Pinia's mutation object and `MutationType`.
- **`StoreActionContext`**: the per-invocation context an `OnAction` callback receives (`Name`,
  `Store`, and the `After`/`OnError` hooks) — the port of the `$onAction` context.
- **`StoreSubscription`**: the disposable stop handle both `Subscribe` and `OnAction` return.
- **`StoreSubscriptionCallback<TState>`** and **`StoreActionCallback`** (`Delegates/`): the subscribe
  and action callbacks.

Internal (`Internal/`, exercised through `InternalsVisibleTo` tests): `StorePlugin<TNode>` (the
`IPlugin` adapter that provides the registry app-wide and sets it active on install) and
`StoreEntry` (a registry's per-store record: instance, owning scope, and owning definition).

## Using it

```csharp
using Assimalign.Viu;
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
app.Use(pinia.AsPlugin());          // provides the registry app-wide

// Or, on an application builder, register it through services ([V01.01.03.24]) — this also keeps the
// plugin/provide parity, so UseStore() resolves either way (service-first-then-provide):
builder.AddStore(pinia);            // Services.AddSingleton(pinia) + Use(pinia.AsPlugin())

// Inside a component Setup — resolves the app's registry, no argument needed:
var counter = UseCounter.UseStore();
// From plain C#/DI — pass the registry handle explicitly:
var same = UseCounter.UseStore(pinia);     // ReferenceEquals(counter, same) == true

pinia.Dispose();                            // stops every store's scope
```

For the full instance API, derive from `Store<TState>` over a `[Reactive]` state object:

```csharp
[Reactive]                                  // per-member track/trigger from the Roslyn generator
partial class CounterState
{
    public partial int Count { get; set; }
    public partial int Step { get; set; }
}

sealed class CounterStore : Store<CounterState>
{
    public CounterStore()
        : base("counter",
            () => new CounterState { Count = 0, Step = 1 },        // initial-state factory (enables Reset)
            (target, source) => { target.Count = source.Count; target.Step = source.Step; })
    {
        Doubled = Reactive.Computed(() => State.Count * 2);        // a getter (recomputes only on Count)
    }

    public Computed<int> Doubled { get; }

    // An action routed through RunAction so OnAction can observe it.
    public void Increment() => RunAction(nameof(Increment), () => State.Count += State.Step);
}

var store = UseCounter.UseStore(pinia);
store.Subscribe((mutation, state) => Save(state), detached: true);  // $subscribe, batched once per flush
store.OnAction(context => context.After(_ => Log(context.Name)));   // $onAction, After/OnError hooks
store.Patch(s => { s.Count = 5; s.Step = 2; });                     // one notification for both writes
store.Reset();                                                      // back to the factory's initial state
```

## Boundaries

- References **only** `Assimalign.Viu.Core` (for `EffectScope`/`Reference`/`Computed`, the
  `[Reactive]` state contract, `Watch`, and batching, plus `App`, `IPlugin`, provide/inject, the
  current component instance, and `ViuWatch`/the scheduler that batches subscription notifications).
  It references **no DOM/JavaScript-interop assembly** — stores are
  platform-agnostic and run in a plain .NET test host.
- Trimming- and NativeAOT-safe: store construction goes through the typed setup delegate captured at
  `DefineStore` time — never `Activator.CreateInstance` or attribute scanning — so there is no
  reflection-based activation and no dynamic code generation.
- Not thread-safe (single-threaded JS event-loop model). The registry is per app instance, so
  server-side (multi-request) hosting stays isolated without global mutable state; the ambient
  `ActiveRegistry` is a client/testing convenience — pass the registry explicitly on the server.
- Design rationale, the Pinia counterpart mapping, and the deliberate C#/WASM divergences:
  [DESIGN.md](DESIGN.md).
