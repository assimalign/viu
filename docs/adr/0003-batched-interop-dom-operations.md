# ADR-0003: Batched JS-interop DOM operations as the performance budget

- **Status:** Accepted
- **Date:** 2026-07-19 (foundational C#/WASM premise; formally recorded under [V01.01.13.01], #98)
- **Scope:** `Assimalign.Viu.RuntimeDom`, `Assimalign.Viu.RuntimeCore` (renderer, scheduler, block
  tree), `Assimalign.Viu.Shared` (the flag vocabulary), and the compiler's static-optimization
  passes.

## Context

In a browser WASM app every DOM mutation crosses the .NET ↔ JavaScript interop boundary, and that
marshaling is the framework's dominant per-operation cost — far more so than the equivalent property
access in Vue's JavaScript runtime. Vue's [compiler-informed rendering](https://vuejs.org/guide/extras/rendering-mechanism.html)
(patch flags, shape flags, the block tree that flattens dynamic descendants) exists to skip work;
on WASM each skipped patch visit is additionally a **marshaling round-trip avoided**, so the same
idea pays off more.

## Decision

**The interop boundary is Viu's performance budget, and the runtime is organized to spend as few
crossings as possible and keep each one cheap.**

- Decision logic lives in .NET; the JS side is a dumb applier. Patch operations batch into a
  command buffer applied by **one** JS call per scheduler flush.
- Events use the invoker pattern: one delegated JS listener per (element, event); a re-rendered
  handler is a .NET delegate swap on the invoker — zero `addEventListener`/`removeEventListener`
  interop between renders.
- Static content is stringified aggressively and inserted via `innerHTML` (`insertStaticContent`),
  collapsing many node ops into one.
- The compiler and runtime share the `PatchFlags`/`ShapeFlags`/`SlotFlags` bitmask vocabulary (in
  `Assimalign.Viu.Shared`); the renderer patches only what the flags mark dynamic, and the block
  tree flattens dynamic nodes so patching skips the static structure.
- JS-side handles and event listeners are always cleaned up deterministically (two-sided release).

## Consequences

- Node identity crosses the boundary as **int handles**, not `JSObject` proxies — the measured,
  RuntimeDom-local realization of this budget, recorded in
  [`Assimalign.Viu.Browser/docs/ADR-0001-interop-marshaling.md`](../../libraries/Assimalign.Viu.Browser/docs/ADR-0001-interop-marshaling.md)
  (the library was renamed from `Assimalign.Viu.RuntimeDom` in [V01.01.12.22]).
- The command buffer requires primitive-typed ops (opcode + int + string), which int handles
  satisfy and proxies would not.
- The compiler carries static hoisting and stringification passes (`cacheStatic` / `stringifyStatic`)
  whose payoff is counted in avoided interop calls — see
  [`Assimalign.Viu.Syntax.Templates/docs/DESIGN.md`](../../libraries/Assimalign.Viu.Syntax.Templates/docs/DESIGN.md).
- WASM size and startup budgets gate CI from W03; a benchmark suite ([V01.01.11.04]) re-measures
  interop cost under AOT.

## Alternatives considered

- **Per-operation interop** (a JS call per node op) — the naive port, rejected: it spends the exact
  resource that is scarcest.
- **`JSObject` proxy per node** — natural JS ergonomics, but measured ~2× slower for node
  creation/teardown and incompatible with the batched command buffer; see the RuntimeDom ADR-0001
  measurement.

## References

- [`docs/PLAN.md`](../PLAN.md) — founding decisions 1 and 4.
- [`Assimalign.Viu.Browser/docs/OVERVIEW.md`](../../libraries/Assimalign.Viu.Browser/docs/OVERVIEW.md),
  [`DESIGN.md`](../../libraries/Assimalign.Viu.Browser/docs/DESIGN.md), and its
  [ADR-0001](../../libraries/Assimalign.Viu.Browser/docs/ADR-0001-interop-marshaling.md).
- Vue 3: [rendering mechanism](https://vuejs.org/guide/extras/rendering-mechanism.html).
