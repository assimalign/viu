using System;
using System.Collections.Generic;
using System.Globalization;
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
public sealed class DevToolsTimelineTests
{
    private const string HandshakeCorpus =
        "{\"protocol\":\"assimalign.viu.devtools\",\"messages\":["
        + "{\"version\":1,\"type\":\"future.timeline.command\",\"payload\":{}},"
        + "{\"version\":1,\"type\":\"handshake.request\",\"payload\":{\"supportedVersions\":[1]}},"
        + "{\"version\":1,\"type\":\"future.timeline.command\",\"payload\":{}}]}";

    [Fact]
    public void TimelineRing_OverflowAndWraparound_ReportsExactLossOncePerDrain()
    {
        // [DVT-11] retains the newest bounded events and counts evictions since the previous drain.
        TimelineRecorder recorder = new(new DevToolsSessionOptions { TimelineCapacity = 3 });
        for (int sequence = 1; sequence <= 7; sequence++)
        {
            recorder.Record(CreateEvent(sequence));
        }

        recorder.HasEvents.ShouldBeTrue();
        List<SequencedProtocolEnvelope> first = recorder.Drain();
        first.Select(message => message.Sequence).ShouldBe([1L, 5L, 6L, 7L]);
        first[0].Envelope.Type.ShouldBe("timeline.dropped");
        first[0].Envelope.Payload.GetProperty("count").GetInt64().ShouldBe(4);
        first.Skip(1).ShouldAllBe(message => message.Envelope.Type == "timeline.event");
        recorder.HasEvents.ShouldBeFalse();
        recorder.Drain().ShouldBeEmpty();

        for (int sequence = 8; sequence <= 12; sequence++)
        {
            recorder.Record(CreateEvent(sequence));
        }

        List<SequencedProtocolEnvelope> second = recorder.Drain();
        second.Select(message => message.Sequence).ShouldBe([8L, 10L, 11L, 12L]);
        second[0].Envelope.Payload.GetProperty("count").GetInt64().ShouldBe(2);
    }

    [Fact]
    public void TimelineSampling_AcrossDrains_CapturesFirstThenEveryIntervalWithoutLossMarker()
    {
        // [DVT-11] sampling is intentional selection, not dropped telemetry.
        TimelineRecorder recorder = new(new DevToolsSessionOptions { TimelineSamplingInterval = 3 });
        for (int sequence = 1; sequence <= 5; sequence++)
        {
            recorder.Record(CreateEvent(sequence));
        }

        List<SequencedProtocolEnvelope> first = recorder.Drain();
        first.Select(message => message.Sequence).ShouldBe([1L, 4L]);
        first.ShouldAllBe(message => message.Envelope.Type == "timeline.event");
        for (int sequence = 6; sequence <= 10; sequence++)
        {
            recorder.Record(CreateEvent(sequence));
        }

        List<SequencedProtocolEnvelope> second = recorder.Drain();
        second.Select(message => message.Sequence).ShouldBe([7L, 10L]);
        second.ShouldAllBe(message => message.Envelope.Type == "timeline.event");
    }

    [Fact]
    public void TimelineRateLimit_DrainDoesNotResetWindow_NewSecondAdmitsEvents()
    {
        // [DVT-11] rate windows use monotonic elapsed microseconds and count rejected candidates.
        TimelineRecorder recorder = new(new DevToolsSessionOptions
        {
            MaximumTimelineEventsPerSecond = 2,
        });
        recorder.Record(CreateEvent(1, timestamp: 0));
        recorder.Record(CreateEvent(2, timestamp: 10));
        recorder.Record(CreateEvent(3, timestamp: 20));
        List<SequencedProtocolEnvelope> first = recorder.Drain();
        first.Select(message => message.Sequence).ShouldBe([1L, 2L, 3L]);
        first[2].Envelope.Type.ShouldBe("timeline.dropped");
        first[2].Envelope.Payload.GetProperty("count").GetInt64().ShouldBe(1);

        recorder.Record(CreateEvent(4, timestamp: 999_999));
        recorder.HasEvents.ShouldBeTrue();
        List<SequencedProtocolEnvelope> limited = recorder.Drain();
        limited.Count.ShouldBe(1);
        limited[0].Envelope.Type.ShouldBe("timeline.dropped");
        limited[0].Envelope.Payload.GetProperty("count").GetInt64().ShouldBe(1);

        recorder.Record(CreateEvent(5, timestamp: 1_000_000));
        List<SequencedProtocolEnvelope> nextWindow = recorder.Drain();
        nextWindow.Count.ShouldBe(1);
        nextWindow[0].Envelope.Type.ShouldBe("timeline.event");
        nextWindow[0].Envelope.Payload.GetProperty("timestamp").GetInt64().ShouldBe(1_000_000);
    }

