# Assimalign.Vue.Syntax.Css — design

Why the CSS area is shaped the way it is, and its deliberate divergences from a full CSS engine. The
scaffold that preceded this (a raw whole-source root node pinning the pipeline seam) was replaced by real
rule-level parsing and the scoped-selector rewrite with scoped CSS **[V01.01.06.04]** (issue #60).

## Two-phase parse, context-directed, recoverable

Parsing is the classic two phases of [CSS Syntax Module Level 3](https://www.w3.org/TR/css-syntax-3/):

1. **`CssTokenizer`** scans the source into a `CssToken` stream (offsets only, no copied substrings). The
   token set is trimmed to what rule structure and scoping need — numeric variants collapse into one
   `Number` kind, and `url()`/unicode-range are not special-cased, because the parser keeps raw value
   slices rather than typed numerics.
2. **`CssParseEngine`** is a recursive-descent rule parser over the tokens. It is **context-directed**
   rather than a generic component-value tree: the top level and conditional-group at-rules
   (`@media`/`@supports`/`@container`/`@layer`) consume a *list of rules*; a qualified rule and
   declaration-only at-rules (`@font-face`/`@page`) consume a *list of declarations*; `@keyframes`
   consumes keyframe rules. This is exactly enough structure to make correct scoping decisions without a
   full CSS object model.

Both phases are **recoverable** per the spec's error handling: an unterminated block/string/comment, a
stray `}`, an empty selector, or a colon-less declaration reports a Vuecs-defined `CssError` (2000-based
catalog, `Severity.Error`, `RawCode` projection — following the single-file-component parser's
precedent) and the parser resynchronizes. It never throws; the only expected exception is
`OperationCanceledException`.

Every node upholds the base cluster's **exact-slice `SourceLocation` invariant**: `Location.Source ==
source.Substring(Start.Offset, End.Offset - Start.Offset)`, pinned recursively by
`CssSyntaxParserTests`. The tree is a record graph, so identical input yields an equal, equally-hashed
result — the incremental-generator caching contract.

## The flat selector model

A qualified rule's prelude is parsed (`CssSelectorParser`) into a `CssSelectorListNode` of
`CssComplexSelectorNode`s, each a **flat, source-ordered list** of `CssSelectorPartNode` parts —
simple selectors, pseudo selectors, and the combinators between compounds. This mirrors
`postcss-selector-parser`'s node list, which is the representation Vue's scoped plugin walks to pick the
compound that receives the `[data-v-hash]` attribute (the last part that is neither a combinator nor a
pseudo). Compounds are implicit (runs of adjacent simple/pseudo parts between combinators), which is all
the rewrite needs. The reserved functional pseudos `:deep()`/`:slotted()`/`:global()` have their inner
selector lists parsed recursively; every other functional pseudo (`:not(...)`, `:nth-child(...)`) keeps
its argument as verbatim text.

## The scoped transform

`CssScopedRewriter` is the pure-.NET port of `@vue/compiler-sfc`'s `pluginScoped.ts`, pinned against that
plugin's [test fixtures](https://github.com/vuejs/core/blob/main/packages/compiler-sfc/__tests__/compileStyle.spec.ts)
in `CssScopedRewriterTests`. Scope id `data-v-test` yields the attribute `[data-v-test]` (no value) and
the keyframes short id `test`:

| Input | Output |
| --- | --- |
| `.foo { }` | `.foo[data-v-test] { }` |
| `h1 .foo { }` | `h1 .foo[data-v-test] { }` (last compound only) |
| `h1 .foo, .bar { }` | `h1 .foo[data-v-test], .bar[data-v-test] { }` |
| `.foo:after { }` | `.foo[data-v-test]:after { }` |
| `::selection { }` | `[data-v-test]::selection { }` (prepended) |
| `:deep(.foo) { }` / `::v-deep(.foo)` | `[data-v-test] .foo { }` (inner unscoped) |
| `.a :deep(.foo) { }` | `.a[data-v-test] .foo { }` |
| `:slotted(.foo) { }` | `.foo[data-v-test-s] { }` (slotted suffix) |
| `:global(.foo) { }` | `.foo { }` (whole selector unscoped) |
| `@keyframes x` + `animation: x 5s` | `@keyframes x-test` + `animation: x-test 5s` |

Keyframe names are collected in a first pass (so an `animation`/`animation-name` reference resolves
regardless of source order, matching the plugin's `AtRule`-collect / `OnceExit`-rewrite split) and
suffixed with `-<shortId>`. Serialization is **deterministic** (canonical two-space indent,
`prop: value;`, `selector {`), so the incremental cache holds; the scoped output's whitespace is therefore
normalized rather than source-preserving (unscoped `@style` blocks pass through the generator verbatim, so
their formatting is never touched).

## The scope-id scheme

The scope id is `data-v-` + an FNV-1a hash (8 hex) of the component's **project-relative `.viu` path**
(normalized to forward slashes), computed by the generator's `StyleScopeId`. This mirrors Vue's dev-mode
scheme of hashing the short file path (`@vitejs/plugin-vue`): deterministic, stable across machines and
rebuilds (asset-caching contract), and unique per component file. A path-based id intentionally does not
change when only the file's *content* changes — the id identifies the component, not a revision; Vue's
production build additionally folds the source in for cache-busting, which is deferred with the
static-web-asset emission below. A linked file outside the project directory falls back to hashing its
leaf name so the id stays machine-independent.

## Composition boundary

The library never wires itself into the `.viu` parser. The [V01.01.06.02] generator composition root
(`SingleFileComponentParserComposition`) registers `CssSyntaxParser` against `@style` block sources on the
`AggregateSyntaxParserOptions` seam, dispatches CSS parse diagnostics through the style-origin envelope
(`VUECS130x`) composed onto `.viu` coordinates, runs the scoped rewrite for `scoped` blocks with the
component's scope id, and emits the scope id + extracted CSS as generated constants. Runtime projects see
only the scope-id string.

## Non-goals (deliberate scope boundaries)

- **CSS Modules and `v-bind()` in CSS** — [V01.01.06.06], not this item. The parser tree and kind catalog
  are built to host them; the seams are left, not implemented.
- **Physical static-web-asset bundling.** A Roslyn source generator emits C#, not content files (and
  `System.IO` is off-limits under RS1035), so the extracted CSS surfaces as the generated `ExtractedStyles`
  constant. Writing the CSS into `dotnet publish` output as a bundled stylesheet is an MSBuild-task
  follow-up on the [V01.01.06.02] pipeline side.
- **Legacy deep combinators** `>>>` and `/deep/` — deprecated upstream in favor of `:deep()`; not
  supported.
- **Deep inside `:is()`/`:where()`/`:not()`** — upstream's `splitSelectorForNestedDeep` and the
  `:is`/`:where` inner-recursion refinements are not ported; those functional pseudos are treated as
  opaque verbatim arguments. Straightforward `:deep()`/`:slotted()`/`:global()` and
  compound/complex/grouped scoping are covered.
- **Comment preservation in scoped output.** Comments are tokenized (for exact spans) but dropped by the
  canonical serializer; scoped CSS is machine-generated.
- **Tailwind / utility-class generation** — [#129] is a separate consumer of this parser. It reuses the
  tokenizer, tree, and (for its own scoping) `CssScopedRewriter`; it does not need changes here.
