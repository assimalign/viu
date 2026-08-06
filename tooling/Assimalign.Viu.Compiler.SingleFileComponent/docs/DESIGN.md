# Assimalign.Viu.Compiler.SingleFileComponent — design

Why this library exists and how it is shaped. It is the **shared `.viu`/`.vue` → C# projection
core** extracted with **[V01.01.06.11]** (issue #258) from the `Assimalign.Viu.Generators.Syntax`
source generator's `Internal/` folder, so the build compiler and the editor run **one** projection.

## Why a separate library

Project-aware editor tooling needs the same understanding of a `.viu` file the build has: the same
component name and namespace, the same `@script` region split, the same generated-context scaffold,
the same source maps, the same diagnostics on the same coordinates. Before this extraction the
language service had started to re-implement pieces of that by hand — its `ScriptDeclarationReader`
mirrored `ScriptBlockAnalyzer`'s leading-using split with a comment pleading to "keep the split
rules in sync". That is drift by construction: two implementations of one contract, kept equal only
by vigilance. A build compiler and an editor that disagree about the same file are worse than either
alone: the editor reports a problem the build does not have, or stays silent about one it does.

So the projection lives **once**, here, and both hosts call it:

1. the `Assimalign.Viu.Generators.Syntax` incremental **source generator** projects every `.viu`
   `AdditionalText` through `SingleFileComponentProjection.Project` and emits the result inside the
   Roslyn analyzer sandbox, and
2. the `Assimalign.Viu.LanguageService` (shipped in the language-server payload of the Visual Studio
   extension) drives the same core for editor features — declared `@script` members
   (`ScriptBlockAnalyzer.DescribeMembers`), block-to-file position composition
   (`SingleFileComponentDiagnostics.ComposeToFilePosition`), and, per the semantic roadmap
   (`extensions/VisualStudio/Assimalign.Viu.VisualStudio/docs/DESIGN.md`), the full projected
   document.

The two-host equality is not a convention but a pinned contract:
`SingleFileComponentProjectionConformanceTests` (`analyzers/Assimalign.Viu.Generators.Syntax/test`)
drives every fixture through the generator harness AND this facade and asserts ordinal-identical
generated source, hint names, and diagnostic sets;
`SingleFileComponentProjectionLineMappingTests` (this library's `test/`) compiles the emitted
scaffold at the library boundary and asserts a deliberate `@script` type error maps back to the
`.viu` line and column through the emitted `#line` map.

## What stays host-specific

The library is the projection only. Each host keeps its own concerns on top:

| Host | Kept host-side |
| --- | --- |
| Source generator | The incremental pipeline and cache, file reads, hot-reload gating, `.vue`-shadowing (a multi-file MSBuild concern), and the Roslyn materialization of diagnostics (`SingleFileComponentDiagnosticAdapter`, so RS2008 / `AnalyzerReleases.Shipped.md` release tracking stays in the analyzer project). |
| Language service | Request caching (the bounded content-keyed `ScriptDeclarationReader` cache), LSP mapping of positions and severities, and completion/outline surfaces. |

Diagnostics are therefore **host-neutral by design**: the projection returns `DiagnosticInfo`
(a stable `SingleFileComponentDiagnosticDescriptor` catalog entry + `LocationInfo` + message), and
each host materializes at its own edge. The 1:1 adapter coverage is pinned by
`SingleFileComponentDiagnosticAdapterTests`.

## The `DocumentationMode` seam

The one deliberate per-consumer divergence: the **split arithmetic is shared, the parse options are
not**. `ScriptBlockAnalyzer.Analyze` (the build path) parses with `DocumentationMode.None` — the
build has no use for doc comments and the cheaper parse keeps generation fast.
`ScriptBlockAnalyzer.DescribeMembers` (the editor path) parses with `DocumentationMode.Parse` so
`///` summaries surface in completion and outline. Both run the SAME `LocateLeadingUsingSplit`
core, so the region boundaries — and every `#line`-mapped coordinate derived from them — cannot
diverge between the hosts. The documentation mode is a parameter of the shared core, never a fork
of it.

## Boundaries

- **netstandard2.0** (`$(TargetFrameworkForAnalyzers)`), `EnablePreviewFeatures=false`, linked
  `Shims/` — it loads in the Roslyn analyzer sandbox alongside the syntax cluster (documented
  deviation from the net10.0 library default, recorded in the csproj).
- **No I/O.** `EnforceExtendedAnalyzerRules=true` keeps the RS1035 guard the code lived under inside
  the generator; inputs arrive as already-read text in `SingleFileComponentProjectionInput`.
- **No reflection, no dynamic codegen.** Recoverable by design: malformed input still produces a
  model plus diagnostics; the only expected exception is `OperationCanceledException`.
- **All-internal surface.** The facade's result exposes the whole evolving model graph, so a public
  surface would freeze ~14 model types as API. The `InternalsVisibleTo` list in
  `src/Properties/AssemblyInfo.cs` IS the sanctioned-consumer declaration (the two hosts + test
  assemblies), matching the `Assimalign.Viu.Compiler.Css` → generator precedent.
- **Packaging.** The assembly joins the generator's computed analyzer closure (packed at
  `analyzers/dotnet/cs` and listed in the ref pack's `FrameworkList.xml`) and the self-contained
  language-server publish. Roslyn itself never rides along — `PackageReference` assets do not enter
  the ProjectReference closure — and it must stay OUT of the `Assimalign.Viu.Sdk.Tasks` /
  `Compiler.Css` graph, whose output directory sweeps wholesale into the SDK package's `Tasks/`
  payload (both boundaries are asserted by `area-packaging.yml`).

## Non-goals / future

- **Roslyn-workspace semantics.** Steps 3–6 of the semantic IntelliSense roadmap
  (`extensions/VisualStudio/Assimalign.Viu.VisualStudio/docs/DESIGN.md`) — `MSBuildWorkspace`
  loading, synthetic documents, and mapped semantic features — build ON this projection in the
  language-server process; none of that belongs here.
- **Style compilation.** Owned by `Assimalign.Viu.Compiler.Css` ([V01.01.12.12]); this library layers
  the compile-only concerns (module accessor map, `v-bind()` binding-metadata rewrite) on top of it.
- **A public API.** If a third consumer ever appears, promoting a stable facade to `Abstraction/` is
  a deliberate follow-up work item, not an incidental change.
