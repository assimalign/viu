using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

using Shouldly;
using Xunit;

using Assimalign.Viu.Components;
using Assimalign.Viu.Testing;

namespace Assimalign.Viu.DevTools.Client.Tests;

public sealed class ClientMalformedInputTests
{
    [Fact]
    public async Task Receive_NullOrMissingNestedMembers_DiscardsWholePayloadAndKeepsPanesUsable()
    {
        // [DVT-14]: malformed valid JSON must never publish null collections to compiled panes.
        ManualScheduler scheduler = new();
        ProbeTransport transport = new();
        await using DevToolsClientSession session = new(transport, scheduler);
        await Connect(session, transport, scheduler);
        session.SelectComponent(1);
        session.SelectInspector("sample");
        session.SelectInspectorNode("sample", "node");
        await transport.Receive("component.mounted", """{"identifier":1,"index":0,"name":"Counter"}""");
        scheduler.Drain();

        (string Type, string Payload)[] invalid =
        [
            ("handshake.response", "{}"),
            ("tree.snapshot", "{}"),
            ("tree.snapshot", """{"roots":null}"""),
            ("tree.snapshot", """{"roots":[null]}"""),
            ("tree.snapshot", """{"roots":[{"identifier":2,"name":"Broken","children":null}]}"""),
            ("tree.snapshot", """{"roots":[{"identifier":2,"children":[]}]}"""),
            ("component.mounted", """{"identifier":2,"index":0,"name":null}"""),
            ("component.snapshot", """{"identifier":1,"parameters":null,"state":[],"events":[]}"""),
            ("component.snapshot", """{"identifier":1,"parameters":[],"state":[null],"events":[]}"""),
            ("component.snapshot", """{"identifier":1,"parameters":[],"state":[{"name":"Count","value":null}],"events":[]}"""),
            ("component.snapshot", """{"identifier":1,"parameters":[],"state":[{"name":"Count","value":{"kind":"scalar","typeName":"int","path":[],"children":null}}],"events":[]}"""),
            ("component.snapshot", """{"identifier":1,"parameters":[],"state":[],"events":[null]}"""),
            ("component.expand", """{"identifier":1,"section":"state","path":null}"""),
            ("component.expand", """{"identifier":1,"section":"state","path":[null]}"""),
            ("state.edit.response", """{"identifier":1,"accepted":true,"path":null}"""),
            ("timeline.event", "{}"),
            ("timeline.layer.registered", """{"identifier":null,"displayName":"Broken"}"""),
            ("inspector.registered", """{"identifier":"sample","displayName":null}"""),
            ("inspector.tree", """{"inspectorIdentifier":"sample","roots":[{"identifier":"node","label":"Node","children":null}]}"""),
            ("inspector.state", """{"inspectorIdentifier":"sample","nodeIdentifier":"node","state":null}"""),
            ("inspector.state", """{"inspectorIdentifier":"sample","nodeIdentifier":"node","state":[{"name":null,"value":null}]}"""),
        ];
        foreach ((string type, string payload) in invalid)
        {
            await transport.Receive(type, payload);
        }
        await transport.Receive("future.message", """{"unknown":null}""");
        scheduler.Drain();

        session.IsConnected.ShouldBeTrue();
        session.Tree.Nodes.Count.ShouldBe(1);
        session.Snapshot.ShouldBeNull();
        session.Expansions.ShouldBeEmpty();
        session.LastEdit.ShouldBeNull();
        session.Timeline.Count.ShouldBe(0);
        session.Inspectors.ShouldBeEmpty();
        session.InspectorTree.ShouldBeNull();
        session.InspectorState.ShouldBeNull();
        using ComponentWrapper panel = Mount(session);
        await panel.FlushAsync();
        panel.Text().ShouldContain("Counter");

        await transport.Receive("component.snapshot", """{"identifier":1,"parameters":[],"state":[{"name":"","value":{"kind":"scalar","typeName":"System.String","displayValue":"empty key","expandable":false,"path":[""],"children":[]}}],"events":[]}""");
        scheduler.Drain();
        session.Snapshot.ShouldNotBeNull().State[0].Name.ShouldBe(string.Empty);
        await panel.FlushAsync();
    }

    [Fact]
    public async Task Receive_DuplicateTreeIdentitiesOrCyclicParentChange_PreservesPriorTree()
    {
        ManualScheduler scheduler = new();
        ProbeTransport transport = new();
        await using DevToolsClientSession session = new(transport, scheduler);
        await Connect(session, transport, scheduler);
        await transport.Receive("component.mounted", """{"identifier":1,"index":0,"name":"Root"}""");
        await transport.Receive("component.mounted", """{"identifier":2,"parentIdentifier":1,"index":0,"name":"Child"}""");
        scheduler.Drain();

        await transport.Receive("tree.snapshot", """{"roots":[{"identifier":3,"name":"Duplicate","children":[{"identifier":3,"name":"Duplicate","children":[]}]}]}""");
        await transport.Receive("component.updated", """{"identifier":1,"parentIdentifier":2,"index":0,"name":"Root"}""");
        scheduler.Drain();

        session.Tree.Nodes.Count.ShouldBe(2);
        session.Tree.Roots.Count.ShouldBe(1);
        session.Tree.Roots[0].Identifier.ShouldBe(1);
        session.Tree.Roots[0].Children.Count.ShouldBe(1);
    }

