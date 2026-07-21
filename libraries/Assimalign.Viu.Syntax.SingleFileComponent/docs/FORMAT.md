# The `.viu` single-file component format

`Assimalign.Viu.Syntax.SingleFileComponent` defines and parses the `.viu` single-file component (SFC) format — the Viu
counterpart to Vue's `.vue` files. This document is the authoritative specification for the container
syntax and the block parser (`SingleFileComponentParser.Parse`). It matches the parser's behavior exactly; the test
suite (`libraries/Assimalign.Viu.Syntax.SingleFileComponent/test/`) pins every rule stated here.

Work item: **[V01.01.06.01]**. Block semantics mirror the Vue SFC specification
(<https://vuejs.org/api/sfc-spec.html>); descriptor shape mirrors `parse()` in `@vue/compiler-sfc`
(vuejs/core, `packages/compiler-sfc/src/parse.ts`).

## 1. The @-block container is a documented Viu divergence

Vue wraps SFC blocks in HTML-like tags — `<template>…</template>`, `<script>…</script>`,
`<style>…</style>`. Viu deliberately diverges (design decision, 2026-07-17): a `.viu` file uses
**@-block container syntax** instead.

```
@template {
    <!-- Vue template markup: directives, {{ }} interpolation -->
    <div>{{ message }}</div>
}

@script {
    // C# — the component's partial-class body
    public string Message = "Hello";
}

@style scoped {
    /* CSS */
    .box { color: red; }
}
```

Only the **container** is different. The block *semantics* — what `template`/`script`/`style`/custom
blocks mean, and what their options mean — follow the Vue SFC spec unchanged. In particular, the markup
inside `@template` is **standard Vue template syntax**; the block parser does not parse it. It only
slices the file into blocks and records their source spans. The template markup is parsed later by the
template compiler (`Assimalign.Viu.Syntax.Templates`, [V01.01.05.01]); the C# in `@script` is analysed by
[V01.01.06.03].

A `.viu` with an `@template` compiles to a **mountable component**: the source generator emits the
compiled render function ([V01.01.05.05]), merges the `@script` C# into the partial class ([V01.01.06.03]),
and — as of [V01.01.06.07] — generates the `IComponent` bridge (a `Name` plus a `Setup` that
allocates the render cache, wires slots, applies any `v-bind()` CSS custom properties, and returns the
render delegate). So a `@template`-bearing `.viu` is passed straight to `BrowserApplication.CreateBuilder(...)` /
`VirtualNodeFactory.Component(...)` with no hand-written wiring, and reactive `@script` members drive
re-render. A `.viu` with **no** `@template` (a `@style`-only CSS-bundle unit, or a `@script`-only partial)
stays a plain partial class — no component bridge — so it keeps compiling exactly as before. This library
still only *slices*; the bridge is emitted by the generator that consumes the descriptor.

### Upstream-semantics mapping

| `.viu`                         | Vue SFC                          | Meaning (per the Vue SFC spec)                    |
| ------------------------------ | -------------------------------- | ------------------------------------------------- |
| `@template { … }`              | `<template> … </template>`       | The component's markup.                            |
| `@template lang="html" { … }`  | `<template lang="html">`         | Markup pre-processor language.                     |
| `@script { … }`                | `<script> … </script>`           | The component's script body.                       |
| `@script lang="csharp" { … }`  | `<script lang="…">`              | Script language.                                   |
| `@style { … }`                 | `<style> … </style>`             | Component CSS (a file may have several).           |
| `@style scoped { … }`          | `<style scoped>`                 | [Scoped CSS](https://vuejs.org/api/sfc-css-features.html#scoped-css). |
| `@style module { … }`          | `<style module>`                 | [CSS Modules](https://vuejs.org/api/sfc-css-features.html#css-modules) (default name). |
| `@style module="classes" { … }`| `<style module="classes">`       | CSS Modules bound to a named object.              |
| `@style lang="scss" { … }`     | `<style lang="scss">`            | CSS pre-processor language.                        |
| `@docs { … }` (any other name) | `<docs>` (custom block)          | [Custom block](https://vuejs.org/api/sfc-spec.html#custom-blocks), preserved verbatim. |

> The Vue `<script setup>` distinction is script *analysis*, deferred to [V01.01.06.03]. This parser
> treats every `@script` block uniformly and allows at most one per file.

## 2. Block headers

A block is introduced by a **header line**:

```
@<name> <options>? {
```

- The header line's **first column must be `@`** (see §4 — column 0 is structural). A header that is
  indented is not recognised as a header.
- `<name>` immediately follows the `@` and matches `[A-Za-z_][A-Za-z0-9_-]*` (a letter or `_`, then
  letters, digits, `_`, or `-`).
- The three well-known names `template`, `script`, and `style` are matched **case-sensitively and in
  lowercase**. Any other name (including a different casing such as `@Template`) is a **custom block**.
- The opening `{` must be the **last non-whitespace character on the header line**. Content begins on
  the next line.

### 2.1 Options

Options replace Vue's block attributes and are written between the name and the `{`, separated by
whitespace:

```
@<name> option option="value" {
```

- An option is either a **valueless flag** (`scoped`) or a **key with a double-quoted value**
  (`lang="scss"`).
- An option name matches `[A-Za-z_][A-Za-z0-9_-]*`.
- A value is written as `name="value"` with **no whitespace around `=`**. The value is any run of
  characters except `"` — values are simple tokens (language names, identifiers); there is no escape
  syntax, so a value cannot itself contain a double quote.
- Options are preserved on every block, in source order, each with its own source span. Unknown options
  on any block, and any options on custom blocks, are preserved rather than rejected.

Honored (typed) options, per the Vue SFC spec:

| Option              | Blocks                    | Surfaced as                                        |
| ------------------- | ------------------------- | -------------------------------------------------- |
| `scoped`            | `@style`                  | `SingleFileComponentStyleBlock.Scoped` (`bool`)                    |
| `module`            | `@style`                  | `SingleFileComponentStyleBlock.IsModule` (`bool`)                  |
| `module="name"`     | `@style`                  | `SingleFileComponentStyleBlock.IsModule` + `SingleFileComponentStyleBlock.ModuleName` |
| `lang="…"`          | `@style`/`@script`/`@template` (and custom) | `SingleFileComponentBlock.Lang` (`string?`)      |

All other options remain available through `SingleFileComponentBlock.HasOption(name)` and
`SingleFileComponentBlock.GetOptionValue(name)`.

## 3. Block content

A block's content is the **exact raw source** between the header line and the closing brace:

- It starts at the first character of the line *after* the header line.
- It ends at the first character of the closing-brace line (see §4).
- It is never re-parsed, trimmed, or normalised. Interior indentation and the trailing newline that
  precedes the closing brace are preserved verbatim. An empty body yields an empty string.

`SingleFileComponentBlock.Content` equals `SingleFileComponentBlock.ContentLocation.Source`, and every span the parser emits satisfies
`Location.Source == source.Substring(Start.Offset, End.Offset - Start.Offset)`. Positions carry a
zero-based `Offset`, a one-based `Line`, and a one-based `Column` — suitable for `#line` mapping
([V01.01.06.03]) and IDE diagnostics.

## 4. The termination rule — column 0 is structural

This is the core rule. It is deterministic, language-agnostic, and requires no knowledge of C#, CSS, or
HTML syntax:

> **A block opened by a header line closes at the first later line whose first column is `}`.**

Restated as a single principle: **column 0 is structural.**

- At the **top level** (outside any block), a line whose first column is `@` begins a block header.
- **Inside a block**, a line whose first column is `}` closes the block. Nothing else is examined — the
  parser scans line starts only.
- Every other line is **content** (inside a block) or **stray content** (at the top level).

The immediate consequence is the one requirement the format places on authors:

> **Block content must be indented.** No content line may begin at column 0 with `}`.

Because the parser only inspects the first column of each line, unbalanced or literal braces *inside*
content never terminate a block, as long as content is indented:

```
@script {
    var json = "{ \"a\": 1 }";   // literal { and } inside a C# string — fine
    var closing = "}";           // the } is not at column 0 — fine
}
```

```
@style {
    .a {
        color: red;
    }                            // the CSS rule's own } is indented — fine
    .b { color: blue; }
}
```

```
@template {
    <p>Use { and } carefully</p> <!-- braces in text — fine -->
    @template {                  <!-- even a line that looks like a header is just content -->
}
```

### 4.1 The indentation requirement, made concrete

If CSS (or any content) is written flush against column 0, the content's own `}` closes the block
early. This is the documented flip side of the rule, not a bug:

```
@style {
.a {
    color: red;
}          ← this column-0 } closes the @style block early
}          ← this } is now stray top-level content → StrayTopLevelContent
```

Authors avoid this simply by indenting block bodies (as every example here does).

### 4.2 Closing-brace line details

- Recognition is purely "first column is `}`". Any characters after the `}` on that line are ignored,
  so `} // closes the block` is a valid closer. The block's whole-span `Location` ends immediately
  after the `}`.
- A block that is never closed (end of file is reached with no column-0 `}`) is reported as
  `UnterminatedBlock`; for recovery the block's content is taken to end of file and the block still
  appears in the descriptor.

### 4.3 Between and around blocks

- Blank lines (empty or whitespace-only) at the top level are ignored; they may separate blocks.
- Any non-blank top-level line that is not a valid header is reported as `StrayTopLevelContent` and
  skipped; parsing continues with the next line.

## 5. The descriptor

`SingleFileComponentParser.Parse(string source)` returns an `SingleFileComponentParseResult` — an `SingleFileComponentDescriptor` plus the diagnostics.
Both the descriptor and the blocks are **immutable records with structural (value) equality**: parsing
identical file content twice yields equal (and equal-hashing) descriptors, and any content or location
difference makes them unequal. This is the prerequisite for incremental-generator caching
([V01.01.06.02]).

`SingleFileComponentDescriptor` exposes, mirroring Vue's `SFCDescriptor`:

- `Template` — the single `@template` block, or `null`.
- `Script` — the single `@script` block, or `null`.
- `Styles` — the `@style` blocks, in source order (Vue allows several).
- `CustomBlocks` — all other blocks (e.g. `@docs`), in source order.
- `Source` — the full original file text.

A file may contain **at most one** `@template` and **at most one** `@script`; a second of either is
reported (`DuplicateTemplateBlock` / `DuplicateScriptBlock`) and ignored — the **first** is kept.

## 6. Diagnostics

Parsing is **recoverable**: malformed input is reported through `SingleFileComponentParseResult.Errors` and the parser
never throws for bad content (a `null` source argument throws `ArgumentNullException` — that is API
misuse, not input). Multiple problems are reported in a single pass, each with a code, a message, and a
source location.

The diagnostic codes (`SingleFileComponentErrorCode`) are **Viu-defined**. Unlike `Assimalign.Viu.Syntax.Templates`'s
`CompilerErrorCode`, which mirrors vuejs/core's numbering, the `@`-block container is a Viu divergence
and has no upstream vuejs/core codes to align to. Values start at 1000 to stay visibly distinct from any
upstream-aligned catalog.

| Code                          | Value | Raised when                                                              |
| ----------------------------- | ----- | ----------------------------------------------------------------------- |
| `StrayTopLevelContent`        | 1001  | Non-whitespace text appears at the top level, outside any block.        |
| `MalformedBlockHeader`        | 1002  | A top-level line begins with `@` but no valid block name follows.       |
| `MissingOpeningBrace`         | 1003  | A header names a block but has no opening `{` on its line.              |
| `ContentAfterOpeningBrace`    | 1004  | Non-whitespace follows the opening `{` on a header line.                |
| `MalformedOptionValue`        | 1005  | An option value is not a well-formed double-quoted string.              |
| `DuplicateTemplateBlock`      | 1006  | A file declares more than one `@template`.                              |
| `DuplicateScriptBlock`        | 1007  | A file declares more than one `@script`.                                |
| `UnterminatedBlock`           | 1008  | A block is opened but end of file is reached with no column-0 `}`.      |

### Recovery policy

- A structurally openable header (`@<name> … {`) **always opens the block**; option problems
  (`MalformedOptionValue`) and trailing content (`ContentAfterOpeningBrace`) are reported but do not
  suppress the block, so its content is still sliced.
- A header only fails to open when it has **no valid name** (`MalformedBlockHeader`) or **no `{`**
  (`MissingOpeningBrace`); the header line is then skipped.
- `UnterminatedBlock` still yields the block (content to end of file), so downstream tooling has
  something to work with.
