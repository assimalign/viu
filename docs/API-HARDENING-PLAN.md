# API surface hardening plan — [V01.01.14]

**Status: ARC OPEN. Wave 1 in progress on `feature/V01.01.14-api-surface-hardening`.**

This document is the session-independent source of truth for the API-hardening arc. Any session
(human or agent) resuming this work reads this file first, checks the *State* table, and continues
from the first incomplete unit. Update the State table in the same commit as any unit's progress.

Produced from a 32-agent audit on 2026-08-05 (15 parallel scopes → per-scope adversarial verification
→ synthesis → completeness critic): 147 raw findings, 3 refuted, merged into 17 themes plus 9 gap
findings. Direction set by Chase.

## Motivation

The source is unusually disciplined — sealing is effectively 100% (122 `public sealed class` +
124 `public sealed record` against 2 deliberate unsealed publics), ~95% of public members carry XML
docs, `Try*` follows the BCL convention 38/38, and there are zero ALL_CAPS or trailing-underscore
publics. What is missing is **delivery and enforcement**, not craft:

- `[EditorBrowsable]`, `[Obsolete]`, `[DebuggerDisplay]` — **zero usages repo-wide**.
- `GenerateDocumentationFile` was set nowhere, so all 22 packages shipped a bare DLL.
- No `PublicAPI.Shipped.txt`, no public-API analyzer.

`public` is a zero-friction keyword here because nothing mechanically surfaces a surface change.
Every theme below exists downstream of that.

## Ground truth — what an app developer actually sees

`frameworks/Assimalign.Viu.App.props:57-64` is authoritative. The shared framework ships **seven**
assemblies that every Viu app references with zero opt-in: `Assimalign.Viu.App`, `.Shared`,
`.Components`, `.Reactivity`, `.State`, `.Core`, `.Browser`.

**Not** in the app reference set: the `Syntax.*` parsers and `Tooling.*` (they reach consumers only
inside `analyzers/dotnet/cs/` of the Ref pack), `ServerRenderer`, `Router`/`Router.Browser` (opt-in
packages), and `Testing` (dev-time).

Raw public-type counts badly misrank the work: `Syntax.Templates`' 59 public types are build-time
only, while `Shared`'s 11 are in every app's IntelliSense. Prioritize the seven.

### Verified dependency layering

```
Shared ── Components ── Core ── Browser          <- browser host
   |          |           +---- ServerRenderer   <- server host
   |          +-- Reactivity
   |          +-- State
   +--------- Router                             <- host-agnostic (Router.Browser is its bridge)
```

`Shared`, `Reactivity`, `State`, `Components`, `Core` are all **host-agnostic**. This is why the
packaging decision below matters: a component-library author must be able to reference them without
taking `Browser` and its `browser-wasm` runtime pin.

## Decisions on record

| # | Decision | Date | Rationale |
|---|---|---|---|
| D1 | **Nothing has shipped publicly.** No `[Obsolete]` shims are required anywhere in this arc; renames are plain renames and dead code is deleted outright. | 2026-08-05 | SDK, framework, extensions and libraries are all unreleased. Revisit the moment the first public release goes out. |
| D2 | **Packaging: publish the six framework libraries standalone AND emit `data/PackageOverrides.txt`.** | 2026-08-05 | A component library must be able to `PackageReference` the host-agnostic set from a stock `Microsoft.NET.Sdk` project. Framework-only delivery would force a `FrameworkReference` on `Assimalign.Viu.App`, pulling `Browser` and pinning `browser-wasm`. PackageOverrides makes the `lib/` asset inert on the SDK path. This is the `Microsoft.Extensions.*` model. |
| D3 | **Delete `Assimalign.Viu.Syntax.JavaScript`; keep `Assimalign.Viu.Syntax.Html`.** | 2026-08-05 | Both are scaffolds today, but Html is the base for an in-flight POC supporting raw HTML rendering at runtime, external to the build process. Html's packaging is therefore left alone rather than blanket-unshipped — it may need to ship as a runtime library. |
| D4 | **Work tracking: area epic + this plan doc; feature items created just before each wave starts.** | 2026-08-05 | Avoids writing five detailed specs up front that later drift. The GitHub issue body stays the authoritative requirement source for the wave in flight. |

