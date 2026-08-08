using System;

using Assimalign.Viu.Components;

namespace Assimalign.Viu;

/// <summary>Pairs an immutable element node with its mounted host element.</summary>
/// <remarks>Specified by <c>[CMP-7]</c> and <c>[HYD-4]</c>.</remarks>
public sealed class DirectiveHostElement
{
    internal DirectiveHostElement(ElementNode value, object element)
    {
        ArgumentNullException.ThrowIfNull(value);
        ArgumentNullException.ThrowIfNull(element);
        Value = value;
        Element = element;
    }

    /// <summary>Gets the immutable element node.</summary>
    public ElementNode Value { get; }

    /// <summary>Gets the boxed mounted host element.</summary>
    public object Element { get; }
}
