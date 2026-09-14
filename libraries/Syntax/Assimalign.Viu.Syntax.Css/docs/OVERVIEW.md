# Assimalign.Viu.Syntax.Css — overview

The build-time CSS language area of the `Assimalign.Viu.Syntax.*` cluster: it tokenizes and rule-parses
CSS per [CSS Syntax Module Level 3](https://www.w3.org/TR/css-syntax-3/) into a located,
value-equatable tree, rewrites CSS Modules and compile-time `v-bind()` expressions, and
**constructs the same tree programmatically** so a build-time generator can emit CSS from scratch. It is
reached through the aggregate-parser registration seam: the [V01.01.06.02] generator composition root
registers `CssSyntaxParser` for `<style>` blocks in `.viu` and `.vue` inputs and legacy `@style`
blocks in `.viu`. Scoped CSS was removed on 2026-09-14 by [V01.01.06.17] (#367); use CSS Modules for local
class names.

## Public surface

- **`CssSyntaxParser`** — the `SyntaxParser<CssSyntaxNode>` the composition root registers for `@style`
  blocks and `.css` sources. Recoverable: malformed input reports a `CssError` and never throws.
- **The node tree** — `CssStylesheetNode` → `CssQualifiedRuleNode` / `CssAtRuleNode` /
  `CssDeclarationNode` / `CssKeyframeRuleNode`, and the parsed selector model under `Selectors/`
  (`CssSelectorListNode` → `CssComplexSelectorNode` → the flat `CssSelectorPartNode` parts). Every node is
  an immutable record carrying an exact-slice `SourceLocation`.
- **`CssModuleRewriter` / `CssBindingRewriter`** — deterministic class-name and custom-property rewrites.
- **`CssStylesheetWriter.Write(stylesheet)`** — the canonical serializer, the emission
  path a generated stylesheet takes.
- **The construction surface** ([V01.01.12.11]) — `CssSyntaxFactory` builds the record-graph node types
  (declarations, qualified rules, `@media`/conditional-group at-rules, selectors) from code; the fluent
  `CssStylesheetBuilder` accumulates a stylesheet; `CssSyntheticLocation` marks and recognizes the
  synthetic `SourceLocation`s constructed nodes carry. Generic CSS construction only — it knows nothing of
  utilities. The standalone utility add-on ([V01.01.12.16]) is a consumer; the surface is
  also available to developer-authored CSS tooling without taking a dependency on that add-on.
- **`CssError` / `CssErrorCode`** — the Viu-defined recoverable-diagnostic catalog (2000-based).

## Boundary

Roots on `Assimalign.Viu.Syntax` only. It never references the `SingleFileComponent` parser or any other
language library; the composition root wires them together. Runtime framework projects do not reference it —
generated CSS and module accessor names form the output boundary, while developer tooling may consume the
public parser directly. It targets the netstandard2.0 analyzer TFM so it can run inside Roslyn generator
hosts and other build/editor-time processes.

Rationale and deliberate divergences live in [DESIGN.md](DESIGN.md).
