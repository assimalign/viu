# Assimalign.Viu.Syntax.SingleFileComponent — design

Why the `.viu` parser is shaped the way it is. What it is: see [OVERVIEW.md](OVERVIEW.md); the exact
grammar is [FORMAT.md](FORMAT.md). Upstream counterpart:
[`@vue/compiler-sfc`](https://github.com/vuejs/core/tree/main/packages/compiler-sfc) `parse()`
(`packages/compiler-sfc/src/parse.ts`).

## The `@`-block container is a deliberate divergence

Vue wraps SFC blocks in HTML-like tags (`<template>`, `<script>`, `<style>`); a `.viu` file uses
`@`-block container syntax instead (decided 2026-07-17). Only the **container** differs — block
*semantics* follow the Vue SFC spec unchanged, and the markup inside `@template` remains standard Vue
template syntax. This is one half of [ADR-0005](../../../docs/adr/0005-no-runtime-template-compilation.md)
(build-time-only compilation); the full rule set and its rationale are in [FORMAT.md](FORMAT.md).

## Tag-based `.vue` compatibility stays separate

[V01.01.06.09] adds `VueSingleFileComponentParser` as a dedicated compatibility parser; it does not
change `SingleFileComponentParser` or the canonical `.viu` grammar. The parser follows Vue 3.5's SFC
tokenizer boundary: an HTML `template` uses nested markup boundaries, while root `script`, `style`,
custom blocks, and preprocessed templates are raw text until their matching end tag. Its scanner keeps
end-tag-shaped text inside quoted attributes, comments, and nested raw-text elements from closing the
root template. See
[`tokenizer.ts`](https://github.com/vuejs/core/blob/v3.5.34/packages/compiler-core/src/tokenizer.ts)
and
[`parse.ts`](https://github.com/vuejs/core/blob/v3.5.34/packages/compiler-sfc/src/parse.ts).

The compatibility result deliberately uses `VueSingleFileComponentDescriptor` rather than changing
the canonical descriptor. Vue allows one ordinary `<script>` and one `<script setup>` in the same
file, while `.viu` has one uniform `@script` slot pending its own script-analysis work. Both descriptor
types reuse the immutable block, option, diagnostic, and source-location values where their semantics
are identical.

## Slice, don't parse

The parser only slices the file into blocks and records spans — it never re-parses, trims, or
normalizes block content. Canonical blocks use the purely structural column-0 `}` termination rule.
Tag-based blocks use the Vue-compatible nested-template or raw-text closing-tag boundary described
above. Neither container parser needs knowledge of C#, CSS, or template-expression semantics.
Downstream libraries parse the block contents: the template compiler
(`Assimalign.Viu.Syntax.Templates`) for `@template`, the CSS library for `@style`, and script
analysis for `@script` ([V01.01.06.03]). The source generator that composes those parsers then
assembles the result into the mountable component — the compiled render, merged C# script, compiled
styles, and the `IComponentTemplate` bridge that makes a template-bearing component a real runtime component
([V01.01.06.07]). None of that lives here: this library's output is the descriptor, nothing more.

## The registration seam

`SingleFileComponentSyntaxParser` and `VueSingleFileComponentSyntaxParser` are
`AggregateSyntaxParser` adapters over the shared `Assimalign.Viu.Syntax` pipeline. Each exposes its
blocks as a `SyntaxSource` (content, block name, `lang`) so a composition root (the generator, a build
task, or a test) can register the parsers the build embeds — without this library referencing any of
them. The `.vue` adapter includes both ordinary and setup script slots and preserves source order. A
registration-free parse is just the plain container parse, preserving `@vue/compiler-sfc` parity
(`parse()` never looks inside block content).

## Generator compatibility contract

The syntax generator accepts `.viu` and `.vue` `AdditionalText` inputs and converges both aggregate
results on the same template, style, script-analysis, source-mapping, and emission pipeline. A `.vue`
ordinary or setup script is merged only when it declares the exact `lang="csharp"` contract. Missing
and other language values produce `VIU1206`; their content is never executed or merged. The two legal
script slots remain separate through analysis and each retains its own exact `#line` map, then both
contribute C# partial-class members and template binding metadata. This is an explicit C# compatibility
contract: Viu does not execute JavaScript or implement Vue's JavaScript compiler macros. A
same-directory, same-base `.viu`/`.vue` pair produces `VIU1004`; canonical `.viu` wins and only one
component is emitted.

The packaged analyzer targets glob both `**/*.viu` and `**/*.vue` into the same
`ViuSingleFileComponent`, `AdditionalFiles`, and `Watch` graph. The physical component-style bundler
selects the matching outer parser per file, then uses the same CSS parser/compiler as the generator.
It filters a shadowed `.vue` peer with the same normalized same-base rule, preventing a duplicate or
contradictory stylesheet segment when the generator reports `VIU1004`.

Inline `.vue` blocks may begin after their opening tag on the same physical line. The generator
therefore carries both start line and start column through C# script analysis and pads the first
emitted `#line`-mapped line to retain exact compiler spans. Template expression mappings compose the
template-relative span with the block content's full line-and-column position.

## Value equality and recoverable diagnostics

The descriptor and every block are immutable records with structural equality, so identical file
content yields equal (and equally hashed) descriptors — the prerequisite for incremental-generator
caching ([V01.01.06.02], and see
[`Assimalign.Viu.Syntax/docs/DESIGN.md`](../../Assimalign.Viu.Syntax/docs/DESIGN.md)). Parsing is
recoverable: malformed input is reported through diagnostics and the parser never throws for bad
content.

## Deltas from Vue 3

- **`@`-block container** instead of tag-based blocks (above; specified in [FORMAT.md](FORMAT.md)).
- **Viu-defined error codes** (`SingleFileComponentErrorCode`, 1000-based). Unlike the template
  compiler, which mirrors vuejs/core's numbering, the `@`-block container is a Viu divergence with no
  upstream codes to align to.

## Non-goals

- **Vue JavaScript setup macros.** Explicit C# `<script setup lang="csharp">` is partial-class member
  shorthand; JavaScript top-level execution and macros such as `defineProps` are not evaluated.
- **Parsing block interiors.** By design — that belongs to the per-language parsers.
