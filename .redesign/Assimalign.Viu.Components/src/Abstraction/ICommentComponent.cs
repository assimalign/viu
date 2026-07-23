namespace Assimalign.Viu.Components;

/// <summary>Describes a comment or empty-render placeholder.</summary>
public interface ICommentComponent : IComponent
{
    /// <summary>Gets the optional comment content.</summary>
    string? Text { get; }
}

