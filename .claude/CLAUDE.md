# Vuecs

A faithful re-implementation of Vue.js 3 in C#/.NET, targeting the browser through the .NET
WebAssembly build tools (`Microsoft.NET.Sdk.WebAssembly`, `JSImport`/`JSExport` interop). The
architecture mirrors Vue 3's package boundaries (`@vue/reactivity`, `runtime-core`, `runtime-dom`,
compiler packages, `server-renderer`) as `Assimalign.Vue.*` class libraries, with Roslyn source
generators standing in for everything Vue does with JS `Proxy` and runtime `new Function` — WASM is
AOT/trimming territory, so reflection-based serialization and dynamic code generation are forbidden.

## Layout

- `libraries/` — framework libraries, inverted layout: `libraries/Assimalign.Vue.<Name>/{src|test}`
  (the folder name is the assembly/package id; `src/` holds the shipping project, `test/` its tests —
  no area wrapper folders)
- `examples/` — sample WASM apps (`Assimalign.Vue.WebApp` is the current demo)
- `docs/` — repo-level planning docs (`PLAN.md` is the delivery plan)
- `.claude/rules/` — the canonical working conventions for this repo (auto-load by path):
  `general-rules` (C# style, Abstraction/Internal folders, whole-word naming, explicit usings, AOT),
  `build-system` (central `build/`, `VuecsProjectReference`/`VuecsPackageReference`), `testing`,
  `documentation`, `deviations`, `checklist`, and `workflow` (branches, WBS, scope creep)

## Build and test

- `dotnet build Assimalign.Vuecs.slnx`
- `dotnet test <project>/tests/`
- Run the demo: `dotnet run --project examples/Assimalign.Vue.WebApp`

## Work tracking

All development is tracked as WBS-coded work items (`[V01.01.NN...]` titles) in the org GitHub
Project **#15 "Vuecs"**, mirroring the sibling Cohesion repo's model. Use the **vuecs-work-items**
skill (`.claude/skills/vuecs-work-items/`) to create, place, and link items — especially to capture
scope creep discovered mid-branch. The GitHub issue body is the authoritative source of a work
item's requirements. Project #15 is vuecs-only; if `assimalign/cohesion` items (`[Lxx...]` codes)
ever appear on it, flag it (a project auto-add workflow may be re-adding them) and never modify
them from this repo.
