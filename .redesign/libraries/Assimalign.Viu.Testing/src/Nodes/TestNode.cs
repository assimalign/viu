using System;

namespace Assimalign.Viu.Testing;

/// <summary>Represents one DOM-free host node used by the testing renderer.</summary>
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
