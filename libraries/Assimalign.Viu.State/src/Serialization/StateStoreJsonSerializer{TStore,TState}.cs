using System;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;

namespace Assimalign.Viu.State;

/// <summary>
/// Implements state-store payload serialization with caller-supplied state access, restore, and
/// source-generated JSON metadata. No reflection serializer overload is reachable through this
/// type. Specified by <c>[STA-9]</c> and <c>[EXE-4]</c>.
/// </summary>
/// <typeparam name="TStore">The state-store type.</typeparam>
/// <typeparam name="TState">The serialized state shape.</typeparam>
public sealed class StateStoreJsonSerializer<TStore, TState> : IStateStoreSerializer<TStore>
    where TStore : class
{
    private readonly Func<TStore, TState> _getState;
    private readonly Action<TStore, TState> _restoreState;
    private readonly JsonTypeInfo<TState> _jsonTypeInformation;

    /// <summary>
    /// Creates a serializer from explicit typed delegates and source-generated JSON metadata.
    /// The delegates preserve arbitrary store implementations without member discovery.
    /// </summary>
    /// <remarks>Specified by <c>[STA-9]</c> and constrained by <c>[EXE-4]</c>.</remarks>
    /// <param name="getState">Gets the state value to capture.</param>
    /// <param name="restoreState">Applies a deserialized state value to the store.</param>
    /// <param name="jsonTypeInformation">
    /// The source-generated JSON metadata for <typeparamref name="TState"/>.
    /// </param>
    public StateStoreJsonSerializer(
        Func<TStore, TState> getState,
        Action<TStore, TState> restoreState,
        JsonTypeInfo<TState> jsonTypeInformation)
    {
        ArgumentNullException.ThrowIfNull(getState);
        ArgumentNullException.ThrowIfNull(restoreState);
        ArgumentNullException.ThrowIfNull(jsonTypeInformation);
        _getState = getState;
        _restoreState = restoreState;
        _jsonTypeInformation = jsonTypeInformation;
    }

    /// <inheritdoc />
    public void Serialize(Utf8JsonWriter writer, TStore stateStore)
    {
        ArgumentNullException.ThrowIfNull(writer);
        ArgumentNullException.ThrowIfNull(stateStore);
        JsonSerializer.Serialize(
            writer,
            _getState(stateStore),
            _jsonTypeInformation);
    }

    /// <inheritdoc />
    public void Restore(TStore stateStore, JsonElement state)
    {
        ArgumentNullException.ThrowIfNull(stateStore);
        TState? restoredState = state.Deserialize(_jsonTypeInformation);
        _restoreState(stateStore, restoredState!);
    }
}
