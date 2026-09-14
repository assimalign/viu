# Assimalign.Viu.Syntax.Css — design

Why the CSS area is shaped the way it is, and its deliberate divergences from a full CSS engine. The
scaffold that preceded this (a raw whole-source root node pinning the pipeline seam) was replaced by real
rule-level parsing under **[V01.01.06.04]** (issue #60). Scoped CSS was removed on 2026-09-14
by owner decision **[V01.01.06.17]** ([#367](https://github.com/assimalign/viu/issues/367)); CSS
Modules and `v-bind()` in CSS were added with **[V01.01.06.06]** (issue #62), reusing the same tree and
serializer machinery.

## Two-phase parse, context-directed, recoverable

Parsing is the classic two phases of [CSS Syntax Module Level 3](https://www.w3.org/TR/css-syntax-3/):

1. **`CssTokenizer`** scans the source into a `CssToken` stream (offsets only, no copied substrings). The
   token set is trimmed to what rule structure and selector processing need — numeric variants collapse into one
   `Number` kind, and `url()`/unicode-range are not special-cased, because the parser keeps raw value
   slices rather than typed numerics.
2. **`CssParseEngine`** is a recursive-descent rule parser over the tokens. It is **context-directed**
   rather than a generic component-value tree: the top level and conditional-group at-rules
   (`@media`/`@supports`/`@container`/`@layer`) consume a *list of rules*; a qualified rule and
   declaration-only at-rules (`@font-face`/`@page`) consume a *list of declarations*; `@keyframes`
   consumes keyframe rules. This is exactly enough structure to rename module classes and rewrite CSS bindings without a
   full CSS object model.

Both phases are **recoverable** per the spec's error handling: an unterminated block/string/comment, a
stray `}`, an empty selector, or a colon-less declaration reports a Viu-defined `CssError` (2000-based
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
simple selectors, pseudo selectors, and the combinators between compounds. A flat list preserves
source order for module traversal and serialization. Recognized functional pseudos
`:deep()`/`:slotted()`/`:global()` retain parsed argument lists for syntax consumers; every other
functional pseudo keeps its argument as verbatim text. Their presence does not enable scoped CSS.

## Removed scoped transform

Scoped CSS was removed by owner decision on 2026-09-14 ([V01.01.06.17], #367). The selector
rewriter, keyframe renaming, scope identity, and element stamping are removed. Canonical `.viu`
scoped options report parser error 1018 (`VIU1001` in the generator); compatible `.vue` inputs
report parser warning 1019 (`VIU1002` in the generator) and retain the block as an ordinary global
stylesheet. CSS Modules remain the supported component-local naming
feature. The old [STY-1] id is non-normative removal history.

## Stable CSS name hashing

`CssComponentHash` in the shared compiler derives an eight-digit FNV-1a hash from the normalized
project-relative component path. A linked file outside the project falls back to its leaf name.
This salt is independent of content and stays stable across machines and rebuilds. Module class
and CSS binding names retain their existing hashes; the salt is never emitted as an element
attribute or selector scope condition.

## CSS Modules and `v-bind()` — the [V01.01.06.06] rewrites

Two tree-to-tree transforms are reached through the same composition boundary — the
composition-root generator runs them over the parsed tree, not the `.viu` parser wiring them in.

- **`CssModuleRewriter`** implements CSS Modules (`[STY-2]`). It renames every
  local class selector `.foo` to `.foo_<hash>` and returns the original → hashed map for the generated
  `$style` accessor. `<hash>` is the eight-hex-digit FNV-1a of `<componentPathHash>-foo` (`CssHash`, the same
  FNV-1a as `CssComponentHash`), so it is deterministic, stable across rebuilds, and unique per component. The
  rename touches only class selectors in normal compound position; class names inside functional-pseudo
  arguments (`:not()`, `:deep()`, `:slotted()`, `:global()`) are left alone. Recognized pseudo arguments
  preserve their authored names; other pseudo arguments remain verbatim text. This syntax retention
  does not implement the removed scoped-style selector rewrites.
- **`CssBindingRewriter`** implements `v-bind()` in CSS (`[STY-6]`). It scans each declaration value for
  `v-bind(expr)` lexically — skipping string literals and `/* */` comments and balancing nested parens —
  replaces each with `var(--<hash>)`, and collects the distinct `(hash, expression, location)` bindings for
  the deferred runtime application design. `<hash>` is the FNV-1a of `<componentPathHash>-<expr>`,
  preserving the custom-property names a future runtime application must supply. Each binding
  carries the block-relative source location of its expression so the composition root can map a
  compile diagnostic back onto the exact `.viu` coordinate. An unterminated `v-bind(` or an empty
  `v-bind()` reports a recoverable 2000-band `CssError` on the declaration and is left in place.

### The `v-bind()` expression rewriting path ([V01.01.06.06.01])

`CssBindingRewriter` extracts the expression **text**; the C# rewriting is the template compiler's job, so the
composition-root generator routes each extracted expression through
`Assimalign.Viu.Syntax.Templates`' `TemplateExpressionCompiler.CompileInstanceExpression` with the component's
binding metadata — the same binding-aware rewriting a render expression gets. This makes `v-bind(count)` unwrap
a script `Reference<T>` member to `count.Value` automatically, so a style block and a template read the same
member the same way, instead of forcing the author to write `v-bind(count.Value)` in one and `count` in the other. The getter runs as an **instance member** of the component
partial class when runtime application is implemented; the retained compilation uses instance-member mode: bindings read through the implicit
`this` (no `_ctx.`), a definite `Reference<T>` unwraps to `.Value`, and every other binding reads bare — the
generator only marks a binding a definite reference when its declared type is reactive, so no `unref` (and thus no
runtime-helper import) is needed in the getter. A **malformed** expression surfaces its `X_INVALID_EXPRESSION`
diagnostic on the exact `.viu` style coordinate through the same style-origin envelope (`VIU1301`) the CSS parse
diagnostics use; the recoverable original text still emits, so the reported error fails the build. Member
existence is left to the C# compiler (the same permissive choice the render path makes, so a member declared in a
hand-written sibling partial is not false-flagged). Runtime application remains deferred under [STY-6]–[STY-8]. The Browser `CssVariables` directive
remains available independently; it is not emitted automatically by the component compiler.

Both rewrites update the parsed selector parts and declaration values, and their results compose.
A rewritten block is serialized by **`CssStylesheetWriter`** in canonical two-space form. A plain
block with neither feature is emitted verbatim, so only rewritten blocks lose original whitespace.

## Programmatic construction — the [V01.01.12.11] surface

The parser turns CSS *text* into the record graph; `CssSyntaxFactory` (and the fluent
`CssStylesheetBuilder`) build the **same** graph from code, so a build-time generator can synthesize rules
from scratch and hand them to the same canonical serializer. The module and
`v-bind()` rewrites transform an *already-parsed* tree, so none of them ever needed to *create* a rule;
the standalone utility add-on ([V01.01.12.16]) was its first rule-generating consumer. The surface is
deliberately **language-agnostic generic CSS construction** — it knows nothing about utilities, variants, or
themes — so `Assimalign.Viu.Syntax.Css` stays a leaf in the cluster. The standalone engine at
`libraries/Utilities/Assimalign.Viu.UtilityCss` may reference Css; Css never references back, and future developer
tooling can consume the public construction surface independently. `CssSyntaxFactory.QualifiedRule`,
`Declaration`, `Media`/`ConditionalGroupAtRule`, `Stylesheet`, and the selector builders
(`SimpleSelector`/`Combinator`/`Pseudo`/`ComplexSelector`/`SelectorList`) mint the existing node types; a
constructed rule renders its `Prelude` from its parts (via the internal `CssSelectorWriter`) so the node is
self-consistent, though the serializers read the parts, never the prelude.

### The synthetic-location divergence

Every parser-produced node upholds the exact-slice `SourceLocation` invariant (`Location.Source ==
source.Substring(Start.Offset, End.Offset - Start.Offset)`), pinned by `CssSyntaxParserTests`. A
constructed node has **no source string**, so that invariant cannot and does not apply — it is scoped to
parser output, and construction is a distinct path. Rather than fabricate a span into a non-existent
source, a constructed node carries a **synthetic** location (`CssSyntheticLocation`): both ends are the
sentinel `Position` with `Offset == -1` (a value no real, zero-based parse position takes), and its `Source`
carries only the exact text the node contributes where the serializer reads text back — a pseudo's
`:hover`, a simple selector's `.foo` — and the empty string for the container and declaration nodes the
serializer composes from typed properties. `CssSyntheticLocation.IsSynthetic` classifies a node off the
sentinel offset, so the check is exact and total. This is the one deliberate, **tested** divergence from
the invariant (`CssConstructionTests` walks a constructed graph asserting every node is synthetic, and
re-asserts a parsed graph is *not*). Because the sentinel is constant and the carried `Source` is derived
deterministically from the node's inputs, synthetic locations preserve record value-equality: two graphs
built from equal inputs are equal and equally hashed (the incremental-cache contract), and a synthetic node
never compares equal to a parsed node with the same text, because their offsets differ (`-1` versus a real
position).

### Deterministic serialization order

The canonical serializer (`CssStylesheetWriter`) emits **in
exact graph order and never reorders**:

- **Property/declaration order within a rule** — declarations serialize in the order they appear in
  `CssQualifiedRuleNode.Declarations` (and `CssKeyframeRuleNode.Declarations`).
- **Rule and media order across a stylesheet** — top-level rules serialize in `CssStylesheetNode.Rules`
  order; rules nested in a conditional-group at-rule in `CssAtRuleNode.Body` order (recursively, so a
  nested `@media` inside a `@media` is faithful — pinned by `CssConstructionTests`).
- Grouped selectors serialize in `CssSelectorListNode.Selectors` order; a complex selector's parts in
  `CssComplexSelectorNode.Parts` order.

Determinism is therefore a property of **construction**: identical node lists serialize byte-for-byte
identically, and construction converges on the same canonical text as parsing the equivalent source
(pinned against a parsed graph in `CssConstructionTests`). The construction surface preserves the order it
is given and applies no canonicalization of its own — so a consumer that needs a canonical ordering (the
standalone utility add-on is the example) sorts **before**
constructing. Keeping ordering policy in the consumer is exactly what lets Css stay language-agnostic: it
holds no opinion on how utilities, media queries, or properties "should" be ordered.

## Composition boundary

The library never wires itself into the `.viu` parser. The [V01.01.06.02] generator composition root
(`SingleFileComponentParserComposition`) registers `CssSyntaxParser` against `@style` block sources on the
`AggregateSyntaxParserOptions` seam, dispatches CSS parse diagnostics through the style-origin envelope
(`VIU130x`) composed onto component coordinates, and delegates module/binding rewrites and
ordinary style extraction to the shared CSS compiler. Runtime projects consume generated module
accessors and extracted CSS metadata, without a scope identifier.

## Non-goals (deliberate scope boundaries)

- **Class renaming inside functional-pseudo arguments** for CSS Modules — `.foo` inside `:not()`,
  `:deep()`, `:slotted()`, `:global()` is not renamed (see the [V01.01.06.06] section). Arguments
  retain their authored names without enabling the removed scoped-style selector rewrites.
- **A readable debug spelling for the `v-bind` custom property** — an `id-<escaped-expr>` form would be
  friendlier in dev tools, but Viu always uses the deterministic
  component-specific hash (`--<hash>`), which is unambiguous and needs no escaping, and records the readable
  expression in metadata instead.
- **Physical static-web-asset bundling.** A Roslyn source generator emits C#, not content files (and
  `System.IO` is off-limits under RS1035), so the extracted CSS surfaces as the generated `ExtractedStyles`
  constant. Writing the CSS into `dotnet publish` output as a bundled stylesheet was deferred to an
  MSBuild-task follow-up on the [V01.01.06.02] pipeline side — **now landed as [V01.01.12.12]**: the
  `ViuBundleCss` MSBuild task (`sdks/Assimalign.Viu.Sdk/Tasks`) re-runs the *same* deterministic
  `@style` compilation — lifted into the shared `Assimalign.Viu.Compiler.Css` core, which the generator now
  delegates to — over the same `.viu` inputs, so the physical bundle is byte-identical to
  `ExtractedStyles`. The base SDK packs the resulting `.viu.css` and a `buildTransitive`
  registration for component libraries; the Browser SDK registers application and transitive
  library styles as static web assets. See the
  [`Assimalign.Viu.Compiler.Css` design](../../../../tooling/Compiler/Assimalign.Viu.Compiler.Css/docs/DESIGN.md)
  and the active [component-CSS delivery contract](../../../../sdks/Assimalign.Viu.Sdk.Browser/docs/CSS-DELIVERY.md).
- **Scoped CSS and deep/slotted selector rewriting.** Removed by [V01.01.06.17]; no restoration
  is planned. Parsed pseudo syntax is retained for source preservation and CSS Modules traversal.
- **Comment preservation in rewritten output.** Comments are tokenized for exact spans but dropped
  by the canonical serializer. Ordinary component styles retain their source text.
- **Standalone add-on history — utility-class generation.** [#129] introduced a separate consumer of this
  library that reused the tokenizer, tree, and the programmatic construction surface
  added by [V01.01.12.11]. Its Viu integration was removed on 2026-08-13; the engine is now independently
  published from `libraries/Utilities/Assimalign.Viu.UtilityCss`. Tailwind CSS v4.3.3 compatibility
  belongs only to that standalone add-on. The retained, non-normative design history is
  [`docs/UTILITY-CSS-DESIGN.md`](../../../../docs/UTILITY-CSS-DESIGN.md).
- **Reserved functional pseudos, `@keyframes`, and statement at-rules are not *constructed*.** The
  [V01.01.12.11] factory builds ordinary selectors, declarations, qualified rules, and conditional-group
  at-rules — enough for a rule-generating add-on. `:deep()`/`:slotted()`/`:global()` are parsed syntax, not built from code; `@keyframes`/keyframe-rule and
  `;`-terminated statement at-rules have no construction entry point yet. All are additive if a later
  consumer needs them, and the parser still produces every one of them.