    [Fact]
    public void TimelineSamplingAndRateLimit_OnlySelectedCandidatesConsumeRateAllowance()
    {
        // [DVT-11] sampling precedes the rate limiter; unsampled candidates are never loss.
        TimelineRecorder recorder = new(new DevToolsSessionOptions
        {
            TimelineSamplingInterval = 3,
            MaximumTimelineEventsPerSecond = 2,
        });
        for (int sequence = 1; sequence <= 7; sequence++)
        {
            recorder.Record(CreateEvent(sequence));
        }

        List<SequencedProtocolEnvelope> messages = recorder.Drain();
        messages.Select(message => message.Sequence).ShouldBe([1L, 4L, 7L]);
        messages.Take(2).ShouldAllBe(message => message.Envelope.Type == "timeline.event");
        messages[2].Envelope.Type.ShouldBe("timeline.dropped");
        messages[2].Envelope.Payload.GetProperty("count").GetInt64().ShouldBe(1);
    }

    [Fact]
    public void TimelineLoss_RateRejectionThenOlderEviction_MarkerUsesEarliestLostSequence()
    {
        // [DVT-11] mixed loss cannot reorder the marker past the earliest evicted event.
        TimelineRecorder recorder = new(new DevToolsSessionOptions
        {
            TimelineCapacity = 2,
            MaximumTimelineEventsPerSecond = 2,
        });
        recorder.Record(CreateEvent(1, timestamp: 0));
        recorder.Record(CreateEvent(2, timestamp: 1));
        recorder.Record(CreateEvent(3, timestamp: 2));
        recorder.Record(CreateEvent(4, timestamp: 1_000_000));

        List<SequencedProtocolEnvelope> messages = recorder.Drain();
        messages.Select(message => message.Sequence).ShouldBe([1L, 2L, 4L]);
        messages[0].Envelope.Type.ShouldBe("timeline.dropped");
        messages[0].Envelope.Payload.GetProperty("count").GetInt64().ShouldBe(2);
    }

    [Fact]
    public void TimelineLabel_UnknownMemberKey_DoesNotCallApplicationFormatting()
    {
        // [DVT-9] opaque keys get a stable synthetic name without invoking application code.
        TimelineRecorder recorder = new(new DevToolsSessionOptions());
        object dependency = new();
        ThrowingMemberKey key = new();
        string label = recorder.GetLabel(dependency, key, debugLabel: null);

        label.ShouldStartWith("dependency-");
        recorder.GetLabel(dependency, key, debugLabel: null).ShouldBe(label);
        recorder.GetLabel(dependency, "Count", debugLabel: null).ShouldBe("Count");
        recorder.GetLabel(dependency, "Count", debugLabel: "counter.count").ShouldBe("counter.count");
        key.FormattingCalls.ShouldBe(0);
    }

