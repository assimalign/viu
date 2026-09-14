# Documentation conventions

This page defines Viu's documentation system: where each kind of document lives, what belongs in it,
and when it must be updated. It exists so every WBS area documents itself the same way without having
to re-decide. The canonical *working* conventions (C# style, build system, testing) live under
[`.claude/rules/`](../.claude/rules/); this page covers the prose-documentation layout and lifecycle
and complements [`.claude/rules/documentation.md`](../.claude/rules/documentation.md).

## Placement policy

Three rules decide where a Markdown file lives, and [`docs/README.md`](README.md) indexes the result:

1. **Documentation scoped to one project** lives in `<project folder>/docs/` — under `libraries/`,
   `tooling/`, `analyzers/`, `sdks/`, `frameworks/`, `benchmarks/`, or `extensions/`.
2. **`README.md` stays put.** Any folder may carry its own `README.md` as that folder's entry point.
3. **Everything else** lives in the repository-root `docs/`.

Packaging inputs are exempt because they are not documentation: `THIRD-PARTY-NOTICES.md` files packed
into nupkgs, Roslyn `AnalyzerReleases.*.md` release-tracking files, and
`extensions/VisualStudio/Marketplace.md` (the Marketplace listing body read by `vs-publish.json` and
the release workflow). So are the agent-configuration trees `.claude/` and `.agents/`.

## The map

| Document | Location | What it holds |
| --- | --- | --- |
| Root `README.md` | repository root | The project mission, the repository map (projects under `libraries/`, `tooling/`, `analyzers/`, `sdks/`, `frameworks/`, `benchmarks/`, and `extensions/`), the external showcase link, and clone/build instructions. |
| `docs/README.md` | [`docs/README.md`](README.md) | The index of repository-level documentation and the placement policy above. |
| `SPECIFICATION.md` | [`docs/SPECIFICATION.md`](SPECIFICATION.md) | **Normative.** What Viu is and what it guarantees, in numbered clauses with stable ids. Highest authority for semantics; every other document below is subordinate to it and must not contradict it. |
| `API-HARDENING-PLAN.md` | [`docs/API-HARDENING-PLAN.md`](API-HARDENING-PLAN.md) | The completion record for `[V01.01.14]`: public-surface decisions, terminal work disposition, gates, and deferred platform-segmentation trigger. |
| `COMPONENT-MODEL-PLAN.md` | [`docs/COMPONENT-MODEL-PLAN.md`](COMPONENT-MODEL-PLAN.md) | The plan of record for the `[V01.01.15]` component-model arc: adopted layer charter, designed seams, type disposition, migration trains, and completion gates. |
| `COMPONENT-MODEL-EXECUTION.html` | [`docs/COMPONENT-MODEL-EXECUTION.html`](COMPONENT-MODEL-EXECUTION.html) | The component model in execution, as four diagrams: type ownership across the assemblies, the first-mount sequence, invocation-to-bindings resolution, and the reactive update loop. Subordinate to `SPECIFICATION.md` and cites its clause ids. HTML rather than Markdown because the content is diagrammatic; it is one self-contained file with no sibling assets. |
| `PLAN.md` | [`docs/PLAN.md`](PLAN.md) | The delivery narrative: the wave strategy, the WBS map, and the founding design decisions. Describes *when*, not *what*. The GitHub [Project #15](https://github.com/orgs/assimalign/projects/15) board is the authoritative *backlog*. |
| `DEVELOPER-EXAMPLES.md` | [`docs/DEVELOPER-EXAMPLES.md`](DEVELOPER-EXAMPLES.md) | Worked package-consumer examples for Components, Reactivity, State, Core, and Browser. |
| Getting-started guide | [`docs/guide/getting-started.md`](guide/getting-started.md) | The external-consumer walkthrough from manual project creation through browser execution and publish. |
| Documentation site | [Documentation home](api-reference/index.md) · [generator notes](api-reference/README.md) | One navigation tree for the overview, getting started, guides, libraries and SDKs, specification, and API reference; [`Build-ApiReference.ps1`](../scripts/Build-ApiReference.ps1) emits the versioned site under `_out/api-reference/`. |
| `UTILITY-CSS-DESIGN.md` | [`docs/UTILITY-CSS-DESIGN.md`](UTILITY-CSS-DESIGN.md) | **Standalone add-on design; non-normative for Viu core.** The former Viu integration and the independently published add-on's Tailwind CSS v4.3.3 target. |
| `NET-RESHAPE-PLAN.md` | [`docs/NET-RESHAPE-PLAN.md`](NET-RESHAPE-PLAN.md) | The dated historical record of the completed .NET reshape and its later supersession notes. |
| `RELEASING.md` | [`docs/RELEASING.md`](RELEASING.md) | Package and extension release channels, credentials, validation, and publication sequence. |
| Architecture decision records | [`docs/adr/`](adr/) | The append-only log of repo-wide, cross-cutting decisions (see [`adr/README.md`](adr/README.md)). Normative for *rationale*, not for current API shape. |
| `PERFORMANCE-RESEARCH.md` | [`docs/PERFORMANCE-RESEARCH.md`](PERFORMANCE-RESEARCH.md) | The ledger of optimization techniques observed in other rendering frameworks. **Explicitly non-normative**: nothing there constrains Viu until it is measured, adopted, and written into `SPECIFICATION.md` or an ADR. |
| Upstream reproductions | [`docs/upstream/`](upstream/) | Minimal, version-pinned external toolchain reproductions prepared for upstream issue reports; evidence only, never semantic authority for Viu. |
| Per-project `OVERVIEW.md` | `<project folder>/docs/OVERVIEW.md` | What the library **is**. |
| Per-project `DESIGN.md` | `<project folder>/docs/DESIGN.md` | **Why** the library is shaped the way it is. |
| Per-project topic docs | `<project folder>/docs/*.md` | Focused specs or local ADRs (e.g. `FORMAT.md`, a library-local `ADR-000N-*.md`). |
| XML doc comments | in source, on every public member | The API-level reference: what each member does, what it guarantees, and why its shape is what it is (see [`.claude/rules/documentation.md`](../.claude/rules/documentation.md)). |

**Precedence.** `SPECIFICATION.md` → the normative delegates it names (currently the `.viu`
`FORMAT.md`) → ADRs → library `DESIGN.md` → `PLAN.md`. A lower-precedence document that
contradicts a higher one is wrong and must be corrected, not reconciled.

## What belongs in `OVERVIEW.md`

The reader-facing description of the library. Keep it concise and accurate — describe what exists,
not what is planned.

- **Purpose** — one or two sentences: the role the library plays in Viu, stated in Viu's own terms.
  A scaffold says so plainly.
- **Public surface** — the entry points and currency types a consumer touches (the facade, the key
  public types), with a one-line note on each. Not an exhaustive member list — that is the XML docs.
- **Specification anchor** — the `SPECIFICATION.md` section and clause ids this library implements,
  cited as text (`[RND-BLOCK-2]`), so a reader can go from the library to the normative statement of
  its behavior. Cite only clauses the specification actually contains.
- **Boundaries** — allowed dependency direction; any interop/AOT/generator constraint; a pointer to
  `DESIGN.md`.

## What belongs in `DESIGN.md`

The rationale and the trade-offs — why the shape, not the shape itself.

- **Design rationale** — the internal structure and the forces behind it (the interop budget, the
  AOT/trimming constraint, the single-threaded model, the incremental-generator caching contract).
- **External compatibility targets** — where the library implements a documented foreign format
  (the `.vue` single-file-component container, WHATWG HTML serialization, or the Language Server
  Protocol), name it and link a version-pinned reference. There
  the citation *is* the requirement: it constrains a format Viu deliberately consumes, and it is not
  an authority over Viu's own semantics. Tailwind CSS v4.3.3 is no longer a Viu compatibility
  target; it remains only the target recorded for the standalone, non-normative utility add-on in
  [`UTILITY-CSS-DESIGN.md`](UTILITY-CSS-DESIGN.md).
- **Platform adaptations** — where a design that reads oddly is forced by the WASM/AOT/single-thread
  reality rather than chosen, say so and link the test that pins the chosen behavior. A repo-wide
  decision links its ADR under [`docs/adr/`](adr/); a library-local one is documented here.
- **Non-goals** — what is intentionally out of scope, sequenced to the work item that will add it.
  A non-goal that is a *decision* rather than a deferral says so, and matches
  [`SPECIFICATION.md` §18](SPECIFICATION.md#18-non-goals-and-current-limits).

## When documents must be updated

- **Same change as the code.** An `OVERVIEW.md`/`DESIGN.md` that lags the code actively misleads;
  update it in the commit that changes the public surface or the design it describes.
- **A new public type or behavior** — add the XML doc comment (what it does, what it guarantees, why
  the shape), and reflect any new entry point in `OVERVIEW.md`.
- **A change to specified behavior** — the `SPECIFICATION.md` clause, the tests that pin it, and the
  XML docs that cite it move in the same change. A specification change gets the same review as a
  behavior change, because it *is* one (see
  [`.claude/rules/deviations.md`](../.claude/rules/deviations.md)).
- **Behavior that is not yet in the specification** — pin it with a test that asserts the chosen
  behavior and reference the `[V01.01.NN…]` work item that specified it. Do not cite a clause id the
  specification does not yet contain.
- **A cross-cutting or repo-wide decision** — add an ADR (never edit a past one; supersede it — see
  [`adr/README.md`](adr/README.md)).

## Where new things go

- **A new publicly consumable library** —
  `libraries/<Area>/Assimalign.Viu.<Name>/{src,test,docs}`. This root contains both runtime packages
  and the `netstandard2.0` Syntax parser cluster, plus standalone add-ons under `Utilities`. The area
  folder expresses product ownership; the assembly-id folder still owns the inverted `{src,test}`
  project layout.
- **A new compiler or editor project** —
  `tooling/<Area>/Assimalign.Viu.<Name>/{src,test,docs}`. Seed `docs/OVERVIEW.md` and
  `docs/DESIGN.md` with the code,
  wire the csprojs per [`.claude/rules/build-system.md`](../.claude/rules/build-system.md)
  ("Adding a new library"), and add a row to the root `README.md` repository map.
- **A new sample** — add it to
  [`assimalign/viu-examples`](https://github.com/assimalign/viu-examples), where samples consume
  packaged Viu artifacts rather than project references. Update this repository's showcase link
  only when the external entry point changes.
- **A new ADR** — copy [`adr/template.md`](adr/template.md) to `adr/NNNN-kebab-title.md` (next number),
  and add it to the [`adr/README.md`](adr/README.md) index.
- **Work items** — every change traces to a `[V01.01.NN…]` WBS item on Project #15; capture
  mid-branch scope creep with the `viu-work-items` skill (see
  [`.claude/rules/workflow.md`](../.claude/rules/workflow.md)).

## Links must resolve

Every relative Markdown link must resolve to a file or repository directory, and every linked
fragment must exist in its target document. Run the offline gate from the repository root:

```sh
pwsh scripts/Test-DocumentationLinks.ps1
```

The gate covers `docs/**/*.md`, the root `README.md`, `libraries/**/docs/*.md`, `sdks/**/*.md`,
and `extensions/**/README.md`, excluding `bin/`, `obj/`, `node_modules/`, and `_out/`. It checks
relative links, document anchors, and specification clause citations through the same
[`ViuApiReference.psm1`](../scripts/modules/ViuApiReference.psm1) mapping used by the site. It stops
at the first failure with the source file, line, and target, and reports file, link, and anchor
counts on success. Code examples are not navigation links, but clause citations are validated
inside examples too. WBS work-item codes and ADR numbers are separate numbering systems.

External links must be absolute `https://` URLs. This check validates their shape only and never
contacts the network; it cannot confirm an external page's availability or fragments. Link genuine
standards and foreign formats at version-pinned references, and use repository links for work items
and the external sample gallery. The [Documentation workflow](../.github/workflows/api-reference.yml)
runs this gate before generating the site; docfx also validates the staged reader pages.
Docfx `xref:` destinations are internal API identifiers, not external URLs. The Markdown gate
checks their syntax; docfx resolves identifiers in the published pages with warnings as errors.

A reference to Viu's own behavior is a `SPECIFICATION.md` clause id written as text — `[SCH-4]`,
never a URL — so the API-reference generator ([V01.01.13.04]) resolves ids to anchors from one
mapping and the docs survive the site moving.

## Build the API reference

The [docfx project](api-reference/) delivers the API reference
([[V01.01.13.04] #101](https://github.com/assimalign/viu/issues/101)) and the in-repository
documentation site ([[V01.01.13.05] #102](https://github.com/assimalign/viu/issues/102)).
Install PowerShell 7 and the
.NET SDK selected by [`global.json`](../global.json). Prepare the solution's dependencies once,
and repeat restore whenever its dependency graph changes:

```sh
dotnet workload install wasm-tools --skip-manifest-update
dotnet restore Assimalign.Viu.slnx -p:Configuration=Release
pwsh scripts/Test-DocumentationLinks.ps1
pwsh scripts/Build-ApiReference.ps1
```

The script restores the exact docfx version in
[`.config/dotnet-tools.json`](../.config/dotnet-tools.json), builds the solution in Release with
XML documentation and missing-public-member checks, then runs `docfx metadata` and `docfx build`
with warnings as errors. Solution dependency restore is separate: after prerequisites and the local
tool are restored, compilation and site generation use local inputs without fetching remote themes
or cross-reference maps. The generated site is `_out/api-reference/`; its entry page is
`_out/api-reference/index.html`. Generated metadata and HTML belong under `_out/`, never in a commit.

Metadata covers the shipping library inventory in
[`ViuPackaging.psm1`](../scripts/modules/ViuPackaging.psm1), plus all five public Syntax parser
assemblies. Standalone Utilities APIs are included because they are public package surfaces;
their inclusion does not make them Viu core semantics or add them to an SDK or framework.
`Assimalign.Viu.UtilityCss.Build` is checked for XML coverage with the other shipping packages,
but its MSBuild tasks are excluded from API metadata. Test assemblies, analyzer internals,
SDK tasks, and compiler/editor implementation assemblies are also excluded.

Cite existing specification clauses as plain text in XML comments:

```xml
/// <remarks>Specified by <c>[RND-FLAGS-1]</c>.</remarks>
```

The preprocessing step builds one mapping from clause declarations at paragraph starts in
[`SPECIFICATION.md`](SPECIFICATION.md), inserts stable clause anchors into the staged specification,
and replaces matching XML-comment citations with links to those anchors. Unknown clause ids fail
generation. Keep work-item codes such as `[V01.01.13.04]` as work-item references; they are not
specification clauses. Never add an invented clause id or a framework counterpart to satisfy a
documentation check.

The documentation home has one navigation tree:

- **Overview** — what Viu is, the documented package version and its implemented behavior, and
  a plain repository link to the external sample gallery.
- **Getting started** — the [packaged-consumer walkthrough](guide/getting-started.md).
- **Guides** — [developer examples](DEVELOPER-EXAMPLES.md) and other reader pages under `docs/guide/`.
- **Libraries** — every library `docs/OVERVIEW.md`, grouped by Runtime, Browser, Router, State,
  ServerRenderer, DevTools, Syntax, and Utilities, plus SDK documentation including static prerendering.
- **Specification** — Viu's normative behavior and stable clause anchors.
- **API reference** — the generated public namespaces, types, and members.

The staging pipeline publishes primary pages and their directly linked reader documentation.
A library `DESIGN.md` publishes when its overview links it. Further source navigation uses links to
the exact Git revision, keeping contributor instructions out of the reader tree. Adding an overview
or guide extends the discovered navigation without a second generator.

Each page labels the package version evaluated from `ViuVersion` in
[`Build.Version.props`](../build/Targets/Build.Version.props), supplied through docfx global metadata.
The landing page describes implemented behavior at that version; it is not a claim that a package
has been published to a registry. Search uses a generated local index and bundled browser assets;
see [search and static hosting](api-reference/README.md) for the serving requirements.
CI builds on every push to `main` and relevant pull requests, fails on link or generation errors,
and uploads the `api-reference` artifact.

Run `pwsh scripts/tests/Test-ApiReference.ps1` and
`pwsh scripts/tests/Test-DocumentationLinks.ps1` for the offline regression checks.
The build log and machine-readable generation summary are in `_out/api-reference-work/`.

## Publish the documentation site

Deployment is **off until the repository owner enables it**. In repository Settings, select
**Pages → Build and deployment → Source: GitHub Actions**, then set the repository Actions variable
**`VIU_DEPLOY_DOCS=true`** under **Settings → Secrets and variables → Actions → Variables**.
Both settings are required. A subsequent successful `main` workflow build then deploys its
`api-reference` artifact through the `github-pages` environment. Pull requests never deploy.

Only the separate `deploy` job receives `pages: write` and `id-token: write`, alongside
`contents: read`; the build job retains read-only repository permissions. Disabling or removing
the variable stops future deployment jobs; it does not remove an already published site.
All workflow actions are pinned by commit with version comments.

[ADR-0006](adr/0006-documentation-site-generator.md) records docfx as the interim generator and
the remaining #102 dogfooding migration: move the landing page and examples index into the sibling
`viu-docs` application after it adopts the base SDK's static-prerender target (`[SSG-1]` through
`[SSG-6]`). This repository change does not modify that application or enable publication.
