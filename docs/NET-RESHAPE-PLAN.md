# .NET reshape plan — from faithful port to idiomatic framework

**Status: in progress — R1, R2, and R3 implemented (awaiting main-session review); R4+ not started.** This
document is the session-independent source of truth for the reshape arc. Any session (human or agent)
resuming this work reads this file first, checks the *State* table, and continues from the first
incomplete unit. Update the State table in the same commit as any unit's progress.

## Motivation (recorded 2026-07-20, direction set by Chase)

The faithful Vue 3 re-implementation has reached its Wave 4/5 milestone: the port is functionally
broad and semantically pinned. The next arc converts it into a framework a typical .NET developer
finds familiar — public reactive primitives, one consolidated core library rooted at the
`Assimalign.Viu` namespace, browser naming instead of DOM-port naming, an application model with a
builder, and bring-your-own dependency injection over `System.IServiceProvider`. Upstream parity
of *behavior* stays; upstream *packaging and naming* stops being load-bearing.

## Hard sequencing prerequisites

1. **[SATISFIED 2026-07-20]** ~~Merge the four green Wave 5 PRs first~~: #223, #224, #226, #228 are
   all merged to `main`. The reshape renames sweep
   nearly every file those PRs touch — merging them after the renames means unresolvable conflict
   churn. Do not start unit R2 until all four are merged.
2. **The hydration branch is the train base.** `feature/V01.01.07.03-hydration-walker` (issue #66,
   stacked on #224's branch) carries the walker plus the four review fixes (snapshot-safe fragment
   removal + text split, upstream-aligned `ShouldHydrateProperty`, exact `data-allow-mismatch`
   gating). Its PR is deliberately **unpublished** — the user holds it so the reshape rides the
   same train. After #224 merges, restack it onto `main`; refactor units stack on it in order.
3. **PR shape at publish time**: preference is the established bottom-up stacked-PR train
   (hydration → R1 → R2 → R3 → R4 → R5, each PR reviewable alone). A single combined PR is the
   fallback if the user prefers one merge. Decide at publish; nothing publishes until the user says.

## Work units (stacked branch train, in order)

Each unit gets a WBS work item via the `viu-work-items` skill before its branch starts (codes below
are the intended placements — verify the next free child code at creation time). Each unit follows
the session pattern: Opus agent in a worktree, local commits only, main-session review, no push
until reviewed.

### R1 — Reactivity public surface (user item 1)

