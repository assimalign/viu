---
paths:
  - "**/*.cs"
  - "**/*.csproj"
  - "**/*.props"
  - "**/*.targets"
  - "**/*.ps1"
  - ".github/workflows/**"
  - "**/*.slnx"
---

# Benchmarks and performance budgets

**Every Viu benchmark lives in the sibling `assimalign/viu-benchmarks` repository, and so does every
performance budget that gates on a measured number.** This repository holds the framework; that one
holds the measurement of it. A benchmark added here is a mistake, not a shortcut — there is no
`benchmarks/` project here to add it to, no BenchmarkDotNet package in the central catalog
([build-system.md](build-system.md)), and no workflow that would run it.

## What lives where

`viu-benchmarks` owns:

- `benchmarks/Assimalign.Viu.Testing.Benchmarks` — BenchmarkDotNet wall-clock benchmarks and the
  deterministic interop-count harness, plus its tests.
- `benchmarks/baselines/InteropCounts.json` — the reviewed interop-crossing ceilings.
- `scripts/Measure-PublishBudget.ps1`, `scripts/Test-StartupBudget.ps1`,
  `scripts/budgets/PublishBudgets.json`, and their hermetic tests — the publish-size, trimming, and
  startup gates.
- The `benchmarks` and `budget-gates` workflows that run all of the above.

This repository still owns the *measurement subject*, and that division is deliberate:

- `benchmarks/Assimalign.Viu.Testing.EndToEnd` — the packaged-consumer Playwright harness. It is a
  **test harness, not a benchmark**, and the folder it sits in is a historical name. It is driven by
  `scripts/Test-EndToEnd.ps1` and gated here by `end-to-end.yml`.
- `scripts/Test-EndToEnd.ps1`, `scripts/Install-Local.ps1`, and `scripts/fixtures/**` — packing,
  publishing, serving, and driving a real browser. These need this repository's source, so moving
  them would mean packing the whole framework from a repository that does not contain it.

## Working across the boundary

- The benchmark projects consume Viu as **packages**, never a project reference across repository
  boundaries. CI checks both repositories out as siblings, packs this one at HEAD, and passes the
  packed version through `-p:ViuVersion`, so the interop gate measures the current renderer.
- **A renderer change here can move interop-crossing counts, and nothing in this repository will
  tell you.** The gate runs in `viu-benchmarks` on its own schedule. When a change to
  `libraries/Runtime/**`, `libraries/ServerRenderer/**`, or the reactivity generator is expected to
  change node-operation counts, say so in the pull request and re-run the gate there — a baseline
  edit is a reviewed decision, and the reviewer needs to know a rise is intentional.
- Budget and baseline files are **never ratcheted by CI**. Raising a ceiling is an explicit edit in a
  reviewed pull request, and provenance (revision, SDK, runner, measured value) is recorded beside
  the new number.
- Do not re-add `BenchmarkDotNet`, a `benchmarks/` project, or a budget manifest here to make a local
  investigation easier. Run it in `viu-benchmarks` against a locally packed feed
  (`scripts/Install-Local.ps1`), which is what its CI does. Throwaway measurement that never lands
  belongs in `_out/`, which is gitignored.

Deviating from this rule follows [deviations.md](deviations.md): name it, confirm intent, and say so
in the change summary.
