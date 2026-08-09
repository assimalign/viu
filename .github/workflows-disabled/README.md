# Disabled workflows

GitHub only schedules workflows found in `.github/workflows/`. A file here is **preserved but never
runs** — it is parked, not deleted, and each entry below records why and what would bring it back.

Move a file back into `.github/workflows/` to re-enable it. Nothing else is required.

A parked file still references its **original** `.github/workflows/…` path in its own `paths:` filters.
That is deliberate, not an oversight: the filter is inert while the file is parked, and correct the
moment it is moved back. Leave it alone.

## `budget-gates.yml`

Parked 2026-08-06. The original blockers were the API migration in the packaged-consumer showcase
and the anticipated SDK/framework segmentation recorded as **D6** in
[`docs/API-HARDENING-PLAN.md`](../../docs/API-HARDENING-PLAN.md).

**Original reason.** Its `publish-size + trimming gate` job publishes the packaged-consumer showcase
from the sibling `viu-examples` repository and enforces `scripts/budgets/PublishBudgets.json`. At the
time it was parked, that showcase did not build: it carried a `CS8618` in
`Components/Shared/FeatureCard.viu`, and the `[V01.01.14]` arc had changed the application and router
APIs it consumed. The lane was red for failures outside the measurement it was meant to protect.

**Current position (2026-08-08).** The showcase now consumes the hardened application and
`Assimalign.Viu.Browser.Router` surfaces, both `[V01.01.14]` and `[V01.01.15]` are complete, and D6 is
deliberately deferred until a first non-browser host exists. Those conditions no longer justify
waiting to restore the current browser budget. The workflow remains parked while its package step,
budget baselines, and consumer validation are reconciled; restoration is tracked as a
Documentation/Tooling work item.

**What it covers, and what is unguarded while it is parked.**

| Job | Status while parked |
|---|---|
| `budget gate tests` | Unguarded. Self-contained — `scripts/tests/Assimalign.Viu.PublishBudget.Tests` — and could be re-homed into another workflow sooner if wanted. |
| `publish-size + trimming gate` | Unguarded. **Published WASM size and trim-warning regressions are not caught.** This is the real loss. |
| `AOT compilation lane (optional)` | Was already optional. |
| `startup-time gate (deferred)` | Was already skipped, pending the Playwright harness. |

**To restore:** validate the current packaged showcase, revisit
`scripts/budgets/PublishBudgets.json` against the shipping browser framework, repair the package and
consumer lanes, then move the file back. D6 is not a prerequisite for restoring the current browser
gate; a later platform split can rebaseline it again when a second host exists.
