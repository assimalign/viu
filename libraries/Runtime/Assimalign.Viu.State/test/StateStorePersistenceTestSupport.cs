using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

using Assimalign.Viu.Reactivity;

namespace Assimalign.Viu.State.Tests;

internal static class StateStorePersistenceTestSupport
{
    internal static StateStoreDefinition<PersistenceStateStore> CreateDefinition(
        StateStorePersistenceDescriptor? persistence = null,
        string identifier = "preferences",
        Action<PersistenceStateStore>? setup = null,
        IStateStoreSerializer<PersistenceStateStore>? serializer = null)
        => new(
            identifier,
            _ =>
            {
                PersistenceStateStore store = new(identifier);
                setup?.Invoke(store);
                return store;
            },
            serializer ?? CreateSerializer(),
            persistence ?? new StateStorePersistenceDescriptor("persisted-preferences"));

    internal static StateStoreJsonSerializer<PersistenceStateStore, PersistenceSnapshot>
        CreateSerializer()
        => new(
            static store => new PersistenceSnapshot
            {
                Count = store.State.Count,
                Title = store.State.Title,
                Profile = new PersistenceProfileSnapshot
                {
                    Theme = store.State.Profile.Theme,
                    Secret = store.State.Profile.Secret,
                },
                Tags = store.State.Tags,
            },
            static (store, snapshot) => store.Patch(
                state =>
                {
                    state.Count = snapshot.Count;
                    state.Title = snapshot.Title;
                    state.Profile.Theme = snapshot.Profile.Theme;
                    state.Profile.Secret = snapshot.Profile.Secret;
                    state.Tags = snapshot.Tags;
                }),
            PersistenceJsonContext.Default.PersistenceSnapshot);

    internal static string Payload(string state, string identifier = "preferences")
        => "{\"version\":1,\"stores\":{\"" + identifier + "\":" + state + "}}";

    internal static JsonElement ReadState(RecordingStateStorage storage, string key = "persisted-preferences")
    {
        using JsonDocument document = JsonDocument.Parse(storage.Values[key]);
        JsonElement.ObjectEnumerator entries = document.RootElement.GetProperty("stores").EnumerateObject();
        entries.MoveNext();
        return entries.Current.Value.Clone();
    }
}

internal sealed class PersistenceStateStore : StateStore<PersistenceReactiveState>
{
    internal PersistenceStateStore(string key)
        : base(
            key,
            new PersistenceReactiveState
            {
                Count = 1,
                Title = "default-title",
                Profile = new PersistenceReactiveProfile
                {
                    Theme = "light",
                    Secret = "default-secret",
                },
                Tags = ["default"],
            })
    {
    }
}

[Reactive]
internal partial class PersistenceReactiveState
{
    internal partial int Count { get; set; }

    internal partial string? Title { get; set; }

    internal partial PersistenceReactiveProfile Profile { get; set; }

    internal partial string[] Tags { get; set; }
}

[Reactive]
internal partial class PersistenceReactiveProfile
{
    internal partial string Theme { get; set; }

    internal partial string Secret { get; set; }
}

internal sealed class PersistenceSnapshot
{
    public int Count { get; set; }

    public string? Title { get; set; }

    public PersistenceProfileSnapshot Profile { get; set; } = new();

    public string[] Tags { get; set; } = [];
}

internal sealed class PersistenceProfileSnapshot
{
    public string Theme { get; set; } = string.Empty;

    public string Secret { get; set; } = string.Empty;
}

[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(PersistenceSnapshot))]
internal sealed partial class PersistenceJsonContext : JsonSerializerContext
{
}

internal sealed class RecordingStateStorage : IStateStorage, IDisposable
{
    internal Dictionary<string, string> Values { get; } = new(StringComparer.Ordinal);

    internal int ReadCalls { get; private set; }

    internal int WriteCalls { get; private set; }

    internal int RemoveCalls { get; private set; }

    internal int DisposeCalls { get; private set; }

    internal Exception? ReadFailure { get; set; }

    internal Exception? WriteFailure { get; set; }

    internal Exception? RemoveFailure { get; set; }

    public bool TryRead(string key, out string value)
    {
        ReadCalls++;
        if (ReadFailure is { } failure)
        {
            throw failure;
        }

        return Values.TryGetValue(key, out value!);
    }

    public void Write(string key, string value)
    {
        WriteCalls++;
        if (WriteFailure is { } failure)
        {
            throw failure;
        }

        Values[key] = value;
    }

    public void Remove(string key)
    {
        RemoveCalls++;
        if (RemoveFailure is { } failure)
        {
            throw failure;
        }

        Values.Remove(key);
    }

    public void Dispose() => DisposeCalls++;
}