## State

| Unit | Theme | Wave | Status |
|---|---|---|---|
| `V01.01.14.01` | T01 ship XML documentation + T15 derivative doc phrasing | 1 | **IN PROGRESS** |
| `V01.01.14.02` | T04 compiler-seam hardening (`[EditorBrowsable]` + language-service filter) | 1 | Not started |
| `V01.01.14.03` | T03 unship build-time packages | 1 | Not started |
| `V01.01.14.04` | T02 public-API baseline and hardening-attribute conventions | 1 | Not started |
| `V01.01.14.05` | T05 internalize friend-only publics (~120 types) | 2 | Not started |
| `V01.01.14.06` | T13 single source of truth for the helper-name contract | 2 | Not started |
| `V01.01.14.07` | T16 drop the viral `RequiresPreviewFeatures` stamp | 2 | Not started |
| `V01.01.14.08` | T06 namespace segmentation (`Assimalign.Viu.Hosting`) | 3 | Not started — decide after `.02` lands |
| `V01.01.14.09` | T07 product-prefix and duplicate facades | 3 | Not started |
| `V01.01.14.10` | T08 naming-rule compliance | 3 | Not started |
| `V01.01.14.11` | T09 mutable state and encapsulation | 4 | Not started |
| `V01.01.14.12` | T10 compiler-seam typing | 4 | Not started |
| `V01.01.14.13` | T11 async conventions | 4 | Not started |
| `V01.01.14.14` | T12 close open hierarchies | 5 | Not started |
| `V01.01.14.15` | T14 dead and speculative surface | 5 | Not started |
| `V01.01.14.16` | T17 debugger presentation + untracked `Peek()` | 5 | Not started |
| `V01.01.14.17` | G1 `PackageOverrides.txt` (see D2) | 1 or 2 | Not started |
| `V01.01.14.18` | G2 `IApplication` disposal contract | 4 | Not started |
| `V01.01.14.19` | G3 `SlotFlags` is not a bitmask | 3 | Not started |
| `V01.01.14.20` | G4 equality operators on app-visible `IEquatable<T>` | 4 | Not started |
| `V01.01.14.21` | G5 covariant read-only reactive reference | 4 | Not started |

## Waves

**Wave 1 — surface delivery and guardrails.** Zero behavior change. Make the surface visible,
hideable, diffable, and stop shipping what should never have shipped.

**Wave 2 — internalization and single source of truth.** The largest single reduction available
(~120 types), almost entirely mechanical via `InternalsVisibleTo`, which the repo already uses in 16
places. Must precede baseline generation so the baseline never records surface that is about to go.

**Wave 3 — namespace segmentation and naming.** Namespace moves precede renames so the surface
churns once.

**Wave 4 — state safety, typed seams, async conventions.** Behavior-affecting; each item needs its
own test coverage.

**Wave 5 — extensibility closure and long tail.** Defense in depth; no live break.

## Findings that were refuted — do not re-propose

The adversarial verification pass killed three attractive-looking recommendations. They are recorded
here so a future session does not rediscover and act on them:

1. **Do not delete `RenderHelpers._withHandler(Delegate)`.** C# has no implicit delegate-to-delegate
   conversion, so an expression already typed as one of the five shapes
   `CreateComponentEventListener` supports binds *only* the `Delegate` overload. Deleting it makes
   `@click="SomeComponentEventHandlerField"` a compile error for all five.
2. **Do not delete `Suspense.CreateComponent` / `KeepAlive.CreateComponent` as dead API.** Both have
   callers in two test assemblies, including cross-assembly from `ServerRenderer.Tests`. The proposed
   strong typing also cannot remove the weak typing: `[BLT-6]` *specifies* the string decoding.
3. **Do not internalize `RouterLinkClickEvent`.** `RouterLinkDomBridge` installs as the
   process-global `BrowserObjectEvents.Invoker`, so any app component with an `Action<object?>` click
   handler receives one. `[RTR-1]`/`[RTR-7]` make it the stated platform-agnostic carrier.

