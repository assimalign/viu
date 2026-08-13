# The `.viu` single-file component format

`Assimalign.Viu.Syntax.SingleFileComponent` defines and parses the `.viu` single-file component (SFC) format — the Viu
counterpart to Vue's `.vue` files. This document is the authoritative specification for the container
syntax and the block parser (`SingleFileComponentParser.Parse`). It matches the parser's behavior exactly; the test
suite (`tooling/Assimalign.Viu.Syntax.SingleFileComponent/test/`) pins every rule stated here.

Work items: **[V01.01.06.01]** (original @-block format), **[V01.01.06.10]** (hybrid-container pivot,
#257). Block semantics mirror the Vue SFC specification (<https://vuejs.org/api/sfc-spec.html>);
descriptor shape mirrors `parse()` in `@vue/compiler-sfc` (vuejs/core,
`packages/compiler-sfc/src/parse.ts`).

## 1. The hybrid container

A `.viu` file uses a **hybrid container**: the markup and CSS blocks are **tag-based** — `<template>`
and `<style>`, exactly as in Vue — while the component's C# lives in an **`@script { }` block**, and
custom blocks keep the `@`-form.

> **Decision record.** The original [V01.01.06.01] design (2026-07-17) made the `@`-block container
> the canonical form for *every* block. This was **partially reversed on 2026-08-02 per user
> direction ([V01.01.06.10], #257)**: `<template>`/`<style>` tags are canonical again (matching Vue),
> `@script { }` is kept (a C# block gains nothing from an HTML wrapper and the `@` reads as "C#
> starts here", as in Razor), and custom blocks stay `@`-syntax. The legacy `@template`/`@style`
> containers still parse during a migration window with a Warning-severity diagnostic (§6.1). A
> top-level `<script>` tag is rejected (§6.2). Docs that recorded the 2026-07-17 decision
> (`docs/PLAN.md` founding decision 3, ADR-0005, this file, `DESIGN.md`) note the reversal.

```
<template>
    <!-- Vue template markup: directives, {{ }} interpolation -->
    <div>{{ message }}</div>
</template>

@script {
    // C# — the component's partial-class body
    public string Message = "Hello";
}

<style scoped>
    /* CSS */
    .box { color: red; }
</style>
```

Only the **container** differs from Vue (and only for script/custom blocks). The block *semantics* —
what `template`/`script`/`style`/custom blocks mean, and what their options mean — follow the Vue SFC
spec unchanged. In particular, the markup inside `<template>` is **standard Vue template syntax**; the
block parser does not parse it. It only slices the file into blocks and records their source spans.
The template markup is parsed later by the template compiler (`Assimalign.Viu.Syntax.Templates`,
[V01.01.05.01]); the C# in `@script` is analysed by [V01.01.06.03].

A `.viu` with a template block compiles to a **mountable component**: the source generator emits the
compiled render function ([V01.01.05.05]), merges the `@script` C# into the partial class ([V01.01.06.03]),
and — as of [V01.01.06.07] — generates the `IComponent` implementation (registration plus a `Setup` that
allocates the render cache, wires slots, applies any `v-bind()` CSS custom properties, and returns the
render delegate). A template-bearing `.viu` is registered with the application-selected
`IComponentFactory` and requested through `ComponentTree.Template<TComponent>()`, including when
that request is assigned to `ApplicationOptions.RootComponent` through
`BrowserApplicationBuilder.ConfigureApplication`; reactive `@script`
members then drive re-rendering with no hand-written component bridge. A `.viu` with **no**
template block (a `<style>`-only CSS-bundle unit, or a `@script`-only partial) stays a plain partial
class — no component bridge — so it keeps compiling exactly as before. This library still only
*slices*; the bridge is emitted by the generator that consumes the descriptor.

### Upstream-semantics mapping

| `.viu`                           | Vue SFC                          | Meaning (per the Vue SFC spec)                    |
| -------------------------------- | -------------------------------- | ------------------------------------------------- |
| `<template> … </template>`       | `<template> … </template>`       | The component's markup.                            |
| `<template lang="html">`         | `<template lang="html">`         | Markup pre-processor language.                     |
| `@script { … }`                  | `<script> … </script>`           | The component's script body (**C#**; §6.2).        |
| `@script lang="csharp" { … }`    | `<script lang="…">`              | Script language.                                   |
| `<style> … </style>`             | `<style> … </style>`             | Component CSS (a file may have several).           |
| `<style scoped>`                 | `<style scoped>`                 | [Scoped CSS](https://vuejs.org/api/sfc-css-features.html#scoped-css). |
| `<style module>`                 | `<style module>`                 | [CSS Modules](https://vuejs.org/api/sfc-css-features.html#css-modules) (default name). |
| `<style module="classes">`       | `<style module="classes">`       | CSS Modules bound to a named object.              |
| `<style lang="scss">`            | `<style lang="scss">`            | CSS pre-processor language.                        |
| `@docs { … }` (any other name)   | `<docs>` (custom block)          | [Custom block](https://vuejs.org/api/sfc-spec.html#custom-blocks), preserved verbatim. |

> The Vue `<script setup>` distinction is script *analysis*, deferred to [V01.01.06.03]. This parser
> treats every `@script` block uniformly and allows at most one per file — `.viu` has **no**
> `ScriptSetup` slot (unlike the `.vue` compatibility descriptor).

## 2. Top-level structure

Top-level dispatch is line-oriented and **column 0 is structural** for *opening* a block:

- A line whose first column is `@` opens an **@-block** (§3): `@script`, a custom block, or a legacy
  `@template`/`@style` (§6.1).
- A line whose first column is `<` opens a **tag construct** (§4): a `<template>`/`<style>` block, an
  HTML comment, the diagnosed `<script>` tag (§6.2), or a stray/malformed tag.
- Blank (empty or whitespace-only) lines at the top level are ignored; they may separate blocks.
- Any other non-blank top-level line — including an *indented* `@` or `<` header — is reported as
  `StrayTopLevelContent` and skipped.

An HTML comment `<!-- … -->` starting at column 0 is tolerated **between** blocks (matching the
`.vue` container) and may span multiple lines; parsing resumes after `-->`.

When a tag construct closes mid-line, the remainder of that line must be blank; a non-whitespace
remainder is reported as `StrayTopLevelContent` (nothing can open a block mid-line, because opening is
column-0 structural).

## 3. @-blocks — `@script` and custom blocks

A block is introduced by a **header line**:

```
@<name> <options>? {
```

- The header line's **first column must be `@`** (§2). A header that is indented is not recognised.
- `<name>` immediately follows the `@` and matches `[A-Za-z_][A-Za-z0-9_-]*` (a letter or `_`, then
  letters, digits, `_`, or `-`).
- The well-known name `script` is matched **case-sensitively and in lowercase** (as are the legacy
  `template`/`style`, §6.1). Any other name (including a different casing such as `@Script`) is a
  **custom block**.
- The opening `{` must be the **last non-whitespace character on the header line**. Content begins on
  the next line.

### 3.1 @-options

Options are written between the name and the `{`, separated by whitespace:

```
@<name> option option="value" {
```

- An option is either a **valueless flag** or a **key with a double-quoted value** (`lang="csharp"`).
- An option name matches `[A-Za-z_][A-Za-z0-9_-]*`.
- A value is written as `name="value"` with **no whitespace around `=`**. The value is any run of
  characters except `"` — values are simple tokens (language names, identifiers); there is no escape
  syntax, so a value cannot itself contain a double quote. A malformed value reports
  `MalformedOptionValue` (1005).

### 3.2 The @-termination rule — column 0 is structural

> **An @-block opened by a header line closes at the first later line whose first column is `}`.**

- **Inside an @-block**, a line whose first column is `}` closes the block. Nothing else is examined —
  the parser scans line starts only. Any characters after the `}` on that line are ignored, so
  `} // closes the block` is a valid closer; the block's whole-span `Location` ends immediately after
  the `}`.
- Every other line is **content**. Because the parser only inspects the first column, unbalanced or
  literal braces *inside* content never terminate a block, as long as content is indented:

```
@script {
    var json = "{ \"a\": 1 }";   // literal { and } inside a C# string — fine
    var closing = "}";           // the } is not at column 0 — fine
}
```

The immediate consequence is the one requirement the @-form places on authors: **@-block content must
be indented** — no content line may begin at column 0 with `}`. C# whose own `}` sits at column 0
closes the block early, and the real closing `}` then becomes `StrayTopLevelContent`. (Tag blocks have
no such requirement — §4.2.)

An @-block that is never closed (end of file with no column-0 `}`) reports `UnterminatedBlock` (1008);
for recovery its content is taken to end of file and the block still appears in the descriptor.

## 4. Tag blocks — `<template>` and `<style>`

A tag block is introduced by an **opening tag whose `<` is at column 0** (§2):

```
<template lang="html">
<style scoped>
```

- The tag name matches the HTML tag-name grammar (a letter, then letters, digits, `-`, `_`, `:`, `.`)
  and the two block names `template`/`style` are matched **case-sensitively and in lowercase**.
- The opening tag may span multiple lines and is parsed with the **HTML attribute grammar** (§5).
- A **self-closing** tag (`<template />`, `<style scoped />`) is an empty block.
- A top-level `<script …>` tag is **not** a block — it is diagnosed and discarded (§6.2).
- Any **other** tag name at the top level (e.g. `<docs>`) is **not** a custom block — custom blocks
  stay @-syntax. It reports `StrayTopLevelContent` at the opening tag; recovery skips the whole
  element to its matching closing tag when one exists (so a multi-line element reports one
  diagnostic, not one per line), else moves to the next line.
- A top-level closing tag with no open block reports `UnexpectedClosingTag` (1011); a malformed tag
  reports `MalformedTagBlock` (1009) or `MalformedTagAttribute` (1010) and recovery skips to the next
  line boundary.

### 4.1 Tag content boundaries (the `.vue` rules)

The closing boundary is exactly Vue 3.5's SFC tokenizer rule, shared with the `.vue` compatibility
parser:

- An **HTML template** (`<template>` with no `lang`, or `lang="html"`) closes at its matching
  `</template>` found with a lightweight nested-markup scan: end-tag text inside quoted attributes,
  `<!-- -->` comments, `<!`/`<?` declarations, and nested **raw-text elements**
  (`script`/`style`/`textarea`/`title`) cannot close the root template, and nested `<template>`
  elements nest correctly. Void elements never push the stack.
- A **preprocessed template** (`lang` ≠ `html`) and every **`<style>`** block are **raw text**: the
  block runs to the first matching closing tag, and nothing inside is interpreted — CSS braces,
  tag-shaped strings, and even a column-0 `@script {` line inside a template are all content.
- A tag block that never closes reports `UnterminatedTagBlock` (1012) at its opening tag; for
  recovery its content is taken to end of file and the block still appears in the descriptor.

### 4.2 Tag blocks close anywhere

The closing tag does **not** need to be at column 0 — tag blocks close at their matching end tag
anywhere, including on the opening line (`<template><div/></template>`) or indented. The column-0
structural rule (§3.2) applies **only to @-blocks**. Tag-block content therefore needs no
indentation discipline: CSS written flush against column 0 inside `<style>` is fine.

## 5. Option and attribute grammar per container

Both grammars surface the same `SingleFileComponentBlockOption` records — the block-option ↔
tag-attribute mapping is the identity mapping; only the header grammar differs.

| Container | Grammar | Surfaced as |
|---|---|---|
| `<template lang="html">`, `<style scoped>`, `<style module="classes">`, `<style lang="scss">` | HTML attribute grammar (the `.vue` one): values quoted with `"` or `'`, or unquoted; whitespace allowed around `=`; a repeated attribute reports `DuplicateTagAttribute` (1013) | identical `SingleFileComponentBlockOption` records → `Lang`, `Scoped`, `IsModule`, `ModuleName` |
| `@script`, custom `@name`, legacy `@template`/`@style` | @-option grammar (§3.1): double-quoted values only, no whitespace around `=`; malformed values report `MalformedOptionValue` (1005) | unchanged — the same records |

Honored (typed) options, per the Vue SFC spec:

| Option              | Blocks                    | Surfaced as                                        |
| ------------------- | ------------------------- | -------------------------------------------------- |
| `scoped`            | `<style>`                 | `SingleFileComponentStyleBlock.Scoped` (`bool`)                    |
| `module`            | `<style>`                 | `SingleFileComponentStyleBlock.IsModule` (`bool`)                  |
| `module="name"`     | `<style>`                 | `SingleFileComponentStyleBlock.IsModule` + `SingleFileComponentStyleBlock.ModuleName` |
| `lang="…"`          | `<style>`/`@script`/`<template>` (and custom) | `SingleFileComponentBlock.Lang` (`string?`)      |

All other options remain available through `SingleFileComponentBlock.HasOption(name)` and
`SingleFileComponentBlock.GetOptionValue(name)`. Options are preserved on every block, in source
order, each with its own source span; unknown options and any options on custom blocks are preserved
rather than rejected.

## 6. Migration rules

### 6.1 The legacy `@template`/`@style` transition window

The pre-pivot `@template { }` and `@style … { }` containers **still parse** — same slicing, same
@-option grammar, same descriptor blocks — but each header reports a **Warning-severity** migration
diagnostic:

| Legacy header        | Diagnostic                       | Severity |
| -------------------- | -------------------------------- | -------- |
| `@template … {`      | `LegacyTemplateBlockSyntax` 1015 | Warning  |
| `@style … {`         | `LegacyStyleBlockSyntax` 1016    | Warning  |

Rewrite `@template { … }` as `<template>…</template>` and `@style scoped { … }` as
`<style scoped>…</style>`; block options become tag attributes unchanged. The window is temporary —
the legacy forms will be removed. `@script` and custom blocks are canonical @-forms and report
nothing.

Because the legacy blocks still slice, the duplicate rules apply **across syntaxes**: a `<template>`
and a legacy `@template` in one file collide on `DuplicateTemplateBlock` (1006) — the first wins,
whichever container it uses.

### 6.2 The `<script>` tag is rejected

A `.viu` component's C# lives in `@script { }` **only**. A top-level `<script …>` tag — including
`<script setup …>` and a self-closing `<script />` — reports `ScriptTagBlockNotSupported` (1017,
**Error**) at its opening tag and **contributes no block**: its content is sliced past (to the
matching `</script>`, or end of file) and never reaches compilation. This exists so tag-era muscle
memory fails loudly instead of silently shipping a script block that never executes.

## 7. Block content and the descriptor

A block's content is the **exact raw source** inside its container:

- **@-blocks**: from the first character of the line *after* the header line to the first character
  of the closing-brace line.
- **Tag blocks**: from just past the opening tag's `>` to the `<` of the matching closing tag — so
  content may begin on the opening line itself (inline content), and a block whose opening tag ends
  at a line break starts its content with that newline. These are the `.vue` semantics.
- Content is never re-parsed, trimmed, or normalised. Interior indentation and newlines are preserved
  verbatim. An empty body (or a self-closing tag) yields an empty string.

`SingleFileComponentBlock.Content` equals `SingleFileComponentBlock.ContentLocation.Source`, and every span the parser emits satisfies
`Location.Source == source.Substring(Start.Offset, End.Offset - Start.Offset)`. Positions carry a
zero-based `Offset`, a one-based `Line`, and a one-based `Column` — suitable for `#line` mapping
([V01.01.06.03]) and IDE diagnostics. A block's whole-span `Location` covers the entire container:
`@name {` through `}` for @-blocks, `<name …>` through `</name>` for tag blocks.

`SingleFileComponentParser.Parse(string source)` returns an `SingleFileComponentParseResult` — an `SingleFileComponentDescriptor` plus the diagnostics.
Both the descriptor and the blocks are **immutable records with structural (value) equality**: parsing
identical file content twice yields equal (and equal-hashing) descriptors, and any content or location
difference makes them unequal. This is the prerequisite for incremental-generator caching
([V01.01.06.02]).

`SingleFileComponentDescriptor` exposes, mirroring Vue's `SFCDescriptor`:

- `Template` — the single template block (`<template>` or legacy `@template`), or `null`.
- `Script` — the single `@script` block, or `null`.
- `Styles` — the style blocks (`<style>` or legacy `@style`), in source order (Vue allows several).
- `CustomBlocks` — all @-form custom blocks (e.g. `@docs`), in source order.
- `Source` — the full original file text.

A file may contain **at most one** template and **at most one** script block, regardless of container
syntax; a second of either is reported (`DuplicateTemplateBlock` / `DuplicateScriptBlock`) and
ignored — the **first** is kept.

## 8. Diagnostics

Parsing is **recoverable**: malformed input is reported through `SingleFileComponentParseResult.Errors` and the parser
never throws for bad content (a `null` source argument throws `ArgumentNullException` — that is API
misuse, not input). Multiple problems are reported in a single pass, each with a code, a message, a
catalog severity, and a source location. Since [V01.01.06.10], `Errors` carries **all severities** —
the legacy-container diagnostics (1015/1016) are warnings; check `Severity` before treating an entry
as fatal.

The diagnostic codes (`SingleFileComponentErrorCode`) are **Viu-defined**. Unlike `Assimalign.Viu.Syntax.Templates`'s
`CompilerErrorCode`, which mirrors vuejs/core's numbering, the `.viu` container is a Viu divergence
and has no upstream vuejs/core codes to align to. Values start at 1000 to stay visibly distinct from
any upstream-aligned catalog. The tag codes 1009–1013 were minted for the `.vue` compatibility parser
([V01.01.06.09]) and are now also reachable from `.viu` tag blocks; 1014 remains `.vue`-only (`.viu`
has no setup slot).

| Code                          | Value | Severity | Raised when                                                              |
| ----------------------------- | ----- | -------- | ------------------------------------------------------------------------ |
| `StrayTopLevelContent`        | 1001  | Error    | Non-whitespace at the top level outside any block — including unknown top-level tags (§4) and non-blank remainders after a mid-line tag close (§2). |
| `MalformedBlockHeader`        | 1002  | Error    | A top-level line begins with `@` but no valid block name follows.       |
| `MissingOpeningBrace`         | 1003  | Error    | An @-header names a block but has no opening `{` on its line.           |
| `ContentAfterOpeningBrace`    | 1004  | Error    | Non-whitespace follows the opening `{` on an @-header line.             |
| `MalformedOptionValue`        | 1005  | Error    | An @-option value is not a well-formed double-quoted string.            |
| `DuplicateTemplateBlock`      | 1006  | Error    | A file declares more than one template block (either container syntax). |
| `DuplicateScriptBlock`        | 1007  | Error    | A file declares more than one `@script`.                                |
| `UnterminatedBlock`           | 1008  | Error    | An @-block is opened but end of file is reached with no column-0 `}`.   |
| `MalformedTagBlock`           | 1009  | Error    | A top-level tag is not a valid opening tag (also: an unterminated top-level comment). |
| `MalformedTagAttribute`       | 1010  | Error    | An attribute on a top-level tag is malformed.                           |
| `UnexpectedClosingTag`        | 1011  | Error    | A top-level closing tag has no corresponding open block.                |
| `UnterminatedTagBlock`        | 1012  | Error    | A tag block reaches end of file without its matching closing tag.       |
| `DuplicateTagAttribute`       | 1013  | Error    | A top-level tag declares the same attribute more than once.             |
| `DuplicateScriptSetupBlock`   | 1014  | Error    | (`.vue` only) more than one `<script setup>` block.                     |
| `LegacyTemplateBlockSyntax`   | 1015  | **Warning** | A legacy `@template { }` container parsed (§6.1).                    |
| `LegacyStyleBlockSyntax`      | 1016  | **Warning** | A legacy `@style … { }` container parsed (§6.1).                     |
| `ScriptTagBlockNotSupported`  | 1017  | Error    | A top-level `<script>` tag appeared in a `.viu` file (§6.2).            |

### Recovery policy

- A structurally openable @-header (`@<name> … {`) **always opens the block**; option problems
  (`MalformedOptionValue`) and trailing content (`ContentAfterOpeningBrace`) are reported but do not
  suppress the block, so its content is still sliced. An @-header only fails to open when it has
  **no valid name** (`MalformedBlockHeader`) or **no `{`** (`MissingOpeningBrace`); the header line is
  then skipped.
- A well-formed opening tag **always opens its block** (`DuplicateTagAttribute` is reported but does
  not suppress it); a malformed tag (1009/1010) fails to open and recovery skips to the next line.
- `UnterminatedBlock` and `UnterminatedTagBlock` still yield the block (content to end of file), so
  downstream tooling has something to work with.
- The legacy warnings (1015/1016) never suppress their blocks; `ScriptTagBlockNotSupported` (1017)
  always suppresses its would-be block.
