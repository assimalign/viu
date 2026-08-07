namespace Assimalign.Viu.Components;

/// <summary>
/// Describes a component subtree whose mounted state may be retained while inactive. Structural
/// description only; the retention executor is runtime-internal.
/// </summary>
/// <remarks>
/// The cached content arrives through the invocation's lazy default slot, and retention inputs
/// (include, exclude, maximum) ride the invocation arguments, resolved by the executor against
/// the built-in's published contract — slots are never evaluated at description time.
/// </remarks>
public sealed class KeepAliveNode : VirtualNode
{
    /// <summary>Initializes an immutable keep-alive description.</summary>
    /// <param name="invocation">The raw arguments and lazy slots supplied at the invocation site.</param>
    /// <param name="key">The optional sibling identity.</param>
    public KeepAliveNode(ComponentInvocation invocation, object? key = null)
        : base(VirtualNodeKind.KeepAlive, key, null, null)
    {
        Invocation = invocation ?? ComponentInvocation.Empty;
    }

    /// <summary>Gets the raw arguments and lazy slots supplied at the invocation site.</summary>
    public ComponentInvocation Invocation { get; }
}
