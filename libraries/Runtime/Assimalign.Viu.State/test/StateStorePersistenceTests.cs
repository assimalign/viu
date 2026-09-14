using System;
using System.Collections.Generic;
using System.Text.Json;

using Shouldly;
using Xunit;

using Assimalign.Viu.Reactivity;

namespace Assimalign.Viu.State.Tests;

// [V01.01.09.04], [STA-11]: persistence is definition-local, uses [STA-9] serializers,
// and shares the store's [STA-7] notification schedule and [STA-3] lifetime.
public sealed class StateStorePersistenceTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Create_PersistedState_RestoresBeforeSetupSubscriberObservesAnyNotification(bool scheduled)
    {
        RecordingStateStorage storage = new();
        storage.Values["persisted-preferences"] = StateStorePersistenceTestSupport.Payload(
            "{\"count\":17,\"title\":\"restored\",\"profile\":{\"theme\":\"dark\"}}");
        TestReactiveWatchScheduler? scheduler = scheduled ? new() : null;
        List<(int Count, string? Title, string Theme)> observed = [];
        using StateStoreRegistry registry = StateStoreTestSupport.CreateRegistry(scheduler);
        registry.Use(CreatePlugin(storage));
        StateStoreDefinition<PersistenceStateStore> definition =
            StateStorePersistenceTestSupport.CreateDefinition(
                setup: store => store.Subscribe(
                    (_, state) => observed.Add((state.Count, state.Title, state.Profile.Theme))));

        PersistenceStateStore store = registry.GetOrCreate(definition);
        scheduler?.RunUntilIdle();

        store.State.Count.ShouldBe(17);
        store.State.Title.ShouldBe("restored");
        store.State.Profile.Theme.ShouldBe("dark");
        store.State.Profile.Secret.ShouldBe("default-secret");
        observed.ShouldNotBeEmpty();
        observed.ShouldAllBe(value => value.Count == 17 && value.Title == "restored" && value.Theme == "dark");
        storage.WriteCalls.ShouldBe(0);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Create_EarlierPluginSubscriber_ObservesOnlyFullyRestoredState(bool scheduled)
    {
        RecordingStateStorage storage = new();
        storage.Values["persisted-preferences"] = StateStorePersistenceTestSupport.Payload(
            "{\"count\":9,\"title\":\"ready\"}");
        TestReactiveWatchScheduler? scheduler = scheduled ? new() : null;
        List<(int Count, string? Title)> observed = [];
        using StateStoreRegistry registry = StateStoreTestSupport.CreateRegistry(scheduler);
        registry.Use(new PersistenceObserverPlugin(observed));
        registry.Use(CreatePlugin(storage));

        registry.GetOrCreate(StateStorePersistenceTestSupport.CreateDefinition());
        scheduler?.RunUntilIdle();

        observed.ShouldNotBeEmpty();
        observed.ShouldAllBe(value => value.Count == 9 && value.Title == "ready");
        storage.WriteCalls.ShouldBe(0);
    }

    [Fact]
    public void Mutate_DirectWritesAndPatchBeforeFlush_WritesOneCompletePayload()
    {
        RecordingStateStorage storage = new();
        TestReactiveWatchScheduler scheduler = new();
        using StateStoreRegistry registry = StateStoreTestSupport.CreateRegistry(scheduler);
        registry.Use(CreatePlugin(storage));
        PersistenceStateStore store = registry.GetOrCreate(StateStorePersistenceTestSupport.CreateDefinition());
        int notifications = 0;
        store.Subscribe((_, _) => notifications++);

        store.State.Count = 2;
        store.State.Count = 3;
        store.Patch(state =>
        {
            state.Count = 4;
            state.Title = "patched";
            state.Profile.Theme = "dark";
        });

        storage.WriteCalls.ShouldBe(0);
        scheduler.PendingCount.ShouldBe(1);
        scheduler.RunUntilIdle();

        notifications.ShouldBe(1);
        storage.WriteCalls.ShouldBe(1);
        JsonElement state = StateStorePersistenceTestSupport.ReadState(storage);
        state.GetProperty("count").GetInt32().ShouldBe(4);
        state.GetProperty("title").GetString().ShouldBe("patched");
        state.GetProperty("profile").GetProperty("theme").GetString().ShouldBe("dark");
        StateStorePayload.Parse(storage.Values["persisted-preferences"]).StoreKeys.ShouldBe(["preferences"]);

        store.State.Count = 5;
        scheduler.RunUntilIdle();
        storage.WriteCalls.ShouldBe(2);
    }

    [Fact]
    public void Mutate_WithoutScheduler_DirectWritesAreImmediateAndPatchWritesOnce()
    {
        RecordingStateStorage storage = new();
        using StateStoreRegistry registry = StateStoreTestSupport.CreateRegistry();
        registry.Use(CreatePlugin(storage));
        PersistenceStateStore store = registry.GetOrCreate(StateStorePersistenceTestSupport.CreateDefinition());

        store.State.Count = 2;
        storage.WriteCalls.ShouldBe(1);
        store.State.Count = 3;
        storage.WriteCalls.ShouldBe(2);
        store.Patch(state =>
        {
            state.Count = 4;
            state.Title = "batched";
        });

        storage.WriteCalls.ShouldBe(3);
        StateStorePersistenceTestSupport.ReadState(storage).GetProperty("title").GetString().ShouldBe("batched");
    }

    [Fact]
    public void Create_StoreWithoutDescriptor_DoesNotReadOrWriteStorage()
    {
        RecordingStateStorage storage = new();
        using StateStoreRegistry registry = StateStoreTestSupport.CreateRegistry();
        registry.Use(CreatePlugin(storage));
        StateStoreDefinition<PersistenceStateStore> definition = StateStores.Define(
            "ordinary",
            static () => new PersistenceStateStore("ordinary"),
            StateStorePersistenceTestSupport.CreateSerializer());

        registry.GetOrCreate(definition).State.Count = 8;

        storage.ReadCalls.ShouldBe(0);
        storage.WriteCalls.ShouldBe(0);
        storage.RemoveCalls.ShouldBe(0);
    }

    [Fact]
    public void Mutate_NestedIncludesAndExcludes_WritesOnlySelectedJsonMemberPaths()
    {
        RecordingStateStorage storage = new();
        StateStorePersistenceDescriptor descriptor = new(
            "persisted-preferences",
            includePaths: ["count", "profile", "tags"],
            excludePaths: ["profile.secret"]);
        TestReactiveWatchScheduler scheduler = new();
        using StateStoreRegistry registry = StateStoreTestSupport.CreateRegistry(scheduler);
        registry.Use(CreatePlugin(storage));
        PersistenceStateStore store = registry.GetOrCreate(
            StateStorePersistenceTestSupport.CreateDefinition(descriptor));

        store.Patch(state =>
        {
            state.Count = 5;
            state.Title = "not selected";
            state.Profile.Theme = "dark";
            state.Profile.Secret = "never persist";
            state.Tags = ["first", "second"];
        });
        scheduler.RunUntilIdle();

        storage.WriteCalls.ShouldBe(1);
        JsonElement state = StateStorePersistenceTestSupport.ReadState(storage);
        state.GetProperty("count").GetInt32().ShouldBe(5);
        state.TryGetProperty("title", out _).ShouldBeFalse();
        state.GetProperty("profile").GetProperty("theme").GetString().ShouldBe("dark");
        state.GetProperty("profile").TryGetProperty("secret", out _).ShouldBeFalse();
        state.GetProperty("tags").GetArrayLength().ShouldBe(2);
        storage.Values["persisted-preferences"].ShouldNotContain("never persist");
    }

    [Fact]
    public void Create_FilteredPayload_MergesSelectedNestedMembersOntoFreshDefaults()
    {
        RecordingStateStorage storage = new();
        storage.Values["persisted-preferences"] = StateStorePersistenceTestSupport.Payload(
            "{\"count\":44,\"title\":\"ignored\",\"profile\":{\"theme\":\"dark\",\"secret\":\"ignored\"}}");
        StateStorePersistenceDescriptor descriptor = new(
            "persisted-preferences",
            includePaths: ["count", "profile"],
            excludePaths: ["profile.secret"]);
        using StateStoreRegistry registry = StateStoreTestSupport.CreateRegistry();
        registry.Use(CreatePlugin(storage));

        PersistenceStateStore store = registry.GetOrCreate(
            StateStorePersistenceTestSupport.CreateDefinition(descriptor));

        store.State.Count.ShouldBe(44);
        store.State.Title.ShouldBe("default-title");
        store.State.Profile.Theme.ShouldBe("dark");
        store.State.Profile.Secret.ShouldBe("default-secret");
        store.State.Tags.ShouldBe(["default"]);
        storage.WriteCalls.ShouldBe(0);
        storage.RemoveCalls.ShouldBe(0);
    }

    [Fact]
    public void Mutate_ExcludedStateOrUnchangedSelectedJson_DoesNotWrite()
    {
        RecordingStateStorage storage = new();
        TestReactiveWatchScheduler scheduler = new();
        using StateStoreRegistry registry = StateStoreTestSupport.CreateRegistry(scheduler);
        registry.Use(CreatePlugin(storage));
        PersistenceStateStore store = registry.GetOrCreate(
            StateStorePersistenceTestSupport.CreateDefinition(
                new StateStorePersistenceDescriptor("persisted-preferences", includePaths: ["count"])));

        store.State.Title = "changed but omitted";
        scheduler.RunUntilIdle();
        storage.WriteCalls.ShouldBe(0);

        store.State.Count = 8;
        store.State.Count = 1;
        scheduler.RunUntilIdle();
        storage.WriteCalls.ShouldBe(0);

        store.State.Count = 9;
        scheduler.RunUntilIdle();
        storage.WriteCalls.ShouldBe(1);
    }

    [Fact]
    public void Paths_UnknownCaseAndArrayDescendants_AreIgnoredWithoutSelectingExtraMembers()
    {
        RecordingStateStorage storage = new();
        storage.Values["persisted-preferences"] = StateStorePersistenceTestSupport.Payload(
            "{\"count\":7,\"title\":\"unselected\",\"tags\":[\"restored\"]}");
        List<StateStorePersistenceDiagnostic> diagnostics = [];
        using StateStoreRegistry registry = StateStoreTestSupport.CreateRegistry();
        registry.Use(CreatePlugin(storage, diagnostics.Add));
        PersistenceStateStore store = registry.GetOrCreate(
            StateStorePersistenceTestSupport.CreateDefinition(
                new StateStorePersistenceDescriptor(
                    "persisted-preferences", includePaths: ["count", "Title", "tags.0", "missing"])));

        store.State.Count.ShouldBe(7);
        store.State.Title.ShouldBe("default-title");
        store.State.Tags.ShouldBe(["default"]);
        store.State.Count = 8;

        JsonElement state = StateStorePersistenceTestSupport.ReadState(storage);
        state.GetProperty("count").GetInt32().ShouldBe(8);
        state.TryGetProperty("title", out _).ShouldBeFalse();
        state.TryGetProperty("tags", out _).ShouldBeFalse();
        state.TryGetProperty("missing", out _).ShouldBeFalse();
        diagnostics.ShouldBeEmpty();
    }

    [Fact]
    public void Create_InsideReactiveEffect_SerializerDoesNotTrackStoreStateIntoCallerEffect()
    {
        RecordingStateStorage storage = new();
        TestReactiveWatchScheduler scheduler = new();
        using StateStoreRegistry registry = StateStoreTestSupport.CreateRegistry(scheduler);
        registry.Use(CreatePlugin(storage));
        StateStoreDefinition<PersistenceStateStore> definition = StateStorePersistenceTestSupport.CreateDefinition();
        int effectRuns = 0;
        using EffectScope componentScope = Reactive.EffectScope();
        componentScope.Run(() => Reactive.Effect(() =>
        {
            registry.GetOrCreate(definition);
            effectRuns++;
        }));

        registry.GetOrCreate(definition).State.Count = 8;
        scheduler.RunUntilIdle();

        effectRuns.ShouldBe(1);
        storage.WriteCalls.ShouldBe(1);
    }

    [Fact]
    public void Mutate_SeveralChangesBeforeFlush_SerializesOnceForTheStorageWrite()
    {
        RecordingStateStorage storage = new();
        CountingPersistenceSerializer serializer = new();
        TestReactiveWatchScheduler scheduler = new();
        using StateStoreRegistry registry = StateStoreTestSupport.CreateRegistry(scheduler);
        registry.Use(CreatePlugin(storage));
        PersistenceStateStore store = registry.GetOrCreate(
            StateStorePersistenceTestSupport.CreateDefinition(serializer: serializer));
        int initialCaptures = serializer.SerializeCalls;

        store.State.Count = 5;
        store.State.Title = "changed";
        store.Patch(state => state.Profile.Theme = "dark");
        scheduler.RunUntilIdle();

        serializer.SerializeCalls.ShouldBe(initialCaptures + 1);
        storage.WriteCalls.ShouldBe(1);
    }

    [Fact]
    public void Create_CustomRestoreMutatesThenThrows_RestoresBaselineAndDiscardsPayload()
    {
        RecordingStateStorage storage = new();
        storage.Values["persisted-preferences"] = StateStorePersistenceTestSupport.Payload("{\"count\":99}");
        CountingPersistenceSerializer serializer = new()
        {
            RestoreFailure = new InvalidOperationException("restore failed after mutation"),
        };
        List<StateStorePersistenceDiagnostic> diagnostics = [];
        List<int> observed = [];
        using StateStoreRegistry registry = StateStoreTestSupport.CreateRegistry();
        registry.Use(CreatePlugin(storage, diagnostics.Add));

        PersistenceStateStore store = registry.GetOrCreate(
            StateStorePersistenceTestSupport.CreateDefinition(
                setup: candidate => candidate.Subscribe((_, state) => observed.Add(state.Count)),
                serializer: serializer));

        store.State.Count.ShouldBe(1);
        serializer.RestoreCalls.ShouldBe(2);
        observed.ShouldAllBe(value => value == 1);
        storage.RemoveCalls.ShouldBe(1);
        diagnostics.Count.ShouldBe(1);
        diagnostics[0].Operation.ShouldBe(StateStorePersistenceOperation.Restore);
        diagnostics[0].Exception.ShouldBeSameAs(serializer.RestoreFailure);
        storage.WriteCalls.ShouldBe(0);
    }

    [Fact]
    public void Mutate_CustomSerializerThrows_ReportsWriteFailureAndLeavesStoreUsable()
    {
        RecordingStateStorage storage = new();
        CountingPersistenceSerializer serializer = new();
        List<StateStorePersistenceDiagnostic> diagnostics = [];
        using StateStoreRegistry registry = StateStoreTestSupport.CreateRegistry();
        registry.Use(CreatePlugin(storage, diagnostics.Add));
        PersistenceStateStore store = registry.GetOrCreate(
            StateStorePersistenceTestSupport.CreateDefinition(serializer: serializer));
        serializer.SerializeFailure = new InvalidOperationException("capture failed");

        Should.NotThrow(() => store.State.Count = 9);

        store.State.Count.ShouldBe(9);
        diagnostics.Count.ShouldBe(1);
        diagnostics[0].Operation.ShouldBe(StateStorePersistenceOperation.Write);
        diagnostics[0].Exception.ShouldBeSameAs(serializer.SerializeFailure);
        storage.WriteCalls.ShouldBe(0);
    }

    [Fact]
    public void Create_ExplicitNullForNullableMember_PassesThroughTypedSerializer()
    {
        RecordingStateStorage storage = new();
        storage.Values["persisted-preferences"] = StateStorePersistenceTestSupport.Payload("{\"title\":null}");
        using StateStoreRegistry registry = StateStoreTestSupport.CreateRegistry();
        registry.Use(CreatePlugin(storage));

        PersistenceStateStore store = registry.GetOrCreate(StateStorePersistenceTestSupport.CreateDefinition());

        store.State.Title.ShouldBeNull();
        store.State.Count.ShouldBe(1);
        storage.RemoveCalls.ShouldBe(0);
    }

    [Theory]
    [InlineData("not json")]
    [InlineData("null")]
    [InlineData("{\"version\":2,\"stores\":{\"preferences\":{}}}")]
    [InlineData("{\"version\":1,\"stores\":{}}")]
    [InlineData("{\"version\":1,\"stores\":{\"other\":{}}}")]
    [InlineData("{\"version\":1,\"stores\":{\"preferences\":{},\"other\":{}}}")]
    [InlineData("{\"version\":1,\"version\":1,\"stores\":{\"preferences\":{}}}")]
    [InlineData("{\"version\":1,\"stores\":{\"preferences\":{}},\"unexpected\":true}")]
    [InlineData("{\"version\":1,\"stores\":{\"preferences\":{\"count\":\"wrong type\"}}}")]
    [InlineData("{\"version\":1,\"stores\":{\"preferences\":{\"unknownMember\":5}}}")]
    [InlineData("{\"version\":1,\"stores\":{\"preferences\":{\"count\":2,\"count\":3}}}")]
    [InlineData("{\"version\":1,\"stores\":{\"preferences\":{\"profile\":{\"unknownMember\":5}}}}")]
    [InlineData("{\"version\":1,\"stores\":{\"preferences\":{\"tags\":{\"0\":\"wrong shape\"}}}}")]
    public void Create_CorruptOrSchemaMismatchedPayload_DiscardsAndReportsWithoutChangingDefaults(string payload)
    {
        RecordingStateStorage storage = new();
        storage.Values["persisted-preferences"] = payload;
        List<StateStorePersistenceDiagnostic> diagnostics = [];
        using StateStoreRegistry registry = StateStoreTestSupport.CreateRegistry();
        registry.Use(CreatePlugin(storage, diagnostics.Add));

        PersistenceStateStore store = registry.GetOrCreate(StateStorePersistenceTestSupport.CreateDefinition());

        store.State.Count.ShouldBe(1);
        store.State.Title.ShouldBe("default-title");
        store.State.Profile.Theme.ShouldBe("light");
        storage.RemoveCalls.ShouldBe(1);
        storage.Values.ContainsKey("persisted-preferences").ShouldBeFalse();
        diagnostics.Count.ShouldBe(1);
        diagnostics[0].Identifier.ShouldBe("preferences");
        diagnostics[0].Key.ShouldBe("persisted-preferences");
        diagnostics[0].StorageKind.ShouldBe(StateStorageKind.Local);
        diagnostics[0].Operation.ShouldBe(StateStorePersistenceOperation.Restore);
        diagnostics[0].Exception.ShouldNotBeNull();
        registry.Count.ShouldBe(1);
    }

    [Fact]
    public void Create_UnknownJsonMemberOutsideIncludedPaths_IsIgnored()
    {
        RecordingStateStorage storage = new();
        storage.Values["persisted-preferences"] = StateStorePersistenceTestSupport.Payload(
            "{\"count\":7,\"unknown\":{\"arbitrary\":true}}");
        List<StateStorePersistenceDiagnostic> diagnostics = [];
        using StateStoreRegistry registry = StateStoreTestSupport.CreateRegistry();
        registry.Use(CreatePlugin(storage, diagnostics.Add));

        PersistenceStateStore store = registry.GetOrCreate(
            StateStorePersistenceTestSupport.CreateDefinition(
                new StateStorePersistenceDescriptor("persisted-preferences", includePaths: ["count"])));

        store.State.Count.ShouldBe(7);
        diagnostics.ShouldBeEmpty();
        storage.RemoveCalls.ShouldBe(0);
    }

    [Fact]
    public void Create_LocalAndSessionUsingSameStorageKey_RestoreAndWriteIndependently()
    {
        RecordingStateStorage local = new();
        RecordingStateStorage session = new();
        local.Values["shared-key"] = StateStorePersistenceTestSupport.Payload("{\"count\":4}", "local");
        session.Values["shared-key"] = StateStorePersistenceTestSupport.Payload("{\"count\":8}", "session");
        TestReactiveWatchScheduler scheduler = new();
        using StateStoreRegistry registry = StateStoreTestSupport.CreateRegistry(scheduler);
        registry.Use(new StateStorePersistencePlugin(new StateStorePersistenceOptions(local, session)));
        PersistenceStateStore localStore = registry.GetOrCreate(
            StateStorePersistenceTestSupport.CreateDefinition(
                new StateStorePersistenceDescriptor("shared-key", StateStorageKind.Local), "local"));
        PersistenceStateStore sessionStore = registry.GetOrCreate(
            StateStorePersistenceTestSupport.CreateDefinition(
                new StateStorePersistenceDescriptor("shared-key", StateStorageKind.Session), "session"));

        localStore.State.Count.ShouldBe(4);
        sessionStore.State.Count.ShouldBe(8);
        scheduler.RunUntilIdle();
        local.WriteCalls.ShouldBe(0);
        session.WriteCalls.ShouldBe(0);
        localStore.State.Count = 5;
        sessionStore.State.Count = 9;
        scheduler.RunUntilIdle();

        local.WriteCalls.ShouldBe(1);
        session.WriteCalls.ShouldBe(1);
        StateStorePersistenceTestSupport.ReadState(local, "shared-key").GetProperty("count").GetInt32().ShouldBe(5);
        StateStorePersistenceTestSupport.ReadState(session, "shared-key").GetProperty("count").GetInt32().ShouldBe(9);
    }

    [Fact]
    public void Create_SessionStorageMissing_ReportsConfigurationAndKeepsStoreUsable()
    {
        RecordingStateStorage storage = new();
        List<StateStorePersistenceDiagnostic> diagnostics = [];
        using StateStoreRegistry registry = StateStoreTestSupport.CreateRegistry();
        registry.Use(CreatePlugin(storage, diagnostics.Add));

        PersistenceStateStore store = registry.GetOrCreate(
            StateStorePersistenceTestSupport.CreateDefinition(
                new StateStorePersistenceDescriptor("session-key", StateStorageKind.Session)));
        store.State.Count = 8;

        diagnostics.Count.ShouldBe(1);
        diagnostics[0].Operation.ShouldBe(StateStorePersistenceOperation.Configure);
        diagnostics[0].StorageKind.ShouldBe(StateStorageKind.Session);
        storage.ReadCalls.ShouldBe(0);
        storage.WriteCalls.ShouldBe(0);
        store.State.Count.ShouldBe(8);
    }

    [Fact]
    public void Create_StorageReadThrows_ReportsFailureAndLaterMutationCanPersist()
    {
        InvalidOperationException failure = new("storage access denied");
        RecordingStateStorage storage = new() { ReadFailure = failure };
        List<StateStorePersistenceDiagnostic> diagnostics = [];
        using StateStoreRegistry registry = StateStoreTestSupport.CreateRegistry();
        registry.Use(CreatePlugin(storage, diagnostics.Add));

        PersistenceStateStore store = registry.GetOrCreate(StateStorePersistenceTestSupport.CreateDefinition());

        store.State.Count.ShouldBe(1);
        diagnostics.Count.ShouldBe(1);
        diagnostics[0].Operation.ShouldBe(StateStorePersistenceOperation.Read);
        diagnostics[0].Exception.ShouldBeSameAs(failure);
        storage.RemoveCalls.ShouldBe(0);
        store.State.Count = 12;
        storage.WriteCalls.ShouldBe(1);
    }

    [Fact]
    public void Mutate_StorageWriteThrows_ReportsFailureWithoutInterruptingOtherSubscribers()
    {
        InvalidOperationException failure = new("storage quota exceeded");
        RecordingStateStorage storage = new() { WriteFailure = failure };
        List<StateStorePersistenceDiagnostic> diagnostics = [];
        TestReactiveWatchScheduler scheduler = new();
        using StateStoreRegistry registry = StateStoreTestSupport.CreateRegistry(scheduler);
        registry.Use(CreatePlugin(storage, diagnostics.Add));
        PersistenceStateStore store = registry.GetOrCreate(StateStorePersistenceTestSupport.CreateDefinition());
        int notifications = 0;
        store.Subscribe((_, _) => notifications++);

        store.State.Count = 6;
        Should.NotThrow(scheduler.RunUntilIdle);

        notifications.ShouldBe(1);
        storage.WriteCalls.ShouldBe(1);
        diagnostics.Count.ShouldBe(1);
        diagnostics[0].Operation.ShouldBe(StateStorePersistenceOperation.Write);
        diagnostics[0].Exception.ShouldBeSameAs(failure);

        storage.WriteFailure = null;
        store.State.Count = 7;
        scheduler.RunUntilIdle();
        storage.WriteCalls.ShouldBe(2);
        StateStorePersistenceTestSupport.ReadState(storage).GetProperty("count").GetInt32().ShouldBe(7);
    }

    [Fact]
    public void Create_CorruptPayloadAndFailedRemoval_ReportsBothFailuresAndPreservesDefaults()
    {
        InvalidOperationException failure = new("remove denied");
        RecordingStateStorage storage = new() { RemoveFailure = failure };
        storage.Values["persisted-preferences"] = "invalid";
        List<StateStorePersistenceDiagnostic> diagnostics = [];
        using StateStoreRegistry registry = StateStoreTestSupport.CreateRegistry();
        registry.Use(CreatePlugin(storage, diagnostics.Add));

        PersistenceStateStore store = registry.GetOrCreate(StateStorePersistenceTestSupport.CreateDefinition());

        store.State.Count.ShouldBe(1);
        diagnostics.Count.ShouldBe(2);
        diagnostics.ShouldContain(value => value.Operation == StateStorePersistenceOperation.Restore);
        diagnostics.ShouldContain(value => value.Operation == StateStorePersistenceOperation.Remove
            && ReferenceEquals(value.Exception, failure));
    }

    [Fact]
    public void Diagnostic_CallbackThrows_DoesNotFailCreationOrMutation()
    {
        RecordingStateStorage storage = new() { WriteFailure = new InvalidOperationException("quota") };
        storage.Values["persisted-preferences"] = "invalid";
        int diagnosticCalls = 0;
        using StateStoreRegistry registry = StateStoreTestSupport.CreateRegistry();
        registry.Use(CreatePlugin(storage, _ =>
        {
            diagnosticCalls++;
            throw new InvalidOperationException("diagnostic callback failed");
        }));

        PersistenceStateStore store = registry.GetOrCreate(StateStorePersistenceTestSupport.CreateDefinition());
        Should.NotThrow(() => store.State.Count = 7);

        diagnosticCalls.ShouldBe(2);
        store.State.Count.ShouldBe(7);
    }

    [Fact]
    public void Remove_QueuedMutation_CancelsWriteAndRetainsPersistedValueForNextStoreLifetime()
    {
        RecordingStateStorage storage = new();
        storage.Values["persisted-preferences"] = StateStorePersistenceTestSupport.Payload("{\"count\":3}");
        TestReactiveWatchScheduler scheduler = new();
        using StateStoreRegistry registry = StateStoreTestSupport.CreateRegistry(scheduler);
        registry.Use(CreatePlugin(storage));
        StateStoreDefinition<PersistenceStateStore> definition = StateStorePersistenceTestSupport.CreateDefinition();
        PersistenceStateStore store = registry.GetOrCreate(definition);
        scheduler.RunUntilIdle();

        store.State.Count = 8;
        registry.Remove(definition).ShouldBeTrue();
        scheduler.RunUntilIdle();
        store.State.Count = 9;
        scheduler.RunUntilIdle();

        storage.WriteCalls.ShouldBe(0);
        storage.RemoveCalls.ShouldBe(0);
        PersistenceStateStore replacement = registry.GetOrCreate(definition);
        replacement.ShouldNotBeSameAs(store);
        replacement.State.Count.ShouldBe(3);
        storage.ReadCalls.ShouldBe(2);
    }

    [Fact]
    public void Persistence_ComponentScopeStops_ContinuesUntilRegistryDisposalWithoutOwningStorage()
    {
        RecordingStateStorage storage = new();
        TestReactiveWatchScheduler scheduler = new();
        StateStoreRegistry registry = StateStoreTestSupport.CreateRegistry(scheduler);
        registry.Use(CreatePlugin(storage));
        using EffectScope componentScope = Reactive.EffectScope();
        PersistenceStateStore store = componentScope.Run(
            () => registry.GetOrCreate(StateStorePersistenceTestSupport.CreateDefinition()));

        componentScope.Stop();
        store.State.Count = 2;
        scheduler.RunUntilIdle();
        storage.WriteCalls.ShouldBe(1);
        store.State.Count = 3;
        registry.Dispose();
        scheduler.RunUntilIdle();

        storage.WriteCalls.ShouldBe(1);
        storage.DisposeCalls.ShouldBe(0);
    }

    [Fact]
    public void RestorePayload_StagedServerState_OverridesPersistenceAndPersistsFinalState()
    {
        RecordingStateStorage storage = new();
        storage.Values["persisted-preferences"] = StateStorePersistenceTestSupport.Payload("{\"count\":3}");
        TestReactiveWatchScheduler scheduler = new();
        using StateStoreRegistry registry = StateStoreTestSupport.CreateRegistry(scheduler);
        registry.Use(CreatePlugin(storage));
        registry.RestorePayload(StateStorePayload.Parse(
            StateStorePersistenceTestSupport.Payload("{\"count\":12,\"title\":\"server\",\"profile\":{},\"tags\":[]}")));

        PersistenceStateStore store = registry.GetOrCreate(StateStorePersistenceTestSupport.CreateDefinition());
        scheduler.RunUntilIdle();

        store.State.Count.ShouldBe(12);
        store.State.Title.ShouldBe("server");
        storage.WriteCalls.ShouldBe(1);
        StateStorePersistenceTestSupport.ReadState(storage).GetProperty("count").GetInt32().ShouldBe(12);
    }

    private static StateStorePersistencePlugin CreatePlugin(
        IStateStorage storage,
        Action<StateStorePersistenceDiagnostic>? diagnostic = null)
        => new(new StateStorePersistenceOptions(storage, diagnostic: diagnostic));

    private sealed class PersistenceObserverPlugin : IStateStorePlugin
    {
        private readonly List<(int Count, string? Title)> _observed;

        internal PersistenceObserverPlugin(List<(int Count, string? Title)> observed)
        {
            _observed = observed;
        }

        public void Apply(StateStorePluginContext context)
        {
            context.TryGetStore<PersistenceStateStore>(out PersistenceStateStore? store).ShouldBeTrue();
            store!.Subscribe((_, state) => _observed.Add((state.Count, state.Title)));
        }
    }

    private sealed class CountingPersistenceSerializer : IStateStoreSerializer<PersistenceStateStore>
    {
        private readonly IStateStoreSerializer<PersistenceStateStore> _inner =
            StateStorePersistenceTestSupport.CreateSerializer();

        internal int SerializeCalls { get; private set; }

        internal int RestoreCalls { get; private set; }

        internal Exception? SerializeFailure { get; set; }

        internal Exception? RestoreFailure { get; init; }

        public void Serialize(Utf8JsonWriter writer, PersistenceStateStore stateStore)
        {
            SerializeCalls++;
            if (SerializeFailure is { } failure)
            {
                throw failure;
            }

            _inner.Serialize(writer, stateStore);
        }

        public void Restore(PersistenceStateStore stateStore, JsonElement state)
        {
            RestoreCalls++;
            _inner.Restore(stateStore, state);
            if (stateStore.State.Count == 99 && RestoreFailure is { } failure)
            {
                throw failure;
            }
        }
    }
}