    [Fact]
    public async Task Start_OccupiedReactivityHook_ReleasesRuntimeHookOnFailure()
    {
        // [DVT-8] a failed two-seam attachment must not leave a partially installed session.
        Scheduler.Reset();
        using IDisposable occupiedRegistration = ReactivityInspection.Use(new NoOpReactivityHook());
        await using DevToolsSession session = new(new RecordingTransport());

        await Should.ThrowAsync<InvalidOperationException>(() => session.StartAsync().AsTask());

        RuntimeInspection.IsEnabled.ShouldBeFalse();
        ReactivityInspection.IsEnabled.ShouldBeTrue();
        using IDisposable runtimeRegistration = RuntimeInspection.Use(session);
        RuntimeInspection.IsEnabled.ShouldBeTrue();
        Scheduler.Reset();
    }

    [Fact]
    public async Task Start_TransportStartupThrows_ReleasesBothInspectionHooks()
    {
        // [DVT-8] transport startup failure cannot reserve either global inspection seam.
        Scheduler.Reset();
        await using DevToolsSession session = new(new FailingStartTransport());

        await Should.ThrowAsync<InvalidOperationException>(() => session.StartAsync().AsTask());

        RuntimeInspection.IsEnabled.ShouldBeFalse();
        ReactivityInspection.IsEnabled.ShouldBeFalse();
        using IDisposable runtimeRegistration = RuntimeInspection.Use(session);
        using IDisposable reactivityRegistration = ReactivityInspection.Use(new NoOpReactivityHook());
        RuntimeInspection.IsEnabled.ShouldBeTrue();
        ReactivityInspection.IsEnabled.ShouldBeTrue();
        Scheduler.Reset();
    }

    [Fact]
    public async Task SessionSampling_RepeatedReferenceWrites_UsesConfiguredInterval()
    {
        // [DVT-11] session options apply to actual hook capture, not only direct recorder calls.
        Scheduler.Reset();
        using TestSchedulerPump pump = TestSchedulerPump.Install();
        RecordingTransport transport = new();
        await using DevToolsSession session = new(transport, new DevToolsSessionOptions
        {
            TimelineSamplingInterval = 10,
        });
        await session.StartAsync();
        await transport.ReceiveAsync(HandshakeCorpus);
        await PumpUntilAsync(pump, () => ReadMessages(transport.SentFrames)
            .Any(message => MessageType(message) == "handshake.response"));
        Reference<int> reference = Reactive.WithDebugLabel(Reactive.Reference(0), "sampled.value");

        for (int value = 1; value <= 50; value++)
        {
            reference.Value = value;
        }

        await PumpUntilAsync(pump, () => TimelineEvents(transport.SentFrames).Count >= 11);

        IReadOnlyList<JsonElement> events = TimelineEvents(transport.SentFrames);
        events.Count.ShouldBe(11);
        events.Take(10).ShouldAllBe(payload => EventKind(payload) == "state.write"
            && EventLabel(payload) == "sampled.value");
        events[10].GetProperty("kind").GetString().ShouldBe("flush.started");
        long[] sequences = events.Select(payload => payload.GetProperty("sequence").GetInt64()).ToArray();
        sequences.Skip(1).Select((sequence, index) => sequence - sequences[index])
            .ShouldAllBe(difference => difference == 10);
        ReadMessages(transport.SentFrames).ShouldNotContain(message => MessageType(message) == "timeline.dropped");
        Scheduler.Reset();
    }

    [Fact]
    public async Task SlowTransport_ImmediateFollowUpDispatcher_DrainsQueuedTimelineAfterSendCompletes()
    {
        // [DVT-11] send completion must release its gate before synchronously dispatched follow-up work.
        Scheduler.Reset();
        using TestSchedulerPump pump = TestSchedulerPump.Install();
        SlowTransport transport = new();
        await using DevToolsSession session = new(transport);
        await session.StartAsync();
        await transport.ReceiveAsync(HandshakeCorpus);
        await PumpUntilAsync(pump, () => transport.SendCount == 1);

        Reference<int> reference = Reactive.WithDebugLabel(Reactive.Reference(0), "delayed.value");
        reference.Value = 1;
        pump.RunUntilIdle();
        transport.SendCount.ShouldBe(1);

        using IDisposable immediateDispatcher = Scheduler.UseFlushDispatcher(
            static continuation => continuation());
        transport.ReleaseFirstSend();
        for (int attempt = 0; attempt < 400; attempt++)
        {
            lock (transport.SentFrames)
            {
                if (transport.SentFrames.Count >= 2)
                {
                    break;
                }
            }

            await Task.Delay(5);
        }

        string[] frames;
        lock (transport.SentFrames)
        {
            frames = transport.SentFrames.ToArray();
        }

        frames.Length.ShouldBe(2);
        TimelineEvents(frames).ShouldContain(payload => EventKind(payload) == "state.write"
            && EventLabel(payload) == "delayed.value");
        Scheduler.Reset();
    }

