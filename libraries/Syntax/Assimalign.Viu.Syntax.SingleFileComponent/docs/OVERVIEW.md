# Assimalign.Viu.Syntax.SingleFileComponent — overview

The publicly consumable build-time parsers for Viu single-file components.
`SingleFileComponentParser` owns the canonical
`.viu` hybrid container ([V01.01.06.10]): tag-based `<template>`/`<style>` blocks plus
the `@script { }` C# block and `@`-form custom blocks. The isolated [V01.01.06.09]
`VueSingleFileComponentParser` compatibility entry point slices fully tag-based `.vue` containers
according to that format's root rules; the two engines share one internal tag scanner and therefore
cannot drift. Both record exact
source spans and **do not** parse block contents — template markup, C#, and CSS are parsed by other
libraries. Area: `V01.01.06`. Specified by `[SFC-3]`–`[SFC-5]` and `[VUE-1]`–`[VUE-3]`.

Downstream, the source generator turns a template-bearing `.viu` or `.vue` file into a **mountable
component** — the compiled render function, merged C# script, compiled styles, and the
generated `IComponent` implementation ([V01.01.06.07], [V01.01.06.09]). Both ordinary and setup `.vue` scripts
must explicitly use `lang="csharp"`; each is merged as C# partial-class members with an exact source
map, while JavaScript and the JavaScript compiler macros the `.vue` format permits are never
executed (`[VUE-4]`). Applications register
the generated type with their `IComponentFactory` and request it through
`ComponentTree.Template<TComponent>()`, either as the root supplied to
`BrowserApplicationBuilder.ConfigureApplication` through `ApplicationOptions.RootComponent` or as a
child. A style-only component stays a CSS-bundle unit.
This library owns none of that; it only produces the descriptor those consumers read.

The canonical container syntax (the `<template>`/`<style>` tag grammar, the `@script`/custom
`@`-block grammar with its column-0 termination rule, the legacy `@template`/`@style` transition
window, options and attributes, diagnostics) is specified in [FORMAT.md](FORMAT.md) — the
authoritative spec that the test suite pins. The packaged analyzer targets discover both formats, flow them through
`AdditionalFiles` and the `dotnet watch` item graph, and feed their styles to the physical component
bundle. Same-directory, same-base `.viu` takes deterministic precedence over `.vue` in both generator
and bundle output. Visual Studio content-type routing remains a separate extension boundary.

## Public surface

- **`SingleFileComponentParser`** (static) — `Parse(string)` returns a `SingleFileComponentParseResult`
  (an `SingleFileComponentDescriptor` plus recoverable diagnostics). This is the authoritative
  entry point for the canonical container.
- **`SingleFileComponentDescriptor`** — the parsed file: `Template`
  (0/1), `Script` (0/1), `Styles` (0..n, source order), `CustomBlocks`, and `Source`.
- **`VueSingleFileComponentParser`** (static) — parses a tag-based `.vue` source into
  `VueSingleFileComponentParseResult`, with recoverable structural diagnostics.
- **`VueSingleFileComponentDescriptor`** — the compatibility descriptor. It keeps distinct `Script`
  and `ScriptSetup` slots, plus `Template`, repeated `Styles`, `CustomBlocks`, and `Source`, so the
  `.vue` format's valid one-ordinary-plus-one-setup-script shape round-trips without changing
  canonical `.viu` semantics (`[VUE-2]`).
- **The block model** (`Blocks/`) — `SingleFileComponentBlock` and its
  `Template`/`Script`/`Style`/`CustomBlock` kinds, `SingleFileComponentBlockKind`, and
  `SingleFileComponentBlockOption`. Each block carries its raw content and exact-slice source spans.
- **`SingleFileComponentSyntaxParser`** — the `AggregateSyntaxParser<SingleFileComponentBlock>`
  adapter (`ParseComponent`): same slicing, but each block is exposed to the registration seam as a
  `SyntaxSource` so Viu composition roots or developer-authored tooling can attach
  template/style/custom parsers.
- **`VueSingleFileComponentSyntaxParser`** — the equivalent aggregate adapter for tag-based `.vue`
  sources. It dispatches template, ordinary script, setup script, repeated style, and custom blocks
  through the same registered parser contracts while preserving their original block nodes and spans.
- **Diagnostics** (`Diagnostics/`) — `SingleFileComponentError` and the Viu-defined
  `SingleFileComponentErrorCode` (1000-based). Severity comes from the catalog: the [V01.01.06.10]
  legacy-container codes (1015/1016) are warnings, everything else is an error, and the result's
  `Errors` list carries all severities.

## Boundaries

- Roots on **`Assimalign.Viu.Syntax`** only; it never references the template, CSS, or any other
  language library — the composition root wires those in through the aggregate registration seam.
- Build-time library: targets the netstandard2.0 analyzer TFM so it can run inside Roslyn generator
  hosts, language-server processes, and developer-authored tooling; `IsAotCompatible` does not apply
  (a documented deviation for this TFM).
- Design rationale and the divergences: [DESIGN.md](DESIGN.md).
