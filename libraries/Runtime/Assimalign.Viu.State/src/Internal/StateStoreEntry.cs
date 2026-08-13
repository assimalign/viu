using System;
using System.Buffers;
using System.Runtime.ExceptionServices;
using System.Text.Json;

using Assimalign.Viu.Reactivity;

namespace Assimalign.Viu.State;

internal sealed class StateStoreEntry : IDisposable
{
    private bool _isDisposed;
    private readonly Action<Utf8JsonWriter>? _serializeState;
    private readonly Action<JsonElement>? _restoreState;

    internal StateStoreEntry(
        string key,
        object definition,
        object instance,
        IReactiveEffectScope scope,
        Action<Utf8JsonWriter>? serializeState,
        Action<JsonElement>? restoreState)
    {
        Key = key;
        Definition = definition;
        Instance = instance;
        Scope = scope;
        _serializeState = serializeState;
        _restoreState = restoreState;
    }

    internal object Definition { get; }

    internal string Key { get; }

    internal object Instance { get; }

    internal IReactiveEffectScope Scope { get; }

    internal JsonElement SerializeState()
    {
        if (_serializeState is null)
        {
            throw CreateMissingSerializerException();
        }

        ArrayBufferWriter<byte> buffer = new();
        using (Utf8JsonWriter writer = new(buffer))
        {
            _serializeState(writer);
        }

        using JsonDocument document = JsonDocument.Parse(buffer.WrittenMemory);
        return document.RootElement.Clone();
    }

    internal void RestoreState(JsonElement state)
    {
        if (_restoreState is null)
        {
            throw CreateMissingSerializerException();
        }

        _restoreState(state);
    }

    public void Dispose()
    {
        if (_isDisposed)
        {
            return;
        }

        _isDisposed = true;
        ExceptionDispatchInfo? error = null;
        try
        {
            Scope.Stop();
        }
        catch (Exception exception)
        {
            error = ExceptionDispatchInfo.Capture(exception);
        }

        try
        {
            if (Instance is IDisposable disposable)
            {
                disposable.Dispose();
            }
        }
        catch (Exception exception)
        {
            error ??= ExceptionDispatchInfo.Capture(exception);
        }

        error?.Throw();
    }

    private InvalidOperationException CreateMissingSerializerException() =>
        new(
            $"State store \"{Key}\" is materialized but has no AOT-safe "
            + "serializer registration. Supply an IStateStoreSerializer<TStore> when defining "
            + "the store before capturing or restoring an SSR state payload.");
}
