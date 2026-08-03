namespace Assimalign.Viu.Syntax.Templates;

/// <summary>
/// The base of the entries in an element's property list — plain attributes and directives. The C#
/// closed over exactly <see cref="AttributeNode"/> and <see cref="DirectiveNode"/>, so a transform can
/// switch on the two cases exhaustively.
/// </summary>
public abstract record PropertyNode : TemplateSyntaxNode;
