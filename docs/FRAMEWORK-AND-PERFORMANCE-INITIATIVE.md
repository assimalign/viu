# Framework ownership, editor quality, examples, and performance

Owner-requested initiative, 2026-10-22. Work runs directly in the root checkouts of `viu`,
`viu-examples`, and `viu-benchmarks` on `feature/V01.01.12.37-framework-and-performance`.
GitHub status reconciliation is applied; implementations remain In progress until merge.
The verification record below describes local runs; branch delivery is recorded by Git. Project #15 and linked issue requirements remain
authoritative. No worktrees were created.

| Work | Tracking | Result |
| --- | --- | --- |
| Runtime and Browser framework ownership | [#381](https://github.com/assimalign/viu/issues/381) | Producers relocated; shared build implementation centralized; all nineteen current-version packages packed; external consumers and WASM publish pass. |
| Visual Studio editor | [#382](https://github.com/assimalign/viu/issues/382) | Namespace classification stays consistent between lexical and semantic results; completion replaces the current dotted identifier, including its suffix; cached span queries use binary search. Six language-service regressions and the extension test suite pass; actual VSIX and packed C# semantic round trip pass. |
| Board reconciliation | [Audit](BOARD-AUDIT-2026-10-22.md) | Seventeen stale closed-item statuses corrected; three existing main features closed with evidence; #88 reopened for unmet acceptance; #374 remains open with missing built-in contracts implemented locally. All other open items reviewed and retained with reasons. |
| Focused examples | [#385](https://github.com/assimalign/viu/issues/385) | Sixty-five independent examples use Cohesion ViuApplicationBuilder: forty-six browser and nineteen console. Feature-family coverage and a smoke runner accompany the catalog. Existing composition examples, including StandAlone and Web hosting, are retained. Source and project XML are formatted for readability. |
| Scoped styles | [#383](https://github.com/assimalign/viu/issues/383) | Parser, selector rewriting, compiler attributes, CSS Modules composition, native slot-root ownership, SSR, editor vocabulary and scoped stylesheet hot reload restored. Contract: [STY-1]. |
| Area benchmarks and measured improvements | [#384](https://github.com/assimalign/viu/issues/384), [#88](https://github.com/assimalign/viu/issues/88), [#225](https://github.com/assimalign/viu/issues/225) | Nine areas / seventy-three executable benchmark cases; one NativeAOT case; real trimmed WASM times, command frames and per-assembly payload; comparison/history viewer and CI legs. Empty invocation allocation 184 → 64 bytes; partial-update command frames 101 → 2. |
| Discovered correctness gaps | [#386](https://github.com/assimalign/viu/issues/386), [#387](https://github.com/assimalign/viu/issues/387), [#374](https://github.com/assimalign/viu/issues/374) | Dynamic class/style host-property lowering and SSR normalization fixed; Transition tracks slot-only references; four built-ins implement the existing typed definition contract. Compiled fixtures and constrained-call tests pass. |

## Ownership and behavior

Framework producers live under `libraries/Runtime/Assimalign.Viu.App.Refs` and
`libraries/Browser/Assimalign.Viu.App.Browser.{Refs,Runtime}`. Shared manifests, producer defaults,
and pack implementation live under `build/Frameworks/`, following the central build rule. Package
identities, framework composition, and the targeting-only base stay the same. Discovery excludes
explicit framework containers from independent library packages. The old root `frameworks/` no
longer exists; two historical generated packages were retained under ignored `_out/initiative/`.

Scoped styles supersede the 2026-09-14 removal decision. [STY-1] defines compile-time native
ownership, native slot-root projection, and component boundaries. CSS `:deep`, `:global`, `:slotted`,
and keyframe names are rewritten. Component invocation boundaries are not implicitly restamped.
The separate deferred reactive CSS-variable application in [STY-6] through [STY-8] remains deferred.

The renderer optimization skips host-parent/sibling reads only for existing stable native handles
or an unchanged node instance. Type/key replacements retain their real parent and anchor. Shared
empty dictionary snapshots stay immutable and independent of later caller mutations. Larger
array/list snapshot attempts were measured and rejected.

## Verification

| Check | Result |
| --- | --- |
| Complete Viu solution build | Zero warnings, zero errors. |
| Viu tests | 4,092 passing across 35 projects, including the 300-test Visual Studio extension project. |
| Package inventory | All 19 current-version packages: 13 independent libraries, two SDKs, two targeting packs, one Browser runtime pack, and templates. |
| External examples | All 65 focused examples pass; existing three composition examples and their reusable libraries build with published package pins. |
| Packaged Chromium end-to-end | Eleven published/trimmed scenarios pass, including hydration, static prerender, and custom-element cleanup. |
| Hot reload | Eleven Debug browser scenarios pass, including a real scoped `.vue` stylesheet and the Visual Studio-shaped run-host session. |
| Startup | One warmup plus ten fresh-context measurements: 1,252.2 ms median; existing effective ceiling 2,000 ms. |
| Publish size | EndToEndBrowserApp: 2,505,716 brotli bytes; existing ceiling 2,700,000 bytes. |
| Benchmarks | Nine areas / 73 cases execute in dry mode; one NativeAOT case executes; 23 legacy harness plus 31 report/payload/gate tests pass; all nine existing deterministic proxy scenarios stay within baseline. |
| Actual WASM measurement | Eight scenarios, three warmups / twenty samples each; two command frames in every measured sample. Dedicated subject: 8,087,771 raw / 2,341,520 brotli bytes, seventeen managed assemblies. |
| Editor package | VSIX built with win-arm64 and win-x64 single-file server payloads; project-backed semantic round trip passes; ten packaged-server benchmark cases pass. |

Full methodology, individual browser samples, allocation evidence, and coverage limits are in
`viu-benchmarks/docs/PERFORMANCE-RESULTS-2026-10-22.md` and its machine-readable result file. The
examples catalog and coverage map live in `viu-examples/examples/Features/`. Commands and CI
entry points are documented in each repository's README.

## Limits and remaining work

The VSIX has not been installed or tested interactively in Visual Studio. The editor benchmarks
measure server requests and client allocations; they do not measure editor paint latency or server
memory. GitHub CI definitions were parsed locally; remote CI results are outside this local verification record. Browser AOT was not measured; the NativeAOT result covers one opted-in CoreCLR benchmark.
No portable or causal timing speedup is claimed from exploratory runs taken under varying host load.
The performance findings document lists the next measurements, and the board audit retains their
existing backlog requirements rather than claiming every potential performance gap is resolved.

## Pre-completion checklist

- ✅ Complete solution build with zero warnings/errors; affected tests and reactive run-count contracts pass.
- ✅ Packaged consumer WASM builds, trimming, real browser behavior, hot reload and existing budgets pass.
- ✅ Moved projects follow area / assembly / src layout; source links, solution, CI and pack inventories updated.
- ✅ Public interfaces, internal types, file-scoped namespaces, explicit usings and whole-word names retained.
- ✅ One public type per file and existing generic filename conventions retained.
- ✅ Shared build logic centralized; named references retained; existing framework build-order exceptions documented at producer sites.
- ✅ Shipping library AOT settings and non-packable test settings retained; no new reflection activation or serialization introduced into Viu.
- ✅ Public API baselines restored with scoped APIs; XML documentation and specification updated with behavior and ownership.
- ✅ JS handles and listeners retain their existing cleanup paths; packaged teardown scenarios pass.
- ✅ Work items referenced; discovered defects tracked and parented through the viu-work-items workflow.
- ✅ No dangling framework paths or project references remain; `git diff --check` passes in all three repositories.
- N/A New independent runtime package or new public abstraction: none introduced by this initiative.

Logs and transient investigations live under ignored `_out/initiative/` and sibling `.artifacts/`.
Validation does not imply a commit, push, merge, installed extension, remote CI result, or published release.
