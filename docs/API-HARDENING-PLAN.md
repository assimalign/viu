# API surface hardening plan — [V01.01.14]

**Status: ACTIVE. Waves 1 and 2A are merged; the `[V01.01.15]` component-model arc has
landed; and Phase B is complete for every requirement whose public shape is determined.
The remaining design-dependent rows are explicitly blocked rather than guessed.**

This document is the session-independent source of truth for the API-hardening arc. The state table
uses stable theme identifiers; its status, not the original wave number, determines what remains.
The GitHub issue body remains authoritative for a scheduled work item, and
`docs/SPECIFICATION.md` remains authoritative for Viu behavior.

The original 2026-08-05 audit found 147 raw findings. Since then the application-lifetime and
component-model changes removed or replaced substantial parts of the audited surface. D10 records a
fresh source-level audit rather than carrying those stale findings forward.

## Current app-visible and package-visible ground truth

`frameworks/Assimalign.Viu.App.props` currently places these assemblies in the browser shared
framework: `Assimalign.Viu.App`, `.Components`, `.Reactivity`, `.State`, `.Core`, and `.Browser`.
`Assimalign.Viu.Shared` no longer exists. Components, Reactivity, State, Core, and Browser are also
packable libraries; D2/G1 requires the framework packs to override those same-version package assets.

The Syntax, compiler, UtilityCss, language-service, and language-server assemblies are build/editor
tooling, not runtime app references. ServerRenderer, Router, Browser.Router, and Testing remain
opt-in packages. PublicAPI enforcement applies to every packable public project regardless of which
delivery path exposes it.

The runtime dependency direction relevant to this arc is:

```text
Components <- Reactivity
Components <- State
Components <- Core <- Browser
                    <- ServerRenderer
Router <- Browser.Router
```

## Decisions on record

| # | Decision | Date | Rationale |
|---|---|---|---|
| D1 | Nothing has shipped publicly; use direct renames/deletions instead of compatibility shims. | 2026-08-05 | The first release remains the point at which compatibility policy changes. |
| D2 | Publish framework libraries standalone and emit `data/PackageOverrides.txt`. | 2026-08-05 | Stock-SDK component libraries need package references, while Viu SDK apps must consume the framework copy rather than duplicate `lib/` assets. |
| D3/D3a | Delete Syntax.JavaScript; retain Syntax.Html but do not publish it until its runtime dependency/TFM shape is settled. | 2026-08-05 | The retained parser is not yet a restorable runtime package. |
| D4 | Track the arc through its area epic and this plan; create detailed feature items immediately before execution. | 2026-08-05 | Avoids speculative issue bodies drifting from the live surface. |
| D5/D5a | Replace application plugins with build-time composition plus lifetime middleware; expose host start/stop and keep mounting host-specific. | 2026-08-05/06 | This is the landed application-lifetime model. |
| D6 | Segment the SDK and framework by platform. | 2026-08-06 | The current SDK/framework remain browser-coupled; the exact base/browser topology is intentionally undecided. |
| D7 | Do not add `Assimalign.Viu.Hosting` here; that boundary belongs to Cohesion. | 2026-08-06 | Prevents two competing host-authoring abstractions. |
| D8 | `InternalsVisibleTo` is for an assembly's own unit tests only, never for production cross-library sharing. | 2026-08-06 | Assembly boundaries must remain real for runtime and tooling code alike. |
| D9 | Adopt the frame-based component model recorded by [`COMPONENT-MODEL-PLAN.md`](COMPONENT-MODEL-PLAN.md). | 2026-08-07 | The migration deleted the old helper ABI and superseded T05's old Core seam proposals. |
| D10 | Reevaluate every unfinished hardening row against the landed component model; execute only requirements whose public shape is already determined, and mark the rest `BLOCKED-NEEDS-DECISION`. | 2026-08-08 | Hardening must describe and test the product that exists now. It must not preserve deleted findings or invent replacement APIs merely to close rows. |

