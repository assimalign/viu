# Item 3 — API hardening completion report

Date: 2026-08-08  
Branch: `feature/V01.01.15-component-model`  
Work item: #316 `[V01.01.15.03]`

Phase A lifted the hardening stop, reevaluated the plan against the landed frame-based component
model, and recorded D10. Phase B completed every surviving item whose public contract was already
determined. No replacement API was invented for a design-dependent row.

## Phase-A dispositions

| Task | Disposition | Evidence / surviving scope |
|---|---|---|
| T02 | Still relevant | PublicAPI enforcement existed; final additions/removals and warning-as-error verification remained. |
| T05 | Already satisfied / superseded | D9 deleted or replaced all six old Core seams; the plan maps each to the shipped surface. |
| T07 | Still relevant, blocked | Remaining prefixes, duplicate facades, constructors, and stuttering factories need one replacement vocabulary. |
| T08 | Still relevant, reduced | Mechanically determined whole-word and Boolean-clarity violations remained. |
| T09 | Still relevant, split | Testing collection ownership and router guard depth were determined; exception-safe batching needs a design choice. |
| T10 | Still relevant, blocked | Typed metadata landed; the remaining renderer host-operation abstraction is undecided. |
| T11 | Still relevant | Async naming, cancellation propagation, and disposable lifetime contracts remained. |
| T12 | Still relevant, reduced | Surviving parser bases and total CSS visitor behavior remained. |
| T14 | Still relevant, split | Raw identity/object APIs were removable; router/history/click/result/options replacements are undecided. |
| T17 | Still relevant | Fresh non-subscribing reads and safe debugger presentation were absent. |
| G1 | Still relevant | Five standalone packages overlapped the App framework without a targeting-pack override manifest. |
| G3 | Still relevant | `SlotFlags` was a three-value classification rather than flags. |
| G4 | Still relevant | Two app-visible value objects lacked matching equality operators. |
| G5 | Still relevant | Current route leaked an invariant mutable-reference contract. |
| D6-A | Still relevant, blocked | Base/browser SDK imports and payload ownership are unresolved. |
| D6-B | Still relevant, blocked | Base/browser targeting, runtime, and reference topology is unresolved. |

The obsolete `RenderHelpers._withHandler` and underscore-helper findings are superseded by D9
because the static helper ABI no longer exists. KeepAlive weak-input decoding remains protected by
`[BLT-6]`, lazy Suspense slots by `[BLT-11]`, and `RouterLinkClickEvent` by `[RTR-1]`/`[RTR-7]`.

## Phase-B outcomes

| Item | Outcome |
|---|---|
| T08 | Renamed the determined patch, reactivity, browser diagnostic, state, router, syntax, SSR, and server-rendering surface; updated generators, fixtures, docs, tests, and API baselines. |
| G3 | Renamed `SlotFlags` to `SlotStability`; preserved `Stable = 1`, `Dynamic = 2`, `Forwarded = 3` and all linked-source consumers. |
| T09-A | `TestElement` now exposes read-only live views over private mutable stores; mutation attempts and renderer updates are pinned. |
| T09-B | Both router guard registration overloads throw `ArgumentOutOfRangeException` for invalid depths, including negative, equal-to-count, and greater-than-count cases. |
| T09-C | Blocked; exception-safe batch API shape is undecided. |
| T10 | Blocked; renderer host operations have several viable public shapes. |
| T11 | Added `NextTickAsync`, cancellable `PushAsync`/`ReplaceAsync`, cancellable route-enter guards, async Testing names, terminal/idempotent disposable histories, and `ReactiveEffect.Dispose()`. Router cancellation now covers redirects, overlapping pops, compensation ownership, and disposal races. |
| G4 | Added null-safe `==` and `!=` for `RouteLocation` and `RouteParameters`. |
| G5 | Added covariant `IReactiveReadOnlyReference<out T>` and exposed `Router.CurrentRoute` through it. |
| T12 | Closed parser-root construction to the owning assembly and made CSS writers/rewriters explicit and total, including nested keyframes and unsupported-node failures. |
| T14-A | Removed generic identity `Reactive.ToRaw<T>` and `IReactiveObject.ToRaw`; retained observably different collection conversions and generated typed raw-value views. |
| T14-B | Blocked; replacement router/history/click/result and TestRenderer option contracts are undecided. |
| T17 | Added fresh non-subscribing `Peek()` with tracking restoration, run-count tests, and debugger displays that read backing state or `Peek()`. |
| G1 | Added the exact five-package `PackageOverrides.txt` contract to App.Ref only; archive checks cover stable/prerelease versions. The packaged SDK fixture explicitly overlaps all five packages and proves framework conflict resolution. |
| T02 | Updated final `PublicAPI.Unshipped.txt` additions/removals for Browser, Components, Core, Reactivity, Router, ServerRenderer, State, and Testing; shipped baselines remain unchanged because the surface is not released. |
| T07 | Blocked; replacement naming and factory boundaries are undecided. |
| D6-A / D6-B | Blocked; SDK/framework segmentation topology is undecided. |