    [Fact]
    public async Task Handshake_UnknownEnvelopeVersion_DoesNotConnectOrResetUsableSession()
    {
        ManualScheduler scheduler = new();
        ProbeTransport transport = new();
        await using DevToolsClientSession session = new(transport, scheduler);
        await session.StartAsync();
        scheduler.Drain();
        await transport.Receive("handshake.response", """{"accepted":true,"version":1}""", version: 2);
        scheduler.Drain();
        session.IsConnected.ShouldBeFalse();
        await transport.Receive("handshake.response", """{"accepted":true,"version":1}""");
        scheduler.Drain();
        int generation = session.ConnectionGeneration;
        await transport.Receive("handshake.response", """{"accepted":false,"reason":"old"}""", version: 2);
        scheduler.Drain();
        session.IsConnected.ShouldBeTrue();
        session.ConnectionGeneration.ShouldBe(generation);
    }

    [Fact]
    public async Task InspectorSelection_LateTreeAndStateResponses_CannotReplaceCurrentSelection()
    {
        ManualScheduler scheduler = new();
        ProbeTransport transport = new();
        await using DevToolsClientSession session = new(transport, scheduler);
        await Connect(session, transport, scheduler);
        session.SelectInspector("first");
        session.SelectInspector("second");
        await transport.Receive("inspector.tree", """{"inspectorIdentifier":"second","roots":[]}""");
        await transport.Receive("inspector.tree", """{"inspectorIdentifier":"first","roots":[]}""");
        scheduler.Drain();
        session.InspectorTree.ShouldNotBeNull().InspectorIdentifier.ShouldBe("second");

        session.SelectInspectorNode("second", "old-node");
        session.SelectInspectorNode("second", "new-node");
        await transport.Receive("inspector.state", """{"inspectorIdentifier":"second","nodeIdentifier":"new-node","state":[]}""");
        await transport.Receive("inspector.state", """{"inspectorIdentifier":"second","nodeIdentifier":"old-node","state":[]}""");
        await transport.Receive("inspector.state", """{"inspectorIdentifier":"first","nodeIdentifier":"new-node","state":[]}""");
        scheduler.Drain();
        session.InspectorState.ShouldNotBeNull().NodeIdentifier.ShouldBe("new-node");

        await transport.Receive("inspector.unregistered", """{"identifier":"second"}""");
        await transport.Receive("inspector.tree", """{"inspectorIdentifier":"second","roots":[]}""");
        await transport.Receive("inspector.state", """{"inspectorIdentifier":"second","nodeIdentifier":"new-node","state":[]}""");
        scheduler.Drain();
        session.InspectorTree.ShouldBeNull();
        session.InspectorState.ShouldBeNull();
    }

    [Fact]
    public async Task Send_DeferredFailure_NotifiesConnectionErrorOnNextFrame()
    {
        ManualScheduler scheduler = new();
        ProbeTransport transport = new();
        await using DevToolsClientSession session = new(transport, scheduler);
        await Connect(session, transport, scheduler);
        TaskCompletionSource completion = new(TaskCreationOptions.RunContinuationsAsynchronously);
        transport.NextSend = completion.Task;
        int notifications = 0;
        session.Changed += () => notifications++;
        session.SelectComponent(1);
        scheduler.Drain();
        notifications.ShouldBe(1);

        completion.SetException(new InvalidOperationException("connection lost"));
        for (int attempt = 0; attempt < 100 && session.Error is null; attempt++)
        {
            await Task.Delay(5);
        }
        session.Error.ShouldBe("connection lost");
        session.IsConnected.ShouldBeFalse();
        scheduler.Drain();
        notifications.ShouldBe(2);
    }

    private static ComponentWrapper Mount(DevToolsClientSession session)
    {
        ComponentFactory components = new();
        GeneratedViuComponents.Register(components);
        return ComponentTest.Mount(components.Resolve(ComponentReference.ForName("DevToolsPanel")), new ComponentMountOptions
        {
            Arguments = new Dictionary<string, object?> { ["session"] = session },
            Components = components,
        });
    }

    private static async Task Connect(DevToolsClientSession session, ProbeTransport transport, ManualScheduler scheduler)
    {
        await session.StartAsync();
        scheduler.Drain();
        await transport.Receive("handshake.response", """{"accepted":true,"version":1}""");
        scheduler.Drain();
    }

    private sealed class ManualScheduler : IDevToolsClientScheduler
    {
        private readonly Queue<Action> _callbacks = new();
        public void Schedule(Action callback) => _callbacks.Enqueue(callback);
        internal void Drain()
        {
            int remaining = 1000;
            while (_callbacks.TryDequeue(out Action? callback))
            {
                remaining--.ShouldBeGreaterThan(0);
                callback();
            }
        }
    }

    private sealed class ProbeTransport : IDevToolsClientTransport
    {
        private Func<string, ValueTask>? _receiver;
        internal Task? NextSend { get; set; }
        public ValueTask StartAsync(Func<string, ValueTask> receiver, Action connected, CancellationToken cancellationToken = default)
        {
            _receiver = receiver;
            connected();
            return ValueTask.CompletedTask;
        }
        public ValueTask SendAsync(string message, CancellationToken cancellationToken = default)
        {
            Task? task = NextSend;
            NextSend = null;
            return task is null ? ValueTask.CompletedTask : new ValueTask(task);
        }
        internal ValueTask Receive(string type, string payload, int version = 1) =>
            _receiver!($"{{\"version\":{version},\"type\":\"{type}\",\"payload\":{payload}}}");
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
