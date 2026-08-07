using System;

namespace Assimalign.Viu.Components;

/// <summary>Describes one immutable comment node.</summary>
public sealed class CommentNode : VirtualNode
{
    /// <summary>Initializes a comment node.</summary>
    /// <param name="text">The non-null comment payload.</param>
    public CommentNode(string text)
        : base(VirtualNodeKind.Comment, null, null, null)
    {
        ArgumentNullException.ThrowIfNull(text);
        Text = text;
    }

    /// <summary>Gets the comment payload.</summary>
    public string Text { get; }
}
