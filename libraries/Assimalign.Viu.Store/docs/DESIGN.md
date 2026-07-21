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
NativeAOT-safe. `[V01.01.09.01]` fixed the surrounding contract (identity, single construction,
scope-bound lifetime); `[V01.01.09.02]` adds the state/getter/action members and the
`$patch`/`$reset`/`$subscribe`/`$onAction` instance API — see the member-model section below.

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

`App.Use` installs an `IPlugin`. The plugin contract is platform-neutral (it extends the
node-type-agnostic `IApplication`), so `StoreRegistry.AsPlugin()` wraps the registry in an internal
`StorePlugin` — the same plugin installs on a browser app or a server app. On install the plugin:

- `app.Provide(StoreRegistry.InjectionKey, registry)` — provides the registry app-wide, so a
  component's `UseStore()` resolves it through the existing provide/inject chain
  (`AppContext.Provides` is the final inject fallback, `[V01.01.03.12]`). The key is a private
  `InjectionKey<StoreRegistry>` singleton shared between the plugin (provider) and `UseStore()`
  (injector), so no string keys and no reflection.
- `Stores.SetActiveRegistry(registry)` — sets the ambient fallback for non-component resolution.

`UseStore()` mirrors Pinia's resolution order: inside a component `Setup` it injects the app's
registry (via `ComponentInstance.Current`); otherwise it falls back to the active registry; with
neither it throws a descriptive `InvalidOperationException`.

## The member model: `Store<TState>` over a `[Reactive]` state (`[V01.01.09.02]`)

A store is still free to be any object the setup returns (refs + computeds + methods), which is all
"reactive state and getters" (AC1) needs — a `Computed<T>` over a `Reference<T>` already recomputes
only when that ref changes. The Pinia *instance* API (`$patch`/`$reset`/`$subscribe`/`$onAction`) needs
somewhere to live and something to enumerate, so it is an **optional** base class `Store<TState>`. With
no `Proxy` and no reflection, the store cannot discover its own members, so `TState` is a single
source-generated `[Reactive]` object (`TState : IReactiveObject`): the generator gives per-member
track/trigger, so a getter read off `State` recomputes only when the member it read changes
(`StoreStateGetterTests` pins this with run counts, not just values). `State` is held and mutated **in
place** (never replaced), so getters that captured a member's dependency stay wired and `$state`
keeps its identity — Pinia parity.

### `Patch`, `Reset`, and the applier

