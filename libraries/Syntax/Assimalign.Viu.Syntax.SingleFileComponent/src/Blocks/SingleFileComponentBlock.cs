using System;

namespace Assimalign.Viu.Syntax.SingleFileComponent;

/// <summary>
/// The base of every parsed single-file-component block: its name, options, raw content, and precise source
/// spans — one immutable,
/// value-comparable record per block, deriving from the shared <see cref="SyntaxNode"/> for its
/// <see cref="SyntaxNode.Location"/> span. The <c>.viu</c> container and the <c>.vue</c> compatibility
/// container ([V01.01.06.09]) both project into this one block hierarchy, so a consumer downstream of
/// the container parse never branches on which file it came from (<c>[VUE-2]</c>, <c>[VUE-9]</c>).
/// The inherited <see cref="SyntaxNode.Location"/> covers the
/// whole block, including either its <c>@name { }</c> or matching tag container. Derivation is
/// assembly-closed so downstream stages only need to handle library-defined variants. Specified by
/// <c>[SFC-DIAG-3]</c>.
/// </summary>
/// <remarks>
/// Records give the block structural equality — the incremental-caching contract of [V01.01.06.01]:
/// identical file content yields equal blocks (and equal descriptors), so [V01.01.06.02] can cache on
/// the parse output. <see cref="Content"/> is the exact raw slice inside the block container; it is
/// never re-parsed here — the template compiler ([V01.01.05.01]) and script analysis
/// ([V01.01.06.03]) consume it downstream.
/// </remarks>
public abstract record SingleFileComponentBlock : SyntaxNode
{
    private protected SingleFileComponentBlock()
    {
    }

    /// <summary>The block name exactly as authored (for example, <c>template</c>, <c>style</c>, or <c>docs</c>).</summary>
    public required string Name { get; init; }

    /// <summary>The options on the block header, in source order.</summary>
    public required SyntaxList<SingleFileComponentBlockOption> Options { get; init; }

    /// <summary>The raw block content — the exact source between the opening and closing container boundaries.</summary>
    public required string Content { get; init; }

    /// <summary>The source range covering the content region only (exactly what <see cref="Content"/> holds).</summary>
    public required SourceLocation ContentLocation { get; init; }

    /// <summary>The block kind discriminator.</summary>
    public abstract SingleFileComponentBlockKind Kind { get; }

    /// <inheritdoc />
    public sealed override int RawKind => (int)Kind;

    /// <summary>
    /// The <c>lang</c> option's value, or <see langword="null"/> when absent — the block header's
    /// declaration of what language its content is written in (for example, <c>lang="scss"</c> on a
    /// style block). The container parser never acts on it; it is the routing key the aggregate
    /// registration seam and downstream analysis match on.
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
