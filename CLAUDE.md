# Viu

A standalone C#/.NET UI framework targeting the browser through the .NET WebAssembly build tools
(`Microsoft.NET.Sdk.WebAssembly`, `JSImport`/`JSExport` interop). Viu renders through a hierarchical
virtual-node tree with compiler-informed diffing, compiles templates and single-file components at
build time via Roslyn source generators, and ships as `Assimalign.Viu.*` class libraries. WASM is
AOT/trimming territory, so reflection-based serialization and dynamic code generation are forbidden.

**[`docs/SPECIFICATION.md`](docs/SPECIFICATION.md) is the authority for Viu's semantics** — cite its
clause ids (`[RND-KEY-3]`, `[CMP-4]`, …) rather than any external framework. Viu is not a port of and
takes no semantics from any JavaScript framework. Two things are deliberately different:
external **compatibility targets** (the `.vue` container format, Tailwind CSS v4.3.3, WHATWG HTML
serialization) are real product features and are documented as such; and
[`docs/PERFORMANCE-RESEARCH.md`](docs/PERFORMANCE-RESEARCH.md) is the non-normative channel for
evaluating other frameworks' performance work — including Vue's — for possible replication.

## Layout

- `libraries/` — framework libraries, inverted layout: `libraries/Assimalign.Viu.<Name>/{src|test}`
  (the folder name is the assembly/package id; `src/` holds the shipping project, `test/` its tests —
  no area wrapper folders)
- `tooling/` — developer-tooling libraries: build-time and editor tooling (the shared `.viu` → C#
  projection core, the CSS composition core, the Viu Utilities engine) plus the language service and
  the language server. Same inverted `{src|test}` layout, folder name = assembly id. These never ship
  into a Viu app's runtime; they run in analyzer/MSBuild hosts and in the editor.
- `../viu-examples/` — external packaged-consumer WASM showcase (separate repository)
- `docs/` — repo-level planning docs (`PLAN.md` is the delivery plan)
- `.claude/rules/` — the canonical working conventions for this repo (auto-load by path):
  `general-rules` (C# style, Abstraction/Internal folders, whole-word naming, explicit usings, AOT),
  `build-system` (central `build/`, `ViuProjectReference`/`ViuPackageReference`), `testing`,
  `documentation`, `deviations`, `checklist`, and `workflow` (branches, WBS, scope creep)

## Build and test

- `dotnet build Assimalign.Viu.slnx`
- `dotnet test <project>/tests/`
- Run the showcase from the sibling `viu-examples` repository after packing `_out/packages`

## Work tracking

All development is tracked as WBS-coded work items (`[V01.01.NN...]` titles) in the org GitHub
Project **#15 "Viu"**, mirroring the sibling Cohesion repo's model. Use the **viu-work-items**
skill (`.claude/skills/viu-work-items/`) to create, place, and link items — especially to capture
scope creep discovered mid-branch. The GitHub issue body is the authoritative source of a work
item's requirements. Project #15 is viu-only; if `assimalign/cohesion` items (`[Lxx...]` codes)
ever appear on it, flag it (a project auto-add workflow may be re-adding them) and never modify
them from this repo.