`Patch(Action<TState>)` is the mutator form (Pinia's `$patch(fn)`): the writes run inside a reactivity
batch so downstream computeds/effects coalesce, and one notification is delivered tagged
`PatchFunction`. The partial-state form `Patch(TState)` and `Reset()` need to copy values onto the live
state; with no reflection the store supplies a typed `applyState(target, source)` field-copier at
construction (the second constructor). That is the single piece of store-shape metadata the author
provides by hand — a future generator can emit it (see non-goals). A store built without it
(first constructor) supports only the mutator form and, like a Pinia setup store, throws a documented
`NotSupportedException` from `Reset`/object-`Patch` (`StoreResetTests`). `Reset` applies a fresh
factory instance through the same applier, so it too is one in-place notification.

### Subscription notification rides the scheduler (AC2, #75)

Subscribers are notified through **one** fan-out watcher over `State`, created lazily on the first
`Subscribe` inside the *store's* scope (captured at construction) so it lives with the store, never
with whichever component first subscribes. It is a `ViuWatch` (pre-flush on the runtime scheduler), so
several triggers in one turn — the N member writes of a `Patch`, or several direct writes — dedupe into
a single `SchedulerJob` and one notification pass. This is the "flush through the scheduler's batching
so a `Patch` of N properties produces one notification" requirement, and on WASM it bounds the
downstream JS-interop a subscriber (for example the future persistence plugin) triggers. The pass reads
a `_pendingKind` the patch methods tag (defaulting to `Direct`) so the `StoreMutation` carries the
right `StorePatchKind`, then resets it. Tests drive the scheduler with `TestSchedulerPump` for
determinism.

### `OnAction` without a `Proxy`

Pinia's `$onAction` works because the `Proxy` wraps every action call. Viu has no `Proxy`, so an action
opts in by routing its body through the protected `RunAction`/`RunActionAsync` helpers, which fire the
action subscribers (so they can register `After`/`OnError`), run the body, then the `After` hooks with
the return value — awaiting a `Task` first, so `After` sees the *resolved* result — or the `OnError`
hooks on a throw before rethrowing (`StoreActionSubscriptionTests`). Hooks fire in registration order
(Pinia parity). A store's plain methods that are *not* routed through `RunAction` still work and still
mutate state (subscriptions fire via the state watcher); they are simply invisible to `OnAction` — the
documented cost of no `Proxy`.

### Scope-bound subscriptions (AC5)

`Subscribe`/`OnAction` created inside an active `EffectScope` (a component's `Setup`) register an
`OnScopeDispose` that removes them when that scope stops, unless created `detached: true` — Pinia's
`$subscribe`/`$onAction` detached semantics. The fan-out watcher itself is store-scoped, so a component
unsubscribing never tears down notification for other subscribers.

## Deliberate divergences from Pinia

- **The registry is not itself the plugin.** In Pinia the `pinia` object has an `install` method, so
  `app.use(pinia)` and `useStore(pinia)` take the same value. To keep the registry a plain DI handle
  (not an `IApplication` extension), installation goes through `registry.AsPlugin()`, which wraps it in
  an internal `StorePlugin`. The registry stays the DI handle; the adapter is the bridge to `App.Use`.
  Call `AsPlugin` once per app.
- **Setup style only.** Option stores (`state`/`getters`/`actions` object literals) are a Proxy-bound
  convenience with no clean C# analogue; Viu ports the setup form. `Store<TState>` is the typed
  member model on top of it — a single `[Reactive]` `$state` rather than one reactive object per
  store built from loose keys.
- **`$dispose` is on the definition, not the instance.** A Viu store is a user-defined `TStore` with
  no mixed-in `$`-methods, so single-store disposal is `StoreDefinition.Dispose(registry)` rather
  than `store.$dispose()`. Same effect: stop the store's scope and forget it.
- **Actions opt into `OnAction` explicitly.** No `Proxy` means an action is observed only when routed
  through `RunAction`/`RunActionAsync`; the context therefore has no `Arguments` and its `Store` is
  typed `object`.
- **One fan-out state watcher, not one watcher per subscriber.** Pinia's `$subscribe` creates a watch
  per subscriber; Viu shares a single store-scoped watcher that iterates the subscriber list, so a
  patch of N members costs one traversal and one pass regardless of subscriber count. Per-subscriber
  flush timing is therefore not exposed (all subscribers share the store's pre-flush timing).
- **Object-form `Patch` is a full applier copy, not a sparse merge.** A typed `TState` cannot express
  "only these keys" the way a JS object literal can, so `Patch(TState)` copies whatever the author's
  `applyState` copies. For a genuinely sparse update use the mutator form, which touches only the
  members it assigns.
- **Subscriber notification is scheduler-batched per flush.** Because notification rides the runtime
  scheduler (the explicit #75 requirement), two patches issued in the *same* synchronous turn before a
  flush coalesce into one notification carrying the later patch's kind — where Pinia fires each
  synchronously. This is the intended interop-bounding behavior, pinned by the batching tests.
- **No dedicated `ValueTask` action overloads.** `RunActionAsync` takes `Func<Task>` / `Func<Task<T>>`
  only; an `async () => …` lambda is ambiguous between `Task` and `ValueTask` overloads, so a
  `ValueTask` body composes through the `Task` overload (`await` it inside). `After` still receives the
  awaited result.
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
- **The member model adds no runtime activation.** `TState` comes from the `[Reactive]` source
  generator; the state applier, getters, actions, and every `Subscribe`/`OnAction` hook are typed
  delegates. `Patch`/`Reset`/`Subscribe`/`OnAction` never reflect over the state shape, so the whole
  instance API stays trimming- and NativeAOT-safe.

## Non-goals (sequenced work)

- SSR state serialization and client hydration (source-generated `System.Text.Json` contexts only) —
  `[V01.01.09.03]`.
- The store plugin system and the shipped storage-persistence plugin — `[V01.01.09.04]`.
- A source generator that emits the `applyState` field-copier (and a `Patch` payload) for a
  `[Reactive]` store — today the author writes the typed copier by hand; generating it is a later
  ergonomics pass, not a correctness gap.