    [Theory]
    [InlineData("postMessage")]
    [InlineData("webSocket")]
    public async Task Transport_ReactiveComponentWrite_CorrelatesCompleteFlushTimeline(
        string transportKind)
    {
        // [DVT-6] and [DVT-10] require one event corpus through both concrete transports.
        Scheduler.Reset();
        using TestSchedulerPump pump = TestSchedulerPump.Install();
        ITransportHarness harness = TransportHarness.Create(transportKind);
        await using DevToolsSession session = new(harness.Transport);
        await session.StartAsync();
        await harness.ReceiveAsync(HandshakeCorpus);
        await PumpUntilAsync(pump, () => ReadMessages(harness.SentFrames)
            .Any(message => MessageType(message) == "handshake.response"));

        CounterComponent component = new();
        ComponentReference componentReference = ComponentReference.ForType(typeof(CounterComponent));
        ComponentFactory factory = new();
        factory.Register(new ComponentRegistration(
            componentReference,
            new ComponentContract(displayName: "TimelineCounter"),
            _ => component));
        ComponentNode root = new(componentReference);
        ApplicationContext application = new(new ApplicationOptions
        {
            Components = factory,
            RootComponent = root,
        });
        using TestRenderer renderer = new();
        TestElement container = renderer.CreateContainer();
        renderer.Render(root, container, application);
        await PumpUntilAsync(pump, () => TimelineEvents(harness.SentFrames)
            .Any(payload => EventKind(payload) == "component.mounted"));
        component.RenderCount.ShouldBe(1);

        int preFlushRuns = 0;
        int postFlushRuns = 0;
        Scheduler.QueueJob(new SchedulerJob(() => preFlushRuns++) { IsPreFlush = true });
        component.Count.Value = 1;
        Scheduler.QueuePostFlushCallback(new SchedulerJob(() => postFlushRuns++));
        Task nextTick = Scheduler.NextTickAsync();
        pump.RunUntilIdle();
        await nextTick;
        await PumpUntilAsync(pump, () => FindCompletedWrite(harness.SentFrames, "counter.count"));

        component.RenderCount.ShouldBe(2);
        preFlushRuns.ShouldBe(1);
        postFlushRuns.ShouldBe(1);
        IReadOnlyList<JsonElement> allEvents = TimelineEvents(harness.SentFrames);
        JsonElement write = allEvents.Last(payload => EventKind(payload) == "state.write"
            && EventLabel(payload) == "counter.count");
        long correlationIdentifier = write.GetProperty("correlationIdentifier").GetInt64();
        correlationIdentifier.ShouldBeGreaterThan(0);
        JsonElement[] events = allEvents.Where(payload => payload
            .GetProperty("correlationIdentifier").GetInt64() == correlationIdentifier).ToArray();
        string?[] kinds = events.Select(EventKind).ToArray();
        RequireOrdered(kinds,
            "state.write",
            "dependency.triggered",
            "effect.scheduled",
            "flush.started",
            "effect.run.started",
            "effect.run.completed",
            "component.updated",
            "flush.completed");

        long[] sequences = allEvents.Select(payload => payload.GetProperty("sequence").GetInt64())
            .ToArray();
        sequences.ShouldBe(sequences.OrderBy(sequence => sequence));
        sequences.Distinct().Count().ShouldBe(sequences.Length);
        long[] timestamps = allEvents.Select(payload => payload.GetProperty("timestamp").GetInt64())
            .ToArray();
        timestamps.ShouldBe(timestamps.OrderBy(timestamp => timestamp));

        JsonElement triggered = events.First(payload => EventKind(payload) == "dependency.triggered"
            && EventLabel(payload) == "counter.count");
        long dependencyIdentifier = triggered.GetProperty("dependencyIdentifier").GetInt64();
        dependencyIdentifier.ShouldBeGreaterThan(0);
        events.ShouldContain(payload => EventKind(payload) == "dependency.tracked"
            && payload.GetProperty("dependencyIdentifier").GetInt64() == dependencyIdentifier);
        long effectIdentifier = events.First(payload => EventKind(payload) == "effect.scheduled")
            .GetProperty("effectIdentifier").GetInt64();
        events.First(payload => EventKind(payload) == "effect.scheduled")
            .GetProperty("dependencyIdentifier").GetInt64().ShouldBe(dependencyIdentifier);
        events.First(payload => EventKind(payload) == "effect.run.started")
            .GetProperty("effectIdentifier").GetInt64().ShouldBe(effectIdentifier);
        events.First(payload => EventKind(payload) == "effect.run.completed")
            .GetProperty("effectIdentifier").GetInt64().ShouldBe(effectIdentifier);
        events.Single(payload => EventKind(payload) == "component.updated")
            .GetProperty("componentIdentifier").GetInt64().ShouldBeGreaterThan(0);

        JsonElement completed = events.Single(payload => EventKind(payload) == "flush.completed");
        completed.GetProperty("preFlushCount").GetInt32().ShouldBe(1);
        completed.GetProperty("renderCount").GetInt32().ShouldBe(1);
        // The renderer's updated lifecycle callback and the explicit test callback count; drains do not.
        completed.GetProperty("postFlushCount").GetInt32().ShouldBe(2);
        completed.GetProperty("succeeded").GetBoolean().ShouldBeTrue();

        string?[] registeredLayers = ReadMessages(harness.SentFrames)
            .Where(message => MessageType(message) == "timeline.layer.registered")
            .Select(message => message.GetProperty("payload").GetProperty("identifier").GetString())
            .ToArray();
        foreach (string? layer in allEvents.Select(payload => payload.GetProperty("layerIdentifier")
            .GetString()).Distinct())
        {
            registeredLayers.ShouldContain(layer);
        }

        foreach (string frame in harness.SentFrames)
        {
            using JsonDocument document = JsonDocument.Parse(frame);
            document.RootElement.GetProperty("protocol").GetString()
                .ShouldBe("assimalign.viu.devtools");
            foreach (JsonElement message in document.RootElement.GetProperty("messages").EnumerateArray())
            {
                message.GetProperty("version").GetInt32().ShouldBe(1);
            }
        }

        renderer.Render(null, container);
        Scheduler.Reset();
    }

