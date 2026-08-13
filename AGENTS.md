# Viu

A standalone C#/.NET user-interface framework, targeting the browser through the .NET WebAssembly
build tools (`Microsoft.NET.Sdk.WebAssembly`, `JSImport`/`JSExport` interop). Viu renders through a
hierarchical virtual-node tree with compiler-informed diffing, split across `Assimalign.Viu.*` class
libraries by responsibility (reactivity, the component vocabulary, the host-neutral runtime core, the
browser host, the parser cluster, server rendering, routing, state, tooling). Roslyn source
generators are the sanctioned metaprogramming mechanism — WASM is AOT/trimming territory, so
reflection-based serialization and dynamic code generation are forbidden.

`docs/SPECIFICATION.md` is the authority for Viu's semantics; no external project's behavior is
(decision of 2026-08-02). Viu does ship a **`.vue` single-file-component compatibility parser** as a
product feature ([V01.01.06.09], #250), alongside WHATWG HTML serialization and Language Server
Protocol compatibility. Tailwind CSS v4.3.3 is only the target of the standalone add-on at
`libraries/Utilities/Assimalign.Viu.UtilityCss`; it is not a Viu core compatibility target (owner
decision, 2026-08-13). Component `<style>` CSS remains fully supported, including scoping, bundling,
and hot reload.

## Layout

- `libraries/` — publicly consumable package surfaces in the area-based inverted layout
  `libraries/<Area>/<AssemblyId>/{src,test}`. Areas are `Browser` (Browser, Browser.Router),
  `DevTools` (DevTools, Testing), `Router`, `Runtime` (Components, Core, Reactivity, State),
  `ServerRenderer`, `Syntax` (all five Syntax projects), and `Utilities` (standalone add-ons). The public netstandard2.0 build/editor-time
  parser cluster is deliberately here so developers can parse CSS, templates, and single-file
  components directly; `libraries/` no longer means runtime-only.
- `tooling/` — implementation projects under `tooling/<Area>/<AssemblyId>/{src,test}`:
  `Compiler/{Assimalign.Viu.Compiler.Css, Assimalign.Viu.Compiler.SingleFileComponent}` and
  `Editor/{Assimalign.Viu.LanguageService, Assimalign.Viu.LanguageServer}`. No tooling project is
  currently independently published. The independently published UtilityCss add-on lives at
  `libraries/Utilities/Assimalign.Viu.UtilityCss/{src,test}`. It remains outside every Viu SDK and
  framework surface; consumer MSBuild integration arrives separately through #346.
- `extensions/` — ecosystem integration points: `VisualStudio/`, `VisualStudioCode/`, and `dotnet/`;
  templates live at `extensions/dotnet/Assimalign.Viu.Templates`.
- `benchmarks/Assimalign.Viu.Testing.EndToEnd/` — real-browser end-to-end harness.
- `sdks/<SdkId>/Tasks/{src,test}` — SDK task projects.
- `../viu-examples/` — external packaged-consumer WASM showcase (separate repository)
- `docs/` — repo-level planning docs (`PLAN.md` is the delivery plan)
- `.claude/rules/` — the canonical working conventions for this repo (auto-load by path):
  `general-rules` (C# style, Abstraction/Internal folders, whole-word naming, explicit usings, AOT),
  `build-system` (central `build/`, `ViuProjectReference`/`ViuPackageReference`), `testing`,
  `documentation`, `deviations`, `checklist`, and `workflow` (branches, WBS, scope creep)

## Build and test

- `dotnet build Assimalign.Viu.slnx`
- `dotnet test <project>/test/`
- Run the showcase from the sibling `viu-examples` repository after packing `_out/packages`

## Work tracking

All development is tracked as WBS-coded work items (`[V01.01.NN...]` titles) in the org GitHub
Project **#15 "Viu"**, mirroring the sibling Cohesion repo's model. Use the **viu-work-items**
skill (`.agents/skills/viu-work-items/`, mirrored under `.claude/skills/viu-work-items/`) to create,
place, and link items — especially to capture
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
file is touched — do not re-derive conventions from scratch.

Viu is a **standalone** C#/.NET WebAssembly UI framework. **`docs/SPECIFICATION.md` is the authority
for Viu's semantics**, and behavior is pinned by tests in this repository — no external project's
behavior, release, or roadmap is authoritative for Viu (decision of 2026-08-02). Where a type
implements a documented **external compatibility target** — the `.vue` single-file-component
container format ([V01.01.06.09], a shipping feature), WHATWG HTML serialization, or the Language
Server Protocol — name and link that target. That is a compatibility *requirement* on a foreign
format, not a semantic authority over Viu. Tailwind CSS v4.3.3 is only the target of the standalone
add-on at `libraries/Utilities/Assimalign.Viu.UtilityCss`; it is not a Viu core compatibility
target.

### Project layout

- Publicly consumable package surfaces use the area-based inverted layout
  `libraries/<Area>/<AssemblyId>/{src,test}`. The areas are `Browser` (Browser, Browser.Router),
  `DevTools` (DevTools, Testing), `Router`, `Runtime` (Components, Core, Reactivity, State),
  `ServerRenderer`, `Syntax` (Syntax plus Css, Html, SingleFileComponent, and Templates), and
  `Utilities` (standalone add-ons); the
  assembly-id folder remains the package/project identity. `libraries/` therefore contains both
  runtime libraries and the public netstandard2.0 build/editor-time parser cluster. The parsers are
  deliberately consumable by developers and are the foundation for future extensible tooling;
  `libraries/` does not mean runtime-only.
- Compiler and editor implementation projects use the same area-based invariant under
  `tooling/<Area>/<AssemblyId>/{src,test}`: `Compiler/` contains the two `Assimalign.Viu.Compiler.*`
  composition roots and `Editor/` contains `Assimalign.Viu.LanguageService` and
  `Assimalign.Viu.LanguageServer`. The location carries the developer-tooling classification without
  adding a blanket `Tooling.` assembly/namespace segment. No tooling project is currently an
  independently published package.
- `libraries/Utilities/Assimalign.Viu.UtilityCss/{src,test}` is the independently published,
  standalone UtilityCss add-on library. It is not integrated into a Viu SDK or framework surface;
  consumer MSBuild integration arrives separately through [V01.01.12.30] (#346). Utility CSS remains
  non-normative for Viu core. Component `<style>` CSS remains fully supported, including scoping,
  bundling, and hot reload.
- Ecosystem integrations use `extensions/{VisualStudio|VisualStudioCode|dotnet}`; templates live at
  `extensions/dotnet/Assimalign.Viu.Templates`. End-to-end testing lives at
  `benchmarks/Assimalign.Viu.Testing.EndToEnd`, and SDK task projects use
  `sdks/<SdkId>/Tasks/{src,test}`.
- Examples live in the separate sibling `viu-examples` repository; repo planning docs live in
  `docs/`; the base and Browser consumer-facing MSBuild SDKs live in `sdks/`, and the
  `Assimalign.Viu.App` / `Assimalign.Viu.App.Browser` shared-framework pack producers live in
  `frameworks/` (see [`.claude/rules/build-system.md`](.claude/rules/build-system.md)).

### Namespaces

- **File-scoped** namespace declarations (`namespace X;`).
- **Namespace == assembly name**, flat. Every file in `Assimalign.Viu.Browser` declares
  `namespace Assimalign.Viu.Browser;` regardless of subfolder. `Abstraction/` and `Internal/` are
  **physical folders only** — they never appear in a namespace.
- **Recorded exception (origin [V01.01.12.21] R2; retained through the 2026-07 redesign, see
  [V01.01.11.04.02] #251):** `Assimalign.Viu.Core` roots every type at the **`Assimalign.Viu`**
  namespace. The R2 consolidation was superseded when Reactivity, Components, and State were re-split;
  the root-namespace deviation survives for Core alone. Every other library keeps namespace ==
  assembly id.

### Folders within `src/`

- **Public interfaces** → `src/Abstraction/` (flat).
- **Internal types** (classes, structs, enums, records, **and internal interfaces**) → `src/Internal/` (flat).
- **Delegates** (public delegate declarations) → `src/Delegates/`.
- **Public non-interface types** group into **feature folders** (`Rendering/`, `Components/`, `Watch/`, `Blocks/`, …): one folder per coherent feature set. Types used across the whole library (the "currency" types — e.g. `VirtualNode`, the flag enums, a library's facade) stay at the `src/` root.
- Folders are **physical only** — they never appear in a namespace. Create a folder only when it will contain files.
- Several projects link shared-source files through `<Compile Include>`, so their paths are frozen
  for this layout:
  - Syntax siblings link `Shims/IsExternalInit.cs` and `Shims/RequiredMemberShims.cs` through
    `..\..\Assimalign.Viu.Syntax\src\Shims\<File>`.
  - `Assimalign.Viu.Compiler.Css`, `Assimalign.Viu.Compiler.SingleFileComponent`, and the standalone
    `Assimalign.Viu.UtilityCss` engine link those shims through
    `$(ViuRepositoryDirectory)libraries\Syntax\Assimalign.Viu.Syntax\src\Shims\<File>`.
  - `Assimalign.Viu.Syntax.Templates` links `PatchFlags.cs` and `SlotStability.cs` from
    `$(ViuRepositoryDirectory)libraries\Runtime\Assimalign.Viu.Components\src\`, and links
    `DomKnowledgeData.cs` from
    `$(ViuRepositoryDirectory)libraries\ServerRenderer\Assimalign.Viu.ServerRenderer\src\Internal\`.
  - The Visual Studio project links the external-init shim through
    `$(ViuRepositoryDirectory)libraries\Syntax\Assimalign.Viu.Syntax\src\Shims\IsExternalInit.cs`;
    its source and test projects link `Internal/DomKnowledgeData.cs` through
    `$(ViuRepositoryDirectory)libraries\ServerRenderer\Assimalign.Viu.ServerRenderer\src\Internal\DomKnowledgeData.cs`.
  Moving any owner or consumer requires updating every linking csproj in the same change.

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
  linker-unfriendly activation paths.** Roslyn **source generators** are the sanctioned path for every
  form of metaprogramming — reactive property wrappers, component activation, and template
  compilation all happen at build time, never through runtime interception or emitted IL.
- Shipping libraries set `<IsAotCompatible>true</IsAotCompatible>` (see [`.claude/rules/build-system.md`](.claude/rules/build-system.md)).
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

- **`<ViuProjectReference Include="Assimalign.Viu.Components" />`** — public project reference (flows as a
  `.nupkg` dependency). Resolved by assembly name across the indexed repository roots, including
  `libraries/` and `tooling/`.
- **`<ViuPrivateProjectReference Include="..." />`** — private reference (`PrivateAssets=all`; does not
  flow to consumers).
- **`<ViuPackageReference Include="xunit" />`** — package reference with **no `Version` attribute**;
  versions are centralized in `build/Targets/Build.References.Packages.targets`. To add or bump a package,
  edit that central file.
- **`<ViuAnalyzerReference … />`** — for Roslyn analyzers / source generators (see
  `build/Targets/Build.References.Analyzers.targets`).

### Target framework and language

- Opt a project into its TFM via the central alias, never a hardcoded string:
  `<TargetFramework>$(TargetFrameworkForLibraries)</TargetFramework>` (net10.0). Analyzers and
  compiler/build-time tooling hosted by Roslyn or MSBuild use
  `$(TargetFrameworkForAnalyzers)` (netstandard2.0).
- `Nullable`, `LangVersion=preview`, `EnablePreviewFeatures=true`, and `EnforceCodeStyleInBuild` flow
  centrally from `build/Targets/` — do **not** set them per-csproj.

### csproj shapes

Shipping runtime library (`src/`):

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>$(TargetFrameworkForLibraries)</TargetFramework>
    <IsAotCompatible>true</IsAotCompatible>
  </PropertyGroup>
  <!-- optional -->
  <ItemGroup>
    <ViuProjectReference Include="Assimalign.Viu.Components" />
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

Sample apps live in `assimalign/viu-examples` and consume the packaged
`Assimalign.Viu.Sdk.Browser`/frameworks from `_out/packages`; they must not use
`ViuProjectReference`. Host-neutral component libraries use `Assimalign.Viu.Sdk`.

### Versioning and packaging

- The version is centralized in `build/Targets/Build.Version.props` (`$(ViuVersion)` /
  `ViuVersionPrefix` / `ViuVersionSuffix`). **No per-project `<Version>`** — set `VersionPrefix` /
  `VersionSuffix` only through the central file.
- Package output goes to `$(ViuOutputPathForLibraries)` (`_out/packages`).

### Adding a new library

1. Use `libraries/<Area>/Assimalign.Viu.<Name>/{src,test}` for a publicly consumable runtime, parser,
   or standalone add-on library, or `tooling/<Area>/Assimalign.Viu.<Name>/{src,test}` for compiler/build/editor
   implementation code. The area location carries the role without changing the assembly id or
   namespace.
2. Add both csprojs to `Assimalign.Viu.slnx`.
3. Wire a CI workflow entry for the area ([V01.01.12.02]).
4. No dangling references — when a project is renamed or moved, update every referrer.
5. Add host-neutral runtime framework members to `@(ViuFrameworkAssembly)` in
   `frameworks/Assimalign.Viu.App.props`; add Browser-only members to
   `frameworks/Assimalign.Viu.App.Browser.props`. Do not make the base depend on Browser.

### SDK and shared-framework packaging ([V01.01.12.19], #174; [V01.01.12.27], #323)

External consumers use the SDK matching their topology — never `ViuProjectReference`, which is the
**in-repo dogfooding** mechanism:

- **`Assimalign.Viu.Sdk`** uses `sdks/Assimalign.Viu.Sdk/Tasks/{src,test}` for task source and tests;
  its `Tasks/src` project produces the SDK, which chains `Microsoft.NET.Sdk` for host-neutral
  component libraries. It owns
  `.viu`/`.vue` AdditionalFiles, Syntax/Reactivity generators, targeting-only
  `Assimalign.Viu.App`, and component-style packing (`.viu.css` plus generated `buildTransitive`
  registration). It has no Browser, WebAssembly workload, browser assets, or runtime pack.
- **`Assimalign.Viu.Sdk.Browser`** uses `sdks/Assimalign.Viu.Sdk.Browser/Tasks/{src,test}` for task
  source and tests; its `Tasks/src` project produces the SDK, which imports and depends exactly on the
  base SDK and chains
  `Microsoft.NET.Sdk.WebAssembly`, registers `Assimalign.Viu.App.Browser`, and owns browser assets,
  application and component CSS bundling, hot reload, WebAssembly fixes, and publish-budget hooks.
- **`frameworks/Assimalign.Viu.App.Refs`** produces `Assimalign.Viu.App.Ref`: Reactivity,
  Components, State, Core, four package overrides, and the generator/parser closure. It is
  targeting-only.
- **`frameworks/Assimalign.Viu.App.Browser.Refs`** produces
  `Assimalign.Viu.App.Browser.Ref` with Browser and its override;
  **`frameworks/Assimalign.Viu.App.Browser.Runtime`** produces
  `Assimalign.Viu.App.Browser.Runtime.browser-wasm` with the base-plus-Browser runtime closure.
- **`frameworks/Assimalign.Viu.App.targets`** is the shared manifest writer/pack implementation;
  segment props select content. Runtime packs do not carry `PackageOverrides.txt`.
- **Local loop**: `scripts/Install-Local.ps1` packs both SDKs, two targeting packs, and the one
  Browser runtime pack into `_out/packages` (gitignored). Consumption docs: `sdks/README.md`.
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
- [ ] Projects follow `<root>/<Area>/<AssemblyId>/{src,test}`.
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
- [ ] Public APIs have XML docs stating what the member does, what it guarantees, and why its shape is
      what it is — in Viu's own vocabulary, citing a `SPECIFICATION.md` clause id or the `[Vxx.xx.xx]`
      work item, never another framework as the authority.
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
- **Specified behavior** — a change to behavior pinned by `docs/SPECIFICATION.md` or by a `[Vxx.xx.xx]`
  issue must be intentional, documented (in the type's XML docs and, where relevant, a `DESIGN.md`
  non-goal), and pinned by a test that asserts the *chosen* behavior. A specification clause, the tests
  that pin it, and the XML docs that cite it move together.
- **External compatibility targets** — the `.vue` single-file-component container format, WHATWG HTML
  serialization, the Language Server Protocol, and the WHATWG/W3C specifications Viu implements. There
  conformance to the foreign format *is* the requirement; a deliberate departure needs the same explicit
  confirmation. Tailwind CSS v4.3.3 is only the standalone utility-CSS add-on's compatibility target and is
  not part of this Viu core list.




## Testing

---
paths:
  - "**/test/**"
  - "**/*Tests*.cs"
---

- **xUnit v2 + Shouldly** are the sanctioned frameworks. Shouldly is the single assertion library — do not
  add FluentAssertions or lean on raw `Assert`. Package versions come centrally
  ([`.claude/rules/build-system.md`](.claude/rules/build-system.md)); the test csproj declares them by name via `ViuPackageReference`.
- Each library has a sibling test project under
  `libraries/<Area>/Assimalign.Viu.<Name>/test/`, while compiler and editor tests use
  `tooling/<Area>/Assimalign.Viu.<Name>/test/`. Test projects are `IsPackable=false` and reference
  their `src` via `ViuProjectReference`.
- Class `{Feature}Tests`; method names describe `Method_Scenario_ExpectedBehavior` (or an equally explicit
  phrase). Arrange / Act / Assert.

### What to assert

- Pin **observable behavior**, and for reactivity/caching semantics assert **run counts** (effect runs,
  getter invocations), not just final values — caching and dependency-tracking bugs hide behind
  correct-looking values.
- The test pins **Viu's own specified behavior** — the repository's tests *are* the authority for how
  Viu behaves. Spell the pinned behavior out in the test name or a comment ("an empty
  `DynamicChildren` list skips every child visit"), so a later reader can tell an intentional contract
  from an accidental one, and cite the clause in `docs/SPECIFICATION.md` or the `[Vxx.xx.xx]` work item
  that specified it. Never cite another framework's source or documentation as the reason a value is
  what it is.
- Where a test pins a documented **external compatibility target** — the `.vue` single-file-component
  container format, WHATWG HTML serialization, or the Language Server Protocol — name and link that
  target. There the citation *is* the requirement: the test asserts conformance to a foreign format
  Viu deliberately consumes. Tests inside the standalone utility-CSS add-on may still cite Tailwind CSS
  v4.3.3 as that add-on's target; this does not make Tailwind a Viu core compatibility target.
- Cover exception paths (throwing effects/getters, teardown under error) and lifecycle edges (stop,
  dispose, scope teardown), not just the happy path.

### DOM-free by default

- Unit tests must not require a browser. Exercise the runtime through an in-memory adapter/renderer (the
  Core `FakeDomAdapter` today; the shipping `Assimalign.Viu.Testing` renderer once
  [V01.01.11.01] lands). Real-browser coverage is the separate
  `benchmarks/Assimalign.Viu.Testing.EndToEnd` harness ([V01.01.11.03]).
- Use `InternalsVisibleTo` (in `src/Properties/AssemblyInfo.cs`) for tests that probe internal engine
  state.


## Documentation

---
paths:
  - "**/*.md"
  - "**/docs/**"
  - "**/*.cs"
---

- **XML doc comments on every public member.** A Viu doc states three things in Viu's own vocabulary:
  **what** the member does, **what it guarantees** (invariants, ordering, thread affinity, allocation
  behavior), and **why the shape is what it is** where that isn't obvious. Viu is a standalone
  framework; no doc comment may make Viu's behavior *derivative* of another project's.
  - **Banned:** "the C# port of X", "mirrors X", "counterpart of X", "upstream", "parity",
    "faithful", and any `vuejs.org` / `github.com/vuejs` / `router.vuejs.org` URL.
  - **Do not just delete the banned clause.** In many docs it is the only thing carrying the
    semantics (`"Creates a shallow ref (Vue's shallowRef())"`). Replace it with the behavior it stood
    in for: *"Creates a reference cell that notifies only on assignment of a new instance, never on
    mutation of the instance it holds."* A summary that says less after the edit is a regression.
  - **Intent markers must survive.** Where a clause like `(upstream parity)` was encoding "this is
    deliberate, do not 'fix' it", restate the intent as a Viu design decision. Where it pinned a
    frozen value layout (`PatchFlags`, `ShapeFlags`, `SlotStability`, the SSR hydration markers), restate
    it as Viu's own stability guarantee — the layout is a contract with previously compiled output.
- **Pinning behavior.** Behavior is pinned by (a) the prose in the doc comment itself, (b) a
  `[Vxx.xx.xx]` WBS reference to the issue that specified it, and (c) a test asserting the chosen
  behavior. Where `docs/SPECIFICATION.md` contains a clause for the behavior, cite it as text —
  `Specified by <c>[RND-FLAGS-1]</c>.` — never as a URL, so the API-reference generator
  ([V01.01.13.04]) resolves ids to anchors from one mapping. **Do not write a clause id the spec does
  not yet contain.**
- **External links.** `<see href>` is for genuine external standards and for foreign formats Viu
  consumes — W3C UI Events, WHATWG HTML, the Language Server Protocol, and the `.vue`
  single-file-component container format. It is never used to cite another framework as the
  authority for Viu's own behavior. Version-pin format-citation URLs and frame them explicitly, e.g.
  *"Container-format reference for the input this parser accepts: `<see href=…>`"*. Tailwind URLs are
  no longer authorized in Viu XML documentation; they may remain inside the standalone utility-CSS add-on
  tree as references for that add-on's v4.3.3 compatibility target.
- **The `.vue` compatibility surface is a shipping feature, not a legacy reference.** [V01.01.06.09]
  (#250) parses the tag-based `.vue` container so Vue single-file components compile under Viu. Every
  mention of `.vue` files, `VueSingleFileComponent*` types, `SingleFileComponentFormat.Vue`, the
  `viu-vue` document type, `**/*.vue` globs, and `.vue`-format spec compatibility **must be
  preserved** — removing them misdescribes the product. The banned-phrase rules above govern *how
  Viu's own semantics are described*, not the naming of the foreign format Viu reads.
- **Utility CSS is standalone add-on design, not Viu specification.** `docs/UTILITY-CSS-DESIGN.md` is
  non-normative after the 2026-08-13 removal of SDK, hot-reload, and editor integration. The
  independently published engine lives at `libraries/Utilities/Assimalign.Viu.UtilityCss`; it remains
  outside every Viu SDK and framework surface, with consumer MSBuild integration arriving separately
  through [V01.01.12.30] (#346). Do not cite the design document or Tailwind CSS as authority for Viu
  core behavior.
- **Other frameworks are performance research, not specification.** Viu tracks other renderers'
  performance work as an input to its own optimization backlog. That tracking lives in
  `docs/PERFORMANCE-RESEARCH.md` and in the work items it spawns — never in doc comments, and never
  as a reason a Viu behavior is what it is. An adopted technique is documented in Viu's terms and
  pinned by a Viu benchmark; origin acknowledgement, if wanted, goes in `docs/SPECIFICATION.md`
  § "Prior art and influences", once, centrally.
- Per-library design docs mature into
  `libraries/<Area>/Assimalign.Viu.<Name>/docs/OVERVIEW.md` or
  `tooling/<Area>/Assimalign.Viu.<Name>/docs/OVERVIEW.md` (what it is) beside `docs/DESIGN.md` (why it
  is shaped this way, WASM/AOT constraints, non-goals). Keep them
  current in the same change as the code — a `DESIGN.md` that lags the code actively misleads.
- Repo-level planning lives in `docs/` — `docs/SPECIFICATION.md` is the authoritative statement of
  Viu's semantics; `docs/PLAN.md` is the authoritative delivery narrative (architecture map, founding
  decisions, waves); the GitHub Project **#15** board is the authoritative backlog.
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
- Library layout is area-based and inverted:
  `libraries/<Area>/Assimalign.Viu.<Name>/{src,test}` for publicly consumable runtime and parser
  projects, and `tooling/<Area>/Assimalign.Viu.<Name>/{src,test}` for compiler and editor
  implementation projects. Standalone add-ons such as UtilityCss use the `libraries/Utilities/` area
  and remain outside Viu SDK and framework surfaces unless a work item explicitly changes that boundary.
- Preserve later-wave requirements in planning notes even when implementing only current-wave scope.
  If a ticket needs prerequisite work from another ticket, call that out rather than silently
  reordering.

### Hard constraints (every change)

- Trimming-safe and WASM/NativeAOT-compatible: no reflection-based serialization, no dynamic code
  generation, no linker-unfriendly activation paths. Roslyn source generators are the sanctioned path.
- The JS-interop boundary is the dominant performance cost — prefer batched interop over per-op
  calls, and always clean up JS-side handles and event listeners.
- `docs/SPECIFICATION.md` is the authority for Viu's semantics: cite the clause id in the issue, code
  comment, or test that pins the behavior, and never another framework's documentation. Where the
  change implements a documented external compatibility target (the `.vue` container format, WHATWG
  HTML serialization, or the Language Server Protocol), name and link that target — there conformance
  to the foreign format *is* the requirement. Tailwind CSS v4.3.3 may be cited only by work explicitly
  scoped to the standalone utility-CSS add-on, never as a Viu core target.
