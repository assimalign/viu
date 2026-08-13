# Viu

A standalone C#/.NET user-interface framework, running in the browser through the .NET WebAssembly
build tools (`Microsoft.NET.Sdk.WebAssembly`, `JSImport`/`JSExport` interop). Viu renders through a
**hierarchical virtual-node tree with compiler-informed diffing**: an application describes its UI as
an immutable tree of node descriptions, a build-time compiler annotates that tree with what can
change, and the runtime patches only the annotated parts.

[`docs/SPECIFICATION.md`](docs/SPECIFICATION.md) is the authoritative statement of what Viu is and
what it guarantees. Three constraints shape everything below it:

- **Roslyn source generators are the sanctioned metaprogramming mechanism.** WASM is AOT/trimming
  territory, so reflection-based serialization and runtime code generation are forbidden. Templates,
  `[Reactive]` property bodies, and activation paths are all emitted at build time; there is no
  runtime compiler.
- **Reactivity is explicit reference cells.** `Reference<T>` and `Computed<T>` are read and written
  through `.Value`, so a dependency is established by an ordinary property read rather than by
  interception — nothing is tracked invisibly and nothing needs a runtime object proxy.
- **The JS-interop boundary is the performance budget.** Crossing it is the dominant runtime cost, so
  DOM mutations batch into as few crossings as possible and static content is stringified
  aggressively.

Those three are recorded as architecture decisions in [`docs/adr/`](docs/adr/); the delivery
narrative — waves, the WBS map, and the founding decisions — lives in
[`docs/PLAN.md`](docs/PLAN.md).

