# Disabled workflows

GitHub only schedules workflows found in `.github/workflows/`. A file here is **preserved but never
runs** — it is parked, not deleted, and each entry below records why and what would bring it back.

Move a file back into `.github/workflows/` to re-enable it. Nothing else is required.

A parked file still references its **original** `.github/workflows/…` path in its own `paths:` filters.
That is deliberate, not an oversight: the filter is inert while the file is parked, and correct the
moment it is moved back. Leave it alone.

## Currently parked

None. The last parked workflow, `budget-gates.yml`, was re-activated into
`.github/workflows/budget-gates.yml` on 2026-08-09 by `[V01.01.12.26]` (#320) after
`scripts/budgets/PublishBudgets.json` was re-baselined against the measured `EndToEndBrowserApp`
packaged-consumer fixture (size and `boot-to-interactive` startup, with recorded provenance) and
both checkers plus deliberate regressions were verified. Its parked-era rationale lives in this
file's git history. It has since moved, with the benchmark suite, to the sibling
`viu-benchmarks` repository.
