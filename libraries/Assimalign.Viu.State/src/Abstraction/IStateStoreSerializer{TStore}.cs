using System.Text.Json;

namespace Assimalign.Viu.State;

/// <summary>
/// Serializes and restores one state-store type through an explicitly registered, AOT-safe
/// contract. Implementations must not use reflection-backed serialization. Specified by
/// <c>[STA-9]</c> and <c>[EXE-4]</c>.
/// </summary>
/// <typeparam name="TStore">The state-store type.</typeparam>
public interface IStateStoreSerializer<in TStore>
    where TStore : class
{
    /// <summary>
    /// Writes only the registered state value for one store. Specified by
    /// <c>[STA-9]</c> and constrained by <c>[EXE-4]</c>.
    /// </summary>
    /// <param name="writer">The payload-owned JSON writer.</param>
    /// <param name="stateStore">The materialized state store.</param>
    void Serialize(Utf8JsonWriter writer, TStore stateStore);

    /// <summary>
    /// Applies one validated payload value to an already materialized store before first render.
    /// Specified by <c>[STA-9]</c>.
    /// </summary>
    /// <param name="stateStore">The materialized state store.</param>
    /// <param name="state">The JSON value captured for the store's key.</param>
    void Restore(TStore stateStore, JsonElement state);
}