Viu also ships a **`.vue` single-file-component compatibility parser** as a product feature
([V01.01.06.09], [#250](https://github.com/assimalign/viu/issues/250)): tag-based `.vue` files
compile under Viu alongside the canonical `.viu` container. That compatibility target on a
documented external format is specified in
[§9 of the specification](docs/SPECIFICATION.md#9-vue-compatibility--a-shipping-feature).

## Status

Early, active development, delivered in waves (see [`docs/PLAN.md`](docs/PLAN.md) and the
[project board](https://github.com/orgs/assimalign/projects/15) for the authoritative status). The
reactive core, the host-neutral renderer with its scheduler and component model, the browser DOM
bridge, the template compiler front end, the `.viu`/`.vue` single-file-component pipeline, the
router's DOM-free route table and matcher, and the state package's `StateStoreDefinition` /
`StateStoreRegistry` API are all in the tree at varying maturity; each library's `docs/OVERVIEW.md`
states what it currently provides, and the specification describes implemented behavior only (its
[§17](docs/SPECIFICATION.md#17-non-goals-and-current-limits) carries the non-goals and the current
limits). The packaged-consumer showcase lives in the separate
[`assimalign/viu-examples`](https://github.com/assimalign/viu-examples) repository.

## Repository map

Publicly consumable packages use the area-based layout
`libraries/<Area>/<AssemblyId>/{src,test,docs}`. `libraries/` is not limited to application-runtime
assemblies: it also contains the `netstandard2.0` Syntax parsers that developers may consume directly
and standalone add-ons under `Utilities`. Each documented project carries a
`docs/OVERVIEW.md` and, where its shape needs justification, a `docs/DESIGN.md`; neither may
contradict [`docs/SPECIFICATION.md`](docs/SPECIFICATION.md).

Developer tooling follows `tooling/<Area>/<AssemblyId>/{src,test,docs}` under the `Compiler/` and
`Editor/` areas. The `extensions/` root contains ecosystem integrations (`VisualStudio`,
`VisualStudioCode`, and
`dotnet`), while the Playwright end-to-end executable lives with the performance harnesses under
[`benchmarks/Assimalign.Viu.Testing.EndToEnd`](benchmarks/Assimalign.Viu.Testing.EndToEnd).

### Public libraries (`libraries/`)

| Area | Library | Responsibility | Docs |
| --- | --- | --- | --- |
| Runtime | [`Assimalign.Viu.Components`](libraries/Runtime/Assimalign.Viu.Components) | Immutable virtual-node vocabulary, component contracts, bindings, compiler/runtime flags, built-in node descriptions, and explicit activation registry | [OVERVIEW](libraries/Runtime/Assimalign.Viu.Components/docs/OVERVIEW.md) · [DESIGN](libraries/Runtime/Assimalign.Viu.Components/docs/DESIGN.md) |
| Runtime | [`Assimalign.Viu.Reactivity`](libraries/Runtime/Assimalign.Viu.Reactivity) | Dependency engine and reference primitives: `Reference<T>`, `Computed<T>`, effects, scopes, watches, and reactive collections | [OVERVIEW](libraries/Runtime/Assimalign.Viu.Reactivity/docs/OVERVIEW.md) · [DESIGN](libraries/Runtime/Assimalign.Viu.Reactivity/docs/DESIGN.md) |
| Runtime | [`Assimalign.Viu.State`](libraries/Runtime/Assimalign.Viu.State) | Store definitions, registry-owned reactive lifetimes, and the optional `StateStore<TState>` member model | [OVERVIEW](libraries/Runtime/Assimalign.Viu.State/docs/OVERVIEW.md) · [DESIGN](libraries/Runtime/Assimalign.Viu.State/docs/DESIGN.md) |
| Runtime | [`Assimalign.Viu.Core`](libraries/Runtime/Assimalign.Viu.Core) | Host-neutral application lifetime, renderer, scheduler, normalization ABI, reconciliation, hydration, and built-in behavior | [OVERVIEW](libraries/Runtime/Assimalign.Viu.Core/docs/OVERVIEW.md) · [DESIGN](libraries/Runtime/Assimalign.Viu.Core/docs/DESIGN.md) · [KEEP-ALIVE](libraries/Runtime/Assimalign.Viu.Core/docs/KEEP-ALIVE.md) · [ASYNC/DYNAMIC](libraries/Runtime/Assimalign.Viu.Core/docs/ASYNCHRONOUS-AND-DYNAMIC-COMPONENTS.md) |
| Browser | [`Assimalign.Viu.Browser`](libraries/Browser/Assimalign.Viu.Browser) | Browser host adapter, batched DOM interop, event wiring, browser directives, and CSS transitions | [OVERVIEW](libraries/Browser/Assimalign.Viu.Browser/docs/OVERVIEW.md) · [DESIGN](libraries/Browser/Assimalign.Viu.Browser/docs/DESIGN.md) · [ADR-0001](libraries/Browser/Assimalign.Viu.Browser/docs/ADR-0001-interop-marshaling.md) |
| Browser | [`Assimalign.Viu.Browser.Router`](libraries/Browser/Assimalign.Viu.Browser.Router) | Browser click/history bridge for the host-neutral router | [OVERVIEW](libraries/Browser/Assimalign.Viu.Browser.Router/docs/OVERVIEW.md) · [DESIGN](libraries/Browser/Assimalign.Viu.Browser.Router/docs/DESIGN.md) |
| Router | [`Assimalign.Viu.Router`](libraries/Router/Assimalign.Viu.Router) | DOM-free route matching, histories, components, and navigation guards | [OVERVIEW](libraries/Router/Assimalign.Viu.Router/docs/OVERVIEW.md) · [DESIGN](libraries/Router/Assimalign.Viu.Router/docs/DESIGN.md) |
| ServerRenderer | [`Assimalign.Viu.ServerRenderer`](libraries/ServerRenderer/Assimalign.Viu.ServerRenderer) | DOM-free HTML rendering and the hydration-marker protocol | [OVERVIEW](libraries/ServerRenderer/Assimalign.Viu.ServerRenderer/docs/OVERVIEW.md) · [DESIGN](libraries/ServerRenderer/Assimalign.Viu.ServerRenderer/docs/DESIGN.md) |
| DevTools | [`Assimalign.Viu.DevTools`](libraries/DevTools/Assimalign.Viu.DevTools) | Optional runtime inspection protocol and browser bridge | [PROTOCOL](libraries/DevTools/Assimalign.Viu.DevTools/docs/PROTOCOL.md) |
| DevTools | [`Assimalign.Viu.Testing`](libraries/DevTools/Assimalign.Viu.Testing) | In-memory host and component-test wrappers | [OVERVIEW](libraries/DevTools/Assimalign.Viu.Testing/docs/OVERVIEW.md) · [DESIGN](libraries/DevTools/Assimalign.Viu.Testing/docs/DESIGN.md) |
| Syntax | [`Assimalign.Viu.Syntax`](libraries/Syntax/Assimalign.Viu.Syntax) | Shared located-node, diagnostic, and registration-based parser foundation | [OVERVIEW](libraries/Syntax/Assimalign.Viu.Syntax/docs/OVERVIEW.md) · [DESIGN](libraries/Syntax/Assimalign.Viu.Syntax/docs/DESIGN.md) |
| Syntax | [`Assimalign.Viu.Syntax.Templates`](libraries/Syntax/Assimalign.Viu.Syntax.Templates) | Template parsing, transformation, static analysis, patch inference, and render-source generation | [OVERVIEW](libraries/Syntax/Assimalign.Viu.Syntax.Templates/docs/OVERVIEW.md) · [DESIGN](libraries/Syntax/Assimalign.Viu.Syntax.Templates/docs/DESIGN.md) |
| Syntax | [`Assimalign.Viu.Syntax.SingleFileComponent`](libraries/Syntax/Assimalign.Viu.Syntax.SingleFileComponent) | Canonical `.viu` and compatible `.vue` container parsers | [OVERVIEW](libraries/Syntax/Assimalign.Viu.Syntax.SingleFileComponent/docs/OVERVIEW.md) · [DESIGN](libraries/Syntax/Assimalign.Viu.Syntax.SingleFileComponent/docs/DESIGN.md) · [FORMAT](libraries/Syntax/Assimalign.Viu.Syntax.SingleFileComponent/docs/FORMAT.md) |
| Syntax | [`Assimalign.Viu.Syntax.Css`](libraries/Syntax/Assimalign.Viu.Syntax.Css) | CSS tokenizer, parser, and scoped-style rewrite | [OVERVIEW](libraries/Syntax/Assimalign.Viu.Syntax.Css/docs/OVERVIEW.md) · [DESIGN](libraries/Syntax/Assimalign.Viu.Syntax.Css/docs/DESIGN.md) |
| Syntax | [`Assimalign.Viu.Syntax.Html`](libraries/Syntax/Assimalign.Viu.Syntax.Html) | HTML parser used for host-page build-time rewriting | [OVERVIEW](libraries/Syntax/Assimalign.Viu.Syntax.Html/docs/OVERVIEW.md) · [DESIGN](libraries/Syntax/Assimalign.Viu.Syntax.Html/docs/DESIGN.md) |
| Utilities | [`Assimalign.Viu.UtilityCss`](libraries/Utilities/Assimalign.Viu.UtilityCss) | Standalone utility-CSS parsing, scanning, registry, theme, and deterministic emission engine | [OVERVIEW](libraries/Utilities/Assimalign.Viu.UtilityCss/docs/OVERVIEW.md) · [DESIGN](libraries/Utilities/Assimalign.Viu.UtilityCss/docs/DESIGN.md) |

### Developer tooling (`tooling/`)

Compiler and editor projects are organized by host role. None enters a Viu application's runtime,
and none is currently published as an independent tooling package.

| Library | Responsibility | Docs |
| --- | --- | --- |
| [`Assimalign.Viu.Compiler.Css`](tooling/Compiler/Assimalign.Viu.Compiler.Css) | Build-time composition root for component `<style>` compilation and bundling | [OVERVIEW](tooling/Compiler/Assimalign.Viu.Compiler.Css/docs/OVERVIEW.md) · [DESIGN](tooling/Compiler/Assimalign.Viu.Compiler.Css/docs/DESIGN.md) |
| [`Assimalign.Viu.Compiler.SingleFileComponent`](tooling/Compiler/Assimalign.Viu.Compiler.SingleFileComponent) | Shared `.viu`/`.vue` to C# projection used by generation and editor analysis | [OVERVIEW](tooling/Compiler/Assimalign.Viu.Compiler.SingleFileComponent/docs/OVERVIEW.md) · [DESIGN](tooling/Compiler/Assimalign.Viu.Compiler.SingleFileComponent/docs/DESIGN.md) |
| [`Assimalign.Viu.LanguageService`](tooling/Editor/Assimalign.Viu.LanguageService) | Editor-neutral document state, completion, hover, symbols, folding, code actions, and C# semantic analysis | [DESIGN](extensions/VisualStudio/Assimalign.Viu.VisualStudio/docs/DESIGN.md) |
| [`Assimalign.Viu.LanguageServer`](tooling/Editor/Assimalign.Viu.LanguageServer) | Standalone Language Server Protocol executable used by both editor extensions | [DESIGN](extensions/VisualStudio/Assimalign.Viu.VisualStudio/docs/DESIGN.md) |

**Standalone add-on.** Viu Utilities is independently published from
[`libraries/Utilities/Assimalign.Viu.UtilityCss`](libraries/Utilities/Assimalign.Viu.UtilityCss).
It remains outside every Viu SDK and framework surface, and its Tailwind CSS v4.3.3 compatibility
target is not a Viu core contract. Consumer MSBuild integration arrives separately through #346;
component `<style>` CSS compilation, bundling, delivery, and hot reload remain active Viu features.

### Source generators and SDK build tools (`analyzers/`, `sdks/`)

These are build-time (netstandard2.0) components. They are the sanctioned metaprogramming mechanism:
because WASM forbids runtime code generation, everything a dynamic language would do at run time
happens here instead. They never ship in the runtime assemblies.

| Project | Role |
| --- | --- |
| `Assimalign.Viu.Generators.Reactivity` | Emits the tracking/triggering property bodies for `[Reactive]`/`[ShallowReactive]` partial classes, so a plain object becomes reactive with no reflection and no runtime interception. |
| `Assimalign.Viu.Generators.Syntax` | The incremental generator that compiles `.viu` single-file components and templates to C# render methods (the composition root that registers the template and style parsers). |
| `Assimalign.Viu.Sdk.Tasks` | The base SDK's host-neutral MSBuild tasks, including `ViuBundleCss`, which extracts physical `.viu.css` for component-library packages and Browser application bundles outside the analyzer sandbox. |
| `Assimalign.Viu.Sdk.Browser.Tasks` | Browser-only MSBuild tasks for host-page component-CSS link injection and CSS hot-reload worker launch. Its projects use `Tasks/{src,test}`. |
| `Assimalign.Viu.Sdk.CssHotReload` | The Browser SDK's internal Debug `dotnet watch` worker; it regenerates component stylesheets and is never copied into the application or runtime framework. |

### Editor extensions (`extensions/`)

Both editor hosts are thin clients over the **same** editor-neutral language server
(`tooling/Editor/Assimalign.Viu.LanguageServer`, a plain stdio LSP executable with no editor
coupling). [`build/Targets/Build.LanguageServer.targets`](build/Targets/Build.LanguageServer.targets)
is the single publish recipe both use, so they cannot drift on trimming, single-file, or debug-type
settings.

| Integration | Host | Status |
| --- | --- | --- |
| [`extensions/VisualStudio`](extensions/VisualStudio) | Visual Studio 2022 17.14+ / Visual Studio 2026 | Published to the Visual Studio Marketplace as a preview |
| [`extensions/VisualStudioCode`](extensions/VisualStudioCode) | Visual Studio Code 1.85+ | Scaffold — compiles and packages, not published |
| [`extensions/dotnet/Assimalign.Viu.Templates`](extensions/dotnet/Assimalign.Viu.Templates) | `dotnet new` | Packaged application and component-library templates |

The two hosts differ in exactly one build property. The Visual Studio VSIX embeds `win-x64` and
`win-arm64` only, because it ships every payload in one package and each is roughly 18 MB; Visual
Studio Code ships one platform-specific package per runtime identifier and opts into the full
`win-x64;win-arm64;linux-x64;osx-arm64;osx-x64` set through
`ViuLanguageServerPublishAllRuntimeIdentifiers`. Each host publishes to its own output root, and the
shared target fails the build if a payload the host did not ask for is sitting in its publish
directory.

Neither extension project is in `Assimalign.Viu.slnx`, for different reasons. The Visual Studio
extension is a classic **in-process** VSSDK package whose build tasks are .NET Framework MSBuild
tasks and cannot load under `dotnet build`; it is packaged by
[its own `Build.ps1`](extensions/VisualStudio/Build.ps1) through Visual Studio's MSBuild, and only
its test project is in the solution. The Visual Studio Code extension is a TypeScript/npm package
built by [its own `Build.ps1`](extensions/VisualStudioCode/Build.ps1).

The Visual Studio client runs in process because the editor surfaces a Viu palette needs — a content
type Viu owns, its own classification types, and the format definitions that color them — exist only
as MEF exports inside `devenv.exe`. Nothing semantic followed it in: the parsers and Roslyn stay in
the language-server process. See
[the area design record](extensions/VisualStudio/Assimalign.Viu.VisualStudio/docs/DESIGN.md).

### Packaged SDK showcase

[`assimalign/viu-examples`](https://github.com/assimalign/viu-examples) contains the complete
browser showcase. It consumes `Assimalign.Viu.Sdk.Browser`, `Assimalign.Viu.Router`, and
`Assimalign.Viu.Browser.Router` from a local NuGet feed, exercising the packaged navigation boundary
as an external application rather than relying on project references into this repository.

### Packaging (`sdks/`, `frameworks/`)

External consumers use one of two MSBuild project SDKs, never project references. A component
library uses `<Project Sdk="Assimalign.Viu.Sdk">`, which chains `Microsoft.NET.Sdk`, supplies the
generators and style-packing path, and references targeting-only `Assimalign.Viu.App`. A browser
application uses `<Project Sdk="Assimalign.Viu.Sdk.Browser">`; it imports the base SDK, chains
`Microsoft.NET.Sdk.WebAssembly`, and adds `Assimalign.Viu.App.Browser`, browser assets, CSS delivery,
hot reload, and publish hooks. See [`sdks/README.md`](sdks/README.md) for both consumer scenarios and
the local development loop.

| Path | Produces | Role |
| --- | --- | --- |
| `sdks/Assimalign.Viu.Sdk` | `Assimalign.Viu.Sdk` | Host-neutral component-library SDK: base .NET SDK, generators, targeting-only App reference, and pack-carried component styles. |
| `sdks/Assimalign.Viu.Sdk.Browser` | `Assimalign.Viu.Sdk.Browser` | Browser application SDK: imports the base, adds WebAssembly, browser assets, CSS/hot-reload delivery, and publish hooks. |
| `frameworks/Assimalign.Viu.App.Refs` | `Assimalign.Viu.App.Ref` | Base targeting pack: Reactivity, Components, State, Core, the generator closure, and four package overrides; no runtime peer. |
| `frameworks/Assimalign.Viu.App.Browser.Refs` | `Assimalign.Viu.App.Browser.Ref` | Browser-only targeting pack: the Browser reference and its package override; the Browser SDK composes it with the base targeting pack. |
| `frameworks/Assimalign.Viu.App.Browser.Runtime` | `Assimalign.Viu.App.Browser.Runtime.browser-wasm` | Browser runtime pack: the base-plus-Browser implementation closure for `browser-wasm`. |

In-repo projects dogfood the framework through `ViuProjectReference` (see
[`.claude/rules/build-system.md`](.claude/rules/build-system.md)); the SDKs are the external-consumer
surfaces.

## Getting started

### Prerequisites

- The [.NET SDK](https://dotnet.microsoft.com/download) pinned in [`global.json`](global.json)
  (currently `10.0.301`).
- The WebAssembly tools workload, needed to build and run the browser sample:
  ```sh
  dotnet workload install wasm-tools
  ```

### Clone and build

```sh
git clone https://github.com/assimalign/viu.git
cd viu
dotnet build Assimalign.Viu.slnx
```

### Test

Each library's tests live beside it under `test/`:

```sh
dotnet test libraries/Runtime/Assimalign.Viu.Core/test/
```

### Run the showcase

Pack the local SDK and framework, then follow the
[`viu-examples` README](https://github.com/assimalign/viu-examples#run-locally).

## Plan and tracking

- [Specification](docs/SPECIFICATION.md) — the authoritative statement of Viu's semantics: the
  execution model, the component model, reactivity, the rendering architecture, compilation,
  styling, server rendering, routing, state, tooling, and packaging. Clauses carry stable ids
  (`RND-BLOCK-2`, `SCH-4`, …) that code, tests, and issues cite as text.
- [Delivery plan](docs/PLAN.md) — the wave strategy, the WBS map, and the founding design decisions
  (with the historical record of how the areas were originally scoped).
- [Architecture decisions](docs/adr/) — the append-only decision log for repo-wide, cross-cutting
  decisions.
- [Performance research](docs/PERFORMANCE-RESEARCH.md) — the explicitly non-normative ledger for
  optimization techniques observed elsewhere, measured against Viu's benchmark baselines before any
  are adopted.
- [Documentation index](docs/README.md) — what lives under `docs/`, and the placement policy that
  decides whether a document belongs there or with a project.
- [Documentation conventions](docs/CONTRIBUTING.md) — where `OVERVIEW.md`, `DESIGN.md`, and ADRs
  live, what belongs in each, and when they must be updated.
- [Getting started guide](docs/guide/getting-started.md) — build, run, and publish a Viu app with the
  packaged `Assimalign.Viu.Sdk.Browser` (prerequisites → first component → reactivity → publish).
- [Release guide](docs/RELEASING.md) — beta and stable package channels, NuGet trusted publishing,
  GitHub Packages, and the Visual Studio Marketplace preview.
- [Project board](https://github.com/orgs/assimalign/projects/15) — the authoritative backlog
  (`[V01.01.*]` WBS items: program → area epics → features → tasks).
- Work-item intake: [`.claude/skills/viu-work-items`](.claude/skills/viu-work-items/SKILL.md).

## License

See [LICENSE](LICENSE).
