using System;

namespace Assimalign.Vue.Syntax.SingleFileComponent;

/// <summary>
/// The base of every parsed <c>.viu</c> block: its name, options, raw content, and precise source
/// spans. Mirrors Vue 3.5's <c>SFCBlock</c> (<c>@vue/compiler-sfc</c> <c>parse.ts</c>) — one immutable,
/// value-comparable record per block, deriving from the shared <see cref="SyntaxNode"/> for its
/// <see cref="SyntaxNode.Location"/> span. The block <em>semantics</em> follow the Vue SFC specification
/// (https://vuejs.org/api/sfc-spec.html); the <c>@name { }</c> container is the documented Vuecs
/// divergence from Vue's tag wrappers. The inherited <see cref="SyntaxNode.Location"/> covers the whole
/// block, from the <c>@</c> through the closing <c>}</c>.
/// </summary>
/// <remarks>
/// Records give the block structural equality — the incremental-caching contract of [V01.01.06.01]:
/// identical file content yields equal blocks (and equal descriptors), so [V01.01.06.02] can cache on
/// the parse output. <see cref="Content"/> is the exact raw slice between the header line and the
/// closing brace; it is never re-parsed here — the template compiler ([V01.01.05.01]) and script
/// analysis ([V01.01.06.03]) consume it downstream.
/// </remarks>
public abstract record SingleFileComponentBlock : SyntaxNode
{
    /// <summary>The block name exactly as authored (e.g. <c>template</c>, <c>style</c>, <c>docs</c>).</summary>
    public required string Name { get; init; }

    /// <summary>The options on the block header, in source order.</summary>
    public required SyntaxList<SingleFileComponentBlockOption> Options { get; init; }

    /// <summary>The raw block content — the exact source between the header line and the closing brace.</summary>
    public required string Content { get; init; }

    /// <summary>The source range covering the content region only (exactly what <see cref="Content"/> holds).</summary>
    public required SourceLocation ContentLocation { get; init; }

    /// <summary>The block kind discriminator.</summary>
    public abstract SingleFileComponentBlockKind Kind { get; }

    /// <inheritdoc />
    public sealed override int RawKind => (int)Kind;

    /// <summary>
    /// The <c>lang</c> option's value, or <see langword="null"/> when absent. Mirrors Vue's block
    /// <c>lang</c> attribute (e.g. <c>@style lang="scss"</c>, <c>@script lang="csharp"</c>).
    /// </summary>
    public string? Lang => GetOptionValue("lang");

    /// <summary>Whether an option with the given name is present, regardless of its value.</summary>
    /// <param name="name">The option name to look for (ordinal comparison).</param>
    /// <returns><see langword="true"/> when the option is present.</returns>
    public bool HasOption(string name)
    {
        foreach (var option in Options)
        {
            if (string.Equals(option.Name, name, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Gets the value of the named option, or <see langword="null"/> when absent or valueless.</summary>
    /// <param name="name">The option name to look for (ordinal comparison).</param>
    /// <returns>The option's value, or <see langword="null"/>.</returns>
    public string? GetOptionValue(string name)
    {
        foreach (var option in Options)
        {
            if (string.Equals(option.Name, name, StringComparison.Ordinal))
            {
                return option.Value;
            }
        }

        return null;
    }
}
