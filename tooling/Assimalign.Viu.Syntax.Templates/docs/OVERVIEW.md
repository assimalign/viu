# Assimalign.Viu.Syntax.Templates — overview

The Viu template language front end: it tokenizes
and parses template markup into a located AST, runs the transform pipeline (structural and
directive transforms, expression binding, static optimization), and generates a **C# render method**.
The platform-neutral template language and its DOM directive set live in this one project.
It is a build-time library that runs inside the source generator ([V01.01.05.05]/[V01.01.06.02]);
there is no runtime compilation (`[SFC-1]`, and see
[ADR-0005](../../../docs/adr/0005-no-runtime-template-compilation.md)). Area: `V01.01.05`.

The rationale, the code-generation decisions C# forces, and the runtime-helper contract are
detailed in [DESIGN.md](DESIGN.md); this page is the surface map.

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
- **Code generation** (`CodeGeneration/`) — `RenderFunctionEmitter`, its
  options/result, `RenderSourceMapping` (the `#line` correspondence), and the code-generation IR.
- **Diagnostics** (`Diagnostics/`) — `CompilerError` and `CompilerErrorCode`, whose numeric bands are
  a frozen contract.

## Boundaries

- Roots on **`Assimalign.Viu.Syntax`** only. It does **not** reference the runtime: emitted render
  statements name the Components frame/node contracts and typed Browser operations directly, so the
  contract flows one way (see
  [`Assimalign.Viu.Core/docs/OVERVIEW.md`](../../Assimalign.Viu.Core/docs/OVERVIEW.md)).
- Build-time library on the netstandard2.0 analyzer TFM; runs in Roslyn generator hosts.
- Everything a parse or transform produces is value-equatable to preserve the incremental-generator
  caching contract.
