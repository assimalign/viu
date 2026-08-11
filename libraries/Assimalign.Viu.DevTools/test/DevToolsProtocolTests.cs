using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

using Shouldly;
using Xunit;

using Assimalign.Viu;
using Assimalign.Viu.Components;
using Assimalign.Viu.Reactivity;
using Assimalign.Viu.Testing;

namespace Assimalign.Viu.DevTools.Tests;

[Collection("DevTools runtime inspection")]
public sealed class DevToolsProtocolTests
{
    private const string HandshakeCorpus =
        "{\"protocol\":\"assimalign.viu.devtools\",\"messages\":["
        + "{\"version\":1,\"type\":42,\"payload\":{}},"
        + "{\"version\":1,\"type\":\"handshake.request\",\"payload\":{\"supportedVersions\":[1]}},"
        + "{\"version\":1,\"type\":\"future.unknown\",\"payload\":{\"value\":true}}]}";

    private const string RejectedHandshakeCorpus =
        "{\"protocol\":\"assimalign.viu.devtools\",\"messages\":["
        + "{\"version\":1,\"type\":\"handshake.request\",\"payload\":{\"supportedVersions\":[2]}}]}";

    private const string InspectorCorpus =
        "{\"protocol\":\"assimalign.viu.devtools\",\"messages\":["
        + "{\"version\":2,\"type\":\"tree.snapshot.request\",\"payload\":{}},"
        + "{\"version\":1,\"type\":\"inspector.tree.request\",\"payload\":{\"inspectorIdentifier\":null}},"
        + "{\"version\":1,\"type\":\"inspector.tree.request\",\"payload\":{\"inspectorIdentifier\":\"sample\"}},"
        + "{\"version\":1,\"type\":\"inspector.state.request\",\"payload\":{\"inspectorIdentifier\":\"sample\",\"nodeIdentifier\":\"root\",\"depth\":2}}]}";

    static DevToolsProtocolTests() =>
        AppContext.SetSwitch(RuntimeInspection.FeatureSwitchName, true);

    [Theory]
    [InlineData("postMessage")]
    [InlineData("webSocket")]
    public async Task Transport_IdenticalCorpus_NegotiatesAndServesCustomInspector(
        string transportKind)
    {
        // [DVT-2] and [DVT-7] require one corpus across both concrete transports.
        Scheduler.Reset();
        using TestSchedulerPump pump = TestSchedulerPump.Install();
        ITransportHarness harness = TransportHarness.Create(transportKind);
        await using DevToolsSession session = new(harness.Transport);
        using IDisposable inspectorRegistration = session.RegisterInspector(new SampleInspector());
        using IDisposable layerRegistration = session.RegisterTimelineLayer(
            new DevToolsTimelineLayer("sample-events", "Sample events", "#336699"));
        await session.StartAsync();

        await harness.ReceiveAsync(RejectedHandshakeCorpus);
        await PumpUntilMessageAsync(() => harness.SentFrames, pump, "handshake.response");
        IReadOnlyList<JsonElement> rejectedMessages = ReadMessages(harness.SentFrames);
        rejectedMessages.Single(message => MessageType(message) == "handshake.response")
            .GetProperty("payload")
            .GetProperty("accepted")
            .GetBoolean()
            .ShouldBeFalse();
        rejectedMessages.ShouldNotContain(
            message => MessageType(message) == "inspector.registered");

        int messagesBeforeAcceptedHandshake = rejectedMessages.Count;
        await harness.ReceiveAsync(HandshakeCorpus);
        await PumpUntilAsync(
            pump,
            () => ReadMessages(harness.SentFrames)
                .Skip(messagesBeforeAcceptedHandshake)
                .Any(message => MessageType(message) == "timeline.layer.registered"));

        IReadOnlyList<JsonElement> handshakeMessages = ReadMessages(harness.SentFrames)
            .Skip(messagesBeforeAcceptedHandshake)
            .ToArray();
        MessageType(handshakeMessages[0]).ShouldBe("handshake.response");
        handshakeMessages.Skip(1)
            .ShouldContain(message => MessageType(message) == "inspector.registered");
        handshakeMessages.Skip(1)
            .ShouldContain(message => MessageType(message) == "timeline.layer.registered");
        JsonElement handshake = handshakeMessages[0];
        handshake.GetProperty("payload").GetProperty("accepted").GetBoolean().ShouldBeTrue();

        await harness.ReceiveAsync(InspectorCorpus);
        await PumpUntilMessageAsync(() => harness.SentFrames, pump, "inspector.state");

        IReadOnlyList<JsonElement> messages = ReadMessages(harness.SentFrames);
        messages.ShouldContain(message => MessageType(message) == "inspector.tree");
        messages.ShouldContain(message => MessageType(message) == "inspector.state");
        messages.ShouldNotContain(message => MessageType(message) == "tree.snapshot");
        messages.Last(message => MessageType(message) == "inspector.state")
            .GetProperty("payload")
            .GetProperty("state")[0]
            .GetProperty("value")
            .GetProperty("displayValue")
            .GetString()
            .ShouldBe("ready");

        Scheduler.Reset();
    }

