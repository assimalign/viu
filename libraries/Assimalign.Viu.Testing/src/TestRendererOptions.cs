namespace Assimalign.Viu.Testing;

/// <summary>
/// Configures the DOM-free test host with named options, avoiding positional Boolean ordering
/// between renderer construction and lower-level node-operation construction. Specified by
/// <c>[HYD-2]</c> and <c>[CONF-3]</c>.
/// </summary>
public sealed record TestRendererOptions
{
    /// <summary>Gets whether hydration reads an immutable pre-walk of the host tree.</summary>
    public bool SnapshotSemantics { get; init; }

    /// <summary>Gets whether removing the same host node more than once throws.</summary>
    public bool StrictRemoval { get; init; }
}
