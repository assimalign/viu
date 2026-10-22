# Viu Project #15 audit — 2026-10-22

This audit compares issue acceptance criteria with main, local source, tests, and packaged consumers.
Project status is delivery state: changes on the initiative branch remain
**In progress**. A passing smoke test does not complete unrelated acceptance criteria.

## Reconciled delivery

Seventeen already-closed issues still marked In progress were moved to Done:
#61, #67, #74, #92, #93, #94, #204, #210, #211, #212, #215, #218, #307, #320, #327, #329, #332.

Three open items were closed and moved to Done after verifying existing main implementation:

| Issue | Evidence |
| --- | --- |
| [#361](https://github.com/assimalign/viu/issues/361), packaged file routing | Runtime, generator, and compiled-fixture tests; release package inventory; generated Pages consumer and real browser navigation. |
| [#227](https://github.com/assimalign/viu/issues/227), C# component authoring | Current `[Parameter]`, `[Event]`, `OnSetup`, and context lifecycle/watch contracts replace historical API spellings; generator and compiled component fixtures pin the supported model. |
| [#187](https://github.com/assimalign/viu/issues/187), browser history asset | Browser.Router owns its opt-in `_content/Assimalign.Viu.Browser.Router/viu-history.js`; web/hash histories, links, navigation, and back behavior pass in packaged browser examples. |

[#88](https://github.com/assimalign/viu/issues/88) was reopened: main's browser lane was a placeholder,
with no real scenario timings or per-assembly payload breakdown. This branch supplies the missing
measurements and gates. [#374](https://github.com/assimalign/viu/issues/374) remains open after correcting
an initial audit closure: generated definitions existed, but four built-ins did not implement
`IComponentDefinition`. Their local implementation and constrained-call tests now pass. The current
contract name is deliberate; the older issue's proposed `IRegisteredComponent` name is historical.

Snapshot after reconciliation: **274 project items: 224 Done, 40 Backlog, 10 In progress**. This
includes pull requests as well as issues. No foreign repository items were found; no closed-issue /
project-status mismatches remain. A Dependabot pull request arrived during the audit and was left
with its existing status. Snapshots under ignored `_out/initiative/` are evidence, not a second backlog.

## Initiative branch work

All entries below remain In progress until delivery is reviewed and merged.

| Issue | Local result |
| --- | --- |
| [#381](https://github.com/assimalign/viu/issues/381) | Framework producer relocation, central shared manifests, package and consumer validation. |
| [#382](https://github.com/assimalign/viu/issues/382) | Stable namespace classification, correct completion replacement inside a token, bounded cached-span lookup, packaged VSIX. Installed Visual Studio interaction is still unverified. |
| [#383](https://github.com/assimalign/viu/issues/383) | Scoped CSS compilation, native/slot ownership, selector escapes, SSR, editor vocabulary, hot reload, tests and examples. |
| [#384](https://github.com/assimalign/viu/issues/384), [#88](https://github.com/assimalign/viu/issues/88), [#225](https://github.com/assimalign/viu/issues/225) | Nine benchmark areas, real WASM scenarios and frame gates, per-assembly payload, history/comparison tooling, allocation and renderer optimizations. |
| [#385](https://github.com/assimalign/viu/issues/385) | 65 focused examples use Cohesion ViuApplicationBuilder: 46 browser and 19 console cases, plus existing composition examples. |
| [#386](https://github.com/assimalign/viu/issues/386) | Dynamic class/style bindings now reach host property patching and normalize in SSR; compiled/browser regressions pass. |
| [#387](https://github.com/assimalign/viu/issues/387) | Browser Transition tracks reactive reads made only by its default slot; compiled repeated-toggle and browser examples pass. |
| [#374](https://github.com/assimalign/viu/issues/374) | Transition, TransitionGroup, RouterView, and RouterLink implement the existing typed definition contract. |

## Outstanding acceptance criteria

These items remain open. Evidence of a related feature is insufficient to close the specific gap.

| Issue(s) | Outstanding work / reason retained |
| --- | --- |
| #380 | Ordinary-element conditional sibling fallback still emits a mixed-key warning; visible E2E behavior alone does not satisfy warning-free identity requirements. |
| #378 | Reproduce and fix the parallel Rebuild / RunHost output cleanup race. Ordinary Build passes do not exercise it. |
| #372 | Intermittent Playwright reload-race double handling needs a repeatable fix. One passing hot-reload matrix is insufficient. |
| #371 | DevTools client release inventory, extension manifest, relay, and CI scenario remain incomplete. Protocol examples cover only the existing package. |
| #369 | Actual VS Code editor-host smoke and VSIX artifact CI requirements remain separate from unit/LSP process checks. |
| #362 | Cohesion Markdown content bridge. |
| #358 | Complete the published-feed migration across both platform/docs consumers; one migrated consumer does not complete both. |
| #353 | Dedicated utility build-time, size, and payload-purity budgets. General WASM budgets do not substitute. |
| #351 | State-preserving in-place hot reload; current remount behavior remains. |
| #348, #347 | Real Visual Studio `.razor` attachment and multiple-client LSP probes. |
| #343 | Per-identifier render source mappings; root-expression mappings are insufficient. |
| #342 | Record the semantic component-argument typing decision and implement its chosen contract. |
| #339 | Discover arbitrary code-first registrations and validate their parameter/event contracts statically; built-in metadata does not cover this. |
| #263 | Evaluate and record the fast text-mount decision with measured creation costs. Actual command-frame batching changes the cost model; operation counts remain a separate axis. |
| #222 | Defer popstate listener setup until initial navigation settles. |
| #207 | Transition child attribute fallthrough. |
| #205 | Resolve asynchronous component names for KeepAlive filters. |
| #202 | Compiler injection of persisted transition behavior around a single visibility directive. |
| #197 | Relative named-navigation parameter inheritance and dynamic route removal. |
| #194 | Teleport target SVG/MathML namespace lookup. |
| #189 | Source-generated state applier for reactive stores. |
| #166 | Owning diagnostic catalog in style-origin envelopes. |
| #165 | CSS-variable getter line mappings; scoped CSS restoration does not implement the deferred reactive CSS-variable feature. |
| #104 | Existing Cohesion compositions build, but full integration acceptance across the platform bridge remains unverified. |
| #102 | Documentation site deployment, search, link checking, and SSG acceptance need verification in the owning repository. |
| #1, #6, #16, #38, #47, #56, #63, #69, #75, #80, #84, #89, #96, #97 | Parent initiatives remain open while child requirements or their own acceptance criteria remain outstanding. |

The live [Viu board](https://github.com/orgs/assimalign/projects/15) and issue bodies remain authoritative.
Follow-up work uses the existing issues above rather than duplicate tickets.
