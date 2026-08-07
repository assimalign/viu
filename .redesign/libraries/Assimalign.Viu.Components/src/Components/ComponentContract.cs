using System;
using System.Collections.Generic;

namespace Assimalign.Viu.Components;

/// <summary>
/// The static input/output declaration of authored component behavior, readable before
/// activation. Carried by <see cref="ComponentRegistration"/>, never by the instance.
/// </summary>
/// <remarks>
/// Deliberately absent: a style-scope identifier. Scoped CSS is deferred until after the
/// component-model arc; reintroducing it is one additive member here plus serializer and
/// compiler emission — no structural change.
/// Parameters and events are immutable snapshots used before activation. Specified by
/// <c>[CMP-1]</c>, <c>[CMP-4]</c>, <c>[CMP-12]</c>, and <c>[CMP-14]</c>.
/// </remarks>
public sealed class ComponentContract
{
    /// <summary>Initializes an immutable component contract.</summary>
    /// <param name="displayName">The optional diagnostic display name.</param>
    /// <param name="flags">Static input-resolution behavior.</param>
    /// <param name="parameters">Declared input parameters.</param>
    /// <param name="events">Declared output events.</param>
    public ComponentContract(
        string? displayName = null,
        ComponentFlags flags = ComponentFlags.InheritFallthroughBindings,
        IEnumerable<ComponentParameter>? parameters = null,
        IEnumerable<ComponentEvent>? events = null)
    {
        if (displayName is not null)
        {
            ArgumentException.ThrowIfNullOrEmpty(displayName);
        }

        if ((flags & ~ComponentFlags.InheritFallthroughBindings) != 0)
        {
            throw new ArgumentOutOfRangeException(nameof(flags));
        }

        DisplayName = displayName;
        Flags = flags;
        Parameters = CollectionSnapshot.CopyNonNull(parameters, nameof(parameters));
        Events = CollectionSnapshot.CopyNonNull(events, nameof(events));
    }

    /// <summary>Gets the optional diagnostic display name.</summary>
    public string? DisplayName { get; }

    /// <summary>Gets static input-resolution behavior.</summary>
    public ComponentFlags Flags { get; }

    /// <summary>Gets the declared input parameters.</summary>
    public IReadOnlyList<ComponentParameter> Parameters { get; }

    /// <summary>Gets the declared output events.</summary>
    public IReadOnlyList<ComponentEvent> Events { get; }
}
