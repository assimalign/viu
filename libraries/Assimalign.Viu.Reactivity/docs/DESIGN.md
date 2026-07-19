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
subscribers, a `Subscriber` holds its dependencies, and the `Link` nodes between them (internal) are
reused across runs so a stable dependency set allocates nothing on re-track. Batching
(`StartBatch`/`EndBatch`) coalesces triggers so multiple writes produce at most one run per effect;
`PauseTracking`/`ResetTracking` gate collection.

`Subscriber` is a **`public abstract` class with `internal` members and a `private protected`
constructor** — opaque and un-subclassable externally, but a real base so the engine's hot path
(per-trigger notification) dispatches through a vtable virtual call rather than interface dispatch,
which is measurably costlier on mono-wasm / NativeAOT (the repo's "dispatch on hot paths" rule).
Concrete leaves (`ReactiveEffect`, `Computed<T>`) are `sealed` so the JIT can devirtualize.

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
- Reactivity escape hatches and introspection beyond the current surface are sequenced work
  ([V01.01.02.09]).
