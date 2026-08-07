namespace Assimalign.Viu.Components;

/// <summary>
/// Describes one child decorated with host-provided enter/leave transition behavior. Structural
/// description only; the state machine is runtime-internal and host packages supply the platform
/// behavior.
/// </summary>
/// <remarks>
/// The decorated child arrives through the invocation's lazy default slot; the transition's full
/// declared parameter set (name, mode, appear, and the per-phase class and hook inputs) rides
/// the invocation arguments, resolved by the executor against the built-in's published contract.
/// </remarks>
public sealed class TransitionNode : VirtualNode
{
    /// <summary>Initializes an immutable transition description.</summary>
    /// <param name="invocation">The raw arguments and lazy slots supplied at the invocation site.</param>
    /// <param name="key">The optional sibling identity.</param>
    public TransitionNode(ComponentInvocation invocation, object? key = null)
        : base(VirtualNodeKind.Transition, key, null, null)
    {
        Invocation = invocation ?? ComponentInvocation.Empty;
    }

    /// <summary>Gets the raw arguments and lazy slots supplied at the invocation site.</summary>
    public ComponentInvocation Invocation { get; }
}