    [Fact]
    public async Task Attribution_LabelledReferencesAndComputed_AnonymousIdentityStaysStable()
    {
        // [DVT-9] names references without reflection and anonymous dependencies without blanks.
        Scheduler.Reset();
        using TestSchedulerPump pump = TestSchedulerPump.Install();
        RecordingTransport transport = new();
        await using DevToolsSession session = new(transport);
        await session.StartAsync();
        await transport.ReceiveAsync(HandshakeCorpus);
        await PumpUntilAsync(pump, () => ReadMessages(transport.SentFrames)
            .Any(message => MessageType(message) == "handshake.response"));

        Reference<int> anonymous = Reactive.Reference(0);
        Reference<int> labelled = Reactive.WithDebugLabel(Reactive.Reference(10), "named.value");
        Computed<int> computed = Reactive.WithDebugLabel(
            Reactive.Computed(() => labelled.Value * 2),
            "named.doubled");
        int effectRuns = 0;
        using ReactiveEffect effect = Reactive.Effect(() =>
        {
            effectRuns++;
            _ = anonymous.Value + labelled.Value + computed.Value;
        });
        anonymous.Value = 1;
        anonymous.Value = 2;
        await PumpUntilAsync(pump, () => TimelineEvents(transport.SentFrames)
            .Count(payload => EventKind(payload) == "state.write") >= 2);

        effectRuns.ShouldBe(3);
        IReadOnlyList<JsonElement> events = TimelineEvents(transport.SentFrames);
        JsonElement[] tracked = events.Where(payload => EventKind(payload) == "dependency.tracked")
            .ToArray();
        tracked.ShouldContain(payload => EventLabel(payload) == "named.value");
        tracked.ShouldContain(payload => EventLabel(payload) == "named.doubled");
        JsonElement[] anonymousWrites = events.Where(payload => EventKind(payload) == "state.write")
            .ToArray();
        anonymousWrites.Select(EventLabel).Distinct().Count().ShouldBe(1);
        string? anonymousLabel = EventLabel(anonymousWrites[0]);
        anonymousLabel.ShouldNotBeNullOrWhiteSpace();
        anonymousLabel.ShouldStartWith("dependency-");
        long dependencyIdentifier = anonymousWrites[0].GetProperty("dependencyIdentifier").GetInt64();
        anonymousWrites.ShouldAllBe(payload => payload.GetProperty("dependencyIdentifier").GetInt64()
            == dependencyIdentifier);
        tracked.ShouldContain(payload => payload.GetProperty("dependencyIdentifier").GetInt64()
            == dependencyIdentifier && EventLabel(payload) == anonymousLabel);
        Scheduler.Reset();
    }