## Gates

| Gate | Result |
|---|---|
| `dotnet build Assimalign.Viu.slnx -warnaserror` | PASS — 0 warnings, 0 errors. |
| `dotnet test Assimalign.Viu.slnx --no-build` | PASS — 2,600 passed, 0 failed, 0 skipped; +33 from the 2,567 baseline. |
| PublicAPI RS0016 / RS0017 / RS0037 | PASS — changed packable projects and final solution warning-as-error build are clean. |
| D8 friend-assembly scan | PASS — 17 grants, all owning-test grants; 0 production/cross-library grants. |
| Hardening-plan state scan | PASS — 0 ambiguous rows; only DONE, DROPPED, SUPERSEDED, or BLOCKED-NEEDS-DECISION. |
| Pack archives and consumer | PASS — App.Ref carries the exact five overrides, Runtime carries none; isolated packaged consumer passes Build, PublishTrimmed, and PublishAot at `10.0.1-preview.2`. |
| `git diff --check` | PASS — no whitespace errors. |

## Specification clauses touched

`[CMP-18]`, `[CMP-19]`, `[RCT-1]`, `[RCT-5]`, `[RND-FLAGS-1]`, `[RND-FLAGS-2]`,
`[RND-FLAGS-5]`, `[RND-FLAGS-6]`, `[RND-PATCH-4]`, `[SCH-9]`, `[SCH-12]`,
`[SFC-DIAG-3]`, `[STY-8]`, `[RTR-2]`, `[RTR-3]`, `[RTR-4]`, `[RTR-6]`, `[PKG-2]`,
and `[CONF-3]`.

## Pre-completion checklist

- Build/test/PublicAPI: PASS; solution is 0/0 and all 2,600 tests pass.
- Runtime/package consumer: PASS; trimmed and AOT browser-WASM publication succeeds from packages.
- Structure/naming/docs: PASS; linked paths, numeric contracts, XML docs, tests, and specification moved together.
- AOT/trimming and D8: PASS; no reflection/dynamic-code path added and no cross-library friend grant exists.
- Work tracking: PASS; #316 / `[V01.01.15.03]` and `[V01.01.14]` remain the governing work items.

## REMAINING

Only explicit public-design decisions remain; Phase B has no unfinished determined implementation:

1. **T07:** choose one replacement vocabulary/factory boundary for prefixed, duplicate, constructor,
   and stuttering APIs.
2. **T09-C:** choose raw batch pairing, an exception-safe disposable batch scope, or callback batching.
3. **T10:** choose named delegates, a host-operations interface, or an intentional delegate bag for
   `RendererOptions<TNode>`.
4. **T14-B:** choose value/options replacements for router history state/Booleans,
   `RouterLinkClickEvent`, navigation results, and TestRenderer ordering.
5. **D6-A:** decide whether the Browser SDK depends on a thin base SDK or owns a self-contained
   payload, including CSS/static-asset/watch ownership.
6. **D6-B:** decide base/browser framework reference, runtime-pack, re-export, and ServerRenderer
   delivery topology.
