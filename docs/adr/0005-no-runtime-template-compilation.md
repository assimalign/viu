# ADR-0005: No runtime template compilation (build-time source generators only)

- **Status:** Accepted (container framing partially superseded on 2026-08-02 — see the Decision note;
  framework framing annotated the same date — see the note below)
- **Date:** 2026-07-17 (the build-time `.viu` compilation container was decided this date, `docs/PLAN.md`
  founding decision 3; formally recorded as an ADR under [V01.01.13.01], #98, on 2026-07-19)
- **Scope:** `Assimalign.Viu.Syntax.Templates`, `Assimalign.Viu.Syntax.SingleFileComponent`, and the
  `Assimalign.Viu.Generators.Syntax` composition root.

> **Superseded framing (2026-08-02).** On 2026-08-02 the user directed that **Viu is a standalone
> framework, not a port of Vue.js**; Vue is no longer a normative authority for Viu's semantics, and
> [`docs/SPECIFICATION.md`](../SPECIFICATION.md) is now the authority — this ADR's decision is carried
> forward there as `[DEF-3]` and `[SFC-1]`–`[SFC-2]`. **The decision recorded here is unaffected**:
> templates and single-file components compile at build time only, and there is no runtime
> compilation path. What is superseded is the framing — the Context describes the rule as a
> consequence of another framework's runtime compiler being impossible on WebAssembly, whereas it is
> properly Viu's own AOT constraint plus a deliberate tooling choice.
>
> Two clarifications that the standalone decision makes worth stating plainly, since the body
> predates it:
>
> - The `.vue` compatibility parser ([V01.01.06.09], #250) is a **shipping product feature** targeting
>   a documented external container format, specified as `SPECIFICATION.md` §9. It is a compatibility
>   target, not evidence of derivation.
> - "The markup inside the template container remains standard Vue template syntax" describes the
>   template language Viu implements and is retained as a factual statement about the input Viu
>   accepts; the semantics of that language are specified by Viu in `SPECIFICATION.md` §8 and pinned
>   by this repository's tests.
>
> The body is preserved as the historical record and is not rewritten (see [README.md](README.md),
> "Append-only").

## Context

Vue's full build compiles templates to render functions at runtime with `new Function` (its
in-browser compiler). That path is impossible in WASM — dynamic code generation is forbidden AOT
(see [ADR-0001](0001-source-generators-over-reflection.md)). Vue's own guidance already prefers a
build step so the runtime ships without the compiler; Viu makes the build step mandatory.

## Decision

**Templates and `.viu` single-file components compile at build time only, via Roslyn source
generators; there is no runtime compilation path.**

- The template front end (`Assimalign.Viu.Syntax.Templates`) tokenizes, parses, transforms, and
  emits a C# render method; `Assimalign.Viu.Generators.Syntax` is the incremental generator that
  drives it and stitches the output into the component's partial class.
- There is no runtime `compile(templateString)` API — a template that is not present at build time
  cannot be rendered.
- The `.viu` container framing has changed once, and the core decision of this ADR (build-time
  compilation only) was unaffected both times. As decided 2026-07-17, the container used
  `@template`/`@script`/`@style` `@`-block syntax — a deliberate divergence from Vue's tag-based SFC
  container. On 2026-08-02 that framing was **partially reversed by user direction**
  ([V01.01.06.10], #257): the canonical container is now the hybrid `<template>`/`<style>` tag form
  with the C# `@script { }` block retained as the remaining divergence; legacy `@`-blocks parse with
  a warning-severity migration diagnostic during a transition window. The **markup inside the
  template container remains standard Vue template syntax** throughout; only the container framing
  differs. The container is specified in
  [`Assimalign.Viu.Syntax.SingleFileComponent/docs/FORMAT.md`](../../libraries/Assimalign.Viu.Syntax.SingleFileComponent/docs/FORMAT.md).

## Consequences

- Compilation is compiler-grade tooling: diagnostics carry template source locations mapped back to
  the `.viu` file (Roslyn `#line`), so C# errors in a template expression point at the real line and
  column — the tooling story reaches Razor-grade because it *is* a compiler.
- The JavaScript-to-C# serialization divergences (no comma operator, no object literals with
  arbitrary keys, `.Value` unwrapping, `undefined`→`null`, …) are documented and test-pinned in
  [`Assimalign.Viu.Syntax.Templates/docs/DESIGN.md`](../../libraries/Assimalign.Viu.Syntax.Templates/docs/DESIGN.md).
- Compiler-informed patch flags, block trees, and static hoisting all fall out of build-time
  compilation and feed the interop budget ([ADR-0003](0003-batched-interop-dom-operations.md)).
- Dynamic, string-sourced templates are unsupported by design.

## Alternatives considered

- **A runtime compiler** (`new Function` equivalent) — impossible/forbidden AOT.
- **A runtime template interpreter** — technically AOT-safe but rejected: it forfeits compiler-
  informed patch flags and static hoisting, adds per-render cost, and gives up the build-time
  diagnostics that make the authoring experience strong.

## References

- [`docs/PLAN.md`](../PLAN.md) — founding decision 3.
- [`Assimalign.Viu.Syntax.Templates/docs/DESIGN.md`](../../libraries/Assimalign.Viu.Syntax.Templates/docs/DESIGN.md)
  and [`Assimalign.Viu.Syntax.SingleFileComponent/docs/FORMAT.md`](../../libraries/Assimalign.Viu.Syntax.SingleFileComponent/docs/FORMAT.md).
- Vue 3: [render functions](https://vuejs.org/guide/extras/render-function.html),
  [SFC spec](https://vuejs.org/api/sfc-spec.html).