    [Fact]
    public async Task TestingHost_KeyedReorderAndSnapshot_MatchLiveComponentHierarchy()
    {
        // [DVT-4] and [DVT-5] pin the live tree and reflection-free snapshot contract.
        Scheduler.Reset();
        using TestSchedulerPump pump = TestSchedulerPump.Install();
        RecordingTransport transport = new();
        await using DevToolsSession session = new(transport);
        await session.StartAsync();
        await transport.ReceiveAsync(HandshakeCorpus);
        await PumpUntilMessageAsync(() => transport.SentFrames, pump, "handshake.response");

        ComponentReference parentReference = ComponentReference.ForType(typeof(TreeParentComponent));
        ComponentReference childReference = ComponentReference.ForType(typeof(TreeChildComponent));
        TreeParentComponent parent = new(childReference);
        ComponentFactory factory = new();
        factory.Register(new ComponentRegistration(
            parentReference,
            new ComponentContract(
                displayName: "TreeParent",
                events: [new ComponentEvent("changed")]),
            _ => parent));
        factory.Register(new ComponentRegistration(
            childReference,
            new ComponentContract(
                displayName: "TreeChild",
                parameters: [new ComponentParameter("label")]),
            _ => new TreeChildComponent()));
        ComponentNode root = new(parentReference);
        ApplicationContext application = new(
            new ApplicationOptions
            {
                Components = factory,
                RootComponent = root,
            });
        using TestRenderer renderer = new();
        TestElement container = renderer.CreateContainer();

        renderer.Render(root, container, application);
        await RequestAndPumpAsync(
            transport,
            pump,
            "{\"version\":1,\"type\":\"tree.snapshot.request\",\"payload\":{}}",
            "tree.snapshot");
        JsonElement initialTree = LastMessage(transport.SentFrames, "tree.snapshot")
            .GetProperty("payload");
        JsonElement initialRoot = initialTree.GetProperty("roots")[0];
        initialRoot.GetProperty("name").GetString().ShouldBe("TreeParent");
        initialRoot.GetProperty("children")[0].GetProperty("key").GetString().ShouldBe("first");
        initialRoot.GetProperty("children")[1].GetProperty("key").GetString().ShouldBe("second");
        int parentIdentifier = initialRoot.GetProperty("identifier").GetInt32();
        int firstChildIdentifier = initialRoot.GetProperty("children")[0]
            .GetProperty("identifier").GetInt32();
        int secondChildIdentifier = initialRoot.GetProperty("children")[1]
            .GetProperty("identifier").GetInt32();

        parent.Reversed.Value = true;
        pump.RunUntilIdle();
        await RequestAndPumpAsync(
            transport,
            pump,
            "{\"version\":1,\"type\":\"tree.snapshot.request\",\"payload\":{}}",
            "tree.snapshot",
            minimumOccurrence: 2);
        JsonElement reorderedTree = LastMessage(transport.SentFrames, "tree.snapshot")
            .GetProperty("payload");
        JsonElement reorderedRoot = reorderedTree.GetProperty("roots")[0];
        reorderedRoot.GetProperty("children")[0].GetProperty("key").GetString().ShouldBe("second");
        reorderedRoot.GetProperty("children")[1].GetProperty("key").GetString().ShouldBe("first");
        reorderedRoot.GetProperty("children")[0]
            .GetProperty("identifier").GetInt32().ShouldBe(secondChildIdentifier);
        reorderedRoot.GetProperty("children")[1]
            .GetProperty("identifier").GetInt32().ShouldBe(firstChildIdentifier);
        ReadMessages(transport.SentFrames)
            .ShouldContain(message => MessageType(message) == "component.reordered");

        await RequestAndPumpAsync(
            transport,
            pump,
            $"{{\"version\":1,\"type\":\"component.snapshot.request\",\"payload\":{{\"identifier\":{parentIdentifier},\"depth\":1}}}}",
            "component.snapshot");
        JsonElement parentSnapshot = LastMessage(transport.SentFrames, "component.snapshot")
            .GetProperty("payload");
        parentSnapshot.GetProperty("state")
            .EnumerateArray()
            .ShouldContain(value => value.GetProperty("name").GetString() == "count"
                && value.GetProperty("value").GetProperty("kind").GetString() == "reference");
        JsonElement doubled = parentSnapshot.GetProperty("state")
            .EnumerateArray()
            .Single(value => value.GetProperty("name").GetString() == "doubled")
            .GetProperty("value");
        doubled.GetProperty("kind").GetString().ShouldBe("reference");
        doubled.GetProperty("children")[0]
            .GetProperty("value")
            .GetProperty("displayValue")
            .GetString()
            .ShouldBe("6");
        parentSnapshot.GetProperty("state")
            .EnumerateArray()
            .ShouldContain(value => value.GetProperty("name").GetString() == "opaque"
                && value.GetProperty("value").GetProperty("kind").GetString() == "placeholder");
        parentSnapshot.GetProperty("events")[0].GetProperty("name").GetString().ShouldBe("changed");

        await RequestAndPumpAsync(
            transport,
            pump,
            $"{{\"version\":1,\"type\":\"component.snapshot.request\",\"payload\":{{\"identifier\":{parentIdentifier},\"depth\":0}}}}",
            "component.snapshot",
            minimumOccurrence: 2);
        JsonElement deferredCount = LastMessage(transport.SentFrames, "component.snapshot")
            .GetProperty("payload")
            .GetProperty("state")
            .EnumerateArray()
            .Single(value => value.GetProperty("name").GetString() == "count")
            .GetProperty("value");
        deferredCount.GetProperty("expandable").GetBoolean().ShouldBeTrue();
        deferredCount.GetProperty("children").GetArrayLength().ShouldBe(0);

        parent.Count.Value = 4;
        await RequestAndPumpAsync(
            transport,
            pump,
            $"{{\"version\":1,\"type\":\"component.expand.request\",\"payload\":{{\"identifier\":{parentIdentifier},\"section\":\"state\",\"path\":[\"count\",\"value\"],\"depth\":1}}}}",
            "component.expand");
        LastMessage(transport.SentFrames, "component.expand")
            .GetProperty("payload")
            .GetProperty("value")
            .GetProperty("displayValue")
            .GetString()
            .ShouldBe("4");

        await RequestAndPumpAsync(
            transport,
            pump,
            $"{{\"version\":1,\"type\":\"component.snapshot.request\",\"payload\":{{\"identifier\":{firstChildIdentifier},\"depth\":1}}}}",
            "component.snapshot",
            minimumOccurrence: 3);
        LastMessage(transport.SentFrames, "component.snapshot")
            .GetProperty("payload")
            .GetProperty("parameters")[0]
            .GetProperty("name")
            .GetString()
            .ShouldBe("label");

        parent.EmitChanged();
        pump.RunUntilIdle();
        await WaitUntilAsync(() => ReadMessages(transport.SentFrames)
            .Any(message => MessageType(message) == "component.event"));

        renderer.Render(null, container);
        await WaitUntilAsync(() => ReadMessages(transport.SentFrames)
            .Count(message => MessageType(message) == "component.unmounted") >= 3);
        Scheduler.Reset();
    }

