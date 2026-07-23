namespace Assimalign.Viu.Components;

/// <summary>An immutable comment or empty-render placeholder.</summary>
public sealed class CommentComponent : ICommentComponent
{
    /// <summary>Creates a comment component.</summary>
    /// <param name="text">The optional comment content.</param>
    public CommentComponent(string? text = null)
    {
        Text = text;
    }

    /// <inheritdoc/>
    public ComponentKind Kind => ComponentKind.Comment;

    /// <inheritdoc/>
    public object? Key => null;

    /// <inheritdoc/>
    public string? Text { get; }
}

