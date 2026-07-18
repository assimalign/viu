namespace Assimalign.Vue.Compiler;

/// <summary>
/// The base of the entries in an element's property list — plain attributes and directives. The C#
/// port of the <c>AttributeNode | DirectiveNode</c> element-<c>props</c> union in Vue 3.5
/// (<c>@vue/compiler-core</c> <c>ast.ts</c>).
/// </summary>
public abstract record PropertyNode : SyntaxNode;