    [Fact]
    public async Task SlowTransport_TelemetryFlood_PreservesCrossPlaneChronologyAndReportsDrops()
    {
        // [DVT-5] keeps control messages reliable while bounding renderer telemetry.
        Scheduler.Reset();
        using TestSchedulerPump pump = TestSchedulerPump.Install();
        SlowTransport transport = new();
        await using DevToolsSession session = new(
            transport,
            new DevToolsSessionOptions
            {
                BufferCapacity = 4,
            });
        await session.StartAsync();
        await transport.ReceiveAsync(HandshakeCorpus);
        for (int index = 0; index < 10; index++)
        {
            session.RegisterInspector(new NumberedInspector(index));
        }

        pump.RunUntilIdle();
        await WaitUntilAsync(() => transport.SendCount == 1);
        IReadOnlyList<JsonElement> firstBatch = ReadMessages([transport.SentFrames[0]]);
        firstBatch.ShouldContain(message => MessageType(message) == "handshake.response");
        firstBatch.Count(message => MessageType(message) == "inspector.registered")
            .ShouldBe(10);

        InspectionDependencyComponent component = new();
        ComponentReference reference = ComponentReference.ForType(
            typeof(InspectionDependencyComponent));
        ComponentFactory factory = new();
        factory.Register(new ComponentRegistration(
            reference,
            new ComponentContract(displayName: "InspectionDependency"),
            _ => component));
        ComponentNode root = new(reference);
        ApplicationContext application = new(
            new ApplicationOptions
            {
                Components = factory,
                RootComponent = root,
            });
        using TestRenderer renderer = new();
        TestElement container = renderer.CreateContainer();
        renderer.Render(root, container, application);
        IRuntimeInspectionHook hook = session;
        for (int index = 0; index < 10; index++)
        {
            hook.ComponentEvent(component, "first-flood", [index]);
        }

        await transport.ReceiveAsync(
            "{\"version\":1,\"type\":\"tree.snapshot.request\",\"payload\":{}}");
        hook.ComponentEvent(component, "after-snapshot", [0]);

        pump.RunUntilIdle();
        transport.SendCount.ShouldBe(1);
        transport.ReleaseFirstSend();
        await WaitUntilAsync(() => pump.PendingFlushCount > 0);
        pump.RunUntilIdle();
        await WaitUntilAsync(() => transport.SendCount == 2);

        IReadOnlyList<JsonElement> secondBatch = ReadMessages([transport.SentFrames[1]]);
        secondBatch.ShouldContain(message => MessageType(message) == "tree.snapshot");
        secondBatch.Single(message => MessageType(message) == "telemetry.dropped")
            .GetProperty("payload")
            .GetProperty("count")
            .GetInt32()
            .ShouldBe(8);
        secondBatch.Count(message => MessageType(message)?.StartsWith(
            "component.",
            StringComparison.Ordinal) == true)
            .ShouldBe(4);
        secondBatch.Select(message => MessageType(message) == "component.event"
                ? message.GetProperty("payload").GetProperty("name").GetString()
                : MessageType(message))
            .ShouldBe(
            [
                "telemetry.dropped",
                "first-flood",
                "first-flood",
                "first-flood",
                "tree.snapshot",
                "after-snapshot",
            ]);

        renderer.Render(null, container);
        Scheduler.Reset();
    }

