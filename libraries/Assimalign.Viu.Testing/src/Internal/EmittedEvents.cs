using System;
using System.Collections.Generic;

using Assimalign.Viu.Components;

namespace Assimalign.Viu.Testing;

internal sealed class EmittedEvents
{
    private static readonly IReadOnlyDictionary<
        string,
        IReadOnlyList<IReadOnlyList<object?>>> Empty =
        new Dictionary<string, IReadOnlyList<IReadOnlyList<object?>>>(StringComparer.Ordinal);

    private readonly Dictionary<
        ComponentContext,
        Dictionary<string, List<IReadOnlyList<object?>>>> _events =
        new(ReferenceEqualityComparer.Instance);

    internal void Record(
        ComponentContext context,
        string eventName,
        IReadOnlyList<object?> arguments)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentException.ThrowIfNullOrEmpty(eventName);
        ArgumentNullException.ThrowIfNull(arguments);
        if (!_events.TryGetValue(
            context,
            out Dictionary<string, List<IReadOnlyList<object?>>>? eventsByName))
        {
            eventsByName = new Dictionary<
                string,
                List<IReadOnlyList<object?>>>(StringComparer.Ordinal);
            _events.Add(context, eventsByName);
        }

        if (!eventsByName.TryGetValue(
            eventName,
            out List<IReadOnlyList<object?>>? occurrences))
        {
            occurrences = [];
            eventsByName.Add(eventName, occurrences);
        }

        object?[] snapshot = new object?[arguments.Count];
        for (int index = 0; index < arguments.Count; index++)
        {
            snapshot[index] = arguments[index];
        }

        occurrences.Add(snapshot);
    }

    internal IReadOnlyList<IReadOnlyList<object?>> Occurrences(
        ComponentContext? context,
        string eventName) =>
        context is not null
        && _events.TryGetValue(
            context,
            out Dictionary<string, List<IReadOnlyList<object?>>>? eventsByName)
        && eventsByName.TryGetValue(
            eventName,
            out List<IReadOnlyList<object?>>? occurrences)
            ? occurrences
            : Array.Empty<IReadOnlyList<object?>>();

    internal IReadOnlyDictionary<string, IReadOnlyList<IReadOnlyList<object?>>> All(
        ComponentContext? context)
    {
        if (context is null
            || !_events.TryGetValue(
                context,
                out Dictionary<string, List<IReadOnlyList<object?>>>? eventsByName))
        {
            return Empty;
        }

        Dictionary<string, IReadOnlyList<IReadOnlyList<object?>>> result =
            new(eventsByName.Count, StringComparer.Ordinal);
        foreach (KeyValuePair<string, List<IReadOnlyList<object?>>> pair in eventsByName)
        {
            result.Add(pair.Key, pair.Value);
        }

        return result;
    }
}
