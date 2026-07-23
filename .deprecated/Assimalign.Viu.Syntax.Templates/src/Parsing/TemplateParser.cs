using System;

namespace Assimalign.Viu.Syntax.Templates;

/// <summary>
/// The template parser entry point: turns template markup into a fully located, immutable AST. The C#
/// port of Vue 3.5's <c>baseParse</c> (<c>@vue/compiler-core</c> <c>parser.ts</c>). Parsing is
/// recoverable — malformed input is reported through <see cref="ParserOptions.OnError"/> and never
/// throws — and produces value-comparable output suitable for incremental-generator caching.
/// </summary>
/// <remarks>
/// Runs entirely at build time inside a Roslyn generator: no runtime DOM, no file or network I/O. See
/// https://vuejs.org/guide/essentials/template-syntax.html and the WHATWG parsing spec
/// https://html.spec.whatwg.org/multipage/parsing.html.
/// </remarks>
public static class TemplateParser
{
    /// <summary>Parses <paramref name="source"/> with the default (base-mode) options.</summary>
    /// <param name="source">The template source.</param>
    /// <returns>The located AST root.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="source"/> is <see langword="null"/>.</exception>
    public static RootNode Parse(string source) => Parse(source, new ParserOptions());

    /// <summary>Parses <paramref name="source"/> with the given <paramref name="options"/>.</summary>
    /// <param name="source">The template source.</param>
    /// <param name="options">The parser options.</param>
    /// <returns>The located AST root.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="source"/> or <paramref name="options"/> is <see langword="null"/>.</exception>
    public static RootNode Parse(string source, ParserOptions options)
    {
        if (source is null)
        {
            throw new ArgumentNullException(nameof(source));
        }

        if (options is null)
        {
            throw new ArgumentNullException(nameof(options));
        }

        return new ParserContext(options).Parse(source);
    }
}
