using System;

namespace Assimalign.Viu.Components;

/// <summary>
/// Describes one compile-time-known directive invocation without activating it.
/// </summary>
/// <remarks>
/// The immutable invocation travels with its virtual node; host behavior resolves the type token
/// through an explicit directive seam. Specified by <c>[CMP-7]</c>.
/// </remarks>
public sealed class DirectiveInvocation
{
    /// <summary>Initializes an immutable directive invocation.</summary>
    /// <param name="directiveType">The registered directive type token.</param>
    /// <param name="value">The value captured for this render.</param>
    public DirectiveInvocation(Type directiveType, object? value = null)
    {
        ArgumentNullException.ThrowIfNull(directiveType);
        DirectiveType = directiveType;
        Value = value;
    }

    /// <summary>Gets the directive type token. The token is never used for reflective activation.</summary>
    public Type DirectiveType { get; }

    /// <summary>Gets the optional directive value.</summary>
    public object? Value { get; }
}
