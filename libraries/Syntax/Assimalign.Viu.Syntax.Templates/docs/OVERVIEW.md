# Assimalign.Viu.Syntax.Templates — overview

The Viu template language front end: it tokenizes
and parses template markup into a located AST, runs the transform pipeline (structural and
directive transforms, expression binding, static optimization), and generates **C# render method
bodies** for either the interactive virtual-node tree or direct server markup.
The platform-neutral template language and its DOM directive set live in this one publicly
consumable project. It is a build-time library used by the source generator
([V01.01.05.05]/[V01.01.06.02]) and available to developer-authored tooling; there is no runtime
compilation (`[SFC-1]`, and see
[ADR-0005](../../../../docs/adr/0005-no-runtime-template-compilation.md)). Area: `V01.01.05`.

The rationale, the code-generation decisions C# forces, and the runtime-helper contract are
detailed in [DESIGN.md](DESIGN.md); this page is the surface map.

The current compiler includes per-mount static caching, bounded HTML/SVG/MathML static-fragment
stringification, and ordinary plus keyed-list `v-memo` emission. It intentionally accepts only simple C#
identifiers for `v-for` aliases and slot scopes; generalized destructuring is diagnosed before emission.
Per-component-type static fields and inferred generalized handler caching are explicit non-goals because
they add lifetime or delegate-semantic risk without reducing host operations ([V01.01.05.10], issue #321).

## Public surface

- **Parsing** (`Parsing/`) — `TemplateParser.Parse` (the authoritative entry point),
  `TemplateSyntaxParser` (the registration adapter over the shared pipeline),
  `TemplateSyntaxParserResult`, `ParserOptions`, `TemplateParseMode`, `WhitespaceStrategy`.
- **The AST** (`Ast/`, plus root-level `TemplateSyntaxNode` / `TemplateChildNode` / `ExpressionNode`
  / `PropertyNode` / `NodeType`) — `RootNode`, `ElementNode`, `AttributeNode`, `DirectiveNode`,
  `InterpolationNode`, `TextNode`, `CommentNode`, `SimpleExpressionNode`, `CompoundExpressionNode`,
  and the `ElementType` / `ElementNamespace` / `ConstantType` classifiers. `NodeType`'s values are a
  frozen catalog — additive only, never renumbered.
- **Transforms** (`Transforms/`) — `Transformer`, `TransformContext`, `TransformOptions`,
  `TransformResult`, `TemplateExpressionCompiler`, and the `NodeTransform` / `DirectiveTransform`
  delegates.
- **Binding** (`Binding/`) — `BindingMetadata`, `BindingType`, `BindingRewriteMode`, and the CSS
  module accessors (`CssModuleAccessor`, `CssModuleAccessors`) that let the compiler resolve
  `$style.x`.
- **Code generation** (`CodeGeneration/`) — `RenderFunctionEmitter`, its options/result,
  `RenderFunctionTargetProfile` (`VirtualNodeTree` or `ServerMarkup`), `RenderSourceMapping` (the
  `#line` correspondence), and the code-generation IR. The server profile writes static structure and
  interpolations directly to `SsrRenderState`; subtrees it cannot prove safe retain a local
  virtual-node fallback. Tooling callers select the profile explicitly; the single-file-component
  generator selects both profiles project-wide when `ViuServerRendering=true` under a Viu SDK
  (`[SSR-TARGET-1]`, `[SSR-TARGET-2]`).
- **Diagnostics** (`Diagnostics/`) — `CompilerError` and `CompilerErrorCode`, whose numeric bands are
  a frozen contract.

## Boundaries

- Roots on **`Assimalign.Viu.Syntax`** only. It does **not** reference the runtime: emitted render
  statements name the Components frame/node contracts, typed Browser operations, and public
  ServerRenderer serialization helpers directly, so the contract flows one way (see
  [`Assimalign.Viu.Core/docs/OVERVIEW.md`](../../../Runtime/Assimalign.Viu.Core/docs/OVERVIEW.md)).
- Build-time library on the netstandard2.0 analyzer TFM; runs in Roslyn generator hosts and can be
  consumed directly by other build/editor-time tooling.
- Everything a parse or transform produces is value-equatable to preserve the incremental-generator
  caching contract.
