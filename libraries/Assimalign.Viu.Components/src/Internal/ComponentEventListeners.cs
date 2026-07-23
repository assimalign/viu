using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace Assimalign.Viu.Components;

internal static class ComponentEventListeners
{
    internal static IReadOnlyDictionary<string, ComponentEventListener>? Copy(
        IReadOnlyDictionary<string, ComponentEventListener>? listeners)
    {
        if (listeners is null || listeners.Count == 0)
        {
            return null;
        }

        Dictionary<string, ComponentEventListener> snapshot =
            new(listeners.Count, StringComparer.Ordinal);
        foreach (KeyValuePair<string, ComponentEventListener> listener in listeners)
        {
            ArgumentException.ThrowIfNullOrEmpty(listener.Key);
            ArgumentNullException.ThrowIfNull(listener.Value);
            snapshot.Add(listener.Key, listener.Value);
        }

        return new ReadOnlyDictionary<string, ComponentEventListener>(snapshot);
    }
}
