# External performance research

## 1. Purpose, and what this document is not

Viu evaluates the performance work published by other UI frameworks and adopts techniques that
survive measurement against Viu's own baselines. This document is the ledger for that evaluation:
what was reviewed, what was found, what was measured, and what was decided.

> **This document is non-normative.** Nothing recorded here obligates Viu, describes Viu's behavior,
> or makes any external project authoritative for Viu's semantics. Viu's semantics are defined by
> [`docs/SPECIFICATION.md`](SPECIFICATION.md) alone (clauses `[PERF-1]`–`[PERF-4]`). **A finding in
> this file becomes part of Viu only when it lands in the specification or an ADR**, and only after
> it is implemented and pinned by a test.

The separation is deliberate. The specification is normative and slow-changing; this ledger is
append-only and fast-changing. Recording external observations *inside* the normative document would
re-couple Viu's semantics to another project's release cadence — the exact coupling the 2026-08-02
standalone-framework decision removed. Keeping them apart also makes the boundary legible to a
reader: everything in `SPECIFICATION.md` is Viu's own; everything here is an external input under
evaluation.

## 2. Scope

**In scope:** performance and implementation-strategy observations — algorithmic improvements,
allocation reductions, data-layout changes, batching strategies, measurement methodology, and CI
gate design.

**Out of scope, and it must not be raised through this channel:** semantic, API, or behavioral
parity with any external project. "Framework X changed how Y behaves" is not a finding. If a
behavioral change is wanted for Viu, it is a Viu design decision, argued on Viu's terms, filed as a
Viu work item, and specified in `SPECIFICATION.md`.

This guard rail is the point of §2. Without it the ledger becomes a parity backlog by drift.

## 3. What gets reviewed

Projects whose renderer, reactivity, or compiler performance work is worth surveying. This list is
open; add a row when a project starts producing relevant work, and note why.

| Project | Why it is surveyed | What to look at |
| --- | --- | --- |
| Vue.js (`vuejs/core`) | Its compiler-informed rendering strategy is the same architectural family Viu independently adopts, so its optimizations are unusually often applicable | Release notes and PRs touching the renderer, scheduler, reactivity engine, and compiler optimization passes |
| Blazor (`dotnet/aspnetcore`) | The closest managed↔host boundary problem; prior art for batched render diffs on .NET | Render-batch format, diff algorithm, WASM startup and size work |
| Solid / Svelte | Compile-time-heavy strategies with different trade-offs from Viu's | Compiler output shape, update-granularity techniques |
| .NET runtime and WASM tooling | Directly changes Viu's cost model | mono-wasm/NativeAOT codegen, trimming, startup, interop marshaling |

## 4. Review cadence

- **Quarterly**, on the first Monday of January, April, July, and October. The heartbeat matters more
  than the date; a predictable cadence is what makes a quiet quarter distinguishable from a forgotten
  one.
- **Out of band on any minor release** of a surveyed project, because a minor release is the actual
  delivery vehicle for optimizations.

**Owner:** the Testing and benchmarks area (`V01.01.11`), which already owns the measurement gates a
finding has to clear.

A review that raises nothing MUST still be recorded in [§6](#6-review-log). "No findings" is a
result; a missing row is not.

## 5. Findings ledger

Append only. Never edit a decided row — supersede it with a new row that cites the old one.

| Date | Source (project + version + link) | Observed technique | Applicability to Viu | Measured impact | Decision | Work item |
| --- | --- | --- | --- | --- | --- | --- |
| _(no findings recorded yet)_ | | | | | | |

### Rules for a row

1. **Source** names the project, the exact version or commit, and a link. "Recent Vue" is not a
   source.
2. **Applicability** must reason from **Viu's own constraints** — the interop budget, AOT and
   trimming, the absence of an object-proxy layer, immutable render trees, single-threaded
   execution — not from architectural similarity. "They do it and we look like them" is not an
   applicability argument; "this removes N interop crossings per keyed reorder, and our command
   buffer already carries the opcode" is.
3. **Measured impact** MUST cite a run against `benchmarks/Assimalign.Viu.Testing.Benchmarks` and/or
   `benchmarks/baselines/InteropCounts.json`. **An unmeasured finding cannot be adopted**
   (`[PERF-3]`). Record the harness, the machine class, and whether the run was AOT or interpreted.
4. **Decision** is exactly one of `Adopted`, `Rejected`, `Deferred`, `Not applicable`, with a
   one-line reason. `Deferred` MUST name what it is waiting on.
5. **Work item** is the `[V01.01.NN…]` issue. An adopted finding without a work item is not adopted;
   it is an intention.

### What "adopted" means

An adopted finding becomes Viu's own design decision. Concretely:

1. a work item is filed (use the `viu-work-items` skill);
2. the implementation is documented **in Viu's terms** — no doc comment, test comment, or DESIGN.md
   section attributes the behavior to the source project
   ([`.claude/rules/documentation.md`](../.claude/rules/documentation.md));
3. the behavior is pinned by a Viu test and, where it is observable, a `SPECIFICATION.md` clause;
4. the benchmark delta that justified it is recorded in the baseline files, so a later regression is
   caught;
5. if the technique is architecturally significant, an ADR records it.

Origin acknowledgement, where it is wanted, goes in `SPECIFICATION.md` §19 "Prior art and
influences" — once, centrally — never scattered through member documentation.

## 6. Review log

Every scheduled and out-of-band review gets a row, including reviews that found nothing.

| Date | Reviewer | Trigger | Versions surveyed | Findings raised |
| --- | --- | --- | --- | --- |
| _(no reviews recorded yet)_ | | | | |

## 7. Related

- [`docs/SPECIFICATION.md`](SPECIFICATION.md) §18 — the normative statement this document implements.
- [`docs/adr/`](adr/) — where an architecturally significant adoption is recorded.
- `benchmarks/Assimalign.Viu.Testing.Benchmarks`, `benchmarks/baselines/InteropCounts.json`,
  `scripts/Measure-PublishBudget.ps1` — the measurement gates a finding must clear.
- [`.claude/rules/documentation.md`](../.claude/rules/documentation.md) — why an adopted technique is
  documented in Viu's terms.
