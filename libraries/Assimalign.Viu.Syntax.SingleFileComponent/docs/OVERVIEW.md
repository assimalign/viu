# Assimalign.Viu.Syntax.SingleFileComponent — overview

The build-time parser for the `.viu` single-file component (SFC) — the Viu counterpart of Vue's
`.vue` files and the role [`@vue/compiler-sfc`](https://github.com/vuejs/core/tree/main/packages/compiler-sfc)
`parse()` plays: it slices a `.viu` file into its blocks and records their source spans. It does
**not** parse the contents of a block — the template markup, C#, and CSS inside are parsed by other
libraries. Area: `V01.01.06`.

Downstream, the source generator turns a `@template`-bearing `.viu` into a **mountable component** — the
compiled render function, the merged `@script`, and the `IComponentDefinition` bridge ([V01.01.06.07]) that
lets it be passed straight to `CreateApp` / `VirtualNodeFactory.Component`. A `@style`-only `.viu` stays a
CSS-bundle unit. This library owns none of that; it only produces the descriptor those consumers read.

The exact container syntax (the `@template`/`@script`/`@style` `@`-block grammar, the column-0
termination rule, options, diagnostics) is specified in [FORMAT.md](FORMAT.md) — the authoritative
spec that the test suite pins.

## Public surface

- **`SingleFileComponentParser`** (static) — `Parse(string)` returns a `SingleFileComponentParseResult`
  (an `SingleFileComponentDescriptor` plus recoverable diagnostics). This is the authoritative,
  `@vue/compiler-sfc`-parity entry point.
- **`SingleFileComponentDescriptor`** — the parsed file, mirroring Vue's `SFCDescriptor`: `Template`
  (0/1), `Script` (0/1), `Styles` (0..n, source order), `CustomBlocks`, and `Source`.
- **The block model** (`Blocks/`) — `SingleFileComponentBlock` and its
  `Template`/`Script`/`Style`/`CustomBlock` kinds, `SingleFileComponentBlockKind`, and
  `SingleFileComponentBlockOption`. Each block carries its raw content and exact-slice source spans.
- **`SingleFileComponentSyntaxParser`** — the `AggregateSyntaxParser<SingleFileComponentBlock>`
  adapter (`ParseComponent`): same slicing, but each block is exposed to the registration seam as a
  `SyntaxSource` so build tooling can attach the template/style/custom parsers.
- **Diagnostics** (`Diagnostics/`) — `SingleFileComponentError` and the Viu-defined
  `SingleFileComponentErrorCode` (1000-based).

## Boundaries

- Roots on **`Assimalign.Viu.Syntax`** only; it never references the template, CSS, or any other
  language library — the composition root wires those in through the aggregate registration seam.
- Build-time library: targets the netstandard2.0 analyzer TFM and runs inside Roslyn generator hosts;
  `IsAotCompatible` does not apply (a documented deviation for this TFM).
- Design rationale and the divergences: [DESIGN.md](DESIGN.md).
