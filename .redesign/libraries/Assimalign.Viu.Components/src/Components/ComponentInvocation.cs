using System.Collections.Generic;

namespace Assimalign.Viu.Components;

/// <summary>
/// Carries raw immutable inputs supplied by a parent-created component node.
/// </summary>
/// <remarks>
/// Arguments, slots, listeners, and directives are copied when the invocation is constructed and
/// are never contract-resolved in place. Specified by <c>[CMP-2]</c>, <c>[CMP-7]</c>, and
/// <c>[CMP-18]</c>.
/// </remarks>
public sealed class ComponentInvocation
{
    private static readonly IReadOnlyDictionary<string, object?> EmptyArguments =
        CollectionSnapshot.CopyDictionary<object?>(null);
    private static readonly IReadOnlyDictionary<string, ComponentSlot> EmptySlots =
        CollectionSnapshot.CopyDictionary<ComponentSlot>(null);

    /// <summary>Gets an invocation with no parent-supplied inputs.</summary>
    public static ComponentInvocation Empty { get; } = new();

    /// <summary>Initializes an immutable invocation.</summary>
    /// <param name="arguments">Raw named arguments.</param>
    /// <param name="slots">Raw named slot functions.</param>
    /// <param name="listeners">Raw named event listeners.</param>
    /// <param name="directives">Directives attached to the invocation site.</param>
    public ComponentInvocation(
        IReadOnlyDictionary<string, object?>? arguments = null,
        IReadOnlyDictionary<string, ComponentSlot>? slots = null,
        IReadOnlyDictionary<string, ComponentEventListener>? listeners = null,
        IEnumerable<DirectiveInvocation>? directives = null)
    {
        Arguments = arguments is null ? EmptyArguments : CollectionSnapshot.CopyDictionary(arguments);
        Slots = slots is null
            ? EmptySlots
            : CollectionSnapshot.CopyNonNullDictionary(slots, nameof(slots));
        Listeners = CollectionSnapshot.CopyNonNullDictionary(listeners, nameof(listeners));
        Directives = CollectionSnapshot.CopyNonNull(directives, nameof(directives));
    }

    /// <summary>Gets raw named arguments before contract resolution.</summary>
    public IReadOnlyDictionary<string, object?> Arguments { get; }

    /// <summary>Gets raw named slots before fallback and stability resolution.</summary>
    public IReadOnlyDictionary<string, ComponentSlot> Slots { get; }

    /// <summary>Gets raw parent listeners before declared-event consumption.</summary>
    public IReadOnlyDictionary<string, ComponentEventListener> Listeners { get; }

    /// <summary>Gets directives attached at the invocation site.</summary>
    public IReadOnlyList<DirectiveInvocation> Directives { get; }
}
