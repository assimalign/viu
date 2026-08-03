namespace Assimalign.Viu.Testing;

/// <summary>
/// The base of the in-memory node tree the test renderer drives. The tree fulfills the renderer's
/// node-ops contract exactly as a real platform does, so a green test against it implies the same
/// operation sequence against the browser DOM. Not thread-safe: tests run single-threaded, matching
/// the JS event-loop model the runtime targets ([EXE-1]).
/// </summary>
public abstract class TestNode
{
    private static int _nextIdentifier;

    private protected TestNode()
    {
        Identifier = ++_nextIdentifier;
    }

    /// <summary>A process-unique id for diagnostics and op-log correlation.</summary>
    public int Identifier { get; }

    /// <summary>The parent element, or null when detached.</summary>
    public TestElement? Parent { get; internal set; }
}
