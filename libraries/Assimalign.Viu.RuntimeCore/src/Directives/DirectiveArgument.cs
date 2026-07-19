using System;
using System.Collections.Generic;

namespace Assimalign.Viu.RuntimeCore;

/// <summary>
/// One entry in the array passed to
/// <see cref="Directives.WithDirectives(VirtualNode, DirectiveArgument[])"/> — the C# port of a
/// <c>[directive, value, arg, modifiers]</c> tuple in upstream's <c>withDirectives</c>
/// (<c>packages/runtime-core/src/directives.ts</c>). Bundles a directive with the value, argument,
/// and modifiers to bind it with.
/// </summary>
public sealed class DirectiveArgument
{
    /// <summary>Creates an argument binding <paramref name="directive"/> with the given data.</summary>
    /// <param name="directive">The directive to attach.</param>
    /// <param name="value">The bound value, or null.</param>
    /// <param name="argument">The directive argument (the <c>foo</c> in <c>v-x:foo</c>), or null.</param>
    /// <param name="modifiers">The modifiers (each present name maps to true), or null for none.</param>
    /// <exception cref="ArgumentNullException"><paramref name="directive"/> is null.</exception>
    public DirectiveArgument(
        IDirective directive,
        object? value = null,
        string? argument = null,
        IReadOnlyDictionary<string, bool>? modifiers = null)
    {
        ArgumentNullException.ThrowIfNull(directive);
        Directive = directive;
        Value = value;
        Argument = argument;
        Modifiers = modifiers;
    }

    /// <summary>The directive to attach.</summary>
    public IDirective Directive { get; }

    /// <summary>The bound value, or null.</summary>
    public object? Value { get; }

    /// <summary>The directive argument, or null.</summary>
    public string? Argument { get; }

    /// <summary>The modifiers, or null when there are none.</summary>
    public IReadOnlyDictionary<string, bool>? Modifiers { get; }
}
