using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;

namespace Assimalign.Viu.Components;

/// <summary>
/// The immutable storage behind every string-keyed snapshot a component invocation or its resolved
/// bindings hold. The dictionary is owned exclusively by the snapshot and never escapes, so an
/// instance is safe to share: a snapshot handed back into an invocation or bindings constructor is
/// adopted as-is instead of being copied again, which is what lets contract resolution hand its
/// result tables over without a second copy per component update. Mutating members throw
/// <see cref="NotSupportedException"/>. Specified by <c>[CMP-2]</c>.
/// </summary>
/// <typeparam name="TValue">The value type.</typeparam>
internal sealed class SnapshotDictionary<TValue> : IReadOnlyDictionary<string, TValue>, IDictionary<string, TValue>
{
    /// <summary>
    /// The shared empty snapshot. It is immutable and holds no caller-owned collection, so every
    /// empty invocation or bindings surface can share it. Pinned by <c>[V01.01.11.07]</c>.
    /// </summary>
    internal static readonly SnapshotDictionary<TValue> Empty =
        new(new Dictionary<string, TValue>(StringComparer.Ordinal), isNullFree: true);

    private readonly Dictionary<string, TValue> _entries;

    /// <summary>Wraps <paramref name="entries"/>, which the caller must not retain or mutate.</summary>
    /// <param name="entries">The ordinal-keyed entries, owned by the snapshot from this point on.</param>
    /// <param name="isNullFree">Whether every value has already been verified non-null.</param>
    internal SnapshotDictionary(Dictionary<string, TValue> entries, bool isNullFree)
    {
        _entries = entries;
        IsNullFree = isNullFree;
    }

    /// <summary>
    /// Whether every value has already been verified non-null, so a consumer that requires
    /// non-null values can adopt the snapshot without scanning it again.
    /// </summary>
    internal bool IsNullFree { get; }

    /// <summary>The owned entries, exposed only so a sibling snapshot can share the same storage.</summary>
    internal Dictionary<string, TValue> Entries => _entries;

    /// <inheritdoc/>
    public int Count => _entries.Count;

    /// <inheritdoc/>
    public TValue this[string key] => _entries[key];

    TValue IDictionary<string, TValue>.this[string key]
    {
        get => _entries[key];
        set => throw CreateReadOnlyException();
    }

    /// <inheritdoc/>
    public IEnumerable<string> Keys => _entries.Keys;

    /// <inheritdoc/>
    public IEnumerable<TValue> Values => _entries.Values;

    ICollection<string> IDictionary<string, TValue>.Keys => _entries.Keys;

    ICollection<TValue> IDictionary<string, TValue>.Values => _entries.Values;

    bool ICollection<KeyValuePair<string, TValue>>.IsReadOnly => true;

    /// <inheritdoc/>
    public bool ContainsKey(string key) => _entries.ContainsKey(key);

    /// <inheritdoc/>
    public bool TryGetValue(string key, [MaybeNullWhen(false)] out TValue value) =>
        _entries.TryGetValue(key, out value);

    /// <inheritdoc/>
    public IEnumerator<KeyValuePair<string, TValue>> GetEnumerator() => _entries.GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => _entries.GetEnumerator();

    bool ICollection<KeyValuePair<string, TValue>>.Contains(KeyValuePair<string, TValue> item) =>
        ((ICollection<KeyValuePair<string, TValue>>)_entries).Contains(item);

    void ICollection<KeyValuePair<string, TValue>>.CopyTo(KeyValuePair<string, TValue>[] array, int arrayIndex) =>
        ((ICollection<KeyValuePair<string, TValue>>)_entries).CopyTo(array, arrayIndex);

    void IDictionary<string, TValue>.Add(string key, TValue value) => throw CreateReadOnlyException();

    bool IDictionary<string, TValue>.Remove(string key) => throw CreateReadOnlyException();

    void ICollection<KeyValuePair<string, TValue>>.Add(KeyValuePair<string, TValue> item) =>
        throw CreateReadOnlyException();

    void ICollection<KeyValuePair<string, TValue>>.Clear() => throw CreateReadOnlyException();

    bool ICollection<KeyValuePair<string, TValue>>.Remove(KeyValuePair<string, TValue> item) =>
        throw CreateReadOnlyException();

    private static NotSupportedException CreateReadOnlyException() =>
        new("The collection snapshot is read-only.");
}