- **WBS / branch**: `V01.01.02.10` (feature under the Reactivity epic #6) /
  `feature/V01.01.02.10-reactivity-public-surface`, stacked on the hydration branch.
- **Scope**: in `Assimalign.Viu.Reactivity` as it exists today (pre-move, so this unit is cleanly
  reviewable and the later move carries it):
  - Rename `Internal/Link.cs` → `SubscriberLink.cs`; type `Link` → `SubscriberLink`; make it
    **public** with readable properties; every state-mutating member (version bookkeeping, list
    splicing, recycling) stays `internal`.
  - Make `Subscriber` public the same way: the type and its observable properties public-readable;
    tracking/notification internals stay `internal` (the existing `public abstract` +
    `private protected` ctor hot-path pattern already points this direction — extend it, do not
    regress the vtable-dispatch design in `general-rules.md`).
  - The tracked-reference abstraction (user wrote "IITrackedReference" — **verify the exact
    current type name in `src/`**, likely `ITrackedReference` or similar internal interface):
    make it public and move it to `Abstraction/` (repo convention is singular `Abstraction/`,
    not `Abstractions/` — follow the repo).
  - Public members gain XML docs with upstream counterpart links; new tests pin that the public
    surface cannot corrupt engine state (nothing settable that breaks version counters).
- **Non-goals**: no behavior change; dependency-engine semantics and test run-counts identical.

### R2 — Consolidate Reactivity into `Assimalign.Viu.Core`, root namespace `Assimalign.Viu` (user items 2 + 3)

- **WBS / branch**: `V01.01.12.21` (Tooling epic #89 — precedent: `.09` modularization, `.18`
  rename) / `feature/V01.01.12.21-core-consolidation`, stacked on R1.
- **Scope** (one unit because splitting the move from the rename would touch every file twice):
  - Merge `Assimalign.Viu.Reactivity` sources+tests into the RuntimeCore library; delete the
    Reactivity library folder.
  - Rename `Assimalign.Viu.RuntimeCore` → **`Assimalign.Viu.Core`** (folder = assembly = package
    id), but with **`<RootNamespace>Assimalign.Viu</RootNamespace>`** — all types in the merged
    library declare `namespace Assimalign.Viu;`. Remove `RuntimeCore`/`Reactivity` from every
    namespace and `using` repo-wide.
  - **Amend `.claude/rules/general-rules.md`**: the "namespace == assembly name" rule gains the
    deliberate exception — `Assimalign.Viu.Core` roots at `Assimalign.Viu` (record rationale: the
    core IS the product; every other library keeps namespace == assembly id). Also update the
    rule's `Subscriber` example path and any Reactivity references.
  - Rename `analyzers/Assimalign.Viu.Reactivity.Generators` → `Assimalign.Viu.Core.Generators`
    (assembly, namespace, `ViuAnalyzerReference` names, generator emitted `global::` qualifiers —
    note `Assimalign.Viu.Syntax.Generators` emits `global::Assimalign.Viu.RuntimeCore.*` today,
    from the #216 bridge — becomes `global::Assimalign.Viu.*`).
  - Cascade (checklist, none optional): every `ViuProjectReference`; `InternalsVisibleTo` strings
    (grantors and grantees); `frameworks/Assimalign.Viu.App.props` `@(ViuFrameworkAssembly)` +
    analyzer manifest + FrameworkList entries; root + per-area `slnx` (delete Reactivity's, rename
    RuntimeCore's); CI workflows (`area-reactivity.yml` deleted, `area-runtimecore.yml` →
    `area-core.yml` with new paths/lane names); shared-source `<Compile Include>` link paths (the
    frozen-path rule in general-rules — update every linking csproj in the same change);
    benchmarks project references; README tables; `docs/PLAN.md` architecture map (annotate the
    V01.01.02/V01.01.03 rows as merged/renamed — epics keep their history); per-library docs
    moved/merged; getting-started guide namespaces; sample apps.
- **Verify**: full solution + every suite + trimmed WebApp publish + budget gate + interop-count
  gate green; `grep -ri runtimecore` and `-ri "Viu.Reactivity"` return only deliberate historical
  notes (PLAN/ADR history).

### R3 — `Assimalign.Viu.RuntimeDom` → `Assimalign.Viu.Browser` (user item 4)

- **WBS / branch**: `V01.01.12.22` (Tooling) / `feature/V01.01.12.22-browser-rename`, stacked on R2.
- **Scope**: rename library folder/assembly/namespace to `Assimalign.Viu.Browser`; **consequence
  the user did not name but consistency demands (flag in PR): `Assimalign.Viu.Router.RuntimeDom`
  → `Assimalign.Viu.Router.Browser`**. Cascade identical in kind to R2: references, IVT, framework
  pack manifests, slnx files, `area-runtimedom.yml` → `area-browser.yml` +
  `area-router-runtimedom.yml` → `area-router-browser.yml`, `viu-dom.js` asset path/`_content/`
  segment (SDK `assets/` flow + module import paths in `BrowserRuntime`/`RouterHistory` — the
  `/_content/<package id>/` URLs change with the package ids), docs, samples, guide.
- **Open naming question for the user at review**: keep `viu-dom.js` filename (it is DOM-bridge
  JS, still accurate) or rename to `viu-browser.js` — default: keep filename, change only the
  `_content` package segment.

### R4 — Application model: `IApplication` + abstract base + `IApplicationBuilder` (user item 5)

- **WBS / branch**: `V01.01.03.23` (RuntimeCore→Core epic #16) /
  `feature/V01.01.03.23-application-model`, stacked on R3.
- **Scope** (design work, not mechanical — the issue body finalizes the contract):
  - `IApplication` (+ generic surface as needed) in Core `Abstraction/`; abstract
    `Application` base with virtual lifecycle seams (creation, plugin install, mount, dispose)
    replacing today's sealed-shape `Application<TNode>`.
  - **Kill the external `BrowserRuntime.InitializeAsync()` step**: `BrowserApplication` (Browser
    library) implements/extends the base and owns its own initialization internally (module
    imports awaited inside its mount path or builder `Build`), so consumer bootstrap is
    builder → build → mount with no runtime pre-call. `ServerApplication` (ServerRenderer) and the
    Testing renderer's mount align to the same contract.
  - `IApplicationBuilder` + default `ApplicationBuilder` in Core: configuration surface for root
    component/props, plugins (`Use`), provides, and (R5) services; platform packages supply
    entry points (e.g. Browser's `CreateApplicationBuilder`). Existing `CreateApp`/`CreateSsrApp`
    become thin wrappers or get obsoleted per the issue's decision — samples/guide updated either way.
- **Compat blast radius**: every sample `Program.cs`, Testing harness mounts, Router/Store
  `AsPlugin` docs, getting-started guide, SSR round-trip tests.

### R5 — Bring-your-own DI: `System.IServiceProvider` integration (user item 6)

- **WBS / branch**: `V01.01.03.24` (Core epic) /
  `feature/V01.01.03.24-service-provider-integration`, stacked on R4.
- **Scope**: replace the current app-level dependency mechanism with `System.IServiceProvider`:
  - `IApplicationBuilder` accepts an **`IServiceProviderBuilder`** (our interface, e.g.
    `IServiceProvider Build()` over registrations) so users bring any container; the built
    provider hangs off the application and is reachable from component context.
  - Default implementation must be **AOT-safe** (factory-delegate registry — no reflection
    activation, no required `Microsoft.Extensions.DependencyInjection` dependency in Core; an
    MS.Ext.DI adapter can be a later optional package).
  - **Boundary to preserve (Vue parity)**: component-tree `Provide`/`Inject` with typed
    `InjectionKey<T>` stays — it is the Vue-semantic feature. What migrates to services is the
    app-level singleton wiring (the pattern Router/Store use via `app.Use` + app-wide provides);
    Router/Store gain service-based registration/resolution paths per the issue body's decision.
  - Samples + guide updated to the builder+services bootstrap.

## State (update in the same commit as any progress)

| Unit | Work item | Branch | State |
| --- | --- | --- | --- |
| Train base | #66 hydration + review fixes | `feature/V01.01.07.03-hydration-walker` | fixes done (c530119), restacked on main, branch pushed, PR held |
| R1 | #230 (`V01.01.02.10`) | `feature/V01.01.02.10-reactivity-public-surface` | implemented, awaiting main-session review |
| R2 | #231 (`V01.01.12.21`) | `feature/V01.01.12.21-core-consolidation` | implemented, awaiting main-session review |
| R3 | #232 (`V01.01.12.22`) | `feature/V01.01.12.22-browser-rename` | implemented, awaiting main-session review |
| R4 | not yet filed (`V01.01.03.23`) | `feature/V01.01.03.23-application-model` | not started |
| R5 | not yet filed (`V01.01.03.24`) | `feature/V01.01.03.24-service-provider-integration` | not started |

## Resume protocol

1. Read this file and `git branch --list 'feature/V01.01*'` + `gh pr list` to reconcile the State
   table with reality (branches/PRs beat this table if they disagree — then fix the table).
2. File the next unfiled work item with `.claude/skills/viu-work-items/scripts/New-ViuWorkItem.ps1`
   (issue body carries this document's scope for that unit plus anything learned since).
3. Launch the unit as an Opus agent in a worktree, stacked on the previous unit's branch; agent
   commits locally, main session reviews, pushes only after review; **no PRs until the user
   lifts the hold**.
4. R2 is the point of no return for parallel work: while R2+ are in flight, file new non-reshape
   work items instead of starting branches off `main` (they would collide with the renames).
