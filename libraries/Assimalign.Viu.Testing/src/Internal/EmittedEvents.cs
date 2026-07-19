using System;
using System.Collections.Generic;

using Assimalign.Viu.RuntimeCore;

namespace Assimalign.Viu.Testing;

/// <summary>
/// Records component-emitted events per instance, in order, with their payloads — the capture
/// behind a wrapper's <c>Emitted()</c> (the C# port of <c>@vue/test-utils</c>'s <c>emitted()</c>,
/// https://test-utils.vuejs.org/api/#emitted). Installed as the app context's emit observer before
/// mount, so events emitted during mount are captured too.
/// </summary>
internal sealed class EmittedEvents
{
    private static readonly IReadOnlyDictionary<string, IReadOnlyList<IReadOnlyList<object?>>> EmptyByName
        = new Dictionary<string, IReadOnlyList<IReadOnlyList<object?>>>(0, StringComparer.Ordinal);

    private readonly Dictionary<ComponentInstance, Dictionary<string, List<IReadOnlyList<object?>>>> _events = [];

    /// <summary>Records one emit (the observer callback signature).</summary>
    /// <param name="instance">The emitting instance.</param>
    /// <param name="eventName">The event name.</param>
    /// <param name="arguments">The event payload.</param>
    public void Record(ComponentInstance instance, string eventName, object?[] arguments)
    {
        if (!_events.TryGetValue(instance, out var byName))
        {
            byName = new Dictionary<string, List<IReadOnlyList<object?>>>(StringComparer.Ordinal);
            _events[instance] = byName;
        }
        if (!byName.TryGetValue(eventName, out var occurrences))
        {
            occurrences = [];
            byName[eventName] = occurrences;
        }
        occurrences.Add(arguments);
    }

    /// <summary>The ordered occurrences of one event on one instance (each is the argument list).</summary>
    /// <param name="instance">The instance whose events to read.</param>
    /// <param name="eventName">The event name.</param>
    public IReadOnlyList<IReadOnlyList<object?>> Occurrences(ComponentInstance instance, string eventName)
        => _events.TryGetValue(instance, out var byName) && byName.TryGetValue(eventName, out var occurrences)
            ? occurrences
            : Array.Empty<IReadOnlyList<object?>>();

    /// <summary>All events emitted by one instance, keyed by event name.</summary>
    /// <param name="instance">The instance whose events to read.</param>
    public IReadOnlyDictionary<string, IReadOnlyList<IReadOnlyList<object?>>> All(ComponentInstance instance)
    {
        if (!_events.TryGetValue(instance, out var byName))
        {
            return EmptyByName;
        }
        var result = new Dictionary<string, IReadOnlyList<IReadOnlyList<object?>>>(byName.Count, StringComparer.Ordinal);
        foreach (var (eventName, occurrences) in byName)
        {
            result[eventName] = occurrences;
        }
        return result;
    }
}
