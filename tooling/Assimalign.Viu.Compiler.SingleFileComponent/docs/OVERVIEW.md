# Assimalign.Viu.Compiler.SingleFileComponent — overview

The shared, build-time `.viu`/`.vue` → C# **projection core** ([V01.01.06.11], #258). It is the ONE
implementation of the parse → analyze → compile → model pipeline that turns a single-file-component
source into its generated partial-class scaffold, its bidirectional source maps, and its mapped
diagnostics. It exists so the two hosts that need that projection — the
`Assimalign.Viu.Generators.Syntax` **source generator** (which emits the scaffold at build time) and
the `Assimalign.Viu.LanguageService` **language server** (which reasons about the same source in the
editor) — run one implementation and cannot drift. Specified by
[`[TOOL-2]`](../../../docs/SPECIFICATION.md#14-the-tooling-and-editor-contract).

The rationale, the netstandard2.0 / no-I/O constraints, and the `DocumentationMode` seam are in
[DESIGN.md](DESIGN.md).

## Surface

The generator and language service consume explicit public build-time contracts. Implementation-only
types remain internal, and only the projection core's own tests receive friend access.

- **`SingleFileComponentProjection`** — the facade: `Project(input, cancellationToken)` takes a
  value-equatable `SingleFileComponentProjectionInput` (format, path, text, resolved names, scope id,
  hot-reload identity) and returns a value-equatable `SingleFileComponentProjectionResult` (the
  scaffold `SingleFileComponentModel` plus host-neutral `DiagnosticInfo` diagnostics).
- **`SingleFileComponentSourceEmitter`** — renders the model into the full generated C# source, with
  the `#line` script map and the `#line (l,c)-(l,c)` render-body span map.
- **`ScriptBlockAnalyzer`** — the `@script` region split and member classification: `Analyze` (the
  build path) and `DescribeMembers` (the editor path — declared-member names, kinds, details, doc
  summaries, block-relative locations).
- **`SingleFileComponentNameResolver`** / **`SingleFileComponentHotReloadMetadataFactory`** — the
  shared name/namespace/hint-name resolution and the path-stable hot-reload identity. Hint names are
  path-derived and readable; `SelectCaseCollidingPaths` takes the whole emitted set and reports which
  components must add the path-hash discriminator, because Roslyn's `AddSource` treats hint names
  differing only by case as one name (`[SFC-CG-5]`, [V01.01.06.10.01]).
- **`SingleFileComponentDiagnostics`** — the host-neutral VIU diagnostic catalog (`VIU1001` …
  `VIU1405`) and the block-to-file position composition; each host materializes at its own edge (the
  generator's Roslyn adapter, the language service's LSP mapping).
- **Component identity validation** — the parsed-template usage manifest and compilation-wide declaration
  catalog preserve PascalCase component precedence over case-colliding native elements, include
  parameterless declarations, distinguish missing from ambiguous identities, and exempt only explicit
  runtime selection and configured compiler/runtime built-ins (`[SFC-CG-8]`, `[SFC-USE-5]`,
  [V01.01.05.11]).
- **Model records** — `SingleFileComponentModel`, `ScriptRegions`, `ScriptBinding`,
  `ScriptDeclaredMember`, `CssModuleClassEntry`, `CssVariableBindingEntry`, `LocationInfo`,
  `DiagnosticInfo`, `EquatableArray<T>` — all value-equatable so both hosts can cache on them.

## Boundaries

- References `Assimalign.Viu.Syntax`, `.Syntax.SingleFileComponent`, `.Syntax.Templates`, and
  `.Compiler.Css` (which owns the parser composition, scope-id hash, and style compilation), plus
  `Microsoft.CodeAnalysis.CSharp` for the `@script` parse. Composition-root code, like the sibling
  `Assimalign.Viu.Compiler.Css` — not a peer `Assimalign.Viu.Syntax.*` language library.
- **netstandard2.0** (`$(TargetFrameworkForAnalyzers)`), **no I/O** (`EnforceExtendedAnalyzerRules` /
  RS1035 kept from the generator), no reflection, no dynamic codegen — it loads inside the Roslyn
  analyzer sandbox and in the language-server process alike.
- **Deterministic and value-equatable end to end** — the generator's incremental cache and the
  language service's snapshot caching both key on the records this library returns.
- Conformance is pinned by test: `SingleFileComponentProjectionConformanceTests`
  (`analyzers/Assimalign.Viu.Generators.Syntax/test`) holds the generator host and this facade
  ordinal-identical on generated source, hint names, and diagnostics.
