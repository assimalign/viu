# Disabled workflows

GitHub only schedules workflows found in `.github/workflows/`. A file here is **preserved but never
runs** — it is parked, not deleted, and each entry below records why and what would bring it back.

Move a file back into `.github/workflows/` to re-enable it. Nothing else is required.

A parked file still references its **original** `.github/workflows/…` path in its own `paths:` filters.
That is deliberate, not an oversight: the filter is inert while the file is parked, and correct the
moment it is moved back. Leave it alone.

## `budget-gates.yml`

Parked 2026-08-06, pending the SDK and framework segmentation recorded as **D6** in
[`docs/API-HARDENING-PLAN.md`](../../docs/API-HARDENING-PLAN.md).

**Why.** Its `publish-size + trimming gate` job publishes the packaged-consumer showcase from the
sibling `viu-examples` repository and enforces `scripts/budgets/PublishBudgets.json`. That showcase
currently does not build: it carries a pre-existing `CS8618` in `Components/Shared/FeatureCard.viu`,
and the `[V01.01.14]` arc changed the API it consumes — `IApplicationPlugin` was deleted, the
application entry point became `new BrowserApplicationBuilder()`, and `Assimalign.Viu.Router.Browser`
was renamed to `Assimalign.Viu.Browser.Router`. The gate has therefore been red on every commit for
reasons that live outside this repository, which trains everyone to ignore a failing lane — the worst
state for a gate to be in.

**Why parked rather than fixed now.** D6 splits `Assimalign.Viu.Sdk`/`Assimalign.Viu.App` into
platform-agnostic and browser-specific halves. Publish size, trimming behaviour, and the AOT lane are
all measured against the browser app, so the budgets and the pack step this workflow performs will need
rewriting once that split lands. Repointing it at the current shape would be work done twice.

**What it covers, and what is unguarded while it is parked.**

| Job | Status while parked |
|---|---|
| `budget gate tests` | Unguarded. Self-contained — `scripts/tests/Assimalign.Viu.PublishBudget.Tests` — and could be re-homed into another workflow sooner if wanted. |
| `publish-size + trimming gate` | Unguarded. **Published WASM size and trim-warning regressions are not caught.** This is the real loss. |
| `AOT compilation lane (optional)` | Was already optional. |
| `startup-time gate (deferred)` | Was already skipped, pending the Playwright harness. |

**To restore:** migrate `viu-examples` to the current API, settle D6, revisit the budget values in
`scripts/budgets/PublishBudgets.json` against the segmented framework, then move the file back.
