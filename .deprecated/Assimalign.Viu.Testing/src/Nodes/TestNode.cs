namespace Assimalign.Viu.Testing;

/// <summary>
/// The base of the in-memory node tree the test renderer drives — the C# port of the node model
/// in <c>@vue/runtime-test</c> (<c>packages/runtime-test/src/nodeOps.ts</c>). The tree fulfills
/// the renderer's node-ops contract exactly as a real platform does, so a green test against it
/// implies the same op sequence against the browser DOM. Not thread-safe (tests run
/// single-threaded, mirroring the JS event-loop model).
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
