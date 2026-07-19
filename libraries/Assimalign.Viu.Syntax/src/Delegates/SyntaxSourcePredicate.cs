namespace Assimalign.Viu.Syntax;

/// <summary>
/// Selects the embedded <see cref="SyntaxSource"/> values a registered <see cref="SyntaxParser"/>
/// understands — the matching half of an
/// <see cref="AggregateSyntaxParserOptions{T}.RegisterParser"/> registration (e.g.
/// <c>source =&gt; source.Name == "style"</c> to route <c>@style</c> blocks to a stylesheet parser).
/// </summary>
/// <param name="source">The embedded source to test.</param>
/// <returns><see langword="true"/> when the registered parser should receive <paramref name="source"/>.</returns>
public delegate bool SyntaxSourcePredicate(SyntaxSource source);
