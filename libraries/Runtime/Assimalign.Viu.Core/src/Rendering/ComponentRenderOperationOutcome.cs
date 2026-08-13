namespace Assimalign.Viu;

/// <summary>Identifies how Core completed a host-specific component render operation.</summary>
/// <remarks>
/// A named outcome lets hosts distinguish committed output from an error handled by the component
/// chain without exposing Core's activation or error-routing implementation. Specified by
/// <c>[SSR-TARGET-3]</c>.
/// </remarks>
public enum ComponentRenderOperationOutcome
{
    /// <summary>The operation completed and its output may be committed.</summary>
    Succeeded,

    /// <summary>
    /// The operation failed, but the component error chain handled the failure; any transactional
    /// output from the operation must be discarded.
    /// </summary>
    HandledFailure,
}
