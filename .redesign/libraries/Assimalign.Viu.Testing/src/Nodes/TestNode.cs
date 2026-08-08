using System;

namespace Assimalign.Viu.Testing;

/// <summary>Represents one DOM-free host node used by the testing renderer.</summary>
/// <remarks>Specified by <c>[RND-HOST-3]</c> and <c>[CONF-3]</c>.</remarks>
public sealed class TestNode
{
    /// <summary>Initializes a test node.</summary>
    /// <param name="description">The non-empty diagnostic description.</param>
    public TestNode(string description)
    {
        ArgumentException.ThrowIfNullOrEmpty(description);
        Description = description;
    }

    /// <summary>Gets the diagnostic description.</summary>
    public string Description { get; }
}
