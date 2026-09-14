namespace Assimalign.Viu.State;

/// <summary>Identifies the failed persistence boundary for diagnostics. Specified by <c>[STA-11]</c>.</summary>
public enum StateStorePersistenceOperation
{
    /// <summary>The store or selected storage does not support persistence.</summary>
    Configure,

    /// <summary>Reading a complete payload failed.</summary>
    Read,

    /// <summary>Parsing, validating, or applying stored state failed.</summary>
    Restore,

    /// <summary>Capturing or writing a complete payload failed.</summary>
    Write,

    /// <summary>Discarding an invalid stored payload failed.</summary>
    Remove,
}
