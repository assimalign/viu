namespace Assimalign.Vue.Compiler;

/// <summary>
/// The base of nodes that can appear as children of the root or an element — elements, text,
/// comments, and interpolations. The C# port of the parse-time members of Vue 3.5's
/// <c>TemplateChildNode</c> union (<c>@vue/compiler-core</c> <c>ast.ts</c>).
/// </summary>
public abstract record TemplateChildNode : SyntaxNode;