    [Fact]
    public async Task PendingTimeline_ReleasedReactiveObjects_AreCollectedBeforeDrain()
    {
        // [DVT-9] buffered values and weak identity metadata must not retain application objects.
        Scheduler.Reset();
        using TestSchedulerPump pump = TestSchedulerPump.Install();
        RecordingTransport transport = new();
        await using DevToolsSession session = new(transport);
        await session.StartAsync();
        await transport.ReceiveAsync(HandshakeCorpus);
        await PumpUntilAsync(pump, () => ReadMessages(transport.SentFrames)
            .Any(message => MessageType(message) == "handshake.response"));
        int framesBeforeObservation = transport.SentFrames.Count;

        WeakReference[] references = ObserveCollectibleReactiveObjects();
        ForceFullCollection(references);

        references.ShouldAllBe(reference => !reference.IsAlive);
        transport.SentFrames.Count.ShouldBe(framesBeforeObservation);
        await PumpUntilAsync(pump, () => TimelineEvents(transport.SentFrames)
            .Any(payload => EventLabel(payload) == "collectible.value"));
        Scheduler.Reset();
    }

    private static bool FindCompletedWrite(IReadOnlyList<string> frames, string label)
    {
        IReadOnlyList<JsonElement> events = TimelineEvents(frames);
        foreach (JsonElement write in events.Where(payload => EventKind(payload) == "state.write"
            && EventLabel(payload) == label))
        {
            long correlationIdentifier = write.GetProperty("correlationIdentifier").GetInt64();
            if (events.Any(payload => EventKind(payload) == "flush.completed"
                && payload.GetProperty("correlationIdentifier").GetInt64() == correlationIdentifier))
            {
                return true;
            }
        }

        return false;
    }

    private static void RequireOrdered(IReadOnlyList<string?> kinds, params string[] expected)
    {
        int previousIndex = -1;
        foreach (string kind in expected)
        {
            int currentIndex = -1;
            for (int index = previousIndex + 1; index < kinds.Count; index++)
            {
                if (kinds[index] == kind)
                {
                    currentIndex = index;
                    break;
                }
            }

            currentIndex.ShouldBeGreaterThan(previousIndex,
                $"Expected '{kind}' after index {previousIndex}; observed {string.Join(", ", kinds)}.");
            previousIndex = currentIndex;
        }
    }