## State

Every row is terminal (`DONE`, `DROPPED`, `SUPERSEDED`) or explicitly
`BLOCKED-NEEDS-DECISION`. Split rows separate completed work from design questions so that a
blocked choice cannot hide unrelated work.

| Theme | Rescoped outcome after D10 | Status |
|---|---|---|
| T01/T15 | XML documentation delivery and compiler enforcement landed in the merged waves. | **DONE** |
| T02 | PublicAPI baselines now record the final additions/removals for every changed packable project; warning-as-error builds are clean for RS0016, RS0017, and RS0037. The never-shipped surface remains in `PublicAPI.Unshipped.txt`. | **DONE** |
| T03 | Attribute-target semantics landed in the merged waves. | **DONE** |
| T04 | Accidental runtime exposure was internalized or deleted in the merged waves. | **DONE** |
| T05 | All six old Core decisions were delivered differently or made moot by D9; see the explicit disposition below. | **SUPERSEDED** |
| T06 | Hosting namespace/library split is out of repository scope under D7. | **DROPPED** |
| T07 | Deleted targets are gone, but remaining facade/prefix and constructor changes require choosing replacement public names and factory boundaries. | **BLOCKED-NEEDS-DECISION** |
| T08 | Mechanically determined whole-word and Boolean-clarity renames are complete across runtime, generators, syntax, SSR, tests, docs, and baselines. | **DONE** |
| G3 | The plain three-value classification is now `SlotStability`; linked source paths and numeric values `1`, `2`, and `3` are preserved. | **DONE** |
| T09-A | `TestElement` exposes read-only live views backed by privately owned mutable collections. | **DONE** |
| T09-B | Both `RouterGuards.Register` overloads reject depths outside the matched route range. | **DONE** |
| T09-C | Raw `Reactive.StartBatch`/`EndBatch` can leave effects suppressed after an unmatched call. | **BLOCKED-NEEDS-DECISION** |
| T10 | Typed patch metadata and slot stability landed, but `RendererOptions<TNode>` still exposes a convention-dependent `Action`/`Func` delegate bag. | **BLOCKED-NEEDS-DECISION** |
| T11 | Async naming, cancellation propagation, and terminal disposable lifetime conventions are complete on Router, Scheduler, router history, ReactiveEffect, and Testing wrappers. | **DONE** |
| G4 | `RouteLocation` and `RouteParameters` now provide matching null-safe equality operators. | **DONE** |
| G5 | Covariant `IReactiveReadOnlyReference<T>` now exposes Router's current route without a mutable reference contract. | **DONE** |
| T12 | Surviving parser bases have assembly-closed construction; CSS writers and rewriters explicitly handle supported nodes and reject unsupported variants. | **DONE** |
| T14-A | Generic identity and raw-object conversion surface is removed; observably different collection conversions remain. | **DONE** |
| T14-B | Router history Boolean/state controls, the RouterLink click Boolean cluster, opaque navigation results, and TestRenderer Boolean ordering need replacement-contract choices. `BrowserRuntime.CreateRenderer` is an owning lease and stays. | **BLOCKED-NEEDS-DECISION** |
| T17 | Non-subscribing fresh reads and side-effect-free debugger displays are implemented and pinned with dependency run-count tests. | **DONE** |
| G1 | The Ref pack emits the exact standalone/framework overlap through `data/PackageOverrides.txt`; Runtime excludes it, and an isolated packaged SDK consumer passes build, trimming, AOT, and conflict-resolution evidence checks. | **DONE** |
| D6-A | The SDK remains browser-based; its base/browser split depends on an unresolved import and payload-ownership design. | **BLOCKED-NEEDS-DECISION** |
| D6-B | The framework still includes Browser and pins `browser-wasm`; its base/browser reference/runtime topology is unresolved. | **BLOCKED-NEEDS-DECISION** |

