# Framework ownership, editor quality, examples, and performance

Owner-requested initiative, 2026-10-22. Project #15 and linked issue requirements remain
authoritative. Implementations remain In progress until merge. Work uses the root checkouts;
no worktrees are required.

| Work | Tracking | Implementation |
| --- | --- | --- |
| Framework ownership | [#381](https://github.com/assimalign/viu/issues/381) | Runtime and Browser own their pack producers and build files; Runtime owns the single shared writer. |
| Visual Studio editor | [#382](https://github.com/assimalign/viu/issues/382) | Stable namespace classification during edits, caret-aware dotted completion, and cached-span binary search. |
| Board reconciliation | [Audit](BOARD-AUDIT-2026-10-22.md) | Completed work reconciled with issue state; unresolved acceptance gaps retained. |
| Focused examples | [#385](https://github.com/assimalign/viu/issues/385) | Independent feature examples compose Cohesion ViuApplicationBuilder; existing StandAlone remains unchanged. |
| Scoped styles | [#383](https://github.com/assimalign/viu/issues/383) | Parsing, selector/keyframe rewriting, CSS Modules composition, native slot ownership, SSR, editor vocabulary and hot reload. |
| Performance infrastructure | [#384](https://github.com/assimalign/viu/issues/384), [#88](https://github.com/assimalign/viu/issues/88), [#225](https://github.com/assimalign/viu/issues/225) | Area suites, trimmed WASM scenarios, command-frame counts, payload reports, comparison/history tooling and separate CI host legs. |
| Correctness fixes | [#386](https://github.com/assimalign/viu/issues/386), [#387](https://github.com/assimalign/viu/issues/387), [#374](https://github.com/assimalign/viu/issues/374) | Dynamic class/style lowering, SSR normalization, reactive Transition slots and typed built-in definitions. |
| Test and benchmark layout | [#390](https://github.com/assimalign/viu/issues/390), [#391](https://github.com/assimalign/viu/issues/391) | Generated measurements stay in artifacts; benchmark harnesses use src/test; Viu's browser harness uses tests/EndToEnd. |

## Ownership and behavior

Framework producers live under `libraries/Runtime/Assimalign.Viu.App.Refs` and
`libraries/Browser/Assimalign.Viu.App.Browser.{Refs,Runtime}`. The Runtime area owns
`Assimalign.Viu.App.props`, `Assimalign.Viu.App.Pack.props` and the single
`Assimalign.Viu.App.targets` writer. Browser owns `Assimalign.Viu.App.Browser.props` and imports
the base manifest and writer. This narrowly scoped exception to the central build-file location
rule is authorized by the owner (#381); repository-wide build logic remains in `build/`.
Package identities, framework composition and the targeting-only base are preserved.

The packaged browser test harness lives at `tests/EndToEnd/Assimalign.Viu.Testing.EndToEnd` and
is driven by `scripts/Test-EndToEnd.ps1`. Viu has no `benchmarks/` root. The sibling
`viu-benchmarks` repository owns performance suites, reviewed baselines, budgets and reporting.
Its deterministic gate and reusable fixtures live in
`harness/Assimalign.Viu.Testing.Benchmarks/{src,test}`.

Scoped styles supersede the removal decision of 2026-09-14. [STY-1] defines compile-time native
ownership, native slot-root projection and component boundaries. Selector escapes and keyframes
are rewritten; component invocation boundaries are not implicitly restamped. Reactive CSS-variable
application under [STY-6] through [STY-8] remains deferred.

The renderer skips parent/sibling reads only for stable native handles or an unchanged node instance.
Type/key replacements retain their actual parent and anchor. Shared empty dictionary snapshots
remain immutable and independent of later caller mutations.

## Verification and measurement policy

Build the complete solution, run affected tests, pack the SDK/framework set, validate packaged
consumers and execute the browser and hot-reload checks after changing the owning paths. Commands
and CI entry points live in each repository's README. The examples catalog and coverage map live
in `viu-examples/examples/Features/`.

Generated benchmark reports, history snapshots, payload measurements and machine-specific result
summaries are not source inputs. Keep local output in ignored `_out/` or `.artifacts/` and use CI
artifacts for shared runs. Reviewed baseline and budget manifests remain versioned configuration;
scripts never rewrite those ceilings from measured output.

The VSIX still needs interactive Visual Studio latency assessment. Packaged editor requests do not
measure editor painting or server allocations. Keep JIT, NativeAOT and browser measurements in
separate provenance legs; local execution does not establish remote CI status or a portable timing
speedup. The board audit retains unresolved performance and product requirements.
