using System;

namespace Assimalign.Viu.State;

/// <summary>
/// Carries definition and storage metadata plus the exception for a contained failure. Specified by
/// <c>[STA-11]</c>.
/// </summary>
public sealed class StateStorePersistenceDiagnostic
{
    internal StateStorePersistenceDiagnostic(
        string identifier,
        StateStorePersistenceDescriptor descriptor,
        StateStorePersistenceOperation operation,
        Exception exception)
    {
        Identifier = identifier;
        Key = descriptor.Key;
        StorageKind = descriptor.StorageKind;
        Operation = operation;
        Exception = exception;
    }

    /// <summary>Gets the definition's diagnostic identifier. Specified by <c>[STA-11]</c>.</summary>
    public string Identifier { get; }

    /// <summary>Gets the storage key involved in the failure. Specified by <c>[STA-11]</c>.</summary>
    public string Key { get; }

    /// <summary>Gets the selected storage lifetime. Specified by <c>[STA-11]</c>.</summary>
    public StateStorageKind StorageKind { get; }

    /// <summary>Gets the failed persistence operation. Specified by <c>[STA-11]</c>.</summary>
    public StateStorePersistenceOperation Operation { get; }

    /// <summary>Gets the contained failure for host diagnostics. Specified by <c>[STA-11]</c>.</summary>
    public Exception Exception { get; }
}