    [Theory]
    [InlineData("component.event")]
    [InlineData("component.snapshot")]
    public async Task Observation_ReactiveValue_DoesNotChangeEffectDependencies(
        string observationType)
    {
        // [DVT-3] requires an attached inspection session not to join the app dependency graph.
        int[] withoutSession = MeasureBaselineDependencyRuns();
        int[] withSession = await MeasureInspectedDependencyRunsAsync(observationType);

        withSession.ShouldBe(withoutSession);
        withSession.ShouldBe([1, 1, 2]);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task RetryableInitialization_FaultedOrCanceledFirstAttempt_SecondAttemptSucceeds(
        bool cancelFirstAttempt)
    {
        RetryableInitialization initialization = new();
        int attempts = 0;
        Task Initialize(CancellationToken cancellationToken)
        {
            attempts++;
            if (attempts != 1)
            {
                return Task.CompletedTask;
            }

            return cancelFirstAttempt
                ? Task.FromCanceled(new CancellationToken(canceled: true))
                : Task.FromException(new InvalidOperationException(
                    "Expected first initialization failure."));
        }

        if (cancelFirstAttempt)
        {
            await Should.ThrowAsync<TaskCanceledException>(
                () => initialization.InitializeAsync(Initialize, CancellationToken.None));
        }
        else
        {
            await Should.ThrowAsync<InvalidOperationException>(
                () => initialization.InitializeAsync(Initialize, CancellationToken.None));
        }

        await initialization.InitializeAsync(Initialize, CancellationToken.None);
        attempts.ShouldBe(2);
    }

    [Fact]
    public async Task PostMessageDispatch_PendingReceiver_SerializesFramesAndIsolatesFailure()
    {
        // [DVT-2] requires serial frame delivery by each transport adapter.
        TaskCompletionSource releaseFirst = new(
            TaskCreationOptions.RunContinuationsAsynchronously);
        List<string> started = [];
#pragma warning disable CA1416 // This test invokes only the managed dispatch seam.
        int subscriptionIdentifier = PostMessageInteropDispatch.Register(async message =>
        {
            lock (started)
            {
                started.Add(message);
            }

            if (string.Equals(message, "first", StringComparison.Ordinal))
            {
                await releaseFirst.Task;
            }
            else if (string.Equals(message, "second", StringComparison.Ordinal))
            {
                throw new InvalidOperationException("Expected receiver probe failure.");
            }
        });

        try
        {
            PostMessageInteropDispatch.Dispatch(subscriptionIdentifier, "first");
            PostMessageInteropDispatch.Dispatch(subscriptionIdentifier, "second");
            PostMessageInteropDispatch.Dispatch(subscriptionIdentifier, "third");
            await WaitUntilAsync(() => SnapshotStarted(started).Count == 1);
            SnapshotStarted(started).ShouldBe(["first"]);

            releaseFirst.TrySetResult();
            await WaitUntilAsync(() => SnapshotStarted(started).Count == 3);
            SnapshotStarted(started).ShouldBe(["first", "second", "third"]);
        }
        finally
        {
            PostMessageInteropDispatch.Unregister(subscriptionIdentifier);
        }
#pragma warning restore CA1416
    }

    [Fact]
    public async Task ComponentSnapshot_ThrowingProviderAndCollections_EmitsPlaceholders()
    {
        // [DVT-5] requires provider and collection failures to remain diagnostic values.
        Scheduler.Reset();
        using TestSchedulerPump pump = TestSchedulerPump.Install();
        RecordingTransport transport = new();
        await using DevToolsSession session = new(transport);
        await session.StartAsync();
        await transport.ReceiveAsync(HandshakeCorpus);
        await PumpUntilMessageAsync(() => transport.SentFrames, pump, "handshake.response");

        UnsafeInspectionComponent component = new();
        ComponentReference reference = ComponentReference.ForType(
            typeof(UnsafeInspectionComponent));
        ComponentFactory factory = new();
        factory.Register(new ComponentRegistration(
            reference,
            new ComponentContract(displayName: "UnsafeInspection"),
            _ => component));
        ComponentNode root = new(reference);
        ApplicationContext application = new(
            new ApplicationOptions
            {
                Components = factory,
                RootComponent = root,
            });
        using TestRenderer renderer = new();
        TestElement container = renderer.CreateContainer();
        renderer.Render(root, container, application);

        await RequestAndPumpAsync(
            transport,
            pump,
            "{\"version\":1,\"type\":\"tree.snapshot.request\",\"payload\":{}}",
            "tree.snapshot");
        int identifier = LastMessage(transport.SentFrames, "tree.snapshot")
            .GetProperty("payload")
            .GetProperty("roots")[0]
            .GetProperty("identifier")
            .GetInt32();

        await RequestAndPumpAsync(
            transport,
            pump,
            $"{{\"version\":1,\"type\":\"component.snapshot.request\",\"payload\":{{\"identifier\":{identifier},\"depth\":2}}}}",
            "component.snapshot");
        JsonElement unsafeState = LastMessage(transport.SentFrames, "component.snapshot")
            .GetProperty("payload")
            .GetProperty("state");
        AssertPlaceholder(unsafeState, "throwingDictionary", "unavailable");
        AssertPlaceholder(unsafeState, "throwingListCount", "unavailable");
        AssertPlaceholder(unsafeState, "throwingListIndexer", "unavailable");

        component.ReturnThrowingDictionary = true;
        await RequestAndPumpAsync(
            transport,
            pump,
            $"{{\"version\":1,\"type\":\"component.snapshot.request\",\"payload\":{{\"identifier\":{identifier},\"depth\":2}}}}",
            "component.snapshot",
            minimumOccurrence: 2);
        JsonElement dictionaryFailure = LastMessage(transport.SentFrames, "component.snapshot")
            .GetProperty("payload")
            .GetProperty("state")[0]
            .GetProperty("value");
        dictionaryFailure.GetProperty("kind").GetString().ShouldBe("placeholder");
        dictionaryFailure.GetProperty("displayValue").GetString().ShouldBe("unavailable");

        component.ReturnThrowingDictionary = false;
        component.ThrowOnRead = true;
        await RequestAndPumpAsync(
            transport,
            pump,
            $"{{\"version\":1,\"type\":\"component.snapshot.request\",\"payload\":{{\"identifier\":{identifier},\"depth\":2}}}}",
            "component.snapshot",
            minimumOccurrence: 3);
        JsonElement providerFailure = LastMessage(transport.SentFrames, "component.snapshot")
            .GetProperty("payload")
            .GetProperty("state")[0]
            .GetProperty("value");
        providerFailure.GetProperty("kind").GetString().ShouldBe("placeholder");
        providerFailure.GetProperty("displayValue").GetString().ShouldBe("provider-threw");

        renderer.Render(null, container);
        Scheduler.Reset();
    }

    [Fact]
    public async Task Session_UnmountedComponent_DoesNotRootAuthoredInstance()
    {
        // [DVT-4] requires identity tracking not to extend authored component lifetime.
        Scheduler.Reset();
        using TestSchedulerPump pump = TestSchedulerPump.Install();
        RecordingTransport transport = new();
        await using DevToolsSession session = new(transport);
        await session.StartAsync();
        await transport.ReceiveAsync(HandshakeCorpus);
        await PumpUntilMessageAsync(() => transport.SentFrames, pump, "handshake.response");

        WeakReference componentReference = MountAndUnmountCollectibleComponent();
        pump.RunUntilIdle();
        ForceFullCollection(componentReference);

        componentReference.IsAlive.ShouldBeFalse();
        Scheduler.Reset();
    }

    [Fact]
    public async Task DisposeAsync_PendingInspectorRequest_CancelsProvider()
    {
        // [DVT-7] scopes inspector work to the owning session lifetime.
        Scheduler.Reset();
        using TestSchedulerPump pump = TestSchedulerPump.Install();
        RecordingTransport transport = new();
        DevToolsSession session = new(transport);
        BlockingInspector inspector = new();
        using IDisposable registration = session.RegisterInspector(inspector);
        try
        {
            await session.StartAsync();
            await transport.ReceiveAsync(HandshakeCorpus);
            await PumpUntilMessageAsync(
                () => transport.SentFrames,
                pump,
                "handshake.response");

            Task request = transport.ReceiveAsync(
                "{\"version\":1,\"type\":\"inspector.tree.request\",\"payload\":{\"inspectorIdentifier\":\"blocking\"}}")
                .AsTask();
            await inspector.Started.Task.WaitAsync(TimeSpan.FromSeconds(2));
            await session.DisposeAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(2));
            await request.WaitAsync(TimeSpan.FromSeconds(2));

            inspector.CancellationObserved.ShouldBeTrue();
        }
        finally
        {
            await session.DisposeAsync();
            Scheduler.Reset();
        }
    }

