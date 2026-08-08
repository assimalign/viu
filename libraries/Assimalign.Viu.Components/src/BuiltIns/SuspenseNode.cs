using System;

namespace Assimalign.Viu.Components;

/// <summary>
/// Describes content and fallback branches coordinated by asynchronous dependencies. Structural
/// description only; the boundary executor is runtime-internal.
/// </summary>
/// <remarks>
/// Content arrives through the invocation's lazy default slot and the fallback through the lazy
/// fallback slot — both stay unevaluated at description time, preserving slot laziness and
/// re-render granularity. Specified by <c>[BLT-11]</c> through <c>[BLT-13]</c>.
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
