# Assimalign.Viu.Syntax.JavaScript — design

Why the JavaScript scaffold exists and how it is shaped. What it is: see [OVERVIEW.md](OVERVIEW.md).
There is no `@vue/compiler-*` counterpart — Viu's component logic is C#, so this library covers only
the plain `.js` around the interop boundary (the role a Vite build's JS handling plays for glue and
host-page scripts).

## Why a scaffold now

The library exists to **pin the seam** before the parser is built: the project shape, the
`SyntaxParser<T>` registration dispatch, and the value-equatable result contract the shared
`Assimalign.Viu.Syntax` pipeline requires (see
[`Assimalign.Viu.Syntax/docs/DESIGN.md`](../../Assimalign.Viu.Syntax/docs/DESIGN.md)). A registered
scaffold lets the interop-tooling work item grow the parser in place without disturbing the seam or
its composition roots.

`ParseCore` returns a single `JavaScriptProgramNode` spanning the whole source, upholding the
`SourceLocation` exact-slice invariant; the whole-source span is replaced by real tokenizer positions
when statement-level parsing lands.

## Constraints (inherited from the cluster)

- **netstandard2.0** analyzer TFM, no file/network I/O, no reflection-based serialization, no dynamic
  code generation. Parsing is recoverable — malformed input never throws.
- Results are immutable and value-equatable so incremental generators cache on them.

## Non-goals (until the work item lands)

- Statement- and expression-level parsing per ECMA-262.
- Any transformation of interop glue or host-page scripts — this parser only locates and slices
  today.
