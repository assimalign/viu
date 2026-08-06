# API surface hardening plan — [V01.01.14]

**Status: ARC OPEN. Waves 1 and 2A complete and merged; Wave 2 next (T05).**

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

**Not** in the app reference set: the `Syntax.*` parsers and compiler/editor tooling assemblies,
`ServerRenderer`, `Router`/`Browser.Router` (opt-in packages), and `Testing` (dev-time). The source
generator's parser/compiler closure reaches builds inside `analyzers/dotnet/cs/` of the Ref pack;
`Syntax.Html` remains a tooling-only host-page parser; `UtilityCss` ships through its standalone
package and SDK/editor hosts; and `LanguageService`/`LanguageServer` ship in editor payloads.

Raw public-type counts badly misrank the work: `Syntax.Templates`' 59 public types are build-time
only, while `Shared`'s 11 are in every app's IntelliSense. Prioritize the seven.

### Verified dependency layering

```
Shared ── Components ── Core ── Browser          <- browser host
   |          |           +---- ServerRenderer   <- server host
   |          +-- Reactivity
   |          +-- State
   +--------- Router                             <- host-agnostic (Browser.Router is its bridge)
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
| D3a | **Amends D3: `Assimalign.Viu.Syntax.Html` is retained but *not published*.** The project stays; the package stops. | 2026-08-05 | D3 kept Html packable on the assumption it might ship as a runtime library. Implementing [V01.01.14.04] showed that cannot work yet, on two counts. Its only dependency, `Assimalign.Viu.Syntax`, is itself now unpublished, so a standalone `Assimalign.Viu.Syntax.Html` package is not restorable by anyone. And the project is a build-time analyzer-host assembly on netstandard2.0 whose own csproj states it is "consumed at build time by generators, never shipped into the WASM app" — the wrong shape for a runtime consumer regardless of the dependency. Publishing resumes when the runtime work settles the TFM and the dependency story together. D3's substance is intact: the project is not deleted and the POC is unblocked. Nothing has shipped publicly (D1), so unshipping now costs nothing. |
| D4 | **Work tracking: area epic + this plan doc; feature items created just before each wave starts.** | 2026-08-05 | Avoids writing five detailed specs up front that later drift. The GitHub issue body stays the authoritative requirement source for the wave in flight. |
| D5 | **Redesign the application lifetime: delete `IApplicationPlugin`, separate build-time composition from runtime behavior, and make `Use(...)` a real middleware pipeline around the persistent host lifetime.** Direction set by Chase from an independent architecture review of `fc8a90ba`. Full design in the section below, as amended by D5a. | 2026-08-05 | `IApplicationPlugin` is a deferred initializer with no `next`, no short-circuiting, and no cleanup phase. Verified: it has **zero shipping implementations** — every implementor in the tree is a test double or the showcase's recorder. Deleting it before the polish waves avoids hardening, documenting, and renaming types that are about to be removed. **Requires a `docs/SPECIFICATION.md` amendment replacing `[CMP-25]` (line 333)**, filed per `.claude/rules/deviations.md`; this row is the recorded confirmation. |
| D5a | **Maintainer amendment for PR #305:** `IApplication` exposes `StartAsync`/`StopAsync`; `RunAsync` is an extension; runtime state and the stopping token live on `IApplicationContext`; middleware receives that context directly; a lean `IApplicationBuilder` configures all composition through `ApplicationOptions`; Browser implements the lifetime and mount APIs directly; the generic Core application abstraction, abstract builders, execution wrapper, and static Browser facade are deleted. | 2026-08-06 | This is the accepted review shape for [V01.01.14.08]. It adopts the familiar split between starting a host and running it until shutdown, keeps host-specific mounting out of Core, and removes inheritance that exposed platform mechanics as Core API. The amendment below replaces the original D5 target shape while retaining D5's middleware, cleanup, borrowed-ownership, and server-separation decisions. |
| D6 | **Segment the SDK and the shared framework by platform.** `Assimalign.Viu.Sdk` and `Assimalign.Viu.App` become platform-agnostic; `Assimalign.Viu.Sdk.Browser` and `Assimalign.Viu.App.Browser` carry everything browser-specific. Direction set by Chase. Full design in the section below. | 2026-08-06 | Today there is exactly one SDK and one framework, and both are browser-only by construction: `sdks/Assimalign.Viu.Sdk/Sdk/Sdk.props:8` opens with `<Import Sdk="Microsoft.NET.Sdk.WebAssembly" …>` under the comment *"A Viu app is a WASM browser app, so the chain starts at Microsoft.NET.Sdk.WebAssembly"*, and the single `Assimalign.Viu.App` framework bundles `Browser` with the host-agnostic libraries. So authoring a platform-agnostic Viu component library means taking the WebAssembly SDK and a `browser-wasm` runtime pin for code that renders nothing. The split gives that author a first-class path, and gives every future host (SSR, WebView) a shape to slot into rather than a fork. |
| D7 | **`Assimalign.Viu.Hosting` (T06) is not implemented in this repository.** The host-authoring/app-authoring namespace split moves to the Cohesion project. | 2026-08-06 | Direction set by Chase. The finding stands and is worth keeping recorded — the `Assimalign.Viu` front door mixes app-authoring API with host-adapter plumbing — but the segmentation is being solved in Cohesion rather than duplicated here. D5 also already removed much of what T06 targeted: mounting moved into Browser and the generic Core application abstraction was deleted. T06 stays in the state table as **Will not do (see D7)** so a future session does not re-derive it as an open opportunity. |
| D8 | **`InternalsVisibleTo` is for unit tests only, everywhere.** It is not a mechanism for sharing internals between libraries — including between two build-time-only libraries. Grants live in `src/Properties/AssemblyInfo.cs`. Recorded as a standing rule in `.claude/rules/general-rules.md`. | 2026-08-06 | Direction set by Chase. A cross-library grant makes the assembly boundary a fiction: two assemblies that need each other's internals are either one assembly, or have an API that was never designed. This reverses the premise of T05 as originally scoped, which planned to internalize ~120 types and add grants — see the T05 note below for what it becomes. Being non-shipping is not an exemption: a build-time assembly's surface is invisible to app developers, but the boundary still exists for maintainers. Eight cross-library grants exist today and are in scope to remove. |

## State

**Theme id is the stable key, not the WBS code.** Do not pre-assign WBS codes here. The
`viu-work-items` script derives the next free child of `V01.01.14` at creation time, so a code
reserved in advance will not match the one the item actually receives. Fill the WBS and Issue columns
in as each item is created.

| Theme | Description | Wave | WBS | Issue | Status |
|---|---|---|---|---|---|
| T01 + T15 | Ship XML documentation; sweep derivative doc phrasing | 1 | `.01` | #285 | **MERGED** (PR #288) |
| T04 | Compiler-seam hardening (`[EditorBrowsable]` + language-service filter) | 1 | `.02` | #287 | **MERGED** (PR #289) |
| T16 | Drop the viral `RequiresPreviewFeatures` stamp | 1 | `.03` | #292 | **MERGED** (PR #293) — superseded #280 |
| T03 | Unship build-time packages; delete `Syntax.JavaScript` (retain `Syntax.Html`, unpublished — see D3a) | 1 | `.04` | #294 | **MERGED** (PR #295) |
| T02 | Public-API baseline and hardening-attribute conventions | 1 | `.06` | #299 | **MERGED** (PR #300) |
| D5-A | Application lifetime: middleware pipeline, `StartAsync`/`StopAsync`, `RunAsync` extension, delete `IApplicationPlugin` | 2A | `.08` | #304 | **MERGED** (PR #305) |
| D5-B | Composition surface: lean `IApplicationBuilder`, options-only composition, frozen context | 2A | `.08` | #304 | **MERGED** (PR #305) |
| D5-C | Separate SSR: `ServerApplication` → `ServerRenderApplication`, off `IApplication` | 2A | `.08` | #304 | **MERGED** (PR #305) |
| D5-D | Browser entry point: direct `new BrowserApplicationBuilder()`; no static facade | 2A | `.08` | #304 | **MERGED** (PR #305) |
| D5-E | Router bootstrap: `UseRouter`, `ReadyAsync` cancellation, lazy history init | 2A | `.08` | #304 | **MERGED** (PR #305) |
| D5-F | Specification: replace `[CMP-25]` with application lifecycle clauses; lifetime test suite | 2A | `.08` | #304 | **MERGED** (PR #305) |
| — | Rename `Router.Browser` → `Browser.Router` | 2A | `.09` | #307 | **MERGED** (PR #308) |
| — | Extension-member syntax; `Extensions/` and `Exception/` folders | 2 | `.10` | #309 | **MERGED** (PR #310) |
| T05 | Reduce friend-only publics — **re-scoped by D8**, see note below | 2 | — | — | **NEXT** |
| T13 | Single source of truth for the helper-name contract | 2 | `.07` | #301 | **MERGED** (PR #306) |
| T06 | Namespace segmentation (`Assimalign.Viu.Hosting`) | — | — | — | **Will not do — see D7** (moving to Cohesion) |
| T07 | Product-prefix stutter and duplicate facades | 3 | — | — | Not started |
| T08 | Naming-rule compliance | 3 | — | — | Not started |
| G3 | `SlotFlags` is named `*Flags` but is not a bitmask | 3 | — | — | Not started |
| T09 | Mutable state and encapsulation | 4 | — | — | Not started |
| T10 | Compiler-seam typing | 4 | — | — | Not started |
| T11 | Async conventions | 4 | — | — | Not started |
| G4 | Equality operators on app-visible `IEquatable<T>` | 4 | — | — | Not started |
| G5 | Covariant read-only reactive reference | 4 | — | — | Not started |
| T12 | Close open inheritance hierarchies | 5 | — | — | Not started |
| T14 | Dead and speculative surface | 5 | — | — | Not started |
| T17 | Debugger presentation + untracked `Peek()` | 5 | — | — | Not started |
| D6-A | Segment the SDK: agnostic `Assimalign.Viu.Sdk` + `Assimalign.Viu.Sdk.Browser` | 3 | — | — | Not started — see D6 |
| D6-B | Segment the framework: `Assimalign.Viu.App` + `Assimalign.Viu.App.Browser` | 3 | — | — | Not started — see D6 |
| G1 | `PackageOverrides.txt` (see D2) | 1 or 2 | — | — | Not started |
| G2 | `IApplication` disposal contract | — | — | — | **Absorbed by D5-A / D5-C** |

### Units the D5 redesign absorbs

Do not implement these separately — they are subsumed and would otherwise be done twice, or done to
types that are about to be deleted:

| Absorbed | Into | Why |
|---|---|---|
| G2 `IApplication` declares no disposal; `ServerApplication` implements neither | D5-A, D5-C | The new `IApplication : IAsyncDisposable` closes the contract, and `ServerApplication` leaves the interface entirely rather than growing a teardown path it has no use for. |
| G6 Core's generic application base had non-virtual `Dispose` with a private flag | D5-A | The Browser-owned state machine replaces the single `IsMounted` Boolean, and disposal is defined by it. |
| T09 — `IApplicationContext.ErrorHandler`/`WarnHandler`/`Performance` are `{ get; set; }` on the interface while three docs call the context immutable | D5-B | Diagnostics move into builder options and freeze at `Build()`, which makes the documentation true instead of restating the mutation. |
| T12 — the former abstract builder's configuration methods returned `IApplicationBuilder`, collapsing the concrete Browser builder | D5-B | The abstract base is deleted. The lean interface remains for platform-neutral construction, while concrete builders self-return and implement its two members explicitly where return covariance requires it. |
| T14 — `Performance` has no production reader; `BrowserApplication.CreateServerRendererBuilder` has zero callers | D5-B, D5-D | Both are deleted rather than renamed or documented. |
| T11 — `Router.ReadyAsync` takes no `CancellationToken` | D5-E | Cancellation is required for the middleware pipeline to be cancellable at all. |

### T05 after D8 — what changed and what it costs

T05 was scoped as *"internalize ~120 types and add `InternalsVisibleTo` grants."* **D8 removes the
mechanism**, so the unit is re-scoped rather than merely constrained. The types divide by who consumes
them, and only one group is genuinely hard.

> **"Public" means two different things here — keep them apart.**
>
> **Accessibility** (`public` vs `internal`) is what D8 governs. **Packaging** (`IsPackable`) is not,
> and is unchanged.
>
> A `tooling/` type becoming `public` is a *correct and expected* outcome of D8, not a concession: it
> makes a real dependency into a designed contract. It does **not** put that type on nuget.org. Those
> assemblies stopped publishing in [V01.01.14.04] and stay unpublished — they reach builds only inside
> `analyzers/dotnet/cs/` of the Ref pack. So an app developer's IntelliSense is unaffected either way.
>
> D8 exists to keep the boundaries between libraries honest, not to minimise public surface in
> assemblies no app references. Do not read a `public` in `tooling/` as a regression of the unshipping
> work, and do not "fix" it by re-adding a grant.

| Group | Treatment under D8 |
|---|---|
| **Zero consumers** — e.g. `PatchFlagNames`, `ShapeFlagsExtensions`, `Scheduler.IsFlushing`/`IsFlushPending` | Make `internal` outright. No grant needed; D8 is not engaged. |
| **Consumed only by the assembly's own tests** — e.g. the generators' `*TrackingName` constants | `internal` + a **test** grant. Exactly what D8 sanctions. |
| **Consumed by a sibling `tooling/` assembly** (5 of the 8 grants) | Same decision procedure as a shipping library, and for most of these the answer is simply **make it `public`** — the dependency is real, so it is a contract. Costs nothing developer-facing, since these assemblies do not publish. |
| **Consumed by a sibling shipping library** (3 grants, all from `Core`) | The real work — see below. |

**The `Core` cases.** `Core` grants internals to `Browser`, `ServerRenderer` and `Testing`. Of its 39
internal types, only **six** are reached across the boundary: `ComponentContext`, `EmptyServiceProvider`,
`MountedTemplateNode`, `EmptyComponentFactory`, `ApplicationState`, `MountedComponent`.

Each needs a deliberate answer — promote to public API, move to its sole consumer, or express through an
interface with the concrete type staying internal. Two probably *want* to be public regardless:
`ApplicationState` is the D5 lifecycle enum a host legitimately observes, and the empty resolvers are
D5's "a primitive application should not need dummy dependencies" defaults.

**The `tooling/` cases, measured.** Three of the five grants share exactly **one** type each —
`SingleFileComponentPathComparison` (twice) and `CompilerDomKnowledge` — each a promote-or-move decision
and nothing more. The two substantial ones are both `Assimalign.Viu.Compiler.SingleFileComponent`:
**12** types reach `Generators.Syntax` and **9** reach `LanguageService`, overlapping heavily
(`SingleFileComponentProjection`, `SingleFileComponentSourceEmitter`, `SingleFileComponentDiagnostics`,
`SingleFileComponentFormat`, `SingleFileComponentNameResolver`), for **16** distinct types.

That is not accidental leakage. The projection core's own `docs/OVERVIEW.md` says it exists so that
*"the two hosts that need that projection"* share one implementation — so those types **are** its
contract, and are `internal` only because a grant made that easy. Under D8 they become a designed public
API, which is what the assembly was always for. Watch for `EquatableArray` in that list: near-identical
copies exist in other projects, so it is a duplication question rather than a promotion one.

**Total deliberate decisions: ~23 types** — 6 in `Core`, ~17 across `tooling/`. Tractable, and each one
is a real design call rather than an accessibility edit.

**Expect the headline number to fall.** The original "~120 types internalized" assumed grants were
available. Under D8 a meaningful share stays public — correctly, because the assembly boundary is real.
The gain is honesty about that boundary rather than a smaller type count.

## D6 — platform segmentation of the SDK and framework

### What is browser-specific today

The dependency layering ([verified above](#verified-dependency-layering)) already separates cleanly —
`Shared`, `Reactivity`, `State`, `Components` and `Core` are host-agnostic; `Browser` and
`ServerRenderer` are hosts. The SDK and framework are the only places that fail to reflect it.

| Piece | Agnostic | Browser-specific |
|---|---|---|
| `Assimalign.Viu.Generators.Syntax.props`/`.targets` (the `.viu` generator wiring) | ✅ | |
| CSS composition and utility CSS | ✅ (component styles) | `ViuInjectCssBundleLink` writes into `index.html` |
| `Assimalign.Viu.Sdk.Common.props` | mostly ✅ | |
| `Sdk.props` chaining `Microsoft.NET.Sdk.WebAssembly` | | ✅ |
| `Assimalign.Viu.Sdk.WebAssembly.targets` | | ✅ |
| `Assimalign.Viu.Sdk.StaticWebAssets.targets` (`wwwroot`, `viu-dom.js`) | | ✅ |
| `Assimalign.Viu.Sdk.FrameworkReference.props` (`browser-wasm` runtime pack) | | ✅ |
| CSS hot reload, publish-size budget | | ✅ |

### Target shape

**SDK.** `Assimalign.Viu.Sdk` chains `Microsoft.NET.Sdk`, carries the `.viu` generator wiring, component
compilation, and CSS composition, and references the agnostic framework. It builds a Viu **component
library** that names no platform. `Assimalign.Viu.Sdk.Browser` imports it, chains
`Microsoft.NET.Sdk.WebAssembly`, and adds the static-web-asset, `viu-dom.js`, HTML-injection, hot-reload
and publish-budget machinery — the same relationship `Microsoft.NET.Sdk.Web` has to `Microsoft.NET.Sdk`.

**Framework.** `Assimalign.Viu.App` carries `Shared`, `Components`, `Reactivity`, `State`, `Core` and the
umbrella. `Assimalign.Viu.App.Browser` adds `Browser` and owns the `browser-wasm` runtime pack.

`frameworks/Assimalign.Viu.App.props` **already anticipates this**: its `ItemGroup`s are conditioned on
`$(ViuFrameworkName)` precisely so *"additional framework families (e.g. a future
Assimalign.Viu.App.Server for SSR) can share this manifest file."* The manifest needs new entries, not a
new mechanism.

### Naming — the platform segment is a suffix here, and that is deliberate

This looks inconsistent with [V01.01.14.09], which moved the platform segment to the **front**
(`Assimalign.Viu.Router.Browser` → `Assimalign.Viu.Browser.Router`). It is not: the two are different
axes, and both follow .NET precedent.

- `Assimalign.Viu.Browser.Router` — `Router` is a **feature owned by a platform**. The owner leads, as
  with `Browser`'s DOM bridge and event registry.
- `Assimalign.Viu.Sdk.Browser` / `Assimalign.Viu.App.Browser` — `Sdk` and `App` are **pack families with
  platform variants**. The family leads and the variant is suffixed, exactly as with
  `Microsoft.NET.Sdk` → `Microsoft.NET.Sdk.Web`/`.Razor`/`.WebAssembly`, and
  `Microsoft.NETCore.App` → `Microsoft.NETCore.App.Runtime.browser-wasm`.

Recorded so a future session does not "correct" one to match the other.

### What this changes about D2

D2 chose to publish the six framework libraries standalone plus `PackageOverrides.txt` **specifically**
so a component-library author could reference the host-agnostic set from a stock `Microsoft.NET.Sdk`
project without taking `Browser`. D6 serves that author better: they use
`<Project Sdk="Assimalign.Viu.Sdk">` and get the agnostic framework.

D2 is **not** withdrawn — standalone packages remain the escape hatch for consumers who cannot adopt a
custom SDK — but it stops being the primary answer, and `PackageOverrides.txt` becomes *more* load
bearing, since it must now cover the agnostic framework's assemblies to keep them inert on the SDK path.

### Open questions to settle when this is scheduled

1. **Does the agnostic framework ship a runtime pack?** A component library needs only the targeting
   pack. A RID-less runtime pack may be unnecessary — but `ResolveTargetingPackAssets` and the
   `KnownFrameworkReference` registration need to work without one, which wants proving.
2. **Does `ServerRenderer` become `Assimalign.Viu.App.Server` / `Assimalign.Viu.Sdk.Server`?** The
   manifest comment already names it. If the pattern is right, SSR is its validation; if SSR does not
   fit, that is a signal the split is drawn wrong.
3. **Where does CSS bundling divide?** Component style compilation is agnostic; injecting the bundle
   link into `index.html` is not. The seam runs through `ViuBundleCss`/`ViuInjectCssBundleLink` and needs
   drawing precisely.
4. **Does `Assimalign.Viu.Sdk.Browser` chain `Assimalign.Viu.Sdk`, or duplicate its imports?** Chaining
   is correct but MSBuild SDK chaining across two packaged SDKs needs verifying against the resolver.

## D5 — application lifetime redesign

> **Maintainer amendment (D5a, 2026-08-06, PR #305).** The first implementation proved the middleware
> boundary but exposed host mechanics through Core inheritance and made one method both start and
> span the host lifetime. The accepted review shape below replaces that target: `StartAsync` starts
> the background pipeline and returns at mounted/Running, while the `RunAsync` extension owns the
> full start → wait → stop sequence. All other D5 goals remain in force.

### What is wrong today

`IApplicationPlugin` ([`IApplicationPlugin.cs:6`](../libraries/Assimalign.Viu.Core/src/Abstraction/IApplicationPlugin.cs)) is a
deferred initializer with no `next`, no short-circuiting, and no cleanup phase. Verified across
`libraries/`, `tooling/`, `analyzers/` and `../viu-examples`: **no shipping implementation exists** —
every implementor is a test double (`CountingPlugin`, `RecordingPlugin`, `AsyncPlugin`) or the
showcase's `ShowcasePlugin`, which only records that it ran.

The surrounding abstractions have concrete defects:

- Builder methods returned an interface that erased the concrete Browser builder and its host-specific
  result.
- The old builder could not build a primitive root without dummy component and service resolvers,
  despite documented examples saying otherwise.
- `MountAsync` has reentrancy, cancellation, partial-mount cleanup, and disposal-during-startup
  problems because one `IsMounted` Boolean stands in for the whole lifecycle
  in the former Core implementation.
- Selector mounting initializes Browser *before* plugins, while direct node mounting installs plugins
  *before* Browser initialization ([`BrowserApplication.cs:128`](../libraries/Assimalign.Viu.Browser/src/BrowserApplication.cs)).
- `ServerApplication` implements `IApplication` although it is never mounted, its root context is
  always null, and unmount is a no-op.

### Target abstraction

```csharp
public delegate ValueTask ApplicationDelegate(IApplicationContext context);

