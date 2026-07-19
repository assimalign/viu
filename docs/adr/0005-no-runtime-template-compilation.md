# ADR-0005: No runtime template compilation (build-time source generators only)

- **Status:** Accepted
- **Date:** 2026-07-17 (the build-time `.viu` compilation container was decided this date, `docs/PLAN.md`
  founding decision 3; formally recorded as an ADR under [V01.01.13.01], #98, on 2026-07-19)
- **Scope:** `Assimalign.Viu.Syntax.Templates`, `Assimalign.Viu.Syntax.SingleFileComponent`, and the
  `Assimalign.Viu.Syntax.Generators` composition root.

## Context

Vue's full build compiles templates to render functions at runtime with `new Function` (its
in-browser compiler). That path is impossible in WASM — dynamic code generation is forbidden AOT
(see [ADR-0001](0001-source-generators-over-reflection.md)). Vue's own guidance already prefers a
build step so the runtime ships without the compiler; Viu makes the build step mandatory.

## Decision

**Templates and `.viu` single-file components compile at build time only, via Roslyn source
generators; there is no runtime compilation path.**

- The template front end (`Assimalign.Viu.Syntax.Templates`) tokenizes, parses, transforms, and
  emits a C# render method; `Assimalign.Viu.Syntax.Generators` is the incremental generator that
  drives it and stitches the output into the component's partial class.
- There is no runtime `compile(templateString)` API — a template that is not present at build time
  cannot be rendered.
- The `.viu` container uses `@template`/`@script`/`@style` `@`-block syntax — a deliberate divergence
  from Vue's tag-based SFC container (decided 2026-07-17). The **markup inside `@template` remains
  standard Vue template syntax**; only the container framing differs. The container is specified in
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
