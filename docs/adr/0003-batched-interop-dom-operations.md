# ADR-0003: Batched JS-interop DOM operations as the performance budget

- **Status:** Accepted (decision stands; framing annotated 2026-08-02 — see the note below)
- **Date:** 2026-07-19 (foundational C#/WASM premise; formally recorded under [V01.01.13.01], #98)
- **Scope:** `Assimalign.Viu.RuntimeDom`, `Assimalign.Viu.RuntimeCore` (renderer, scheduler, block
  tree), `Assimalign.Viu.Components` (the flag vocabulary), and the compiler's static-optimization
  passes.

> **Superseded framing (2026-08-02).** On 2026-08-02 the user directed that **Viu is a standalone
> framework, not a port of Vue.js**; Vue is no longer a normative authority for Viu's semantics, and
> [`docs/SPECIFICATION.md`](../SPECIFICATION.md) is now the authority — this ADR's decision is
> carried forward there as clauses `[EXE-11]`–`[EXE-14]` and `[RND-IO-1]`–`[RND-IO-5]`. **The
> decision recorded here is unaffected**: the interop boundary is Viu's performance budget, and
> compiler-informed patching exists to spend as few crossings as possible. What is superseded is the
> framing — compiler-informed rendering is described below as another project's idea that pays off
> more on WebAssembly, whereas it is now the architecture Viu owns and specifies
> ([`docs/SPECIFICATION.md`](../SPECIFICATION.md) §6). Continued evaluation of other frameworks'
> renderer performance work happens through
> [`docs/PERFORMANCE-RESEARCH.md`](../PERFORMANCE-RESEARCH.md), which is explicitly non-normative.
>
> A factual note for a future reader, recorded rather than edited: the scope line names
> `Assimalign.Viu.RuntimeDom` and `Assimalign.Viu.RuntimeCore`, renamed `Assimalign.Viu.Browser`
> ([V01.01.12.22]) and `Assimalign.Viu.Core` ([V01.01.12.21]) respectively. The body is preserved as
> the historical record and is not rewritten (see [README.md](README.md), "Append-only").

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
- The compiler and runtime share the `PatchFlags`/`ShapeFlags`/`SlotStability` bitmask vocabulary (in
  `Assimalign.Viu.Components`); the renderer patches only what the flags mark dynamic, and the block
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
  [`Assimalign.Viu.Syntax.Templates/docs/DESIGN.md`](../../tooling/Assimalign.Viu.Syntax.Templates/docs/DESIGN.md).
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
