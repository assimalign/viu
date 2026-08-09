namespace Assimalign.Viu.State;

/// <summary>
/// Captures and restores the explicitly registered state of a registry's materialized stores.
/// Payload operations use only each definition's typed serializer and never inspect a state shape.
/// Specified by <c>[STA-9]</c> and constrained by <c>[EXE-4]</c>.
/// </summary>
public interface IStateStorePayloadRegistry
{
    /// <summary>
    /// Captures one immutable, versioned payload containing exactly the stores currently
    /// materialized in this registry. Every included definition must have an explicitly
    /// registered serializer.
    /// </summary>
    /// <remarks>Specified by <c>[STA-9]</c> and constrained by <c>[EXE-4]</c>.</remarks>
    /// <returns>The captured state-store payload.</returns>
    /// <exception cref="System.InvalidOperationException">
    /// A materialized store has no serializer registration.
    /// </exception>
    StateStorePayload CapturePayload();

    /// <summary>
    /// Restores matching materialized stores immediately and stages the remaining entries so a
    /// matching store created later is restored before <c>GetOrCreate</c> returns it.
    /// </summary>
    /// <remarks>Specified by <c>[STA-9]</c>.</remarks>
    /// <param name="payload">The validated state-store payload.</param>
    /// <exception cref="System.InvalidOperationException">
    /// A matching materialized store has no serializer registration.
    /// </exception>
    void RestorePayload(StateStorePayload payload);
}
