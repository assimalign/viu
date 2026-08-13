namespace Assimalign.Viu;

/// <summary>Identifies an existing host node while Core hydrates a server-rendered tree.</summary>
/// <remarks>
/// The host reports this closed classification without exposing a platform node model to Core.
/// Specified by <c>[HYD-1]</c> and <c>[HYD-2]</c>.
/// </remarks>
public enum HydrationNodeKind
{
    /// <summary>An element node.</summary>
    Element,

    /// <summary>A text node.</summary>
    Text,

    /// <summary>A comment node.</summary>
    Comment,

    /// <summary>A host node that the renderer cannot adopt.</summary>
    Other,
}