    [Fact]
    public async Task DisposeAsync_PendingTransportSend_CancelsAndCompletes()
    {
        // [DVT-6] bounds session teardown even when transport acceptance is pending.
        Scheduler.Reset();
        using TestSchedulerPump pump = TestSchedulerPump.Install();
        SlowTransport transport = new();
        DevToolsSession session = new(transport);
        try
        {
            await session.StartAsync();
            await transport.ReceiveAsync(HandshakeCorpus);
            pump.RunUntilIdle();
            await WaitUntilAsync(() => transport.SendCount == 1);

            await session.DisposeAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(2));

            transport.CancellationObserved.ShouldBeTrue();
        }
        finally
        {
            transport.ReleaseFirstSend();
            await session.DisposeAsync();
            Scheduler.Reset();
        }
    }

    private static async Task RequestAndPumpAsync(
        RecordingTransport transport,
        TestSchedulerPump pump,
        string envelope,
        string responseType,
        int minimumOccurrence = 1)
    {
        await transport.ReceiveAsync(envelope);
        await PumpUntilAsync(
            pump,
            () => ReadMessages(transport.SentFrames)
                .Count(message => MessageType(message) == responseType) >= minimumOccurrence);
    }

    private static async Task PumpUntilMessageAsync(
        Func<IReadOnlyList<string>> frames,
        TestSchedulerPump pump,
        string messageType) =>
        await PumpUntilAsync(
            pump,
            () => ReadMessages(frames()).Any(message => MessageType(message) == messageType));

    private static async Task PumpUntilAsync(
        TestSchedulerPump pump,
        Func<bool> condition)
    {
        for (int attempt = 0; attempt < 400; attempt++)
        {
            pump.RunUntilIdle();
            if (condition())
            {
                return;
            }

            await Task.Delay(5);
        }

        throw new TimeoutException("The expected DevTools protocol message was not emitted.");
    }

    private static async Task WaitUntilAsync(Func<bool> condition)
    {
        for (int attempt = 0; attempt < 400; attempt++)
        {
            if (condition())
            {
                return;
            }

            await Task.Delay(5);
        }

        throw new TimeoutException("The expected asynchronous test condition was not reached.");
    }

    private static int[] MeasureBaselineDependencyRuns()
    {
        Reference<int> inspected = Reactive.Reference(0);
        Reference<int> tracked = Reactive.Reference(0);
        int runs = 0;
        using ReactiveEffect effect = Reactive.Effect(() =>
        {
            runs++;
            _ = tracked.Value;
        });

        int initialRuns = runs;
        inspected.Value = 1;
        int runsAfterInspectionValueChanged = runs;
        tracked.Value = 1;
        return [initialRuns, runsAfterInspectionValueChanged, runs];
    }

    private static async Task<int[]> MeasureInspectedDependencyRunsAsync(
        string observationType)
    {
        Scheduler.Reset();
        using TestSchedulerPump pump = TestSchedulerPump.Install();
        RecordingTransport transport = new();
        await using DevToolsSession session = new(transport);
        await session.StartAsync();
        await transport.ReceiveAsync(HandshakeCorpus);
        await PumpUntilMessageAsync(
            () => transport.SentFrames,
            pump,
            "handshake.response");

        InspectionDependencyComponent component = new();
        ComponentReference reference = ComponentReference.ForType(
            typeof(InspectionDependencyComponent));
        ComponentFactory factory = new();
        factory.Register(new ComponentRegistration(
            reference,
            new ComponentContract(displayName: "InspectionDependency"),
            _ => component));
        ComponentNode root = new(reference);
        ApplicationContext application = new(
            new ApplicationOptions
            {
                Components = factory,
                RootComponent = root,
            });
        using TestRenderer renderer = new();
        TestElement container = renderer.CreateContainer();
        renderer.Render(root, container, application);

        await RequestAndPumpAsync(
            transport,
            pump,
            "{\"version\":1,\"type\":\"tree.snapshot.request\",\"payload\":{}}",
            "tree.snapshot");
        int identifier = LastMessage(transport.SentFrames, "tree.snapshot")
            .GetProperty("payload")
            .GetProperty("roots")[0]
            .GetProperty("identifier")
            .GetInt32();

        Reference<int> tracked = Reactive.Reference(0);
        int runs = 0;
        using ReactiveEffect effect = Reactive.Effect(() =>
        {
            runs++;
            _ = tracked.Value;
            if (string.Equals(observationType, "component.event", StringComparison.Ordinal))
            {
                ((IRuntimeInspectionHook)session).ComponentEvent(
                    component,
                    "observed",
                    [component.Inspected]);
            }
            else
            {
                transport.ReceiveAsync(
                    $"{{\"version\":1,\"type\":\"component.snapshot.request\",\"payload\":{{\"identifier\":{identifier},\"depth\":1}}}}")
                    .AsTask()
                    .GetAwaiter()
                    .GetResult();
            }
        });

        int initialRuns = runs;
        component.Inspected.Value = 1;
        int runsAfterInspectionValueChanged = runs;
        tracked.Value = 1;
        int finalRuns = runs;

        renderer.Render(null, container);
        Scheduler.Reset();
        return [initialRuns, runsAfterInspectionValueChanged, finalRuns];
    }

    private static JsonElement LastMessage(
        IReadOnlyList<string> frames,
        string messageType) =>
        ReadMessages(frames).Last(message => MessageType(message) == messageType);

    private static IReadOnlyList<JsonElement> ReadMessages(IReadOnlyList<string> frames)
    {
        List<JsonElement> messages = [];
        for (int frameIndex = 0; frameIndex < frames.Count; frameIndex++)
        {
            using JsonDocument document = JsonDocument.Parse(frames[frameIndex]);
            foreach (JsonElement message in document.RootElement
                .GetProperty("messages")
                .EnumerateArray())
            {
                messages.Add(message.Clone());
            }
        }

        return messages;
    }

    private static string? MessageType(JsonElement message) =>
        message.GetProperty("type").GetString();

    private static void AssertPlaceholder(
        JsonElement state,
        string name,
        string displayValue)
    {
        JsonElement value = state.EnumerateArray()
            .Single(entry => entry.GetProperty("name").GetString() == name)
            .GetProperty("value");
        value.GetProperty("kind").GetString().ShouldBe("placeholder");
        value.GetProperty("displayValue").GetString().ShouldBe(displayValue);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static WeakReference MountAndUnmountCollectibleComponent()
    {
        WeakReference? componentReference = null;
        ComponentReference reference = ComponentReference.ForType(typeof(CollectibleComponent));
        ComponentFactory factory = new();
        factory.Register(new ComponentRegistration(
            reference,
            new ComponentContract(displayName: "Collectible"),
            _ =>
            {
                CollectibleComponent component = new();
                componentReference = new WeakReference(component);
                return component;
            }));
        ComponentNode root = new(reference);
        ApplicationContext application = new(
            new ApplicationOptions
            {
                Components = factory,
                RootComponent = root,
            });
        using TestRenderer renderer = new();
        TestElement container = renderer.CreateContainer();
        renderer.Render(root, container, application);
        renderer.Render(null, container);
        return componentReference
            ?? throw new InvalidOperationException("The collectible component was not created.");
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void ForceFullCollection(WeakReference reference)
    {
        for (int attempt = 0; attempt < 10 && reference.IsAlive; attempt++)
        {
            GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced, blocking: true, compacting: true);
            GC.WaitForPendingFinalizers();
            GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced, blocking: true, compacting: true);
        }
    }

    private static IReadOnlyList<string> SnapshotStarted(List<string> started)
    {
        lock (started)
        {
            return started.ToArray();
        }
    }

    private sealed class SampleInspector : IDevToolsInspector
    {
        public string Identifier => "sample";

        public string DisplayName => "Sample";

        public ValueTask<IReadOnlyList<DevToolsInspectorNode>> GetTreeAsync(
            CancellationToken cancellationToken = default) =>
            ValueTask.FromResult<IReadOnlyList<DevToolsInspectorNode>>(
                [new DevToolsInspectorNode("root", "Root")]);

        public ValueTask<IReadOnlyDictionary<string, object?>> GetStateAsync(
            string nodeIdentifier,
            CancellationToken cancellationToken = default) =>
            ValueTask.FromResult<IReadOnlyDictionary<string, object?>>(
                new Dictionary<string, object?>
                {
                    ["status"] = "ready",
                });
    }

    private sealed class NumberedInspector : IDevToolsInspector
    {
        internal NumberedInspector(int number)
        {
            Identifier = $"inspector-{number}";
            DisplayName = $"Inspector {number}";
        }

        public string Identifier { get; }

        public string DisplayName { get; }

        public ValueTask<IReadOnlyList<DevToolsInspectorNode>> GetTreeAsync(
            CancellationToken cancellationToken = default) =>
            ValueTask.FromResult<IReadOnlyList<DevToolsInspectorNode>>([]);

        public ValueTask<IReadOnlyDictionary<string, object?>> GetStateAsync(
            string nodeIdentifier,
            CancellationToken cancellationToken = default) =>
            ValueTask.FromResult<IReadOnlyDictionary<string, object?>>(
                new Dictionary<string, object?>());
    }

    private sealed class BlockingInspector : IDevToolsInspector
    {
        private int _cancellationObserved;

        internal TaskCompletionSource Started { get; } = new(
            TaskCreationOptions.RunContinuationsAsynchronously);

        internal bool CancellationObserved =>
            Volatile.Read(ref _cancellationObserved) != 0;

        public string Identifier => "blocking";

        public string DisplayName => "Blocking";

        public ValueTask<IReadOnlyList<DevToolsInspectorNode>> GetTreeAsync(
            CancellationToken cancellationToken = default) =>
            new(WaitForCancellationAsync(cancellationToken));

        public ValueTask<IReadOnlyDictionary<string, object?>> GetStateAsync(
            string nodeIdentifier,
            CancellationToken cancellationToken = default) =>
            ValueTask.FromResult<IReadOnlyDictionary<string, object?>>(
                new Dictionary<string, object?>());

        private async Task<IReadOnlyList<DevToolsInspectorNode>> WaitForCancellationAsync(
            CancellationToken cancellationToken)
        {
            Started.TrySetResult();
            try
            {
                await Task.Delay(Timeout.Infinite, cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                Volatile.Write(ref _cancellationObserved, 1);
                throw;
            }

            return [];
        }
    }

    private sealed class UnsafeInspectionComponent :
        IComponent,
        IComponentInspectionStateProvider
    {
        internal bool ReturnThrowingDictionary { get; set; }

        internal bool ThrowOnRead { get; set; }

        public ComponentRenderer Setup(ComponentContext context) =>
            static _ => new CommentNode("unsafe inspection");

        public IReadOnlyDictionary<string, object?> GetInspectionState()
        {
            if (ThrowOnRead)
            {
                throw new InvalidOperationException("Expected inspection provider failure.");
            }

            if (ReturnThrowingDictionary)
            {
                return new ThrowingReadOnlyDictionary();
            }

            return new Dictionary<string, object?>
            {
                ["throwingDictionary"] = new ThrowingReadOnlyDictionary(),
                ["throwingListCount"] = new ThrowingList(throwOnCount: true),
                ["throwingListIndexer"] = new ThrowingList(throwOnCount: false),
            };
        }
    }

    private sealed class CollectibleComponent : IComponent
    {
        public ComponentRenderer Setup(ComponentContext context) =>
            static _ => new CommentNode("collectible");
    }

    private sealed class InspectionDependencyComponent :
        IComponent,
        IComponentInspectionStateProvider
    {
        internal Reference<int> Inspected { get; } = Reactive.Reference(0);

        public ComponentRenderer Setup(ComponentContext context) =>
            static _ => new CommentNode("inspection dependency");

        public IReadOnlyDictionary<string, object?> GetInspectionState() =>
            new Dictionary<string, object?>
            {
                ["inspected"] = Inspected,
            };
    }

    private sealed class ThrowingReadOnlyDictionary :
        IReadOnlyDictionary<string, object?>
    {
        public object? this[string key] =>
            throw new InvalidOperationException("Expected dictionary indexer failure.");

        public IEnumerable<string> Keys =>
            throw new InvalidOperationException("Expected dictionary keys failure.");

        public IEnumerable<object?> Values =>
            throw new InvalidOperationException("Expected dictionary values failure.");

        public int Count =>
            throw new InvalidOperationException("Expected dictionary count failure.");

        public bool ContainsKey(string key) =>
            throw new InvalidOperationException("Expected dictionary lookup failure.");

        public IEnumerator<KeyValuePair<string, object?>> GetEnumerator() =>
            throw new InvalidOperationException("Expected dictionary enumeration failure.");

        public bool TryGetValue(string key, out object? value) =>
            throw new InvalidOperationException("Expected dictionary lookup failure.");

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }

    private sealed class ThrowingList : IList
    {
        private readonly bool _throwOnCount;

        internal ThrowingList(bool throwOnCount) =>
            _throwOnCount = throwOnCount;

        public object? this[int index]
        {
            get => throw new InvalidOperationException("Expected list indexer failure.");
            set => throw new NotSupportedException();
        }

        public bool IsFixedSize => true;

        public bool IsReadOnly => true;

        public int Count => _throwOnCount
            ? throw new InvalidOperationException("Expected list count failure.")
            : 1;

        public bool IsSynchronized => false;

        public object SyncRoot => this;

        public int Add(object? value) => throw new NotSupportedException();

        public void Clear() => throw new NotSupportedException();

        public bool Contains(object? value) => throw new NotSupportedException();

        public int IndexOf(object? value) => throw new NotSupportedException();

        public void Insert(int index, object? value) => throw new NotSupportedException();

        public void Remove(object? value) => throw new NotSupportedException();

        public void RemoveAt(int index) => throw new NotSupportedException();

        public void CopyTo(Array array, int index) => throw new NotSupportedException();

        public IEnumerator GetEnumerator() => throw new NotSupportedException();
    }

    private sealed class TreeParentComponent : IComponent, IComponentInspectionStateProvider
    {
        private readonly ComponentReference _childReference;
        private readonly Computed<int> _doubled;
        private ComponentContext? _context;

        internal TreeParentComponent(ComponentReference childReference)
        {
            _childReference = childReference;
            _doubled = Reactive.Computed(() => Count.Value * 2);
        }

        internal Reference<bool> Reversed { get; } = Reactive.Reference(false);

        internal Reference<int> Count { get; } = Reactive.Reference(3);

        public ComponentRenderer Setup(ComponentContext context)
        {
            _context = context;
            return _ => Reversed.Value
                ? new FragmentNode([Child("second"), Child("first")])
                : new FragmentNode([Child("first"), Child("second")]);
        }

        public IReadOnlyDictionary<string, object?> GetInspectionState() =>
            new Dictionary<string, object?>
            {
                ["count"] = Count,
                ["doubled"] = _doubled,
                ["opaque"] = new OpaqueState(),
            };

        internal void EmitChanged() => _context!.Emit("changed", Count.Value);

        private ComponentNode Child(string label) => new(
            _childReference,
            new ComponentInvocation(
                new Dictionary<string, object?>
                {
                    ["label"] = label,
                }),
            key: label);
    }

    private sealed class TreeChildComponent : IComponent
    {
        public ComponentRenderer Setup(ComponentContext context) => _ =>
            new ElementNode(
                new QualifiedName("item"),
                children:
                [
                    new TextNode(Convert.ToString(
                        context.Bindings.Parameters["label"],
                        System.Globalization.CultureInfo.InvariantCulture)
                        ?? string.Empty),
                ]);
    }

    private sealed class OpaqueState;
}

[CollectionDefinition("DevTools runtime inspection", DisableParallelization = true)]
public sealed class DevToolsRuntimeInspectionCollection;
