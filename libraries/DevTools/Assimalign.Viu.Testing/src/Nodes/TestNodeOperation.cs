namespace Assimalign.Viu.Testing;

/// <summary>Describes one recorded operation performed by the in-memory host.</summary>
/// <param name="Type">The operation kind.</param>
/// <param name="TargetNode">The created or affected node.</param>
/// <param name="ParentNode">The structural parent when applicable.</param>
/// <param name="AnchorNode">The insertion anchor when applicable.</param>
/// <param name="PropertyName">The binding name when applicable.</param>
/// <param name="PreviousValue">The prior binding value.</param>
/// <param name="NextValue">The next binding value.</param>
/// <param name="Text">The character-data or static-markup payload.</param>
/// <remarks>Specified by <c>[RND-HOST-1]</c>, <c>[RND-HOST-4]</c>, and <c>[CONF-3]</c>.</remarks>
public readonly record struct TestNodeOperation(
    TestNodeOperationType Type,
    TestNode? TargetNode = null,
    TestNode? ParentNode = null,
    TestNode? AnchorNode = null,
    string? PropertyName = null,
    object? PreviousValue = null,
    object? NextValue = null,
    string? Text = null);
