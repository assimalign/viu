# Assimalign.Viu.Syntax.SingleFileComponent — design

Why the `.viu` parser is shaped the way it is. What it is: see [OVERVIEW.md](OVERVIEW.md); the exact
grammar is [FORMAT.md](FORMAT.md), which is normative. The structural invariants are specified by
`[SFC-3]`–`[SFC-5]`.

## The hybrid container — and the recorded reversal

The original [V01.01.06.01] design (2026-07-17) put *every* block in an `@`-block container. That
decision was **partially reversed on 2026-08-02 per user direction ([V01.01.06.10], #257)**: a `.viu`
file now uses a **hybrid container** — tag-based `<template>`/`<style>`, with the
component's C# in `@script { }` and custom blocks kept `@`-syntax. The rationale: markup and CSS gain
real value from tag wrappers (familiarity, tooling, no indentation discipline for
raw CSS — and the same wrappers the `.vue` compatibility container uses, which is what lets both
converge on one downstream pipeline, `[VUE-9]`), while a C# block gains nothing from an HTML wrapper —
the `@` reads as "C# starts here", as
in Razor. The legacy `@template`/`@style` forms
still parse through a Warning-severity migration window (1015/1016), and a top-level `<script>` tag
is rejected with an error (1017) so its content can never silently skip compilation. Build-time-only
compilation is one half of
[ADR-0005](../../../docs/adr/0005-no-runtime-template-compilation.md); the full rule set is in
[FORMAT.md](FORMAT.md).

## Tag-based `.vue` compatibility stays separate — but shares the scanner

[V01.01.06.09] adds `VueSingleFileComponentParser` as a dedicated compatibility parser; it does not
change `SingleFileComponentParser` or the `.viu` grammar (`[VUE-1]`). Both engines use one tag-boundary
rule: an HTML `template` uses nested markup boundaries, while root `style`, custom
blocks, and preprocessed templates are raw text until their matching end tag. `.viu` adopts the `.vue`
container's boundary rule deliberately — it is what makes the no-drift guarantee below mechanical.
Since [V01.01.06.10]
the tag machinery — opening-tag/attribute parsing, the nested-template boundary, raw-text closing-tag
search, malformed-tag recovery — lives in one shared internal `SingleFileComponentTagScanner` that
both the canonical hybrid engine and the `.vue` engine construct over their own span/report sinks, so
the two containers cannot drift (`[VUE-3]`). The scanner keeps end-tag-shaped text inside quoted
attributes, comments, and nested raw-text elements from closing the root template.
Container-format references for the `.vue` input this parser accepts:
[`tokenizer.ts`](https://github.com/vuejs/core/blob/v3.5.34/packages/compiler-core/src/tokenizer.ts)
and
[`parse.ts`](https://github.com/vuejs/core/blob/v3.5.34/packages/compiler-sfc/src/parse.ts).

The compatibility result deliberately uses `VueSingleFileComponentDescriptor` rather than changing
the canonical descriptor. The `.vue` container allows one ordinary `<script>` and one `<script setup>` in the same
file, while `.viu` has one uniform `@script` slot pending its own script-analysis work. Both descriptor
types reuse the immutable block, option, diagnostic, and source-location values where their semantics
are identical.

## Slice, don't parse

The parser only slices the file into blocks and records spans — it never re-parses, trims, or
normalizes block content. @-blocks use the purely structural column-0 `}` termination rule.
Tag-based blocks — the canonical `.viu` `<template>`/`<style>` and every `.vue` block — use the
shared nested-template or raw-text closing-tag boundary described above. Neither container
parser needs knowledge of C#, CSS, or template-expression semantics. Downstream libraries parse the
block contents: the template compiler (`Assimalign.Viu.Syntax.Templates`) for the template block, the
CSS library for the style blocks, and script analysis for `@script` ([V01.01.06.03]). The source
generator that composes those parsers then assembles the result into the mountable component — the
compiled render, merged C# script, compiled styles, and the `IComponentTemplate` bridge that makes a
template-bearing component a real runtime component ([V01.01.06.07]). None of that lives here: this
library's output is the descriptor, nothing more.

## The registration seam

`SingleFileComponentSyntaxParser` and `VueSingleFileComponentSyntaxParser` are
`AggregateSyntaxParser` adapters over the shared `Assimalign.Viu.Syntax` pipeline. Each exposes its
blocks as a `SyntaxSource` (content, block name, `lang`) so a composition root (the generator, a build
task, or a test) can register the parsers the build embeds — without this library referencing any of
them. The `.vue` adapter includes both ordinary and setup script slots and preserves source order. A
registration-free parse is just the plain container parse — the slice-don't-parse contract holds
either way (`[SFC-5]`).

## Generator compatibility contract

The syntax generator accepts `.viu` and `.vue` `AdditionalText` inputs and converges both aggregate
results on the same template, style, script-analysis, source-mapping, and emission pipeline. A `.vue`
ordinary or setup script is merged only when it declares the exact `lang="csharp"` contract. Missing
and other language values produce `VIU1206`; their content is never executed or merged. The two legal
script slots remain separate through analysis and each retains its own exact `#line` map, then both
contribute C# partial-class members and template binding metadata (`[VUE-4]`, `[VUE-5]`). This is an
explicit C# compatibility contract: Viu does not execute JavaScript, and the JavaScript compiler
macros the `.vue` format permits (`defineProps` and friends) are never evaluated. A
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

## Platform adaptations

Where the `.viu` container deliberately differs from the `.vue` container it stays compatible with:

- **`@script { }` and `@`-form custom blocks** instead of `<script>`/custom tags — the remaining
  container difference after the [V01.01.06.10] hybrid pivot (above; specified in
  [FORMAT.md](FORMAT.md)). `<template>`/`<style>` are tag-based in both; a top-level `<script>`
  tag in `.viu` is a hard error (1017), never silently ignored.
- **A separate error-code catalog** (`SingleFileComponentErrorCode`, 1000-based) so a container
  diagnostic is distinguishable at a glance from the template compiler's `CompilerErrorCode`, whose
  parse band occupies the low numbers. Severity is a catalog property: the legacy-container codes
  (1015/1016) are warnings; everything else is an error.

## Non-goals

- **JavaScript setup macros.** Explicit C# `<script setup lang="csharp">` is partial-class member
  shorthand; JavaScript top-level execution and macros such as `defineProps` are not evaluated
  (`[VUE-4]`).
- **Parsing block interiors.** By design — that belongs to the per-language parsers.
