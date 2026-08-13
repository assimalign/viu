namespace Assimalign.Viu.Syntax.Templates;

/// <summary>
/// The decomposed pieces of a <c>v-for</c> expression: the iterated <see cref="Source"/> and the value, key,
/// and index aliases. The <c>v-for</c> transform — not the parser — performs the alias tokenization
/// ([V01.01.05.02]), because the parser deliberately leaves expression bodies opaque; the aliases become
/// template-local scope entries for expression analysis ([V01.01.05.04]).
/// </summary>
/// <param name="Source">The iterated source expression (right of <c>in</c>/<c>of</c>).</param>
/// <param name="Value">The value alias, or <see langword="null"/> (e.g. <c>v-for="n in 10"</c>'s <c>n</c>).</param>
/// <param name="Key">The key alias, or <see langword="null"/> (the second alias in <c>(value, key)</c>).</param>
/// <param name="Index">The index alias, or <see langword="null"/> (the third alias in <c>(value, key, index)</c>).</param>
public sealed record ForParseResult(
    ExpressionNode Source,
    ExpressionNode? Value,
    ExpressionNode? Key,
    ExpressionNode? Index);
