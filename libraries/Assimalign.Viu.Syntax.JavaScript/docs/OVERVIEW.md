# Assimalign.Viu.Syntax.JavaScript — overview

The `.js` language of the `Assimalign.Viu.Syntax.*` cluster — the parser build tooling registers for
JavaScript around the JS-interop boundary (interop glue modules, host-page scripts). Component logic
is C# (Roslyn's domain) and template expressions stay in `Assimalign.Viu.Syntax.Templates`, so this
library is scoped to the plain `.js` files a Viu build touches. Area: `V01.01.05` (Syntax cluster).

> **Scaffold.** The parser currently produces a single located `JavaScriptProgramNode` carrying the
> raw source, with no diagnostics — enough to pin the pipeline seam and the caching contract.
> Statement-level parsing per [ECMA-262](https://tc39.es/ecma262/) lands with its own work item.

## Public surface

- **`JavaScriptSyntaxParser`** — the `SyntaxParser<JavaScriptSyntaxNode>` build tooling registers for
  `.js`/`.mjs` sources.
- **`JavaScriptProgramNode`** — the located program node (currently the whole-source root).
- **`JavaScriptSyntaxNode`** / **`JavaScriptSyntaxNodeKind`** — the node base and its kind projection.

## Boundaries

- Roots on **`Assimalign.Viu.Syntax`** only; a build-time library on the netstandard2.0 analyzer TFM
  that runs inside Roslyn generator hosts.
- Parsing is recoverable and value-equatable (the incremental-generator caching contract), following
  the shared pipeline in [`Assimalign.Viu.Syntax/docs/OVERVIEW.md`](../../Assimalign.Viu.Syntax/docs/OVERVIEW.md).
- Design notes and the scaffold's rationale: [DESIGN.md](DESIGN.md).
