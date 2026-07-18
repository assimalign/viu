using System;
using System.Threading;

namespace Assimalign.Vue.Syntax;

/// <summary>
/// The language-agnostic parser contract of the <c>Assimalign.Vue.Syntax.*</c> family: turns a
/// <see cref="SyntaxSource"/> into a <see cref="SyntaxParserResult"/> of located, value-comparable
/// nodes and diagnostics. Build tooling programs against this base the way a Roslyn incremental
/// generator programs against registered providers — parsers are <em>registered</em> for the sources
/// they understand (see <see cref="AggregateSyntaxParser{T}"/>) rather than hard-wired, so the same
/// pipeline can host the template parser, the browser-language parsers, and user-supplied parsers for
/// custom blocks or file types.
/// </summary>
/// <remarks>
/// Implementations run at build time inside netstandard2.0 Roslyn generator hosts
/// ([V01.01.05.05]/[V01.01.06.02]): no file or network I/O — source text is handed in as a string —
/// and no reflection or dynamic code generation. Parsing is recoverable: malformed input is reported
/// through <see cref="SyntaxParserResult.Diagnostics"/> (or a language's own upstream-pinned channel)
/// and never throws; <see cref="OperationCanceledException"/> is the only expected exception, on
/// cancellation.
/// </remarks>
public abstract class SyntaxParser
{
    /// <summary>Parses bare <paramref name="text"/> — a <see cref="SyntaxSource"/> with no name or language metadata.</summary>
    /// <param name="text">The source text to parse.</param>
    /// <param name="cancellationToken">Cancels the parse.</param>
    /// <returns>The parse result — nodes plus any recoverable diagnostics.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="text"/> is <see langword="null"/>.</exception>
    public SyntaxParserResult Parse(string text, CancellationToken cancellationToken = default)
    {
        if (text is null)
        {
            throw new ArgumentNullException(nameof(text));
        }

        return Parse(new SyntaxSource { Text = text }, cancellationToken);
    }

    /// <summary>Parses <paramref name="source"/>.</summary>
    /// <param name="source">The source to parse.</param>
    /// <param name="cancellationToken">Cancels the parse.</param>
    /// <returns>The parse result — nodes plus any recoverable diagnostics.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="source"/> is <see langword="null"/>.</exception>
    public abstract SyntaxParserResult Parse(SyntaxSource source, CancellationToken cancellationToken = default);
}
