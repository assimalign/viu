using System;

namespace Assimalign.Viu.Components;

/// <summary>Describes one immutable text node in the closed algebra specified by <c>[CMP-3]</c>.</summary>
public sealed class TextNode : VirtualNode
{
    /// <summary>Initializes a text node.</summary>
    /// <param name="text">The non-null text payload.</param>
    /// <param name="mountReference">The optional mounted reference callback.</param>
    /// <param name="renderPlan">Optional compiler patch information.</param>
    public TextNode(
        string text,
        MountReference? mountReference = null,
        RenderPlan? renderPlan = null)
        : base(VirtualNodeKind.Text, null, mountReference, renderPlan)
    {
        ArgumentNullException.ThrowIfNull(text);
        Text = text;
    }

    /// <summary>Gets the text payload.</summary>
    public string Text { get; }
}
