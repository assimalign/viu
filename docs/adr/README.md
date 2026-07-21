# Architecture decision records

This is Viu's append-only log of architecture decisions — the *why* behind the choices that shape
the framework, especially the deliberate C#/WASM divergences from Vue 3. The narrative summary of
these decisions lives in [`docs/PLAN.md`](../PLAN.md) ("Founding design decisions"); this directory
records each one as a standalone, citable document a future session can act on without that
conversation's context.

## Conventions

- **Numbering.** ADRs are numbered sequentially from `0001`, zero-padded to four digits. The
  filename is `NNNN-kebab-case-title.md`; the document title is `# ADR-NNNN: <decision>`.
- **Append-only.** An ADR is never rewritten to change a past decision. To change course, add a new
  ADR that supersedes the old one: set the new ADR's status to `Accepted`, set the old ADR's status
  to `Superseded by ADR-NNNN` with a link to the new record, and link both ways. History is
  preserved, not edited.
  (Correcting a typo or a broken link is fine; reversing the recorded decision is not.)
- **Template.** Copy [`template.md`](template.md) for a new record.
- **When to write one.** Any decision with lasting architectural consequence — a divergence from
  vuejs/core semantics, a cross-cutting constraint, a technology or boundary choice. Small, local
  choices belong in the relevant `DESIGN.md`, not here.

## Repo-level versus library-level ADRs

This directory holds **repo-wide and cross-cutting** decisions. A decision that is contained within
a single library — measured, library-specific, and unlikely to be cited elsewhere — may instead live
in that library's `docs/` folder as a local ADR. The existing example is
[`Assimalign.Viu.Browser/docs/ADR-0001-interop-marshaling.md`](../../libraries/Assimalign.Viu.Browser/docs/ADR-0001-interop-marshaling.md)
(int-handle node identity over `JSObject` proxies; the library was renamed from `Assimalign.Viu.RuntimeDom`
in [V01.01.12.22]), which is the RuntimeDom-local realization of the
repo-wide budget recorded here in [ADR-0003](0003-batched-interop-dom-operations.md). Library-local
ADRs keep their own numbering within their library folder.

## Index

| ADR | Decision | Status |
| --- | --- | --- |
| [0001](0001-source-generators-over-reflection.md) | Roslyn source generators over reflection and dynamic code generation | Accepted |
| [0002](0002-ref-first-reactivity.md) | Ref-first reactivity instead of JavaScript `Proxy` | Accepted |
| [0003](0003-batched-interop-dom-operations.md) | Batched JS-interop DOM operations as the performance budget | Accepted |
| [0004](0004-composition-only-component-model.md) | Composition-only component model (no Options API, mixins, or global properties) | Accepted |
| [0005](0005-no-runtime-template-compilation.md) | No runtime template compilation (build-time source generators only) | Accepted |

See also [`../CONTRIBUTING.md`](../CONTRIBUTING.md) for how ADRs fit into the wider documentation
convention.
