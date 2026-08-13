# ADR-NNNN: <short decision title, stated as the decision>

- **Status:** Proposed | Accepted | Superseded by ADR-NNNN (link the superseding record) | Deprecated
- **Date:** YYYY-MM-DD (the date the decision was made; keep the original when superseding)
- **Work item:** [V01.01.NN.MM] (#issue), when the decision was made under one
- **Scope:** which area(s) the decision governs (repo-wide, or specific `Assimalign.Viu.*` libraries)

## Context

The forces at play: what problem this decides and the constraints that bound it (AOT/trimming, the
WASM interop budget, the single-threaded model, the immutable render tree, the absence of an
object-proxy layer). State the current specified behavior the decision changes or preserves, citing
the [`docs/SPECIFICATION.md`](../SPECIFICATION.md) clause id — Viu's own specification is the
baseline, and no external project's behavior is authoritative here. Where the decision concerns a
documented **external compatibility target** Viu deliberately consumes — the `.vue`
single-file-component container format, WHATWG HTML serialization, or the Language Server Protocol
— name and link that target: there the citation *is* the requirement. Tailwind CSS v4.3.3 belongs
only to the parked, non-normative utility add-on and is not a Viu core compatibility target.

## Decision

The decision itself, in active voice ("We use X"), specific enough that a future session can apply
it without this conversation's context. State the rule, not just the sentiment.

## Consequences

What becomes easier and what becomes harder. Include the follow-on obligations the decision creates:
new seams to maintain, tests that pin the chosen behavior, budgets to gate, and the
`docs/SPECIFICATION.md` clauses that must be added, amended, or marked superseded in the same change.

## Alternatives considered

Each seriously weighed option and why it was not chosen. For a measured decision, cite the
measurement (link the harness/benchmark). This is where a superseding ADR will look first.

## References

- The [`docs/SPECIFICATION.md`](../SPECIFICATION.md) clause ids the decision adds, amends, or supersedes.
- Any external compatibility target the decision commits Viu to (name + version-pinned link).
- Related ADRs, `docs/PLAN.md` founding decisions, per-library `DESIGN.md` sections.
- For a performance-motivated decision, the
  [`docs/PERFORMANCE-RESEARCH.md`](../PERFORMANCE-RESEARCH.md) finding row and the benchmark run that
  justified it.
