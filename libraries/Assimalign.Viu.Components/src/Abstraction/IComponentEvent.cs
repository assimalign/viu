using System;
using System.Collections.Generic;

namespace Assimalign.Viu.Components;

/// <summary>Describes an event a component template may emit.</summary>
/// <remarks>
/// A declaration is a static value rather than reflection over the template type, so what a
/// component emits is known without runtime type inspection — the property that keeps event
/// dispatch trimming- and AOT-safe. Declaring an event is also what makes a parent's matching
/// <c>onX</c> property a consumed component event instead of a fallthrough attribute. Specified by
/// <c>[CMP-14]</c> and <c>[CMP-17]</c>.
/// </remarks>
public interface IComponentEvent
{
    /// <summary>Gets the event name.</summary>
    string Name { get; }

    /// <summary>
    /// Gets the optional validator. Returning <see langword="false"/> produces a development
    /// warning without preventing dispatch.
    /// </summary>
    Func<IReadOnlyList<object?>, bool>? Validator { get; }
}
