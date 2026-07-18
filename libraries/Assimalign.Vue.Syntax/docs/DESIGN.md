# Assimalign.Vue.Syntax — design

Why the base is shaped the way it is, and the deliberate divergences. The extraction is
[V01.01.05.09] (issue #127); the parser pipeline is its second iteration, replacing the initial
scaffold.

## Registration over hard-wiring

The pipeline exists so **build tooling can register parsers for the sources they understand** — the
role Vite plugins play in a Vue build, modeled on how a Roslyn incremental generator registers
outputs for additional files. `SyntaxSource` (text + name + `lang` hint) is the carrier a
registration's `SyntaxSourcePredicate` matches on; `AggregateSyntaxParser<T>` applies registrations
to a container language's nodes (the `.viu` file being the canonical container). Consequences:

- **Language libraries never reference each other.** The single-file-component library does not know
  the template or CSS parsers exist; the *composition root* (a generator, a build task, a test)
  constructs the aggregate options and registers whatever parsers the build embeds — including
  Vuecs-owned tooling like utility-class style generation, or user-supplied parsers for custom
  blocks.
- **First matching registration wins**, in registration order, so specific predicates
  (`lang`-qualified) register before general ones. Unmatched nodes are simply not dispatched — a
  registration-free aggregate parse *is* the plain container parse, preserving
  `@vue/compiler-sfc` parity (`parse()` never looks inside block content).
- The upstream-parity static entry points (`TemplateParser.Parse`, `SingleFileComponentParser.Parse`)
  remain the authoritative parsing semantics; the instance parsers (`TemplateSyntaxParser`,
  `SingleFileComponentSyntaxParser`) are adapters over them, not reimplementations.

## Value equality is the caching contract

Everything a parse produces — nodes, diagnostics, results, dispatched aggregate entries — is a
record (or a `SyntaxList<T>`), so **identical input yields an equal, equally-hashed result**. Roslyn
incremental generators cache on exactly this. It is why:

- result `Diagnostics` ride in a `SyntaxList<Diagnostic>` rather than a reference-compared
  `IReadOnlyList<T>`;
- `SyntaxList<T>` constrains `T : class` only — it also carries diagnostics, block options, and the
  template IR's upstream-untyped `object` arrays, none of which are nodes;
- the analyzer pipeline appends diagnostics with a `with` clone, which preserves the derived result
  record's runtime type and state (pinned by tests).

## `RawKind`/`RawCode` projections, not base enums

A base-level kind enum would be a closed catalog that every new language library (or user-supplied
parser) would need to extend — the opposite of the registration model. Instead each hierarchy owns
its enum (`NodeType` pinned numerically to `@vue/compiler-core`'s `NodeTypes`; Vuecs-defined
catalogs elsewhere) and projects it as `SyntaxNode.RawKind` / `Diagnostic.RawCode` integers for
language-agnostic infrastructure — Roslyn's `SyntaxNode.RawKind` precedent. The same reasoning keeps
diagnostic *catalogs and delivery* per-language (OnError push for the template compiler versus
result-errors pull for the single-file component — the upstream split).

## Synchronous analyzers

`SyntaxAnalyzer<T>` is synchronous with a cooperative `CancellationToken` (Roslyn's analyzer model).
The generator hosts drive a synchronous pipeline on netstandard2.0, so an async surface would add
scheduling cost and no parallelism worth having; `AnalyzerTimeout` bounds the pass via a linked
`CancellationTokenSource` and surfaces as `OperationCanceledException`.

## Constraints inherited by every library in the cluster

- **netstandard2.0** (`$(TargetFrameworkForAnalyzers)`), `EnablePreviewFeatures=false`, linked
  `Shims/` sources — the assemblies load inside Roslyn analyzer hosts.
- No file/network I/O, no reflection-based serialization, no dynamic code generation. Parsing is
  recoverable: malformed input reports diagnostics and never throws.

## Non-goals

- No shared kind or error-code enum across languages (see above).
- No async parsing surface.
- The Css/Html/JavaScript scaffolds intentionally parse to a raw whole-source root node until their
  work items land (starting with scoped CSS [V01.01.06.04]) — the scaffolds exist to pin the
  pipeline seam, project shape, and caching contract, not to promise parsing.