public delegate ValueTask ApplicationMiddleware(
    IApplicationContext context,
    ApplicationDelegate next);

public interface IApplication : IAsyncDisposable
{
    IApplicationContext Context { get; }
    IApplication Use(ApplicationMiddleware middleware);
    ValueTask StartAsync(CancellationToken cancellationToken = default);
    ValueTask StopAsync(CancellationToken cancellationToken = default);
}

public interface IApplicationContext
{
    bool IsRunning { get; }
    CancellationToken Stopping { get; }
    IComponent RootComponent { get; }
    IComponentFactory Components { get; }
    IServiceProvider Services { get; }
    IStateStoreRegistry? State { get; }
    IDirectiveResolver? Directives { get; }
    Action<Exception, IComponentContext?, string>? ErrorHandler { get; }
    Action<string>? WarnHandler { get; }
}

public interface IApplicationBuilder
{
    IApplicationBuilder ConfigureApplication(Action<ApplicationOptions> configure);
    IApplication Build();
}
```

`ApplicationOptions` is the one mutable composition surface before `Build()`: `RootComponent`,
`Components`, `Services`, `State`, `Directives`, `ErrorHandler`, and `WarnHandler`. `Build()` snapshots
those values into the read-only context. The context's composition never changes; only its observable
runtime members (`IsRunning` and the one attached `Stopping` token) carry lifetime state.

Internal state machine:

```
Created → Starting → Running → Stopping → Stopped
              ↘        ↘          ↘ Failed
