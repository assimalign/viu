# Assimalign.Viu.Syntax.Html — overview

The `.html` language of the `Assimalign.Viu.Syntax.*` cluster — the parser build tooling registers
for HTML documents, above all the WASM host page (`wwwroot/index.html`), the role Vite's HTML entry
processing plays in a Vue build. Vue *template* markup is the separate
`Assimalign.Viu.Syntax.Templates` parser, not this one. Area: `V01.01.05` (Syntax cluster).

> **Scaffold.** The parser currently produces a single located `HtmlDocumentNode` carrying the raw
> source, with no diagnostics — enough to pin the pipeline seam and the caching contract. Element-
> level parsing per the [WHATWG HTML parsing model](https://html.spec.whatwg.org/multipage/parsing.html)
> lands with its own work item.

## Public surface

- **`HtmlSyntaxParser`** — the `SyntaxParser<HtmlSyntaxNode>` build tooling registers for `.html`
  sources.
- **`HtmlDocumentNode`** — the located document node (currently the whole-source root).
- **`HtmlSyntaxNode`** / **`HtmlSyntaxNodeKind`** — the node base and its kind projection.

## Boundaries

- Roots on **`Assimalign.Viu.Syntax`** only; a build-time library on the netstandard2.0 analyzer TFM
  that runs inside Roslyn generator hosts.
- Parsing is recoverable and value-equatable (the incremental-generator caching contract), following
  the shared pipeline in [`Assimalign.Viu.Syntax/docs/OVERVIEW.md`](../../Assimalign.Viu.Syntax/docs/OVERVIEW.md).
- Design notes and the scaffold's rationale: [DESIGN.md](DESIGN.md).
