# Assimalign.Viu.Syntax.Templates — overview

The Vue template language front end — the C# port of
[`@vue/compiler-core`](https://github.com/vuejs/core/tree/main/packages/compiler-core) plus
[`@vue/compiler-dom`](https://github.com/vuejs/core/tree/main/packages/compiler-dom): it tokenizes
and parses Vue template markup into a located AST, runs the transform pipeline (structural and
directive transforms, expression binding, static optimization), and generates a **C# render method**.
It is a build-time library that runs inside the source generator ([V01.01.05.05]/[V01.01.06.02]);
there is no runtime compilation (see
[ADR-0005](../../../docs/adr/0005-no-runtime-template-compilation.md)). Area: `V01.01.05`.

The rationale, the JavaScript-to-C# codegen divergences, and the runtime-helper contract are
detailed in [DESIGN.md](DESIGN.md); this page is the surface map.

## Public surface

- **Parsing** (`Parsing/`) — `TemplateParser.Parse` (the authoritative, `@vue/compiler-core`-parity
  entry), `TemplateSyntaxParser` (the registration adapter over the shared pipeline),
  `TemplateSyntaxParserResult`, `ParserOptions`, `TemplateParseMode`, `WhitespaceStrategy`.
- **The AST** (`Ast/`, plus root-level `TemplateSyntaxNode` / `TemplateChildNode` / `ExpressionNode`
  / `PropertyNode` / `NodeType`) — `RootNode`, `ElementNode`, `AttributeNode`, `DirectiveNode`,
  `InterpolationNode`, `TextNode`, `CommentNode`, `SimpleExpressionNode`, `CompoundExpressionNode`,
  and the `ElementType` / `ElementNamespace` / `ConstantType` classifiers. `NodeType` is pinned
  numerically to `@vue/compiler-core`'s `NodeTypes`.
- **Transforms** (`Transforms/`) — `Transformer`, `TransformContext`, `TransformOptions`,
  `TransformResult`, `TemplateExpressionCompiler`, and the `NodeTransform` / `DirectiveTransform`
  delegates.
- **Binding** (`Binding/`) — `BindingMetadata`, `BindingType`, `BindingRewriteMode`, and the CSS
  module accessors (`CssModuleAccessor`, `CssModuleAccessors`) that let the compiler resolve
  `$style.x`.
- **Code generation** (`CodeGeneration/`) — `RenderFunctionEmitter` (Vue's `generate`), its
  options/result, `RenderSourceMapping` (the `#line` correspondence), and the code-generation IR.
- **Diagnostics** (`Diagnostics/`) — `CompilerError` and `CompilerErrorCode` (numeric parity with
  vuejs/core).

## Boundaries

- Roots on **`Assimalign.Viu.Syntax`** only. It does **not** reference the runtime: the emitted
  render method binds to `Assimalign.Viu.RuntimeCore`'s `RenderHelpers` (and the DOM directive
  helpers in `Assimalign.Viu.RuntimeDom`) **by name**, so the contract flows one way (see
  [`Assimalign.Viu.RuntimeCore/docs/DESIGN.md`](../../Assimalign.Viu.RuntimeCore/docs/DESIGN.md)).
- Build-time library on the netstandard2.0 analyzer TFM; runs in Roslyn generator hosts.
- Everything a parse or transform produces is value-equatable to preserve the incremental-generator
  caching contract.
