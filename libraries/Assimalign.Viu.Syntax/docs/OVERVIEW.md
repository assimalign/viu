# Assimalign.Viu.Syntax — overview

The shared base of the `Assimalign.Viu.Syntax.*` cluster: the primitives and the parser pipeline
every language library roots on. It ships no language of its own.

## What it provides

- **Primitives** — `Position`, `SourceLocation` (with the exact-slice invariant), `SyntaxList<T>`
  (structural equality), and `SyntaxNode` (the located, value-comparable record root of every node
  hierarchy, with the `RawKind` integer projection of each language's own kind enum).
- **Diagnostics** — `Diagnostic` (message, location, severity, `RawCode` projection) and
  `DiagnosticSeverity` (Roslyn-parity members). The base unifies only the *shape*; code catalogs and
  delivery mechanisms stay per-language.
- **The parser pipeline** — `SyntaxParser` (language-agnostic contract over a `SyntaxSource`),
  `SyntaxParser<T>` (typed `ParseCore` + synchronous `SyntaxAnalyzer<T>` passes configured via
  `SyntaxParserOptions<T>`), and the value-equatable `SyntaxParserResult`/`SyntaxParserResult<T>`
  records.
- **The aggregate seam** — `AggregateSyntaxParser<T>` for container languages whose nodes embed other
  languages: registrations on `AggregateSyntaxParserOptions<T>` route each node's embedded
  `SyntaxSource` (content + name + `lang`) to the first matching registered parser,
  incremental-generator style.

## Who roots on it

| Library | Language |
| --- | --- |
| `Assimalign.Viu.Syntax.Templates` | The Viu template language — HTML-flavored markup with directives and interpolation |
| `Assimalign.Viu.Syntax.SingleFileComponent` | The `.viu` container, plus the `.vue` compatibility parser ([V01.01.06.09]) |
| `Assimalign.Viu.Syntax.Css` | CSS (scaffold — raw stylesheet root today) |
| `Assimalign.Viu.Syntax.Html` | Plain HTML documents, e.g. the WASM host page (scaffold) |

All of these run at build time inside netstandard2.0 Roslyn generator hosts
([V01.01.05.05]/[V01.01.06.02]); see [DESIGN.md](DESIGN.md) for the constraints that follow.
