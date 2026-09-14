using System;
using System.Buffers;
using System.Collections.Generic;
using System.Runtime.ExceptionServices;
using System.Text.Json;

using Assimalign.Viu.Reactivity;

namespace Assimalign.Viu.State;

internal sealed class StateStoreEntry : IDisposable
{
    private bool _isDisposed;
    private readonly Action<Utf8JsonWriter>? _serializeState;
    private readonly Action<JsonElement>? _restoreState;
    private Dictionary<Type, object>? _extensions;
    private bool _extensionsStopped;
    private bool _instanceDisposed;

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

    internal void SetExtension<TExtension>(TExtension extension)
        where TExtension : notnull
    {
        ArgumentNullException.ThrowIfNull(extension);
        ObjectDisposedException.ThrowIf(_isDisposed || _extensionsStopped || !Scope.IsActive, this);
        if (_extensions is null)
        {
            _extensions = new Dictionary<Type, object>();
            Scope.Run(() => Reactive.OnScopeDispose(DisposeExtensions, failSilently: true));
        }

        if (!_extensions.TryAdd(typeof(TExtension), extension))
        {
            throw new InvalidOperationException(
                $"State store \"{Key}\" already has an extension for the requested type key.");
        }
    }

    internal TExtension GetExtension<TExtension>()
        where TExtension : notnull
    {
        ObjectDisposedException.ThrowIf(_isDisposed || _extensionsStopped || !Scope.IsActive, this);
        if (_extensions is null || !_extensions.TryGetValue(typeof(TExtension), out object? extension))
        {
            throw new InvalidOperationException(
                $"State store \"{Key}\" has no extension for the requested type key.");
        }

        return (TExtension)extension;
    }

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
            // The explicit fallback also supports scope factories that do not install Reactivity's
            // ambient scope, while the registered cleanup handles direct scope stops.
            DisposeExtensions();
        }
        catch (Exception exception)
        {
            error ??= ExceptionDispatchInfo.Capture(exception);
        }

        try
        {
            DisposeStore();
        }
        catch (Exception exception)
        {
            error ??= ExceptionDispatchInfo.Capture(exception);
        }

        error?.Throw();
    }

    private void DisposeExtensions()
    {
        if (_extensionsStopped)
        {
            return;
        }

        _extensionsStopped = true;
        if (_extensions is null)
        {
            return;
        }

        ExceptionDispatchInfo? error = null;
        HashSet<object> disposedExtensions = new(ReferenceEqualityComparer.Instance);
        foreach (object extension in _extensions.Values)
        {
            try
            {
                if (extension is not IDisposable disposable || !disposedExtensions.Add(extension))
                {
                    continue;
                }

                if (ReferenceEquals(extension, Instance))
                {
                    if (_instanceDisposed)
                    {
                        continue;
                    }

                    _instanceDisposed = true;
                }

                disposable.Dispose();
            }
            catch (Exception exception)
            {
                error ??= ExceptionDispatchInfo.Capture(exception);
            }
        }

        _extensions.Clear();
        error?.Throw();
    }

    private void DisposeStore()
    {
        if (_instanceDisposed || Instance is not IDisposable disposable)
        {
            return;
        }

        _instanceDisposed = true;
        disposable.Dispose();
    }

    private InvalidOperationException CreateMissingSerializerException() =>
        new(
            $"State store \"{Key}\" is materialized but has no AOT-safe "
            + "serializer registration. Supply an IStateStoreSerializer<TStore> when defining "
            + "the store before capturing or restoring an SSR state payload.");
}
