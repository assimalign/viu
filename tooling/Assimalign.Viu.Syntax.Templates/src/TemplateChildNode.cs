namespace Assimalign.Viu.Syntax.Templates;

/// <summary>
/// The base of nodes that can appear as children of the root or an element — elements, text,
/// comments, and interpolations — the node kinds that may appear in a <c>Children</c> list.
/// </summary>
public abstract record TemplateChildNode : TemplateSyntaxNode;
