# Assimalign.Viu.Reactivity — design

Why the reactive core is shaped the way it is. What it is: see [OVERVIEW.md](OVERVIEW.md). Upstream
counterpart: [`@vue/reactivity`](https://github.com/vuejs/core/tree/main/packages/reactivity) (v3.5).

## Ref-first, no `Proxy`

The founding divergence is [ADR-0002](../../../docs/adr/0002-ref-first-reactivity.md): Vue's public
lead is the deep `Proxy` (`reactive(obj)`), which is not AOT/trimming-safe in WASM. Viu leads with
references instead. `Reference<T>` and `Computed<T>` are plain getter/setter types that track on read
and trigger on write — Vue 3.5's own ref internals port directly. `reactive(obj)` becomes a
`[Reactive]` partial class whose property wrappers a source generator emits, and reactive collections
are dedicated types rather than proxied BCL collections.

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
of Vue's `Link`), `Subscriber.FirstDependency`, the already-public `Dependency`, and
`ITrackedReference` let a .NET developer inspect what depends on what: walk a subscriber's
`FirstDependency` → `NextDependency` chain, reach the `Dependency` behind a ref via
`ITrackedReference`, and read each edge's observed `Version`. This mirrors how .NET developers expect
to introspect a framework's object graph, and Vue itself keeps the same structures in `dep.ts`.

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
