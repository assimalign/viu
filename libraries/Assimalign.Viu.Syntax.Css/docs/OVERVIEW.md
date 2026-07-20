# Assimalign.Viu.Syntax.Css — overview

The build-time CSS language area of the `Assimalign.Viu.Syntax.*` cluster: it tokenizes and rule-parses
CSS per [CSS Syntax Module Level 3](https://www.w3.org/TR/css-syntax-3/) into a located,
value-equatable tree, rewrites that tree into attribute-scoped CSS — the pure-.NET port of Vue 3.5's
scoped-CSS feature (`@vue/compiler-sfc` `compileStyle()` with the scoped PostCSS plugin) — and
**constructs the same tree programmatically** so a build-time generator can emit CSS from scratch. It is
reached through the aggregate-parser registration seam: the [V01.01.06.02] generator composition root
registers `CssSyntaxParser` for `.viu` `@style` blocks, and runs `CssScopedRewriter` for the `scoped`
ones. See [the Vue scoped-CSS docs](https://vuejs.org/api/sfc-css-features.html).

## Public surface

- **`CssSyntaxParser`** — the `SyntaxParser<CssSyntaxNode>` the composition root registers for `@style`
  blocks and `.css` sources. Recoverable: malformed input reports a `CssError` and never throws.
- **The node tree** — `CssStylesheetNode` → `CssQualifiedRuleNode` / `CssAtRuleNode` /
  `CssDeclarationNode` / `CssKeyframeRuleNode`, and the parsed selector model under `Selectors/`
  (`CssSelectorListNode` → `CssComplexSelectorNode` → the flat `CssSelectorPartNode` parts). Every node is
  an immutable record carrying an exact-slice `SourceLocation`.
- **`CssScopedRewriter.Rewrite(stylesheet, scopeId)`** — the scoped transform, returning deterministic
  scoped CSS text.
- **`CssStylesheetWriter.Write(stylesheet)`** — the plain (unscoped) canonical serializer, the emission
  path a generated stylesheet takes.
- **The construction surface** ([V01.01.12.11]) — `CssSyntaxFactory` builds the record-graph node types
  (declarations, qualified rules, `@media`/conditional-group at-rules, selectors) from code; the fluent
  `CssStylesheetBuilder` accumulates a stylesheet; `CssSyntheticLocation` marks and recognizes the
  synthetic `SourceLocation`s constructed nodes carry. Generic CSS construction only — it knows nothing of
  utilities. Consumed by the build-time utility-first CSS engine ([V01.01.12.16]).
- **`CssError` / `CssErrorCode`** — the Viu-defined recoverable-diagnostic catalog (2000-based).

## Boundary

Roots on `Assimalign.Viu.Syntax` only. It never references the `SingleFileComponent` parser or any other
language library; the composition root wires them together. Runtime projects never reference it — they see
only the scope-id string in generated component metadata. Targets the netstandard2.0 analyzer TFM because
it runs inside Roslyn generator hosts.

Rationale and deliberate divergences live in [DESIGN.md](DESIGN.md).
