using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using Shouldly;
using Xunit;

using Assimalign.Viu.DevTools;

namespace Assimalign.Viu.DevTools.Client.Tests;

public sealed class ClientSessionTests
{
    [Fact]
    public async Task Receive_LargeTimelineBatch_AppliesBoundedWorkAndNotifiesOncePerFrame()
    {
        // [DVT-14]: large batches cannot turn transport callbacks into unbounded render work.
        ManualScheduler scheduler = new();
        FakeTransport transport = new();
        await using DevToolsClientSession session = new(transport, scheduler, messagesPerFrame: 10, timelineCapacity: 32);
        await session.StartAsync(); scheduler.Drain();
        await transport.Receive("""{"version":1,"type":"handshake.response","payload":{"accepted":true,"version":1}}""");
        scheduler.Drain();
        int notifications = 0;
        session.Changed += () => notifications++;
        List<ProtocolEnvelope> messages = [];
        for (int index = 0; index < 200; index++)
        {
            messages.Add(ProtocolCodec.CreateEnvelope("timeline.event", new TimelineEventPayload(index, index, 1, "reactivity", "state.write", "Count"), DevToolsJsonSerializerContext.Default.TimelineEventPayload));
        }

        await transport.Receive(ProtocolCodec.SerializeBatch(messages));
        session.Timeline.Count.ShouldBe(0);
        notifications.ShouldBe(0);
        scheduler.RunFrame();
        session.Timeline.Count.ShouldBe(10);
        session.PendingMessageCount.ShouldBe(190);
        notifications.ShouldBe(1);
        scheduler.Drain();
        session.Timeline.Count.ShouldBe(32);
        session.Timeline.Events[0].Sequence.ShouldBe(168);
        notifications.ShouldBe(20);
    }

    [Fact]
    public async Task Handshake_Reconnect_ClearsIdentitiesSelectionTimelineAndInspectors()
    {
        ManualScheduler scheduler = new(); FakeTransport transport = new();
        await using DevToolsClientSession session = new(transport, scheduler);
        await session.StartAsync(); scheduler.Drain();
        await transport.Receive("""{"protocol":"assimalign.viu.devtools","messages":[{"version":1,"type":"handshake.response","payload":{"accepted":true,"version":1}},{"version":1,"type":"component.mounted","payload":{"identifier":1,"index":0,"name":"Counter"}},{"version":1,"type":"inspector.registered","payload":{"identifier":"custom","displayName":"Custom"}},{"version":1,"type":"timeline.event","payload":{"sequence":1,"timestamp":5,"correlationIdentifier":1,"layerIdentifier":"reactivity","kind":"state.write","label":"Count"}}]}""");
        scheduler.Drain(); session.SelectComponent(1); scheduler.Drain();
        session.Tree.Nodes.Count.ShouldBe(1); session.Timeline.Count.ShouldBe(1);
        transport.ConnectAgain(); scheduler.Drain();
        session.IsConnected.ShouldBeFalse(); session.Tree.Nodes.ShouldBeEmpty();
        session.SelectedComponentIdentifier.ShouldBeNull(); session.Timeline.Count.ShouldBe(0); session.Inspectors.ShouldBeEmpty();
        await transport.Receive("""{"version":1,"type":"handshake.response","payload":{"accepted":true,"version":1}}""");
        scheduler.Drain(); session.IsConnected.ShouldBeTrue();
        transport.Sent.Count(value => value.Contains("handshake.request", StringComparison.Ordinal)).ShouldBe(2);
    }

    [Fact]
    public async Task Receive_UnknownMalformedAndOtherProtocolFrames_LeavesConnectionUsable()
    {
        ManualScheduler scheduler = new(); FakeTransport transport = new();
        await using DevToolsClientSession session = new(transport, scheduler);
        await session.StartAsync(); scheduler.Drain();
        await transport.Receive("invalid");
        await transport.Receive("""{"protocol":"other","messages":[{"version":1,"type":"handshake.response","payload":{"accepted":true,"version":1}}]}""");
        scheduler.Drain(); session.IsConnected.ShouldBeFalse();
        await transport.Receive("""{"version":1,"type":"handshake.response","payload":{"accepted":true,"version":1}}""");
        await transport.Receive("""{"version":1,"type":"future.message","payload":{}}""");
        scheduler.Drain(); session.IsConnected.ShouldBeTrue();
        transport.Reentered.ShouldBeFalse();
    }

