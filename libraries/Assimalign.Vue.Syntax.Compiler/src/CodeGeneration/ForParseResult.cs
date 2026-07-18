namespace Assimalign.Vue.Syntax.Compiler;

/// <summary>
/// The decomposed pieces of a <c>v-for</c> expression: the iterated <see cref="Source"/> and the value, key,
/// and index aliases. The C# port of Vue 3.5's <c>ForParseResult</c> (<c>@vue/compiler-core</c>
/// <c>ast.ts</c>). Upstream fills this during parsing; in this port the <c>v-for</c> transform performs the
/// minimal alias tokenization ([V01.01.05.02]), leaving expression bodies opaque for [V01.01.05.04].
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
