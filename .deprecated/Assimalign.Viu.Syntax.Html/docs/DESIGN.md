# Assimalign.Viu.Syntax.Html — design

Why the HTML scaffold exists and how it is shaped. What it is: see [OVERVIEW.md](OVERVIEW.md).
Upstream analogue: Vite's HTML entry processing (there is no `@vue/compiler-*` HTML-document parser;
Vue's template markup is a different language, handled by `Assimalign.Viu.Syntax.Templates`).

## Why a scaffold now

The library exists to **pin the seam** before the parser is built: the project shape, the
`SyntaxParser<T>` registration dispatch, and the value-equatable result contract that the shared
`Assimalign.Viu.Syntax` pipeline requires (see
[`Assimalign.Viu.Syntax/docs/DESIGN.md`](../../Assimalign.Viu.Syntax/docs/DESIGN.md)). Registering a
scaffold now means the host-page rewrite work item can grow the parser in place without moving the
seam or churning the composition roots that depend on it.

`ParseCore` returns a single `HtmlDocumentNode` spanning the whole source, upholding the
`SourceLocation` exact-slice invariant; the whole-source span is replaced by real tokenizer positions
when element-level parsing lands.

## Constraints (inherited from the cluster)

- **netstandard2.0** analyzer TFM, no file/network I/O, no reflection-based serialization, no dynamic
  code generation. Parsing is recoverable — malformed input never throws.
- Results are immutable and value-equatable so incremental generators cache on them.

## Non-goals (until the work item lands)

- Element-, attribute-, and text-level parsing per the WHATWG model.
- Host-page entry rewriting (static-web-asset injection) — the consumer-facing feature this parser
  will back.
