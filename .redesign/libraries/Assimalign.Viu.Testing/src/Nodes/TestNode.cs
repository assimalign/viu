namespace Assimalign.Viu.Testing;

/// <summary>
/// Represents the base of the in-memory host tree driven by the production renderer contract.
/// The node model is deliberately single-threaded, matching Viu's JavaScript event-loop model.
/// </summary>
/// <remarks>Specified by <c>[EXE-1]</c>, <c>[RND-HOST-3]</c>, and <c>[CONF-3]</c>.</remarks>
public abstract class TestNode
{
    private static int _nextIdentifier;

    private protected TestNode()
    {
        Identifier = checked(++_nextIdentifier);
    }

    /// <summary>Gets the process-unique identifier used for diagnostics and operation correlation.</summary>
    public int Identifier { get; }

    /// <summary>Gets the parent element, or null while the node is detached.</summary>
    public TestElement? Parent { get; internal set; }
}