    private static async Task PumpUntilAsync(TestSchedulerPump pump, Func<bool> condition)
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

        throw new TimeoutException("The expected timeline event was not emitted.");
    }

    private static IReadOnlyList<JsonElement> TimelineEvents(IReadOnlyList<string> frames) =>
        ReadMessages(frames).Where(message => MessageType(message) == "timeline.event")
            .Select(message => message.GetProperty("payload")).ToArray();

    private static IReadOnlyList<JsonElement> ReadMessages(IReadOnlyList<string> frames)
    {
        List<JsonElement> messages = [];
        foreach (string frame in frames)
        {
            using JsonDocument document = JsonDocument.Parse(frame);
            foreach (JsonElement message in document.RootElement.GetProperty("messages").EnumerateArray())
            {
                messages.Add(message.Clone());
            }
        }

        return messages;
    }

    private static string? MessageType(JsonElement message) => message.GetProperty("type").GetString();

    private static string? EventKind(JsonElement payload) => payload.GetProperty("kind").GetString();

    private static string? EventLabel(JsonElement payload) =>
        payload.TryGetProperty("label", out JsonElement label) ? label.GetString() : null;

    private static TimelineEventPayload CreateEvent(long sequence, long timestamp = 0) =>
        new(sequence, timestamp, 1, "reactivity", "dependency.tracked", "counter.count",
            DependencyIdentifier: 1, EffectIdentifier: 2, OwnerIdentifier: 3);

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static WeakReference[] ObserveCollectibleReactiveObjects()
    {
        object value = new();
        object replacement = new();
        Reference<object> reference = Reactive.WithDebugLabel(Reactive.Reference(value), "collectible.value");
        ReactiveEffect effect = Reactive.Effect(() => _ = reference.Value);
        reference.Value = replacement;
        effect.Dispose();
        return
        [
            new WeakReference(value),
            new WeakReference(replacement),
            new WeakReference(reference),
            new WeakReference(reference.Dependency),
            new WeakReference(effect),
        ];
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void ForceFullCollection(IReadOnlyList<WeakReference> references)
    {
        for (int attempt = 0; attempt < 10 && references.Any(reference => reference.IsAlive); attempt++)
        {
            GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced, blocking: true, compacting: true);
            GC.WaitForPendingFinalizers();
            GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced, blocking: true, compacting: true);
        }
    }

    private sealed class CounterComponent : IComponent
    {
        internal Reference<int> Count { get; } =
            Reactive.WithDebugLabel(Reactive.Reference(0), "counter.count");

        internal int RenderCount { get; private set; }

        public ComponentRenderer Setup(ComponentContext context) => _ =>
        {
            RenderCount++;
            return new TextNode(Count.Value.ToString(CultureInfo.InvariantCulture));
        };
    }

    private sealed class ThrowingMemberKey
    {
        internal int FormattingCalls { get; private set; }

        public override string ToString()
        {
            FormattingCalls++;
            throw new InvalidOperationException("Inspection must not format application keys.");
        }
    }

    private sealed class NoOpReactivityHook : IReactivityInspectionHook
    {
        public void DependencyTracked(in ReactivityInspectionDependency dependency) { }

        public void DependencyTriggered(in ReactivityInspectionDependency dependency) { }

        public void EffectScheduled(in ReactivityInspectionEffect effect) { }

        public void EffectRunStarted(in ReactivityInspectionEffect effect) { }

        public void EffectRunCompleted(in ReactivityInspectionEffect effect) { }
    }

    private sealed class FailingStartTransport : IDevToolsTransport
    {
        public ValueTask StartAsync(Func<string, ValueTask> receiver,
            CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("Expected transport startup failure.");

        public ValueTask SendAsync(string message, CancellationToken cancellationToken = default) =>
            ValueTask.CompletedTask;

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
