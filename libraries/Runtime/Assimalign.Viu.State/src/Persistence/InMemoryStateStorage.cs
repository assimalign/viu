using System;
using System.Collections.Generic;

namespace Assimalign.Viu.State;

/// <summary>
/// Retains ordinal string keys in one host-owned instance for tests and server hosts. It has no
/// global state and is not thread-safe. Specified by <c>[STA-11]</c>.
/// </summary>
public sealed class InMemoryStateStorage : IStateStorage
{
    private readonly Dictionary<string, string> _values = new(StringComparer.Ordinal);

    /// <summary>Creates an empty, independently owned storage instance. Specified by <c>[STA-11]</c>.</summary>
    public InMemoryStateStorage()
    {
    }

    /// <inheritdoc />
    public bool TryRead(string key, out string value)
    {
        ArgumentNullException.ThrowIfNull(key);
        if (_values.TryGetValue(key, out string? storedValue))
        {
            value = storedValue;
            return true;
        }

        value = string.Empty;
        return false;
    }

    /// <inheritdoc />
    public void Write(string key, string value)
    {
        ArgumentNullException.ThrowIfNull(key);
        ArgumentNullException.ThrowIfNull(value);
        _values[key] = value;
    }

    /// <inheritdoc />
    public void Remove(string key)
    {
        ArgumentNullException.ThrowIfNull(key);
        _values.Remove(key);
    }
}