## T05 final disposition

T05 originally proposed public/private surgery around the former Core component model. D8 forbids
using cross-library friend grants to simulate an API, and D9 replaced the underlying model. None of
the six decisions remains pending:

| Old decision | Shipped result |
|---|---|
| 1. Delete `EmptyComponentFactory`. | The type is gone. `ApplicationOptions.Components` defaults to an empty `ComponentFactory`; resolving an unregistered component throws. `ComponentFactory` remains a public extensibility seam with registration and overridable resolution. |
| 2. Make DI absence explicit. | `ApplicationOptions.Services`, `IApplicationContext.Services`, and `ComponentContext.Services` are nullable. The production empty-provider shim is gone. |
| 3. Promote application state. | Public `ApplicationState` is available through `ApplicationLifetime.State`, as specified by `[APP-1]`. |
| 4. Decide mounted context members. | The old mounted-context interface is gone. Public abstract `ComponentContext` exposes `Parent`; scoped-style identity was deliberately deferred by the component-model plan rather than leaked as a placeholder. |
| 5. Promote `MountedComponent`. | The old promotion is moot. The runtime keeps `MountedComponent<TNode>` internal and exposes the operation seam `ComponentHost.RenderAsync(ComponentRenderRequest, CancellationToken)` returning `IComponentRenderScope`, which SSR owns with `await using`. |
| 6. Promote `MountedTemplateNode<TNode>`. | The type is gone. `MountedComponentView<TNode>` and `Renderer<TNode>.GetMountedComponentViews()` provide the stable diagnostic/testing view. |

## Surviving Phase-B requirements

### Mechanical/naming work

- **T08:** expand public abbreviations and ambiguous Boolean names: patch flags (`Properties`,
  `FullProperties`, `NeedsHydration`, `DevelopmentRootFragment`), synchronous watch mode, browser
  diagnostic tuple elements, read-only terminology, state-definition removal, route matching
  sensitivity, MathML casing, `SingleFileComponent`, JavaScript/VirtualNode/Properties syntax names,
  SSR Boolean terminology, and server-renderer Attribute names. Preserve every serialized/numeric
  value and update generators, fixtures, XML docs, tests, specification, and PublicAPI files.
- **G3:** rename the plain `SlotFlags` enum and its linked source file to `SlotStability`, preserving
  values `Stable = 1`, `Dynamic = 2`, and `Forwarded = 3` and updating `[RND-FLAGS-1]`,
  `[RND-FLAGS-5]`, `[RND-FLAGS-6]`, `[CMP-18]`, and `[CMP-19]`.
- **T12:** add `private protected` construction to the surviving Template AST bases,
  `SingleFileComponentBlock`, `HtmlSyntaxNode`, and non-generic `SyntaxParser`; make CSS writer and
  rewriter handling explicit and total where records cannot be closed.
- **T14-A:** remove only the identity/raw-object API. Keep collection raw-view APIs whose result is
  observably different.

### Behavior and contract work

- **T09-A:** public Testing collections are `IReadOnlyDictionary`/`IReadOnlyList`; internal mutation
  continues through owned backing collections.
- **T09-B:** both router guard registration overloads throw `ArgumentOutOfRangeException` when depth
  is outside the matched route range; update `[RTR-4]` and pin negative/equal/greater cases.
- **T11:** application-level async operations use an `Async` suffix; Router navigation accepts and
  propagates cancellation; history implements `IDisposable`; `ReactiveEffect.Dispose()` delegates
  to idempotent `Stop()`. Synchronous history `Push`/`Replace` retain their names.
- **G4:** add matching null-safe `==` and `!=` to `RouteLocation` and `RouteParameters`.
- **G5:** add covariant `IReactiveReadOnlyReference<out T>` with get-only `Value`, implement it on
  reactive values, and expose `Router.CurrentRoute` through it. Keep `Reactive.Computed` returning
  its concrete type because writable computeds are supported.
