using System;

namespace Assimalign.Viu.Components;

/// <summary>An immutable text component.</summary>
public sealed class TextComponent : ITextComponent
{
    /// <summary>Creates a text component.</summary>
    /// <param name="text">The text content.</param>
    /// <param name="optimization">The compiler-produced optimization metadata.</param>
    public TextComponent(string text, ComponentOptimization? optimization = null)
    {
        ArgumentNullException.ThrowIfNull(text);
        Text = text;
        Optimization = optimization ?? ComponentOptimization.None;
    }

    /// <inheritdoc/>
    public ComponentKind Kind => ComponentKind.Text;

    /// <inheritdoc/>
    public object? Key => null;

    /// <inheritdoc/>
    public ComponentOptimization Optimization { get; }

    /// <inheritdoc/>
    public string Text { get; }
}
