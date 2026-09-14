namespace Assimalign.Viu;

/// <summary>Describes one scheduler flush chain without retaining jobs or application objects.</summary>
/// <remarks>
/// Counts include a throwing invocation and exclude disposed or deduplicated jobs. A chain includes
/// synchronous drains and all work queued by its post-flush callbacks. This immutable value is
/// passed by reference to avoid boxing. Specified by <c>[DVT-8]</c> and <c>[DVT-10]</c>.
/// </remarks>
public readonly struct RuntimeInspectionFlush
{
    internal RuntimeInspectionFlush(
        long identifier,
        int preFlushCount,
        int renderCount,
        int postFlushCount,
        bool faulted)
    {
        Identifier = identifier;
        PreFlushCount = preFlushCount;
        RenderCount = renderCount;
        PostFlushCount = postFlushCount;
        Faulted = faulted;
    }

    /// <summary>Gets the process-unique identifier reserved for this execution flow's flush chain.</summary>
    public long Identifier { get; }

    /// <summary>Gets the number of pre-flush jobs invoked in this chain.</summary>
    public int PreFlushCount { get; }

    /// <summary>Gets the number of render-phase jobs invoked in this chain.</summary>
    public int RenderCount { get; }

    /// <summary>Gets the number of post-flush callbacks invoked in this chain.</summary>
    public int PostFlushCount { get; }

    /// <summary>Gets whether an exception caused the remaining work to be abandoned.</summary>
    public bool Faulted { get; }
}
