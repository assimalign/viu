using System;

namespace Assimalign.Viu.Syntax.SingleFileComponent;

/// <summary>
/// The <c>.viu</c> single-file component block parser entry point: slices a file into its
/// <see cref="SingleFileComponentDescriptor"/> of typed, located blocks. Block-level slicing only: the
/// parser never looks inside a block's content (specified by <c>[SFC-5]</c>). The container is hybrid
/// ([V01.01.06.10], specified by <c>[SFC-3]</c>): <c>&lt;template&gt;</c> and <c>&lt;style&gt;</c> are
/// tag-based, while the component's C# lives in <c>@script { }</c> and custom blocks keep the @-form.
/// The markup inside <c>&lt;template&gt;</c> is the Viu template language and is parsed by the template
/// compiler ([V01.01.05.01]).
/// </summary>
/// <remarks>
/// Runs entirely at build time inside a Roslyn generator ([V01.01.06.02]): no file or network I/O — the
/// source text is handed in as a string — no async, and no reflection. Parsing is recoverable: malformed
/// input is reported through <see cref="SingleFileComponentParseResult.Errors"/> and never throws. The
/// grammar is normative in <c>docs/FORMAT.md</c>.
/// </remarks>
public static class SingleFileComponentParser
{
    /// <summary>Parses a <c>.viu</c> <paramref name="source"/> into its descriptor and diagnostics.</summary>
    /// <param name="source">The full <c>.viu</c> file text.</param>
    /// <returns>The parse result — the descriptor plus any recoverable diagnostics.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="source"/> is <see langword="null"/>.</exception>
    public static SingleFileComponentParseResult Parse(string source)
    {
        if (source is null)
        {
            throw new ArgumentNullException(nameof(source));
        }

        return new SingleFileComponentParseEngine(source).Parse();
    }
}
