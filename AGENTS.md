# Viu

A faithful re-implementation of Vue.js 3 in C#/.NET, targeting the browser through the .NET
WebAssembly build tools (`Microsoft.NET.Sdk.WebAssembly`, `JSImport`/`JSExport` interop). The
architecture mirrors Vue 3's package boundaries (`@vue/reactivity`, `runtime-core`, `runtime-dom`,
compiler packages, `server-renderer`) as `Assimalign.Viu.*` class libraries, with Roslyn source
generators standing in for everything Vue does with JS `Proxy` and runtime `new Function` — WASM is
AOT/trimming territory, so reflection-based serialization and dynamic code generation are forbidden.

## Layout

- `libraries/` — framework libraries, inverted layout: `libraries/Assimalign.Viu.<Name>/{src|test}`
  (the folder name is the assembly/package id; `src/` holds the shipping project, `test/` its tests —
  no area wrapper folders)
- `examples/` — sample WASM apps (`Assimalign.Viu.WebApp` is the current demo)
- `docs/` — repo-level planning docs (`PLAN.md` is the delivery plan)
- `.Codex/rules/` — the canonical working conventions for this repo (auto-load by path):
  `general-rules` (C# style, Abstraction/Internal folders, whole-word naming, explicit usings, AOT),
  `build-system` (central `build/`, `ViuProjectReference`/`ViuPackageReference`), `testing`,
  `documentation`, `deviations`, `checklist`, and `workflow` (branches, WBS, scope creep)

## Build and test

- `dotnet build Assimalign.Viu.slnx`
- `dotnet test <project>/tests/`
- Run the demo: `dotnet run --project examples/Assimalign.Viu.WebApp`

## Work tracking

All development is tracked as WBS-coded work items (`[V01.01.NN...]` titles) in the org GitHub
Project **#15 "Viu"**, mirroring the sibling Cohesion repo's model. Use the **viu-work-items**
skill (`.Codex/skills/viu-work-items/`) to create, place, and link items — especially to capture
scope creep discovered mid-branch. The GitHub issue body is the authoritative source of a work
item's requirements. Project #15 is viu-only; if `assimalign/cohesion` items (`[Lxx...]` codes)
ever appear on it, flag it (a project auto-add workflow may be re-adding them) and never modify
them from this repo.

## General rules (C#)

---
paths:
  - "**/*.cs"
  - "**/*.csproj"
---

These are the canonical coding conventions for Viu. They load automatically when a `.cs`/`.csproj`
file is touched — do not re-derive conventions from scratch. Viu is a faithful re-implementation of
Vue.js 3 in C#/.NET WebAssembly; where behavior mirrors Vue, **upstream vuejs/core (v3.5.x) semantics
win** — link the reference in the code, test, or issue that pins the behavior.

### Project layout

- Inverted library layout: `libraries/Assimalign.Viu.<Name>/{src|test}` — the folder name **is** the
  assembly / package id. `src/` holds the shipping project, `test/` its test project. No area wrapper
  folders. Package root is `Assimalign.Viu.*` (product name "Viu"; the GitHub repo slug is
  `assimalign/viu`).
- Examples live in `examples/`; repo planning docs in `docs/`; the consumer-facing MSBuild SDK in
  `sdks/` and the `Assimalign.Viu.App` shared-framework pack producers in `frameworks/` (see
  [build-system.md](build-system.md)).

### Namespaces

- **File-scoped** namespace declarations (`namespace X;`).
- **Namespace == assembly name**, flat. Every file in `Assimalign.Viu.Browser` declares
  `namespace Assimalign.Viu.Browser;` regardless of subfolder. `Abstraction/` and `Internal/` are
  **physical folders only** — they never appear in a namespace.
- **Recorded exception ([V01.01.12.21], `docs/NET-RESHAPE-PLAN.md` R2):** `Assimalign.Viu.Core` — the
  consolidated runtime core + reactivity — roots every type at the **`Assimalign.Viu`** namespace (set via
  `<RootNamespace>Assimalign.Viu</RootNamespace>` on its `src` csproj), *not* `Assimalign.Viu.Core`,
  because the core **is** the product and its primitives read best unprefixed (`Assimalign.Viu.Reference<T>`,
  `Assimalign.Viu.VirtualNode`). This is the single deliberate deviation from the rule; every other library
  keeps namespace == assembly id (the source-generator assemblies included).

### Folders within `src/`

- **Public interfaces** → `src/Abstraction/` (flat).
- **Internal types** (classes, structs, enums, records, **and internal interfaces**) → `src/Internal/` (flat).
- **Delegates** (public delegate declarations) → `src/Delegates/`.
- **Public non-interface types** group into **feature folders** (`Rendering/`, `Components/`, `Watch/`, `Blocks/`, …): one folder per coherent feature set. Types used across the whole library (the "currency" types — e.g. `VirtualNode`, the flag enums, a library's facade) stay at the `src/` root.
- Folders are **physical only** — they never appear in a namespace. Create a folder only when it will contain files.
- Linked shared-source files (`PatchFlags.cs`, `SlotFlags.cs`, `Internal/DomKnowledgeData.cs` from `Assimalign.Viu.Shared`; `Shims/IsExternalInit.cs`, `Shims/RequiredMemberShims.cs` from `Assimalign.Viu.Syntax`) are `<Compile Include>` targets from netstandard2.0 projects — **their paths are frozen**; moving them requires updating every linking csproj in the same change.

### Files and types

- **One public type per file**; the filename is the type name.
- Generic types use `{T}` in the filename: `Store<TState>` → `Store{TState}.cs`. Do **not** use `OfT`
  or similar suffixes in type names or filenames. (A root+generic split family may instead use the
  dotted `.T.cs` form, e.g. `ReactiveValue.cs` + `ReactiveValue.T.cs`, matching its siblings.)
- Group a variant family root-first when splitting (`VirtualDomPatch.cs` + one file per record).

### Naming — spell out whole words

- **No abbreviations.** `Ref` → `Reference`, `Dep` → `Dependency`, `Sub` → `Subscriber`, `Ops` →
  `Operations`, `Prev` → `Previous`, `Prop`/`Props` → `Property`/`Properties`. This applies to types,
  members, parameters, and locals.
- **Well-known acronyms stay acronyms**: DOM, HTML, CSS, SSR, AOT, JSON, WASM (e.g. `IVirtualDomAdapter`,
  `HtmlRenderer`). The approved list is exactly those seven; nothing else is treated as an acronym.
  **SFC is _not_ on the list** — identifiers spell out `SingleFileComponent` (the
  `Assimalign.Viu.Syntax.SingleFileComponent` area), never `Sfc`. Prose may still write "single-file
  component (SFC)".
- Interfaces begin with `I` (editorconfig-enforced at **error** severity).

### Using directives

- **Explicit usings only** — implicit/global usings are disabled repo-wide. Every file declares what it
  uses.
- Order: `System.*` (sorted) → third-party → `Assimalign.*`, then a blank line before the namespace.
  Usings sit **outside** the namespace.

### Design

- **Interface-first**: the public contract is an interface under `Abstraction/`; prefer `internal`
  concrete implementations (surfaced through the interface or a public facade like `Reactive`).
- **Dispatch on hot paths**: interfaces are for public contracts and cold paths. On the engine's hot
  paths (per-trigger notification, patching, diffing) prefer an **abstract base class** over an
  interface — .NET interface dispatch is measurably costlier than a vtable virtual call, and the gap
  widens on mono-wasm / NativeAOT. Put shared per-instance state on the base as fields (direct loads,
  no property-getter dispatch); `seal` concrete leaf types so the JIT can devirtualize. When a public
  type must derive from an otherwise-internal base, make the base a `public abstract` class with
  `internal` members and a `private protected` constructor so it stays opaque and un-subclassable
  externally (see `Assimalign.Viu.Core`'s `Subscriber`).
- **Single-threaded model**: the runtime targets the JS event loop. Ambient `static` state is acceptable,
  but any non-thread-safe type must say so in its XML docs.

### AOT / trimming (hard constraints)

- Trimming- and WASM/NativeAOT-safe: **no reflection-based serialization, no dynamic code generation, no
  linker-unfriendly activation paths.** Roslyn **source generators** are the sanctioned path for anything
  Vue does with `Proxy` or runtime `new Function`.
- Shipping libraries set `<IsAotCompatible>true</IsAotCompatible>` (see [build-system.md](build-system.md)).
- The JS-interop boundary is the dominant performance cost — batch interop, and always clean up JS-side
  handles and event listeners.





## Build system

---
paths:
  - "**/*.csproj"
  - "**/*.props"
  - "**/*.targets"
  - "build/**"
  - "Directory.Build.props"
  - "Directory.Build.targets"
  - "global.json"
  - "**/*.slnx"
---

Shared build logic is centralized under `build/` and imported repo-wide via `Directory.Build.props`
(→ `build/Build.props`) and `Directory.Build.targets` (→ `build/Build.targets`). **Shared build logic
belongs in `build/`, never duplicated in individual csprojs** — this is the most drift-prone area.

### Reference projects and packages by name

Never write a raw `<ProjectReference Include="..\..\...csproj" />` or `<PackageReference>` in a library,
test, or example csproj. Use the by-name item groups the build system resolves:

- **`<ViuProjectReference Include="Assimalign.Viu.Shared" />`** — public project reference (flows as a
  `.nupkg` dependency). Resolved by assembly name against `libraries/**/*.csproj`.
- **`<ViuPrivateProjectReference Include="..." />`** — private reference (`PrivateAssets=all`; does not
  flow to consumers).
- **`<ViuPackageReference Include="xunit" />`** — package reference with **no `Version` attribute**;
  versions are centralized in `build/Targets/Build.References.Packages.targets`. To add or bump a package,
  edit that central file.
- **`<ViuAnalyzerReference … />`** — for Roslyn analyzers / source generators (see
  `build/Targets/Build.References.Analyzers.targets`).

### Target framework and language

- Opt a project into its TFM via the central alias, never a hardcoded string:
  `<TargetFramework>$(TargetFrameworkForLibraries)</TargetFramework>` (net10.0). Analyzers use
  `$(TargetFrameworkForAnalyzers)` (netstandard2.0).
- `Nullable`, `LangVersion=preview`, `EnablePreviewFeatures=true`, and `EnforceCodeStyleInBuild` flow
  centrally from `build/Targets/` — do **not** set them per-csproj.

### csproj shapes

Shipping library (`src/`):

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>$(TargetFrameworkForLibraries)</TargetFramework>
    <IsAotCompatible>true</IsAotCompatible>
  </PropertyGroup>
  <!-- optional -->
  <ItemGroup>
    <ViuProjectReference Include="Assimalign.Viu.Shared" />
  </ItemGroup>
</Project>
```

Test project (`test/`):

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>$(TargetFrameworkForLibraries)</TargetFramework>
    <IsPackable>false</IsPackable>
  </PropertyGroup>
  <ItemGroup>
    <ViuPackageReference Include="Microsoft.NET.Test.Sdk" />
    <ViuPackageReference Include="xunit" />
    <ViuPackageReference Include="xunit.runner.visualstudio" />
    <ViuPackageReference Include="Shouldly" />
  </ItemGroup>
  <ItemGroup>
    <ViuProjectReference Include="Assimalign.Viu.<Name>" />
  </ItemGroup>
</Project>
```

Sample apps (`examples/`) keep their own SDK (e.g. `Microsoft.NET.Sdk.WebAssembly`), set
`<TargetFramework>$(TargetFrameworkLatest)</TargetFramework>`, use `ViuProjectReference`, and do **not**
set `IsAotCompatible` (they are not shipping libraries).

### Versioning and packaging

- The version is centralized in `build/Targets/Build.Version.props` (`$(ViuVersion)` /
  `ViuVersionPrefix` / `ViuVersionSuffix`). **No per-project `<Version>`** — set `VersionPrefix` /
  `VersionSuffix` only through the central file.
- Package output goes to `$(ViuOutputPathForLibraries)` (`_out/packages`).

### Adding a new library

1. `libraries/Assimalign.Viu.<Name>/{src,test}` with the two csproj shapes above.
2. Add both csprojs to `Assimalign.Viu.slnx`.
3. Wire a CI workflow entry for the area ([V01.01.12.02]).
4. No dangling references — when a project is renamed or moved, update every referrer.
5. If the library is a runtime framework member (ships in every Viu app), add it to
   `@(ViuFrameworkAssembly)` in `frameworks/Assimalign.Viu.App.props` so the framework packs
   deliver it.

### SDK and shared-framework packaging ([V01.01.12.19], #174)

External consumers use `<Project Sdk="Assimalign.Viu.Sdk">` — never `ViuProjectReference`, which is
the **in-repo dogfooding** mechanism. The packaging layer mirrors `assimalign/cohesion`:

- **`frameworks/Assimalign.Viu.App.props`** — the authoritative `@(ViuFrameworkAssembly)` /
  `@(ViuFrameworkAnalyzer)` manifest, gated on `$(ViuFrameworkName)`.
- **`frameworks/Assimalign.Viu.App.targets`** — the `ViuWriteFrameworkList` manifest writer and
  pack layout, branching on `$(ViuFrameworkKind)` = `Ref` (targeting pack: `ref/<tfm>/` +
  `data/FrameworkList.xml` + the generators and their parser closure at `analyzers/dotnet/cs/`,
  every DLL listed as an `Analyzer` entry) | `Runtime` (per-RID runtime pack: `runtimes/<rid>/lib/`
  + `data/RuntimeList.xml`).
- **`sdks/Assimalign.Viu.Sdk/`** — the SDK package (packable unit: `Tasks/…Tasks.csproj` with
  `PackageId=Assimalign.Viu.Sdk`). `Sdk.props` chains `Microsoft.NET.Sdk.WebAssembly`, imports a
  pack-time-frozen `Build.Version.props` snapshot, and registers the `KnownFrameworkReference` for
  `Assimalign.Viu.App` (`browser-wasm`). The `.viu` AdditionalFiles wiring and
  `Build.Css.Bundling.targets` are packed **from their in-repo source files**; the `ViuBundleCss`
  task ships under `Tasks/`; `viu-dom.js` ships under `assets/` and flows into consumer
  `wwwroot/_content/`.
- **Local loop**: `scripts/Install-Local.ps1` packs SDK → runtime pack(s) → ref pack into
  `_out/packages` (gitignored). Consumption docs: `sdks/README.md`.
- The `frameworks/` csprojs carry documented deviations from the no-raw-`ProjectReference` rule
  (build-order edges needing `ReferenceOutputAssembly=false` + `UndefineProperties` metadata the
  `ViuProjectReference` transform does not carry).

## Pre-completion checklist

---
paths:
  - "**/*.cs"
  - "**/*.csproj"
---

Run this before declaring any non-trivial change complete. Mark each applicable item ✅ or ❌; if anything
is ❌, fix it before reporting completion, not after. Mark genuinely inapplicable items N/A and move on.

### Build & test
- [ ] `dotnet build Assimalign.Viu.slnx` succeeds with **0 warnings, 0 errors**.
- [ ] Affected `dotnet test` projects pass; new behavior has tests (run counts pinned for reactive/caching
      semantics).
- [ ] For runtime/interop changes, the sample WASM app still builds.

### Structure & naming
- [ ] Public interfaces are in `Abstraction/`; internal types (incl. internal interfaces) in `Internal/`;
      public non-interface types at `src/` root.
- [ ] One public type per file; filename = type name; generics use `{T}` (no `OfT`).
- [ ] Whole words, no abbreviations (acronyms DOM/HTML/CSS/SSR/AOT excepted).
- [ ] File-scoped namespace == assembly name (flat); folders don't leak into namespaces.
- [ ] Explicit, ordered usings (System → third-party → Assimalign); no implicit/global usings.

### Build system
- [ ] Project/package refs use `ViuProjectReference` / `ViuPackageReference` (no raw refs, no inline
      versions); shared settings come from `build/`, not the csproj.
- [ ] Shipping libraries set `IsAotCompatible=true`; tests set `IsPackable=false`.

### Correctness & docs
- [ ] Trimming/WASM-AOT-safe (no reflection serialization, no dynamic codegen); JS handles/listeners
      cleaned up.
- [ ] Public APIs have XML docs; behavior mirroring Vue 3 links the upstream reference.
- [ ] The work item ([V01.01.NN…]) is referenced; scope creep captured via the `viu-work-items` skill.
- [ ] No dangling solution/project references after any rename or move.

## Deviating from these rules

---
paths:
  - "**/*.cs"
  - "**/*.csproj"
  - "**/*.props"
  - "**/*.targets"
---

The rules encode deliberate decisions. When a change must break one, follow this protocol rather than
silently complying or silently ignoring it:

1. **Name the rule** explicitly — don't quietly work around it.
2. **Confirm intent** with the user unless they already acknowledged the deviation.
3. **Scope it narrowly** — the exception covers this one case; the next component in the same session
   still follows the original rule.
4. **Document it in code** at the site:
   `// Deviates from the repo <rule name> rule per design decision: <one-line rationale>.`
5. **Surface it** in the change summary / PR description.

Rules that need especially explicit confirmation before deviating:

- **AOT / trimming safety** — no reflection-based serialization, no dynamic code generation.
- **The central build system** — `ViuProjectReference` / `ViuPackageReference` (no raw
  `<ProjectReference>` / `<PackageReference>`), the `build/` props/targets, and centralized versioning.
- **Upstream Vue 3 parity** — a behavioral divergence from vuejs/core v3.5 must be intentional, documented
  (in the type's XML docs and, where relevant, a `DESIGN.md` non-goal), and pinned by a test that asserts
  the *chosen* behavior.




## Testing

---
paths:
  - "**/test/**"
  - "**/*Tests*.cs"
---

- **xUnit v2 + Shouldly** are the sanctioned frameworks. Shouldly is the single assertion library — do not
  add FluentAssertions or lean on raw `Assert`. Package versions come centrally
  ([build-system.md](build-system.md)); the test csproj declares them by name via `ViuPackageReference`.
- Each library has a sibling test project at `libraries/Assimalign.Viu.<Name>/test/`
  (`Assimalign.Viu.<Name>.Tests`), `IsPackable=false`, referencing its `src` via `ViuProjectReference`.
- Class `{Feature}Tests`; method names describe `Method_Scenario_ExpectedBehavior` (or an equally explicit
  phrase). Arrange / Act / Assert.

### What to assert

- Pin **observable behavior**, and for reactivity/caching semantics assert **run counts** (effect runs,
  getter invocations), not just final values — caching and dependency-tracking bugs hide behind
  correct-looking values.
- Where behavior mirrors Vue 3, the test pins the **upstream contract** — reference the vuejs/core file or
  vuejs.org page in a comment so a divergence is caught, not enshrined.
- Cover exception paths (throwing effects/getters, teardown under error) and lifecycle edges (stop,
  dispose, scope teardown), not just the happy path.

### DOM-free by default

- Unit tests must not require a browser. Exercise the runtime through an in-memory adapter/renderer (the
  Core `FakeDomAdapter` today; the shipping `Assimalign.Viu.Testing` renderer once
  [V01.01.11.01] lands). Real-browser coverage is the separate e2e harness ([V01.01.11.03]).
- Use `InternalsVisibleTo` (in `src/Properties/AssemblyInfo.cs`) for tests that probe internal engine
  state.


## Documentation

---
paths:
  - "**/*.md"
  - "**/docs/**"
  - "**/*.cs"
---

- **XML doc comments on every public member.** Where a type or member mirrors a Vue 3 concept, name the
  counterpart and link the authoritative reference (vuejs.org or the vuejs/core source file) — e.g.
  "the C# port of Vue 3.5's `computed()`". This is how upstream semantics stay pinned.
- Per-library design docs mature into `libraries/Assimalign.Viu.<Name>/docs/OVERVIEW.md` (what it is) and
  `docs/DESIGN.md` (why it is shaped this way, C#/WASM divergences, non-goals). Keep them current in the
  same change as the code — a `DESIGN.md` that lags the code actively misleads.
- Repo-level planning lives in `docs/` — `docs/PLAN.md` is the authoritative narrative (architecture map,
  founding decisions, waves); the GitHub Project **#15** board is the authoritative backlog.
- Markdown docs use whole words and link related rules/issues so a future session can act without this
  conversation's context.


## Workflow

### Commits and branches

- Conventional commits: `type(scope): subject` with `feat|fix|docs|refactor|test|chore`.
- Branches: `main` (production), `feature/{name}`, `fix/{name}`, `docs/{name}`.
  Work tracked in the Viu GitHub Project uses `feature/<wbs>-<slug>`
  (e.g. `feature/V01.01.02.01-dependency-engine`) — the WBS in the branch names the feature in flight.

### GitHub Project execution metadata

- Work items follow `[<wbs>] <title>` (area epic `V01.01.NN` → feature `V01.01.NN.MM` → task
  `V01.01.NN.MM.PP`) in org Project **#15 "Viu"**. Use the `viu-work-items` skill to create,
  place, and link items — especially for capturing scope creep discovered mid-branch.
- **Priority**: lower number = higher priority (P001 before P002).
- **Wave**: lower number = earlier delivery (W01 = rendering foundation … W06 = enterprise polish).
- Autonomous work selection prefers unblocked items in the earliest Priority, then Wave. Conflict
  order: explicit user instruction → dependency/blocker relationships → Priority → Wave.
- The GitHub issue body is the authoritative source of a work item's requirements.
- Project #15 is viu-only. If `assimalign/cohesion` items (`[Lxx...]` codes) ever appear on it,
  flag it and never modify them from this repo.

### Backlog authoring

- Issue bodies must carry enough architectural boundary guidance for a future session to implement
  without this conversation's context: the target `Assimalign.Viu.<Area>` project, allowed
  dependency direction, and any interop/AOT/source-generator boundaries.
- Library layout is inverted: `libraries/Assimalign.Viu.<Name>/{src|test}` — folder name = assembly
  id, no area wrapper folders.
- Preserve later-wave requirements in planning notes even when implementing only current-wave scope.
  If a ticket needs prerequisite work from another ticket, call that out rather than silently
  reordering.

### Hard constraints (every change)

- Trimming-safe and WASM/NativeAOT-compatible: no reflection-based serialization, no dynamic code
  generation, no linker-unfriendly activation paths. Roslyn source generators are the sanctioned path.
- The JS-interop boundary is the dominant performance cost — prefer batched interop over per-op
  calls, and always clean up JS-side handles and event listeners.
- Where behavior mirrors Vue 3, upstream semantics win: link the vuejs.org / vuejs/core reference in
  the issue, code comment, or test that pins the behavior.
