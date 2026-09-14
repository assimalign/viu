using System;
using System.Collections.Generic;

namespace Assimalign.Viu.State;

/// <summary>
/// Provides immutable definition-local persistence selection. Paths name ordinal JSON object
/// members separated by dots; arrays are selected as whole members. Exclusion wins over inclusion.
/// Specified by <c>[STA-11]</c>.
/// </summary>
public sealed class StateStorePersistenceDescriptor
{
    /// <summary>
    /// Copies persistence settings so later changes to input collections cannot change a
    /// definition's behavior. Empty include paths select the whole JSON state object.
    /// Specified by <c>[STA-11]</c>.
    /// </summary>
    /// <param name="key">The non-empty storage key, independent of the definition's identifier.</param>
    /// <param name="storageKind">The host-composed storage lifetime.</param>
    /// <param name="includePaths">Optional member paths to retain; missing paths are ignored.</param>
    /// <param name="excludePaths">Optional member paths to discard on both capture and restore.</param>
    public StateStorePersistenceDescriptor(
        string key,
        StateStorageKind storageKind = StateStorageKind.Local,
        IEnumerable<string>? includePaths = null,
        IEnumerable<string>? excludePaths = null)
    {
        ArgumentException.ThrowIfNullOrEmpty(key);
        if (storageKind is not StateStorageKind.Local and not StateStorageKind.Session)
        {
            throw new ArgumentOutOfRangeException(nameof(storageKind));
        }

        Key = key;
        StorageKind = storageKind;
        IncludePaths = CopyPaths(includePaths, nameof(includePaths));
        ExcludePaths = CopyPaths(excludePaths, nameof(excludePaths));
    }

    /// <summary>Gets the non-empty storage key. Specified by <c>[STA-11]</c>.</summary>
    public string Key { get; }

    /// <summary>Gets the selected storage lifetime. Specified by <c>[STA-11]</c>.</summary>
    public StateStorageKind StorageKind { get; }

    /// <summary>Gets the immutable included JSON paths; empty means all. Specified by <c>[STA-11]</c>.</summary>
    public IReadOnlyList<string> IncludePaths { get; }

    /// <summary>Gets the immutable excluded JSON paths. Specified by <c>[STA-11]</c>.</summary>
    public IReadOnlyList<string> ExcludePaths { get; }

    private static IReadOnlyList<string> CopyPaths(IEnumerable<string>? paths, string parameterName)
    {
        List<string> copies = new();
        if (paths is not null)
        {
            foreach (string path in paths)
            {
                if (string.IsNullOrEmpty(path) || path.StartsWith('.') || path.EndsWith('.')
                    || path.Contains("..", StringComparison.Ordinal))
                {
                    throw new ArgumentException("JSON member paths must have non-empty dot-separated segments.", parameterName);
                }

                copies.Add(path);
            }
        }

        return copies.AsReadOnly();
    }
}
