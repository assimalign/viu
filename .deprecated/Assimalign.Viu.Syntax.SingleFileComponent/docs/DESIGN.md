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

## Slice, don't parse

The parser only slices the file into blocks and records spans — it never re-parses, trims, or
normalizes block content. The termination rule is purely structural ("column 0 is structural": a
line whose first column is `}` closes a block), so it needs no knowledge of C#, CSS, or HTML syntax.
Downstream libraries parse the block contents: the template compiler
(`Assimalign.Viu.Syntax.Templates`) for `@template`, the CSS library for `@style`, and script
analysis for `@script` ([V01.01.06.03]). The source generator that composes those parsers then
assembles the result into the mountable component — the compiled render, the merged `@script`, and the
`IComponent` bridge that makes a `@template`-bearing `.viu` a real runtime component
([V01.01.06.07]). None of that lives here: this library's output is the descriptor, nothing more.

## The registration seam

`SingleFileComponentSyntaxParser` is an `AggregateSyntaxParser` over the shared
`Assimalign.Viu.Syntax` pipeline: it exposes each block as a `SyntaxSource` (content, block name,
`lang`) so a composition root (the generator, a build task, a test) can register the parsers the
build embeds — without this library referencing any of them. A registration-free parse is just the
plain container parse, preserving `@vue/compiler-sfc` parity (`parse()` never looks inside block
content).

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

- **Script analysis.** Every `@script` block is treated uniformly (at most one per file); the Vue
  `<script setup>` distinction and script analysis are [V01.01.06.03].
- **Parsing block interiors.** By design — that belongs to the per-language parsers.
