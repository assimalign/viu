using System;
using System.Text.Json;
using System.Text.Json.Nodes;

using Assimalign.Viu.Reactivity;

namespace Assimalign.Viu.State;

/// <summary>
/// Restores definition-selected JSON state during creation and persists changed selections through
/// the store's existing pre-flush subscription. All persistence failures are contained and reported.
/// This plugin is not thread-safe and uses the registry's single event loop. Specified by
/// <c>[STA-10]</c> and <c>[STA-11]</c> ([V01.01.09.04]).
/// </summary>
/// <remarks>
/// The serializer must emit a JSON object whose setup-default member names define the recognized
/// persistence shape, including optional members at their default values. Hosts assign distinct
/// storage keys to definitions sharing a storage area. Arbitrary dynamic object keys are not
/// discovered; typed serializers validate nullable branches and array elements.
/// </remarks>
public sealed class StateStorePersistencePlugin : IStateStorePlugin
{
    private readonly StateStorePersistenceOptions _options;

    /// <summary>
    /// Creates a plugin with explicitly composed storage; storage ownership stays with the host.
    /// Register one persistence plugin per registry before resolving participating stores.
    /// Specified by <c>[STA-11]</c>.
    /// </summary>
    /// <param name="options">The immutable host storage and diagnostic options.</param>
    public StateStorePersistencePlugin(StateStorePersistenceOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        _options = options;
    }

    /// <summary>
    /// Restores opted-in state before creation notifications and attaches a scope-owned mutation
    /// handler. Unselected definitions do nothing. Unsupported stores or missing storage report a
    /// configuration diagnostic and remain usable without persistence. Specified by <c>[STA-11]</c>.
    /// </summary>
    /// <param name="context">The registry-owned store creation context.</param>
    public void Apply(StateStorePluginContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (context.Persistence is not { } descriptor)
        {
            return;
        }

        Reactive.PauseTracking();
        try
        {
            IStateStorage storage = descriptor.StorageKind == StateStorageKind.Local
                ? _options.LocalStorage
                : _options.SessionStorage
                    ?? throw new InvalidOperationException("Session state storage has not been configured.");
            if (context.Store is not IStateStoreMutationSource mutations)
            {
                throw new InvalidOperationException("Persistence requires a StateStore<TState> with mutation subscriptions.");
            }

            JsonElement defaults = context.Entry.SerializeState();
            JsonObject defaultObject = StateStorePersistenceJson.ReadObject(defaults);
            Restore(context, descriptor, storage, defaults, defaultObject);
            string previousSelection = Capture(context, descriptor);
            mutations.SubscribeMutation(() =>
            {
                Reactive.PauseTracking();
                try
                {
                    string selected = Capture(context, descriptor);
                    if (string.Equals(selected, previousSelection, StringComparison.Ordinal))
                    {
                        return;
                    }

                    storage.Write(descriptor.Key, StateStorePersistenceJson.CreatePayload(context.Identifier, selected));
                    previousSelection = selected;
                }
                catch (Exception exception)
                {
                    Report(context, descriptor, StateStorePersistenceOperation.Write, exception);
                }
                finally
                {
                    Reactive.ResetTracking();
                }
            });
        }
        catch (Exception exception)
        {
            Report(context, descriptor, StateStorePersistenceOperation.Configure, exception);
        }
        finally
        {
            Reactive.ResetTracking();
        }
    }

    private static string Capture(StateStorePluginContext context, StateStorePersistenceDescriptor descriptor)
        => StateStorePersistenceJson.Select(
            StateStorePersistenceJson.ReadObject(context.Entry.SerializeState()), descriptor).ToJsonString();

    private void Restore(
        StateStorePluginContext context,
        StateStorePersistenceDescriptor descriptor,
        IStateStorage storage,
        JsonElement defaults,
        JsonObject defaultObject)
    {
        string stored;
        try
        {
            if (!storage.TryRead(descriptor.Key, out stored))
            {
                return;
            }
        }
        catch (Exception exception)
        {
            Report(context, descriptor, StateStorePersistenceOperation.Read, exception);
            return;
        }

        bool restoreAttempted = false;
        try
        {
            StateStorePayload payload = StateStorePayload.Parse(stored);
            if (payload.StoreKeys.Count != 1 || !payload.TryGetState(context.Identifier, out JsonElement state))
            {
                throw new JsonException("A persisted payload must contain exactly the current definition's key.");
            }

            JsonObject selected = StateStorePersistenceJson.Select(StateStorePersistenceJson.ReadObject(state), descriptor);
            JsonElement merged = StateStorePersistenceJson.ToElement(StateStorePersistenceJson.Merge(defaultObject, selected));
            restoreAttempted = true;
            context.Entry.RestoreState(merged);
        }
        catch (Exception exception)
        {
            Exception failure = exception;
            if (restoreAttempted)
            {
                try
                {
                    // A custom applier can throw after changing state. Restore its captured defaults
                    // through the same explicit seam; never inspect or replace the store by reflection.
                    context.Entry.RestoreState(defaults);
                }
                catch (Exception rollbackException)
                {
                    failure = new AggregateException("Persistence restore and default recovery failed.", exception, rollbackException);
                }
            }

            Report(context, descriptor, StateStorePersistenceOperation.Restore, failure);
            try
            {
                storage.Remove(descriptor.Key);
            }
            catch (Exception removeException)
            {
                Report(context, descriptor, StateStorePersistenceOperation.Remove, removeException);
            }
        }
    }

    private void Report(
        StateStorePluginContext context,
        StateStorePersistenceDescriptor descriptor,
        StateStorePersistenceOperation operation,
        Exception exception)
    {
        try
        {
            _options.Diagnostic?.Invoke(new StateStorePersistenceDiagnostic(context.Identifier, descriptor, operation, exception));
        }
        catch
        {
            // Diagnostics cannot turn optional persistence into an application failure [STA-11].
        }
    }
}
