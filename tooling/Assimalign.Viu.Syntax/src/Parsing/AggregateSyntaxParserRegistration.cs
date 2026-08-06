namespace Assimalign.Viu.Syntax;

/// <summary>
/// One parser registration on an <see cref="AggregateSyntaxParserOptions{T}"/>: the predicate that
/// selects the embedded sources the parser understands, and the parser to dispatch them to. Created
/// through <see cref="AggregateSyntaxParserOptions{T}.RegisterParser"/>.
/// </summary>
/// <param name="Predicate">Selects the sources the parser understands.</param>
/// <param name="Parser">The parser to dispatch matching sources to.</param>
public sealed record AggregateSyntaxParserRegistration(SyntaxSourcePredicate Predicate, SyntaxParser Parser);