- **T17:** `ReactiveValue<T>.Peek()` performs a fresh read while caller dependency tracking is
  paused and then restored. Debugger displays use backing state or `Peek()` and do not subscribe the
  debugger. Tests assert getter/effect run counts, including stale computed refresh.

### Packaging and enforcement work

- **G1:** author the overlap list once and pack it as `data/PackageOverrides.txt` into the Ref
  targeting pack (the .NET consumption contract); assert its absence from Runtime packs, support
  stable and prerelease versions, assert archive contents, and prove an
  external Viu-SDK consumer with an explicit same-version package reference resolves the framework
  asset rather than a duplicate library asset.
- **T02:** after every preceding surface change, regenerate `PublicAPI.Shipped.txt` and leave only
  intentional additions/removals in `PublicAPI.Unshipped.txt`. RS0016, RS0017, and RS0037 must be
  clean under warning-as-error.

## Questions that block public-shape invention

- **T07:** Which replacement vocabulary should be adopted for `ViuTest`, `ViuModelBinding`,
  `IViuLanguageService`/`ViuLanguageServices`, duplicate reactive scope access, public reactive
  constructors, and stuttering asynchronous/dynamic component facades? One coherent naming/factory
  decision is required before changing them.
- **T09-C:** Should raw batching remain, be replaced by a disposable scope, or be wrapped in a
  callback API that guarantees unwind after exceptions?
- **T10:** Should renderer host operations become named delegates, a host-operations interface, or
  intentionally remain a delegate bag?
- **T14-B:** What public value types replace router history's Boolean/state controls,
  `RouterLinkClickEvent`'s four Booleans, and the opaque navigation result? Should TestRenderer's
  Boolean order be changed or replaced with options?
- **D6-A:** Does `Assimalign.Viu.Sdk.Browser` import/depend on a thin base SDK or carry a self-contained
  SDK payload, and which CSS/static-asset/watch tasks belong to each?
- **D6-B:** Is `Assimalign.Viu.App` targeting-only or does it own a host-neutral runtime pack; does the
  Browser framework re-export/reference it; and does ServerRenderer remain opt-in or become another
  framework segment?

## Findings refuted or superseded — do not re-propose

- Protections for `RenderHelpers._withHandler` and underscore-prefixed generated helpers are
  **superseded by D9**. Those types and the static helper ABI no longer exist; generated rendering
  uses mount-local `ComponentRenderFrame` state and qualified calls.
- The old `Suspense.CreateComponent`/`KeepAlive.CreateComponent` shape is gone, but the behavioral
  finding survives in its current form. Preserve public structural `SuspenseNode` and
  `KeepAliveNode`. `[BLT-6]` protects KeepAlive's weak-input decoding (string filters and string or
  integer maximum; invalid/nonpositive maximum is unbounded). `[BLT-11]` protects lazy Suspense
  slots.
- `RouterLinkClickEvent` is still a required public bridge between `RouterLink`, Browser.Router DOM
  handling, and component-test triggering. Preserve it under `[RTR-1]` and `[RTR-7]` unless T14-B
  receives a replacement-contract decision.
- Browser `CreateRenderer` is not dead surface: it returns an owning disposable renderer lease.
- Syntax.Html is intentionally retained but unpublished under D3a; `.vue` single-file-component
  parsing is a shipping compatibility feature and must not be removed.

## Phase-B completion and gates

The work completed in the required order: mechanical/naming changes, behavior/contract changes,
package-overrides integration and external packaged-consumer proof, then PublicAPI baselines. The
final warning-as-error solution build, no-build solution tests, PublicAPI analyzer checks, and D8
scan are recorded in `.hardening/REPORT-item3.md`.

D8 is checked after every group: every `InternalsVisibleTo` target must be the owning library's test
assembly. No production or cross-library grant is acceptable.