```

`StartAsync` claims execution **synchronously** by moving Created to Starting, freezes `Use`, and starts
the pipeline as an independently observed asynchronous task without scheduling work onto a worker
thread. It waits for the host terminal to finish initialization, resolve the mount target, mount, set
`IApplicationContext.IsRunning`, and signal startup. The pipeline itself stays pending. A stop request
moves Starting or Running to Stopping, clears `IsRunning`, signals `IApplicationContext.Stopping`, and
awaits the same pipeline task through unmount and reverse-order cleanup. Startup, live-execution, or
cleanup failure moves to Failed. A failure after startup is reported once through `ErrorHandler` and
remains on the pipeline task so `StopAsync` and `RunAsync` surface it.

### Rules

- `ConfigureApplication` composes dependencies through `ApplicationOptions` **before** `Build()`;
  `Use` decorates runtime execution **after** it.
- `Use` never adds components, directives, services, or state.
- `Use` after execution begins throws.
- Registering the same middleware twice executes it twice — no plugin-style deduplication.
- `StartAsync` is single-use, begins the pipeline in the background, and returns only after the
  terminal signals mounted/Running (or the pipeline ends before mounting).
- The `RunAsync` extension is exactly Start → wait for `Context.Stopping` → Stop.
- Cancellation or `StopAsync` signals `Context.Stopping`, unmounts, awaits the pipeline, and runs
  middleware cleanup in reverse order.
- Composition dependencies stay borrowed; Viu does not dispose them.
- Startup stays asynchronous. A synchronous start or run would be unsafe on single-threaded
  WebAssembly.

For two registrations the execution order is:

```
first before
  second before
    initialize browser host -> resolve mount target -> mount or hydrate
    wait for cancellation / StopAsync
    unmount
  second cleanup
