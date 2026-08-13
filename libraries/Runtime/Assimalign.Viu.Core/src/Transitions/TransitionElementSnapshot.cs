using System;

namespace Assimalign.Viu;

/// <summary>Pairs one immutable child key with its first mounted host element.</summary>
/// <remarks>
/// Snapshots preserve child order and expose only host-neutral boxed handles; Core never performs
/// layout measurement. Specified by <c>[BLT-9]</c>.
/// </remarks>
public sealed class TransitionElementSnapshot
{
    /// <summary>Initializes one immutable transition-group snapshot entry.</summary>
    /// <param name="key">The immutable child key.</param>
    /// <param name="element">The first mounted host element below that child.</param>
    public TransitionElementSnapshot(object key, object element)
    {
        ArgumentNullException.ThrowIfNull(key);
        ArgumentNullException.ThrowIfNull(element);
        Key = key;
        Element = element;
    }

    /// <summary>Gets the immutable child key.</summary>
    public object Key { get; }

    /// <summary>Gets the boxed first mounted host element.</summary>
    public object Element { get; }
}
