# ADR-NNNN: <short decision title, stated as the decision>

- **Status:** Proposed | Accepted | Superseded by ADR-NNNN (link the superseding record) | Deprecated
- **Date:** YYYY-MM-DD (the date the decision was made; keep the original when superseding)
- **Work item:** [V01.01.NN.MM] (#issue), when the decision was made under one
- **Scope:** which area(s) the decision governs (repo-wide, or specific `Assimalign.Viu.*` libraries)

## Context

The forces at play: what problem this decides, the constraints that bound it (AOT/trimming, the
WASM interop budget, upstream Vue 3 parity, the single-threaded model), and the relevant upstream
Vue 3 behavior. Link the authoritative reference — vuejs.org or the specific `vuejs/core` source
file/module — so the parity baseline the decision diverges from (or preserves) is explicit.

## Decision

The decision itself, in active voice ("We use X"), specific enough that a future session can apply
it without this conversation's context. State the rule, not just the sentiment.

## Consequences

What becomes easier and what becomes harder. Include the follow-on obligations the decision creates
(new seams to maintain, tests that pin the chosen behavior, budgets to gate) and the deltas from
Vue 3 that the decision commits Viu to.

## Alternatives considered

Each seriously weighed option and why it was not chosen. For a measured decision, cite the
measurement (link the harness/benchmark). This is where a superseding ADR will look first.

## References

- Vue 3 counterpart(s): vuejs.org guide / `vuejs/core` package or source file.
- Related ADRs, `docs/PLAN.md` founding decisions, per-library `DESIGN.md` sections.
