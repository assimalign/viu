using System;

namespace Assimalign.Vue.Sfc;

/// <summary>
/// The <c>.viu</c> single-file component block parser entry point: slices a file into its
/// <see cref="SfcDescriptor"/> of typed, located blocks. The role Vue 3.5's <c>parse()</c> plays in
/// <c>@vue/compiler-sfc</c> — block-level slicing only. The markup inside <c>@template</c> stays
/// standard Vue template syntax and is parsed by the template compiler ([V01.01.05.01]); this parser
/// never looks inside a block's content.
/// </summary>
/// <remarks>
/// Runs entirely at build time inside a Roslyn generator ([V01.01.06.02]): no file or network I/O — the
/// source text is handed in as a string — no async, and no reflection. Parsing is recoverable: malformed
/// input is reported through <see cref="SfcParseResult.Errors"/> and never throws. See the format
/// specification in <c>docs/FORMAT.md</c> and the Vue SFC spec https://vuejs.org/api/sfc-spec.html.
/// </remarks>
public static class SfcParser
{
    /// <summary>Parses a <c>.viu</c> <paramref name="source"/> into its descriptor and diagnostics.</summary>
    /// <param name="source">The full <c>.viu</c> file text.</param>
    /// <returns>The parse result — the descriptor plus any recoverable diagnostics.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="source"/> is <see langword="null"/>.</exception>
    public static SfcParseResult Parse(string source)
    {
        if (source is null)
        {
            throw new ArgumentNullException(nameof(source));
        }

        return new SfcParseEngine(source).Parse();
    }
}
