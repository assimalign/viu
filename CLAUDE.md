# Viu

A standalone C#/.NET UI framework targeting the browser through the .NET WebAssembly build tools
(`Microsoft.NET.Sdk.WebAssembly`, `JSImport`/`JSExport` interop). Viu renders through a hierarchical
virtual-node tree with compiler-informed diffing, compiles templates and single-file components at
build time via Roslyn source generators, and ships as `Assimalign.Viu.*` class libraries. WASM is
AOT/trimming territory, so reflection-based serialization and dynamic code generation are forbidden.

**[`docs/SPECIFICATION.md`](docs/SPECIFICATION.md) is the authority for Viu's semantics** — cite its
clause ids (`[RND-KEY-3]`, `[CMP-4]`, …) rather than any external framework. Viu is not a port of and
takes no semantics from any JavaScript framework. Two things are deliberately different:
external **compatibility targets** (the `.vue` container format, WHATWG HTML serialization, and the
Language Server Protocol) are real product features and are documented as such; and
[`docs/PERFORMANCE-RESEARCH.md`](docs/PERFORMANCE-RESEARCH.md) is the non-normative channel for
evaluating other frameworks' performance work — including Vue's — for possible replication.
Tailwind CSS v4.3.3 is only the target of the standalone add-on at
`libraries/Utilities/Assimalign.Viu.UtilityCss`; it is not a Viu core compatibility target (owner decision,
2026-08-13). Component `<style>` CSS remains fully supported, including scoping, bundling, and hot
reload.

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
skill (`.claude/skills/viu-work-items/`) to create, place, and link items — especially to capture
scope creep discovered mid-branch. The GitHub issue body is the authoritative source of a work
item's requirements. Project #15 is viu-only; if `assimalign/cohesion` items (`[Lxx...]` codes)
ever appear on it, flag it (a project auto-add workflow may be re-adding them) and never modify
them from this repo.
