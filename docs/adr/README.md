# Architecture decision records

This is Viu's append-only log of architecture decisions — the *why* behind the choices that shape
the framework. [`docs/SPECIFICATION.md`](../SPECIFICATION.md) states *what* Viu guarantees; an ADR
states why a guarantee is shaped that way. The narrative summary of these decisions lives in
[`docs/PLAN.md`](../PLAN.md) ("Founding design decisions"); this directory records each one as a
standalone, citable document a future session can act on without that conversation's context.

> **Framing note (2026-08-02).** ADRs 0001–0005 were written while the repository framed Viu as a
> re-implementation of Vue.js 3. On 2026-08-02 the user directed that **Viu is a standalone
> framework**: Vue is no longer a normative authority for Viu's semantics, and
> [`docs/SPECIFICATION.md`](../SPECIFICATION.md) is. Each of those five records carries a dated
> superseded-framing note at the top; **none of their decisions were reversed** by that direction.
> Per the append-only rule below, their bodies are preserved rather than rewritten. Continued
> evaluation of other frameworks' performance work happens through
> [`docs/PERFORMANCE-RESEARCH.md`](../PERFORMANCE-RESEARCH.md), which is non-normative.

## Conventions

- **Numbering.** ADRs are numbered sequentially from `0001`, zero-padded to four digits. The
  filename is `NNNN-kebab-case-title.md`; the document title is `# ADR-NNNN: <decision>`.
- **Append-only.** An ADR is never rewritten to change a past decision. To change course, add a new
  ADR that supersedes the old one: set the new ADR's status to `Accepted`, set the old ADR's status
  to `Superseded by ADR-NNNN` with a link to the new record, and link both ways. History is
  preserved, not edited.
  (Correcting a typo or a broken link is fine; reversing the recorded decision is not.)
- **Template.** Copy [`template.md`](template.md) for a new record.
- **When to write one.** Any decision with lasting architectural consequence — a change to behavior
  [`docs/SPECIFICATION.md`](../SPECIFICATION.md) specifies, a cross-cutting constraint, a technology
  or boundary choice, or a commitment to a documented external compatibility target. An ADR that
  changes specified behavior must say which specification clauses it adds, amends, or supersedes.
  Small, local choices belong in the relevant `DESIGN.md`, not here.

## Repo-level versus library-level ADRs

This directory holds **repo-wide and cross-cutting** decisions. A decision that is contained within
a single library — measured, library-specific, and unlikely to be cited elsewhere — may instead live
in that library's `docs/` folder as a local ADR. The existing example is
[`Assimalign.Viu.Browser/docs/ADR-0001-interop-marshaling.md`](../../libraries/Assimalign.Viu.Browser/docs/ADR-0001-interop-marshaling.md)
(int-handle node identity over `JSObject` proxies; the Browser name was adopted in
[V01.01.12.22]), which is the Browser-local realization of the
repo-wide budget recorded here in [ADR-0003](0003-batched-interop-dom-operations.md). Library-local
ADRs keep their own numbering within their library folder.

## Index

| ADR | Decision | Status |
| --- | --- | --- |
| [0001](0001-source-generators-over-reflection.md) | Roslyn source generators over reflection and dynamic code generation | Accepted (framing annotated 2026-08-02) |
| [0002](0002-ref-first-reactivity.md) | Ref-first reactivity instead of JavaScript `Proxy` | Accepted (framing annotated 2026-08-02) |
| [0003](0003-batched-interop-dom-operations.md) | Batched JS-interop DOM operations as the performance budget | Accepted (framing annotated 2026-08-02) |
| [0004](0004-composition-only-component-model.md) | Composition-only component model (no Options API, mixins, or global properties) | Accepted (framing annotated 2026-08-02; **stated replacement mechanism outdated — needs a superseding ADR**) |
| [0005](0005-no-runtime-template-compilation.md) | No runtime template compilation (build-time source generators only) | Accepted (container framing partially superseded 2026-08-02; framework framing annotated) |

See also [`../CONTRIBUTING.md`](../CONTRIBUTING.md) for how ADRs fit into the wider documentation
convention.
