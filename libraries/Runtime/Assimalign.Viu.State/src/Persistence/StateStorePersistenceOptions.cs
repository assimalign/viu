using System;

namespace Assimalign.Viu.State;

/// <summary>
/// Composes externally owned storage instances and a diagnostic sink. Neither storage is disposed
/// by the plugin. Sharing non-thread-safe storage across concurrent hosts requires host coordination.
/// Specified by <c>[STA-11]</c>.
/// </summary>
public sealed class StateStorePersistenceOptions
{
    /// <summary>
    /// Creates immutable plugin options. An unconfigured session store disables persistence for
    /// session definitions with a diagnostic. Diagnostic sink exceptions are contained.
    /// Specified by <c>[STA-11]</c>.
    /// </summary>
    /// <param name="localStorage">The required host-owned local storage.</param>
    /// <param name="sessionStorage">The optional independent host-owned session storage.</param>
    /// <param name="diagnostic">The optional callback for contained failures.</param>
    public StateStorePersistenceOptions(
        IStateStorage localStorage,
        IStateStorage? sessionStorage = null,
        Action<StateStorePersistenceDiagnostic>? diagnostic = null)
    {
        ArgumentNullException.ThrowIfNull(localStorage);
        LocalStorage = localStorage;
        SessionStorage = sessionStorage;
        Diagnostic = diagnostic;
    }

    /// <summary>Gets externally owned local storage. Specified by <c>[STA-11]</c>.</summary>
    public IStateStorage LocalStorage { get; }

    /// <summary>Gets externally owned session storage, when configured. Specified by <c>[STA-11]</c>.</summary>
    public IStateStorage? SessionStorage { get; }

    /// <summary>Gets the optional failure callback, whose errors are contained. Specified by <c>[STA-11]</c>.</summary>
    public Action<StateStorePersistenceDiagnostic>? Diagnostic { get; }
}
