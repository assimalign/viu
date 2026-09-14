using System;

namespace Assimalign.Viu.Components;

/// <summary>
/// Describes content and fallback branches coordinated by asynchronous dependencies. Structural
/// description only; the boundary executor is runtime-internal.
/// </summary>
/// <remarks>
/// Content arrives through the invocation's lazy default slot and the fallback through the lazy
/// fallback slot — both stay unevaluated at description time, preserving slot laziness and
/// re-render granularity. The optional <c>timeout</c> argument is an integer millisecond deadline:
/// absent or negative retains the previous active branch, zero shows fallback immediately, and
/// positive waits on the scheduler clock. An initial pending mount without a timeout shows fallback;
/// with a positive timeout it keeps an empty placeholder until the deadline.
/// Invocation listeners named <c>pending</c>, <c>fallback</c>, and <c>resolve</c> receive no arguments.
/// Hidden mounted callbacks and references wait for reveal. Specified by <c>[BLT-11]</c> through
/// <c>[BLT-13]</c> and <c>[BLT-16]</c> through <c>[BLT-21]</c>.
/// </remarks>
public sealed class SuspenseNode : VirtualNode
{
    /// <summary>Initializes an immutable suspense description.</summary>
    /// <param name="invocation">The raw arguments and lazy slots supplied at the invocation site.</param>
    /// <param name="key">The optional sibling identity.</param>
    public SuspenseNode(ComponentInvocation invocation, object? key = null)
        : base(VirtualNodeKind.Suspense, key, null, null)
    {
        ArgumentNullException.ThrowIfNull(invocation);
        Invocation = invocation;
    }

    /// <summary>Gets the raw arguments and lazy slots supplied at the invocation site.</summary>
    public ComponentInvocation Invocation { get; }
}