first cleanup
```

That lifetime boundary is the whole point: if the terminal returned immediately after mount, every
`finally` block would run while the SPA was still alive.

### Intended developer experience

```csharp
await new BrowserApplicationBuilder()
    .ConfigureApplication(options =>
    {
        options.RootComponent = ComponentTree.Template<App>();
        options.Components = components;
        options.Services = services;
        options.State = state;
        options.ErrorHandler = RecordError;
    })
    .Build()
    .UseRouter(router)
    .Use(async (context, next) =>
    {
        await RestoreSessionAsync(context.Stopping);
        await next(context);
    })
    .RunAsync();
```

The Browser host is selected explicitly by constructing `BrowserApplicationBuilder`. There is no
static application facade in Core, Browser, or the SDK.

Because `IServiceProvider` is lookup-only, Viu cannot offer container-agnostic service registration.
Assigning `ApplicationOptions.Services` attaches the developer's provider; feature packages must not
pretend they can mutate it.

### Why a SPA needs this

The packaged showcase performs the whole sequence by hand today
([`../viu-examples/.../Program.cs:19`](https://github.com/assimalign/viu-examples/blob/main/examples/Assimalign.Viu.Showcase/Program.cs)):
initialize browser router history, construct the router, await initial navigation, install
`RouterLinkDomBridge`, mount, wait forever, then uninstall the bridge and dispose routing resources.
That is a textbook around-lifetime concern. `UseRouter(router)` in `Assimalign.Viu.Browser.Router`
collapses it:

```csharp
return application.Use(async (context, next) =>
{
    RouterLinkDomBridge.Install();
    try
    {
        await router.ReadyAsync(context.Stopping);
        await next(context);
    }
    finally
    {
        RouterLinkDomBridge.Uninstall();
    }
});
```

Application middleware also suits session restoration, persisted-state hydration, telemetry and
devtools attachment, service-worker initialization, prerequisite checks, and paired global-listener
cleanup.

It must **not** wrap render or patch, DOM event dispatch, component lifecycle, state actions,
navigation guards, or SSR rendering. Those have typed domain-specific pipelines already, and the
renderer and event paths are too hot or too synchronously constrained for general asynchronous
middleware.

### Host and server separation

`IApplication` represents a persistent runnable host: Browser, a future WebView, and an
application-level test host. `ServerApplication` becomes a composition object
(`ServerRenderApplication`) and stops implementing `IApplication`. If interception is wanted later,
SSR gets its own per-render `ServerRenderMiddleware` carrying `SsrContext`, writer/output, and
cancellation. A once-per-SPA lifetime and a once-per-request render are different abstractions.

### Also required

- Keep lower-level mount APIs in `Assimalign.Viu.Browser` for embedding and testing, documented as
  bypassing top-level lifetime middleware. Core carries no host node type.
- Provide empty default component and service resolvers, so a primitive application needs no dummy
  dependencies.
- Pin middleware ordering, startup signaling, terminal pending behavior, background failure reporting,
  cancellation, borrowed ownership, and mount bypass against the production Browser lifetime.
- Compile the documented fluent example as a package-consumer test so the guide cannot drift again —
  the getting-started guide currently documents several removed dependency APIs.

## Waves

**Wave 1 — surface delivery and guardrails.** Zero behavior change. Make the surface visible,
hideable, diffable, and stop shipping what should never have shipped.

**Wave 2 — internalization and single source of truth.** The largest single reduction available
(~120 types), almost entirely mechanical via `InternalsVisibleTo`, which the repo already uses in 16
places. Must precede baseline generation so the baseline never records surface that is about to go.

**Wave 2A — application lifetime redesign (D5/D5a).** Delivered as [V01.01.14.08]. Sequenced here, not later,
because it *deletes* types the polish waves would otherwise spend effort hardening, documenting, and
renaming. It is a hard break, which D1 makes free.

> **Sequencing constraint:** Wave 2A and Wave 2's `.05` internalization both edit
> `Assimalign.Viu.Core` heavily and must be run **sequentially, not in parallel**. Prefer 2A first —
> internalizing types that D5 deletes is wasted work, and the post-redesign surface is the one the
> `.04` public-API baseline should record.

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

- ~~`.github/workflows/release.yml:230,299` and `docs/RELEASING.md:10-11` assert 21 packages while
  `scripts/Pack-Release.ps1:158-162` produces 22 — **the publish lane throws today.**~~
  **FIXED** — confirmed live on the `[V01.01.14.01]` merge (`Expected 21 release packages, found 22.`),
  which was the sixth consecutive `release.yml` failure. The count is now published by the
  `pack-packages` job and consumed by both publish jobs, so it derives from the same authoritative
  id list `Pack-Release.ps1` validates against and cannot drift again.
- `HelperNames.ResolveFilter` names `_resolveFilter`, which exists on no runtime surface.
- `SingleFileComponentSourceEmitter`'s `DomRenderHelperNames` array substring-scans the emitted body
  to decide whether to write the DOM `using static`; adding an 11th DOM helper without updating it
  silently omits the import, failing with CS0103 in the **consumer's** build.
- The folder `analyzers/Assimalign.Viu.Generators.Reactive/` does not match its assembly id
  `Assimalign.Viu.Generators.Reactivity`, violating the repo's folder-name-is-the-assembly-id rule.
