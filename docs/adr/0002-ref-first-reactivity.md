# ADR-0002: Ref-first reactivity instead of JavaScript `Proxy`

- **Status:** Accepted
- **Date:** 2026-07-19 (foundational C#/WASM premise; formally recorded under [V01.01.13.01], #98)
- **Scope:** `Assimalign.Viu.Reactivity` and every consumer of it (`RuntimeCore`, the compiler's
  expression-binding contract).

## Context

Vue 3's public reactivity story leads with `reactive(obj)` — a deep [`Proxy`](https://vuejs.org/api/reactivity-core.html#reactive)
that intercepts every property read and write and auto-unwraps refs on access — and treats `ref()`
as the primitive for standalone values. The `Proxy` is unavailable AOT/trimming-safe in WASM (see
[ADR-0001](0001-source-generators-over-reflection.md)). However, Vue 3.5's *own* ref internals are a
plain getter/setter over a dependency with explicit track-on-read / trigger-on-write, plus a
version-counter and doubly-linked-list dependency graph — all of which port to C# directly.

## Decision

**`Ref<T>`-style references are the primary reactive primitive; there is no `Proxy`.**

- `Reference<T>` (Vue's `ref()`), `ShallowReference<T>` (`shallowRef()`), `CustomReference<T>`
  (`customRef()`), and `Computed<T>` (`computed()`) are plain C# types with a settable `Value`
  property that tracks on read and triggers on write. The static `Reactive` facade mirrors the
  `@vue/reactivity` API surface (refs, computeds, effects, scopes, tracking control, batching).
- `reactive(obj)` becomes a `[Reactive]` partial class whose per-property reactive wrappers are
  emitted by `Assimalign.Viu.Reactivity.Generators` — not a runtime-proxied object.
- Reactive collections are dedicated types — `ReactiveList<T>`, `ReactiveDictionary<TKey,TValue>`,
  `ReactiveSet<T>` — implementing the BCL collection interfaces, **not** proxied BCL types.
- The dependency engine ports Vue 3.5's version-counter + doubly-linked-list subscriber design.
- The model is single-threaded (the JS event loop); ambient tracking state is `static` and not
  thread-safe by design.

## Consequences

- **The compiler, not a runtime proxy, decides every access form.** With no `Proxy` to auto-unwrap
  refs, a template that reads a ref must emit `.Value`, and — because `Ref<T>.Value` is a settable
  property — compound assignment and increment (`count++`, `count += 1`) rewrite cleanly to
  `count.Value …`. This is the expression-binding contract pinned in
  [`Assimalign.Viu.Syntax.Templates/docs/DESIGN.md`](../../libraries/Assimalign.Viu.Syntax.Templates/docs/DESIGN.md);
  a change to `Ref<T>.Value` requires a matching change there.
- No implicit deep reactivity: authors reach for `[Reactive]` classes or reactive collections
  explicitly, which is more predictable but less magical than a deep `Proxy`.
- Escape hatches and introspection (`unref`/`isRef`, raw markers) are provided explicitly rather
  than falling out of proxy identity.

## Alternatives considered

- **A `Proxy` analogue** (`DispatchProxy`, `Reflection.Emit`) — rejected under
  [ADR-0001](0001-source-generators-over-reflection.md): trimming-unsafe.
- **Proxied BCL collections** — impossible without a `Proxy`; dedicated reactive collection types
  give the same reactivity with AOT-safe, allocation-predictable code.

## References

- [`docs/PLAN.md`](../PLAN.md) — founding decision 2.
- [`Assimalign.Viu.Reactivity/docs/DESIGN.md`](../../libraries/Assimalign.Viu.Reactivity/docs/DESIGN.md).
- Vue 3: [`@vue/reactivity`](https://github.com/vuejs/core/tree/main/packages/reactivity),
  [Reactivity API: Core](https://vuejs.org/api/reactivity-core.html).
