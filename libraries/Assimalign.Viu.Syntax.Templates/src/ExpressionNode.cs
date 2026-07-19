namespace Assimalign.Viu.Syntax.Templates;

/// <summary>
/// The base of expression nodes carried by interpolations and directives. The C# port of Vue 3.5's
/// <c>ExpressionNode</c> union (<c>@vue/compiler-core</c> <c>ast.ts</c>). The parser only ever produces
/// <see cref="SimpleExpressionNode"/>; compound expressions are introduced by later transform stages.
/// </summary>
public abstract record ExpressionNode : TemplateSyntaxNode;