    [Fact]
    public async Task SelectAndExpand_RequestOnlyExplicitDepthAndRefreshAfterAcceptedEdit()
    {
        ManualScheduler scheduler = new(); FakeTransport transport = new();
        await using DevToolsClientSession session = new(transport, scheduler);
        await session.StartAsync(); scheduler.Drain();
        await transport.Receive("""{"version":1,"type":"handshake.response","payload":{"accepted":true,"version":1}}"""); scheduler.Drain();
        session.SelectComponent(7); scheduler.Drain();
        transport.Sent.Last().ShouldContain("\"depth\":0");
        session.Expansions.ShouldBeEmpty();
        session.Expand("state", ["Count"]); scheduler.Drain();
        transport.Sent.Last().ShouldContain("component.expand.request"); transport.Sent.Last().ShouldContain("\"depth\":1");
        session.EditState("state", ["Count", "value"], "42"); scheduler.Drain();
        transport.Sent.Last().ShouldContain("\"value\":42");
        await transport.Receive("""{"version":1,"type":"state.edit.response","payload":{"identifier":7,"path":["Count","value"],"accepted":true}}"""); scheduler.Drain();
        session.LastEdit!.Accepted.ShouldBeTrue(); transport.Sent.Last().ShouldContain("component.snapshot.request");
        transport.Reentered.ShouldBeFalse();
    }

    [Fact]
    public async Task GenericInspector_UsesOnlyRegistrationTreeAndStateMessages()
    {
        ManualScheduler scheduler = new(); FakeTransport transport = new();
        await using DevToolsClientSession session = new(transport, scheduler);
        await session.StartAsync(); scheduler.Drain();
        await transport.Receive("""{"version":1,"type":"handshake.response","payload":{"accepted":true,"version":1}}"""); scheduler.Drain();
        await transport.Receive("""{"version":1,"type":"inspector.registered","payload":{"identifier":"arbitrary","displayName":"Any provider"}}"""); scheduler.Drain();
        session.SelectInspector("arbitrary"); scheduler.Drain(); transport.Sent.Last().ShouldContain("inspector.tree.request");
        await transport.Receive("""{"version":1,"type":"inspector.tree","payload":{"inspectorIdentifier":"arbitrary","roots":[{"identifier":"node","label":"Generic node","children":[]}]}}"""); scheduler.Drain();
        session.InspectorTree!.Roots.Single().Label.ShouldBe("Generic node");
        session.SelectInspectorNode("arbitrary", "node"); scheduler.Drain(); transport.Sent.Last().ShouldContain("inspector.state.request");
        await transport.Receive("""{"version":1,"type":"inspector.state","payload":{"inspectorIdentifier":"arbitrary","nodeIdentifier":"node","state":[]}}"""); scheduler.Drain();
        session.InspectorState!.NodeIdentifier.ShouldBe("node");
    }

    [Fact]
    public async Task Dispose_PendingFrame_DoesNotSendOrNotifyAndDisposesTransportOnce()
    {
        ManualScheduler scheduler = new(); FakeTransport transport = new();
        DevToolsClientSession session = new(transport, scheduler);
        int notifications = 0; session.Changed += () => notifications++;
        await session.StartAsync(); await session.DisposeAsync(); await session.DisposeAsync(); scheduler.Drain();
        transport.DisposeCount.ShouldBe(1); transport.Sent.ShouldBeEmpty(); notifications.ShouldBe(0);
    }

    private sealed class ManualScheduler : IDevToolsClientScheduler
    {
        private readonly Queue<Action> _callbacks = new();
        public void Schedule(Action callback) => _callbacks.Enqueue(callback);
        public void RunFrame() { int count = _callbacks.Count; while (count-- > 0)
            {
                _callbacks.Dequeue()();
            }
        }
        public void Drain() { int count = 1000; while (_callbacks.Count > 0 && count-- > 0) { RunFrame(); } count.ShouldBeGreaterThan(0); }
    }
    private sealed class FakeTransport : IDevToolsClientTransport
    {
        private Func<string, ValueTask>? _receiver;
        private Action? _connected;
        private bool _receiving;
        internal List<string> Sent { get; } = [];
        internal bool Reentered { get; private set; }
        internal int DisposeCount { get; private set; }
        public ValueTask StartAsync(Func<string, ValueTask> receiver, Action connected, CancellationToken cancellationToken = default)
        { _receiver = receiver; _connected = connected; connected(); return ValueTask.CompletedTask; }
        public ValueTask SendAsync(string message, CancellationToken cancellationToken = default)
        { Reentered |= _receiving; Sent.Add(message); return ValueTask.CompletedTask; }
        internal async ValueTask Receive(string json) { _receiving = true; try { await _receiver!(json); } finally { _receiving = false; } }
        internal void ConnectAgain() => _connected!();
        public ValueTask DisposeAsync() { DisposeCount++; return ValueTask.CompletedTask; }
    }
}