Also **not** plumbing despite appearances, per the specification: `ApplicationWatchScheduler`
(`[RCT-12]`), `Reactive.Track`/`Trigger`, `ReactiveTraversal`, `ViuModelBinding`, and
`Dependency`/`Subscriber` (`[RCT-9]` requires them publicly readable).

## Two corrections to the arc's founding premises

Both examples that opened this arc were real problems, but neither had the obvious fix.

**`RenderHelpers._openBlock` and the underscore surface.** The underscore prefix is **load-bearing**,
not a style lapse, and these members cannot become `internal`. The single-file-component emitter
writes `using static global::Assimalign.Viu.RenderHelpers;` into the *same compilation unit* as the
author's `@script` members, so they bind unqualified in user code — PascalCase spellings (`Fragment`,
`Capitalize`, `RenderList`) would be shadowed by any same-named user member. `[SFC-CG-2]` makes the
by-name binding normative, and `[SFC-CG-6]`/`[SFC-CG-7]` name individual members. Renaming is also a
package-version-coupling break: an app resolving an older analyzer against a newer Core would emit
`using static` against a type that no longer exists. The correct lever is `[EditorBrowsable]` plus a
language-service completion filter (unit `.02`).

**`ViuWatch` is not redundant.** Every method delegates to `Reactive.Watch` but adds two behaviors
`Assimalign.Viu.Reactivity` structurally cannot provide (it has zero package dependencies): a
`WatchFlushMode.Pre` default on the `ApplicationWatchScheduler`, and exception routing through
`ComponentErrorHandling` to the component `OnErrorCaptured` chain ([V01.01.03.12], #28). The real
defect is that two identically-shaped `Watch`/`WatchEffect` families are in scope simultaneously,
distinguished only by which `using` won, where the wrong choice silently changes flush timing and
drops the error contract — and `[RCT-5]` makes `Reactive` the spec-discoverable one, so the
spec-discoverable choice is the wrong one for component code. Resolution is a rename to
`ComponentWatch` plus a specification clause defining the two-layer split (unit `.09`).

## Guardrails the arc must land

1. **PublicAPI baseline** — `Microsoft.CodeAnalysis.PublicApiAnalyzers`; RS0016/RS0017/RS0037 become
   build errors for free under the existing `-warnaserror`. Generate baselines *after* Wave 2.
2. **CS1591 as the missing-docs gate** — delivered by unit `.01`.
3. **Packaging drift** — derive the release package count instead of the literal `21`, and assert no
   `Microsoft.CodeAnalysis.*` dependency in any `lib/net10.0` nuspec.
4. **`.editorconfig` severities** — `CA1711`, `CA1002`, `CA1067`, `CS8509`. Enable rule-by-rule with
   an explicit NoWarn baseline; do **not** set `AnalysisMode=Recommended` repo-wide.
5. **Contract-conformance tests** — every `HelperNames` entry resolves to a real public member; no
   `_`-prefixed member appears in `@script` completion (the only mechanism that makes
   `[EditorBrowsable]` bite for `.viu` authors).

## Incidental defects found during the audit

Recorded here because they are unrelated to API surface but were confirmed:

- `.github/workflows/release.yml:230,299` and `docs/RELEASING.md:10-11` assert 21 packages while
  `scripts/Pack-Release.ps1:158-162` produces 22 — **the publish lane throws today.**
- `HelperNames.ResolveFilter` names `_resolveFilter`, which exists on no runtime surface.
- `SingleFileComponentSourceEmitter`'s `DomRenderHelperNames` array substring-scans the emitted body
  to decide whether to write the DOM `using static`; adding an 11th DOM helper without updating it
  silently omits the import, failing with CS0103 in the **consumer's** build.
- The folder `analyzers/Assimalign.Viu.Generators.Reactive/` does not match its assembly id
  `Assimalign.Viu.Generators.Reactivity`, violating the repo's folder-name-is-the-assembly-id rule.
