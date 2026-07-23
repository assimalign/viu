# Assimalign.Viu.Syntax.Css — design

Why the CSS area is shaped the way it is, and its deliberate divergences from a full CSS engine. The
scaffold that preceded this (a raw whole-source root node pinning the pipeline seam) was replaced by real
rule-level parsing and the scoped-selector rewrite with scoped CSS **[V01.01.06.04]** (issue #60); CSS
Modules and `v-bind()` in CSS were added with **[V01.01.06.06]** (issue #62), reusing the same tree and
serializer machinery.

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

## CSS Modules and `v-bind()` — the [V01.01.06.06] rewrites

Two more transforms sit alongside `CssScopedRewriter`, each a pure-.NET port of a `@vue/compiler-sfc`
stage and each reached the same way — the composition-root generator runs them over the parsed tree, not
the `.viu` parser wiring them in.

- **`CssModuleRewriter`** ports `compileStyle()`'s CSS-Modules mode (`postcss-modules`). It renames every
  local class selector `.foo` to `.foo_<hash>` and returns the original → hashed map for the generated
  `$style` accessor. `<hash>` is the eight-hex-digit FNV-1a of `<shortScopeId>-foo` (`CssHash`, the same
  FNV-1a as `StyleScopeId`), so it is deterministic, stable across rebuilds, and unique per component. The
  rename touches only class selectors in normal compound position; class names inside functional-pseudo
  arguments (`:not()`, `:deep()`, `:slotted()`, `:global()`) are left alone — `:deep`/`:global`
  deliberately target external/un-hashed names, and non-reserved pseudo arguments are verbatim text (the
  parser non-goal below).
- **`CssBindingRewriter`** ports `cssVars.ts`. It scans each declaration value for `v-bind(expr)` (a port
  of upstream's `lexBinding`: it skips string literals and `/* */` comments and balances nested parens),
  replaces each with `var(--<hash>)`, and collects the distinct `(hash, expression, location)` bindings for
  the `UseCssVars` runtime. `<hash>` is the FNV-1a of `<shortScopeId>-<expr>`, so the emitted CSS
  `var(--<hash>)` and the runtime's `style.setProperty("--<hash>", …)` agree by construction. Each binding
  carries the block-relative source location of its expression so the composition root can map a
  compile diagnostic back onto the exact `.viu` coordinate. An unterminated `v-bind(` or an empty
  `v-bind()` reports a recoverable 2000-band `CssError` on the declaration and is left in place.

### The `v-bind()` expression rewriting path ([V01.01.06.06.01])

`CssBindingRewriter` extracts the expression **text**; the C# rewriting is the template compiler's job, so the
composition-root generator routes each extracted expression through
`Assimalign.Viu.Syntax.Templates`' `TemplateExpressionCompiler.CompileInstanceExpression` with the component's
binding metadata — the same binding-aware rewriting a render expression gets. This makes `v-bind(count)` unwrap
a script `Reference<T>` member to `count.Value` automatically (matching upstream cssVars ergonomics), instead of
forcing the author to write `v-bind(count.Value)`. The getter runs as an **instance member** of the component
partial class (`ApplyCssVariables`), so the rewriting is instance-member mode: bindings read through the implicit
`this` (no `_ctx.`), a definite `Reference<T>` unwraps to `.Value`, and every other binding reads bare — the
generator only marks a binding a definite reference when its declared type is reactive, so no `unref` (and thus no
runtime-helper import) is needed in the getter. A **malformed** expression surfaces its `X_INVALID_EXPRESSION`
diagnostic on the exact `.viu` style coordinate through the same style-origin envelope (`VIU1301`) the CSS parse
diagnostics use; the recoverable original text still emits, so the reported error fails the build. Member
existence is left to the C# compiler (the same permissive choice the render path makes, so a member declared in a
hand-written sibling partial is not false-flagged). The runtime half is `UseCssVars` in `Assimalign.Viu.Browser`.

Both rewrites are tree-to-tree, so they compose with `scoped` in any order — the scoped serializer reads the
parsed selector parts and declaration values the rewrites already updated. A block that is `module`/`v-bind`
but **not** `scoped` is serialized by **`CssStylesheetWriter`**, the plain (unscoped) sibling of
`CssScopedRewriter` sharing its canonical two-space form; a non-scoped block with neither feature is still
emitted verbatim and never reaches the writer, so only rewritten blocks lose their original whitespace.

## Programmatic construction — the [V01.01.12.11] surface

The parser turns CSS *text* into the record graph; `CssSyntaxFactory` (and the fluent
`CssStylesheetBuilder`) build the **same** graph from code, so a build-time generator can synthesize rules
from scratch and hand them to the same canonical serializer. Scoped CSS ([V01.01.06.04]) and the module /
`v-bind()` rewrites all transform an *already-parsed* tree, so none of them ever needed to *create* a rule;
the utility-first CSS engine ([V01.01.12.16]) generates rules from scratch and does. The surface is
deliberately **language-agnostic generic CSS construction** — it knows nothing about utilities, variants, or
themes — so `Assimalign.Viu.Syntax.Css` stays a leaf in the cluster (the utility grammar/theme/resolver live
in Tooling, which may reference Css; Css never references back). `CssSyntaxFactory.QualifiedRule`,
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

The canonical serializer (`CssStylesheetWriter`, and `CssScopedRewriter` for scoped output) emits **in
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
is given and applies no canonicalization of its own — so a consumer that needs a canonical ordering (e.g.
the utility engine de-duplicating and ordering utilities for a byte-stable bundle) sorts **before**
constructing. Keeping ordering policy in the consumer is exactly what lets Css stay language-agnostic: it
holds no opinion on how utilities, media queries, or properties "should" be ordered.

## Composition boundary

The library never wires itself into the `.viu` parser. The [V01.01.06.02] generator composition root
(`SingleFileComponentParserComposition`) registers `CssSyntaxParser` against `@style` block sources on the
`AggregateSyntaxParserOptions` seam, dispatches CSS parse diagnostics through the style-origin envelope
(`VIU130x`) composed onto `.viu` coordinates, runs the scoped rewrite for `scoped` blocks with the
component's scope id, and emits the scope id + extracted CSS as generated constants. Runtime projects see
only the scope-id string.

## Non-goals (deliberate scope boundaries)

- **Class renaming inside functional-pseudo arguments** for CSS Modules — `.foo` inside `:not()`,
  `:deep()`, `:slotted()`, `:global()` is not renamed (see the [V01.01.06.06] section). This follows the
  verbatim-pseudo-argument non-goal below and, for `:deep`/`:global`, is the correct behavior (those target
  external names).
- **The production `hash(id + raw)` prod-mode `v-bind` name** — upstream's dev build uses a readable
  `id-<escaped-expr>` custom property and its prod build a bare hash; Viu always uses the deterministic
  component-scoped hash (`--<hash>`), which is unambiguous and needs no escaping, and records the readable
  expression in metadata instead.
- **Physical static-web-asset bundling.** A Roslyn source generator emits C#, not content files (and
  `System.IO` is off-limits under RS1035), so the extracted CSS surfaces as the generated `ExtractedStyles`
  constant. Writing the CSS into `dotnet publish` output as a bundled stylesheet was deferred to an
  MSBuild-task follow-up on the [V01.01.06.02] pipeline side — **now landed as [V01.01.12.12]**: the
  `ViuBundleCss` MSBuild task (`analyzers/Assimalign.Viu.Tooling.Tasks`) re-runs the *same* deterministic
  `@style` compilation — lifted into the shared `Assimalign.Viu.Tooling.Css` core, which the generator now
  delegates to — over the same `.viu` inputs, so the physical bundle is byte-identical to `ExtractedStyles`;
  it writes the bundle under `obj/…/viu/` and registers it as a `StaticWebAsset`. See that library's
  `docs/DESIGN.md` and `docs/UTILITY-CSS-DESIGN.md` §2.4.
- **Legacy deep combinators** `>>>` and `/deep/` — deprecated upstream in favor of `:deep()`; not
  supported.
- **Deep inside `:is()`/`:where()`/`:not()`** — upstream's `splitSelectorForNestedDeep` and the
  `:is`/`:where` inner-recursion refinements are not ported; those functional pseudos are treated as
  opaque verbatim arguments. Straightforward `:deep()`/`:slotted()`/`:global()` and
  compound/complex/grouped scoping are covered.
- **Comment preservation in scoped output.** Comments are tokenized (for exact spans) but dropped by the
  canonical serializer; scoped CSS is machine-generated.
- **Tailwind / utility-class generation** — [#129] is a separate consumer of this library. It reuses the
  tokenizer, tree, (for its own scoping) `CssScopedRewriter`, and the programmatic construction surface
  added by [V01.01.12.11]; the utility grammar, theme, and resolver stay in Tooling, never here.
- **Reserved functional pseudos, `@keyframes`, and statement at-rules are not *constructed*.** The
  [V01.01.12.11] factory builds ordinary selectors, declarations, qualified rules, and conditional-group
  at-rules — what a utility generator needs. `:deep()`/`:slotted()`/`:global()` are *consumed* by the
  scoped rewrite, not built from code (utilities emit unscoped global CSS); `@keyframes`/keyframe-rule and
  `;`-terminated statement at-rules have no construction entry point yet. All are additive if a later
  consumer needs them, and the parser still produces every one of them.
