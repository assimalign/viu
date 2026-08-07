# Viu repository documentation

Everything under `docs/` is repository-level documentation — it describes Viu as a whole, or spans
more than one project. Documentation scoped to a single project lives with that project.

## Placement policy

1. **Project-scoped documentation** lives in `<project folder>/docs/` — the `OVERVIEW.md` (what it
   is) and `DESIGN.md` (why it is shaped that way) pair described in
   [`CONTRIBUTING.md`](CONTRIBUTING.md), plus focused topic docs and library-local ADRs. This applies
   to every project folder: `libraries/`, `tooling/`, `analyzers/`, `sdks/`, `frameworks/`, and
   `extensions/`.
2. **`README.md` stays put.** Any folder may carry its own `README.md` as that folder's entry point.
3. **Everything else lives here**, in the repository-root `docs/`.

Packaging inputs are not documentation and are exempt: `THIRD-PARTY-NOTICES.md` files packed into
nupkgs, Roslyn `AnalyzerReleases.*.md` release-tracking files, and
`extensions/VisualStudio/Marketplace.md` (the Marketplace listing body read by
[`vs-publish.json`](../extensions/VisualStudio/vs-publish.json) and the release workflow).

## What lives here

| Document | What it holds |
| --- | --- |
| [`SPECIFICATION.md`](SPECIFICATION.md) | **Normative.** What Viu is and what it guarantees, in numbered clauses with stable ids (`[RND-BLOCK-2]`, `[SCH-4]`, …). The highest authority for Viu's semantics. |
| [`ARCHITECTURE.md`](ARCHITECTURE.md) | The cross-library architecture: the Reactivity / Components / State / Core package split, the unified component tree, the host-generic application model, and the AOT and ownership rules. |
| [`COMPONENT-MODEL-EXECUTION.html`](COMPONENT-MODEL-EXECUTION.html) | A visual walkthrough of the component model in execution — type ownership, first mount, invocation resolution, and the reactive update loop, in four diagrams. Describes the **adopted but not yet implemented** redesign staged under `.redesign/`; open it in a browser. |
| [`PLAN.md`](PLAN.md) | The delivery narrative — wave strategy, the WBS map, and the founding design decisions. Describes *when*, not *what*. |
| [`adr/`](adr/) | The append-only log of repo-wide, cross-cutting architecture decisions (see [`adr/README.md`](adr/README.md)). |
| [`CONTRIBUTING.md`](CONTRIBUTING.md) | Documentation conventions: where each kind of document lives, what belongs in it, and when it must be updated. |
| [`guide/getting-started.md`](guide/getting-started.md) | The consumer-facing walkthrough — build, run, and publish a Viu app with the packaged `Assimalign.Viu.Sdk`. |
| [`DEVELOPER-EXAMPLES.md`](DEVELOPER-EXAMPLES.md) | Worked consumption examples for the Components, Reactivity, State, Core, and Browser packages, written from the application developer's point of view. |
| [`UTILITY-CSS-DESIGN.md`](UTILITY-CSS-DESIGN.md) | The Viu Utilities design — the Tailwind CSS v4.3.3 compatibility target spanning the CSS parser, the build-time engine, the SDK pipeline, and editor IntelliSense. |
| [`PERFORMANCE-RESEARCH.md`](PERFORMANCE-RESEARCH.md) | **Explicitly non-normative.** The ledger of optimization techniques observed in other rendering frameworks, measured against Viu's baselines before any are adopted. |
| [`NET-RESHAPE-PLAN.md`](NET-RESHAPE-PLAN.md) | The historical record of the reshape from a faithful port to an idiomatic .NET framework, with the arcs that have merged and the parts since superseded. |
| [`MIGRATION.md`](MIGRATION.md) | The redesign migration map — previous owner or vocabulary to its replacement — for the promotion of the redesigned boundaries into the shipping tree. |
| [`RELEASING.md`](RELEASING.md) | Release channels, NuGet trusted publishing, GitHub Packages, and the Visual Studio Marketplace preview. |

Per-project documentation is indexed from the repository map in the root
[`README.md`](../README.md).
