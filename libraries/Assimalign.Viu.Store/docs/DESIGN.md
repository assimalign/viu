# Assimalign.Viu.Store — design

Why the store definition API is shaped the way it is. What it is: see [OVERVIEW.md](OVERVIEW.md).
Upstream counterpart: [`pinia`](https://github.com/vuejs/pinia) — `defineStore()`, `createPinia()`,
and store lifetime (`packages/pinia/src/store.ts`, `createPinia.ts`, `rootStore.ts`). Store-lifetime
reference: Vue 3 `effectScope` (https://vuejs.org/api/reactivity-advanced.html#effectscope).

## Setup stores are typed closures, not Proxy objects

Pinia has two authoring styles; Viu ports the **setup** style only, because it is the one that maps
cleanly to C#. A Pinia option store (`state`/`getters`/`actions` objects) leans on the JS `Proxy` to
turn plain objects into reactive state and to bind `this`. Viu has no `Proxy`, so the store is the
object the setup delegate returns and closes over: its `Ref<T>` fields are state, its `Computed<T>`
are getters, its methods are actions. The store definition captures that delegate at `DefineStore`
time and invokes it directly — construction is a plain constructor call, never
`Activator.CreateInstance` or attribute scanning, which is what keeps the whole path trimming- and
NativeAOT-safe. The state/getter/action *members* are co-designed but delivered in `[V01.01.09.02]`;
this feature fixes the surrounding contract (identity, single construction, scope-bound lifetime).

## Lifetime: a detached root, per-store child scopes

This is the heart of the feature and a faithful port of `createSetupStore`:

- The registry (`StoreRegistry`, the C# `Pinia` root) owns one **detached** root `EffectScope`
  (Pinia's `pinia._e = effectScope(true)`). Detached means it never attaches to whatever scope
  happens to be active when the registry is constructed.
- The first `UseStore` for a definition creates the store's own scope **as a child of that root**
  (`_rootScope.Run(() => new EffectScope())`), then runs the setup **inside** that child scope
  (`scope.Run(setup)`). Every `Computed`, `Watch`, and `WatchEffect` the setup creates registers with
  the child scope.

Two consequences fall out of this shape, and both are acceptance criteria:

1. **Disposing the app stops every store.** `StoreRegistry.Dispose()` stops the root scope, which
   cascades to every child store scope (`EffectScope` stops its children), so all setup watchers and
   effects stop firing. This is the C# realization of "disposing the owning app disposes each store's
   scope" — `.NET` servers are long-lived, so the registry, not a module reload, is what bounds store
   lifetime.
2. **The store is detached from the render tree.** Because the child scope is created while the
   *root* is the active scope, it is captured by the root and **not** by whichever component scope
   triggered the lazy `UseStore`. Unmounting that component (stopping its scope) leaves the store
   fully reactive — stores are app-scoped and shared across components, exactly as in Pinia. This is
   the sense in which each store "lives in its own detached `EffectScope`": detached from the render
   tree, owned by the registry root. (`StoreScopeDisposalTests` pins this by resolving inside an
   ambient scope, stopping it, and asserting the store's watcher still fires.)

`StoreDefinition.Dispose(registry)` (the port of `store.$dispose()`) stops just that one store's
child scope and forgets it, leaving siblings live; a later `UseStore` rebuilds it fresh.

**Computeds are not scope-owned.** Per the upstream Vue 3.5 contract (and Viu's `EffectScope` /
`Computed`), a scope never collects computeds — a computed created in setup keeps serving fresh
values after the scope stops, detaching from its sources automatically when it loses its last
subscriber. So "computeds no longer fire" is realized through the **watchers/effects** that observe
them: stopping the store scope stops those observers, so nothing re-runs on a change. The disposal
tests assert the run count of a scope-owned watcher freezes, which is the true scope guarantee.

## Identity and single construction

The registry keys instances by store id (`Dictionary<string, StoreEntry>`, Pinia's `pinia._s`). The
first `UseStore` per registry runs setup once and caches the result; every later `UseStore` returns
the identical instance (reference equality) without re-running setup. `TStore` is constrained to
`class` so that "identical instance" is genuine reference identity with no boxing.

## Id uniqueness is enforced, never silently aliased

Each `StoreEntry` remembers the definition that created it (by reference). Resolving the **same**
definition again is a cache hit; resolving a **different** definition under an id already claimed
raises `DuplicateStoreIdException` (carrying the id) rather than clobbering or sharing state. Which
definition wins is deterministic: the one resolved first owns the id.

## Per-app isolation for SSR

The registry is per app instance and holds no static state, so two apps (two registries) never share
store state — the property multi-request server rendering needs, where one long-lived process serves
concurrent requests. `StoreDefinitionTests` and `StoreApplicationIntegrationTests` assert two
registries / two mounted apps resolve isolated instances. The one piece of ambient state,
`Stores.ActiveRegistry` (Pinia's `activePinia`), is a client and testing convenience for
argument-less `UseStore()` outside a component; server code passes the registry explicitly through
`UseStore(registry)`, matching Pinia's SSR guidance.

## Integration with the App API

`App.Use` installs an `IPlugin<TNode>`. A registry is platform-agnostic, but `IPlugin<TNode>` is
generic over the platform node, so `StoreRegistry.AsPlugin<TNode>()` wraps the registry in an
internal `StorePlugin<TNode>` rather than making the registry itself generic. On install the plugin:

- `app.Provide(StoreRegistry.InjectionKey, registry)` — provides the registry app-wide, so a
  component's `UseStore()` resolves it through the existing provide/inject chain
  (`AppContext.Provides` is the final inject fallback, `[V01.01.03.12]`). The key is a private
  `InjectionKey<StoreRegistry>` singleton shared between the plugin (provider) and `UseStore()`
  (injector), so no string keys and no reflection.
- `Stores.SetActiveRegistry(registry)` — sets the ambient fallback for non-component resolution.

`UseStore()` mirrors Pinia's resolution order: inside a component `Setup` it injects the app's
registry (via `ComponentInstance.Current`); otherwise it falls back to the active registry; with
neither it throws a descriptive `InvalidOperationException`.

## Deliberate divergences from Pinia

- **The registry is not itself the plugin.** In Pinia the `pinia` object has an `install` method, so
  `app.use(pinia)` and `useStore(pinia)` take the same value. C#'s `IPlugin<TNode>` is generic over
  the node type while the registry is not, so installation goes through
  `registry.AsPlugin<TNode>()`. The registry stays the DI handle; the adapter is the bridge to
  `App.Use`. Call `AsPlugin` once per app.
- **Setup style only.** Option stores (`state`/`getters`/`actions` object literals) are a Proxy-bound
  convenience with no clean C# analogue; Viu ports the setup form, which the source generator can
  extend for `[Reactive]` state in later features.
- **`$dispose` is on the definition, not the instance.** A Viu store is a user-defined `TStore` with
  no mixed-in `$`-methods, so single-store disposal is `StoreDefinition.Dispose(registry)` rather
  than `store.$dispose()`. Same effect: stop the store's scope and forget it.
- **Ambient active registry is opt-in for resolution.** Installing sets `ActiveRegistry` (Pinia
  parity), but the DI-friendly, SSR-safe path is the explicit `UseStore(registry)` — the ambient is a
  convenience, not the recommended server path.

## AOT / trimming: no runtime activation

- **Typed setup delegates, not reflection.** Store construction is a captured `StoreSetup<TStore>`
  invoked directly. There is no `Activator.CreateInstance`, no attribute scanning, and no dynamic
  code generation anywhere in the path — the same constraint the Store epic (#75) and the repo AOT
  rule impose.
- **Identity-based injection key.** The registry is provided/injected under a reference-identity
  `InjectionKey<StoreRegistry>`, never a reflected type token or string, so trimming cannot break the
  wiring.

## Non-goals (sequenced work)

- State, getters, and actions as reactivity-integrated members, `Patch`, and `$state` —
  `[V01.01.09.02]`.
- SSR state serialization and client hydration (source-generated `System.Text.Json` contexts only) —
  `[V01.01.09.03]`.
- The store plugin system and the shipped storage-persistence plugin — `[V01.01.09.04]`.
- Mutation/action subscriptions (`Subscribe`/`OnAction`) batched through the scheduler — part of the
  members/subscriptions features above.
