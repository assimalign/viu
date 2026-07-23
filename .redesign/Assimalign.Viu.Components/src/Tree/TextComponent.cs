using System;

namespace Assimalign.Viu.Components;

/// <summary>An immutable text component.</summary>
public sealed class TextComponent : ITextComponent
{
    /// <summary>Creates a text component.</summary>
    /// <param name="text">The text content.</param>
    public TextComponent(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        Text = text;
    }

    /// <inheritdoc/>
    public ComponentKind Kind => ComponentKind.Text;

    /// <inheritdoc/>
    public object? Key => null;

    /// <inheritdoc/>
    public string Text { get; }
}

