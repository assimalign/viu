using System;
using System.Collections.Generic;

namespace Assimalign.Viu.Components;

/// <summary>Describes an event a component template may emit.</summary>
/// <remarks>
/// This is Viu's AOT-safe equivalent of Vue's runtime
/// <see href="https://vuejs.org/guide/components/events.html#events-validation">emit declaration</see>.
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
