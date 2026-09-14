using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

using Shouldly;
using Xunit;

using Assimalign.Viu.Components;
using Assimalign.Viu.DevTools;
using Assimalign.Viu.Testing;

namespace Assimalign.Viu.DevTools.Client.Tests;

public sealed class DevToolsPaneTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Panel_TreeClickAfterComponentDeltas_SelectsAndRequestsShallowSnapshot(bool changeChild)
    {
        // [DVT-14] A compiled row listener remains usable after keyed descendant mount/unmount.
        PaneScheduler scheduler = new();
        PaneTransport transport = new();
        await using DevToolsClientSession session = new(transport, scheduler);
        await Connect(session, transport, scheduler);
        await transport.Receive(ProtocolCodec.SerializeBatch([
            ProtocolCodec.CreateEnvelope("tree.snapshot", new ComponentTreeSnapshotPayload([new(1, "Counter", null, [])]),
                DevToolsJsonSerializerContext.Default.ComponentTreeSnapshotPayload)]));
        scheduler.Drain();
        using ComponentWrapper wrapper = Mount("DevToolsPanel", session);
        TestElement root = wrapper.Get("[data-testid=devtools-tree-node]").Element;
        if (changeChild)
        {
            await transport.Receive(ProtocolCodec.SerializeBatch([
                ProtocolCodec.CreateEnvelope("component.mounted", new ComponentChangePayload(2, 1, 0, "Child", null),
                    DevToolsJsonSerializerContext.Default.ComponentChangePayload)]));
            scheduler.Drain();
            await wrapper.FlushAsync();
            wrapper.FindAll("[data-testid=devtools-tree-node]").Count.ShouldBe(2);
            await transport.Receive(ProtocolCodec.SerializeBatch([
                ProtocolCodec.CreateEnvelope("component.unmounted", new ComponentIdentifierPayload(2),
                    DevToolsJsonSerializerContext.Default.ComponentIdentifierPayload)]));
            scheduler.Drain();
            await wrapper.FlushAsync();
            wrapper.FindAll("[data-testid=devtools-tree-node]").Count.ShouldBe(1);
        }
        wrapper.Get("[data-testid=devtools-tree-node]").Element.ShouldBeSameAs(root);
        root.EventListeners["click"].ShouldBeOfType<Action>();
        wrapper.Get("[data-testid=devtools-tree-node]").Attribute("aria-pressed").ShouldBe("false");

        await wrapper.Get("[data-testid=devtools-tree-node]").TriggerAsync("click");
        session.SelectedComponentIdentifier.ShouldBe(1);
        scheduler.Drain();
        await wrapper.FlushAsync();
        ProtocolEnvelope request = ProtocolCodec.DeserializeBatch(transport.Sent[^1])!.Messages.Single();
        request.Type.ShouldBe("component.snapshot.request");
        request.Payload.GetProperty("identifier").GetInt32().ShouldBe(1);
        request.Payload.GetProperty("depth").GetInt32().ShouldBe(0);
        wrapper.Get("[aria-label=\"Component state\"]").Text().ShouldNotContain("Select a component");
        wrapper.Get("[data-testid=devtools-tree-node]").Attribute("aria-pressed").ShouldBe("true");
    }

    [Fact]
    public async Task Panel_TimelineOnlyBatch_DoesNotRenderUnchangedPanes()
    {
        // [DVT-14] Timeline capture must not repeatedly flatten and render an unchanged component tree.
        PaneScheduler scheduler = new();
        PaneTransport transport = new();
        await using DevToolsClientSession session = new(transport, scheduler);
        await Connect(session, transport, scheduler);
        using ComponentWrapper wrapper = Mount("DevToolsPanel", session);
        ElementWrapper tree = wrapper.Get("[aria-label=\"Component tree\"]");
        ElementWrapper snapshot = wrapper.Get("[aria-label=\"Component state\"]");
        ElementWrapper inspectors = wrapper.Get("[aria-label=\"Custom inspectors\"]");
        ElementWrapper panel = wrapper.Get("[data-testid=devtools-panel]");
        object? treeGeneration = tree.Attribute("data-revision");
        object? snapshotGeneration = snapshot.Attribute("data-revision");
        object? inspectorGeneration = inspectors.Attribute("data-revision");
        object? panelGeneration = panel.Attribute("data-revision");
        await transport.Receive(ProtocolCodec.SerializeBatch([
            ProtocolCodec.CreateEnvelope("timeline.event", new TimelineEventPayload(1, 1, 1, "reactivity", "state.write", "Count"),
                DevToolsJsonSerializerContext.Default.TimelineEventPayload)]));
        scheduler.Drain();
        await wrapper.FlushAsync();

        wrapper.FindAll("[data-testid=devtools-timeline-event]").Count.ShouldBe(1);
        tree.Attribute("data-revision").ShouldBe(treeGeneration);
        snapshot.Attribute("data-revision").ShouldBe(snapshotGeneration);
        inspectors.Attribute("data-revision").ShouldBe(inspectorGeneration);
        panel.Attribute("data-revision").ShouldBe(panelGeneration);
    }

    [Theory]
    [InlineData("/")]
    [InlineData("\0")]
    public async Task SnapshotPane_DelimitersInMemberNames_KeepDistinctPathsAndExpansions(string delimiter)
    {
        // [DVT-14] Dictionary members may contain any delimiter; array paths remain distinct.
        PaneScheduler scheduler = new();
        PaneTransport transport = new();
        await using DevToolsClientSession session = new(transport, scheduler);
        await Connect(session, transport, scheduler);
        session.SelectComponent(1);
        scheduler.Drain();
        DevToolsValuePayload direct = new("scalar", "System.String", "direct value", false, ["left" + delimiter + "right"], []);
        DevToolsValuePayload nested = new("scalar", "System.String", "nested value", false, ["left", "right"], []);
        DevToolsValuePayload parent = new("object", "Dictionary", null, true, ["left"], [new("right", nested)]);
        await transport.Receive(ProtocolCodec.SerializeBatch([
            ProtocolCodec.CreateEnvelope("component.snapshot", new ComponentSnapshotResponsePayload(1, [],
                [new("direct", direct), new("parent", parent)], [], null),
                DevToolsJsonSerializerContext.Default.ComponentSnapshotResponsePayload)]));
        scheduler.Drain();
        using ComponentWrapper wrapper = Mount("DevToolsSnapshotPane", session);
        TestElement directElement = wrapper.FindAll("[data-testid=devtools-state-row]")[0].Element;
        await transport.Receive(ProtocolCodec.SerializeBatch([
            ProtocolCodec.CreateEnvelope("component.expand", new ComponentExpansionResponsePayload(1, "state", ["left", "right"],
                nested with { DisplayValue = "nested updated" }, null),
                DevToolsJsonSerializerContext.Default.ComponentExpansionResponsePayload)]));
        scheduler.Drain();
        await wrapper.FlushAsync();

        IReadOnlyList<ElementWrapper> rows = wrapper.FindAll("[data-testid=devtools-state-row]");
        rows.Count.ShouldBe(3);
        rows[0].Element.ShouldBeSameAs(directElement);
        rows[0].Text().ShouldContain("direct value");
        rows[2].Text().ShouldContain("nested updated");
    }

    [Fact]
    public async Task TimelinePane_LargeHistory_RendersOnlyWindowAndSupportsRangeAndCorrelations()
    {
        // [DVT-14] The compiled pane renders 50 rows even when the retained protocol history is larger.
        PaneScheduler scheduler = new();
        PaneTransport transport = new();
        await using DevToolsClientSession session = new(transport, scheduler);
        await Connect(session, transport, scheduler);
        List<ProtocolEnvelope> messages =
        [
            ProtocolCodec.CreateEnvelope("timeline.layer.registered", new TimelineLayerPayload("reactivity", "Reactivity", null),
                DevToolsJsonSerializerContext.Default.TimelineLayerPayload),
        ];
        for (int index = 0; index < 120; index++)
        {
            messages.Add(ProtocolCodec.CreateEnvelope("timeline.event",
                new TimelineEventPayload(index + 1, index, index / 3 + 1, "reactivity", "state.write", "Count"),
                DevToolsJsonSerializerContext.Default.TimelineEventPayload));
        }
        await transport.Receive(ProtocolCodec.SerializeBatch(messages));
        scheduler.Drain();
        using ComponentWrapper wrapper = Mount("DevToolsTimelinePane", session);

        wrapper.FindAll("[data-testid=devtools-timeline-event]").Count.ShouldBe(50);
        wrapper.Get("button").Attribute("aria-pressed").ShouldBe("true");
        wrapper.Get("[data-testid=devtools-timeline-layer]").Attribute("aria-pressed").ShouldBe("false");
        await wrapper.Get("[data-testid=devtools-timeline-layer]").TriggerAsync("click");
        wrapper.Get("button").Attribute("aria-pressed").ShouldBe("false");
        wrapper.Get("[data-testid=devtools-timeline-layer]").Attribute("aria-pressed").ShouldBe("true");
        wrapper.FindAll("[data-testid=devtools-timeline-event]").ShouldAllBe(row => Equals(row.Attribute("data-related"), "false"));
        await wrapper.Get("[data-testid=devtools-timeline-next]").TriggerAsync("click");
        wrapper.FindAll("[data-testid=devtools-timeline-event]")[0].Attribute("data-sequence").ShouldBe(51L);
        await wrapper.Get("[data-testid=devtools-timeline-from]").SetValueAsync("30");
        await wrapper.Get("[data-testid=devtools-timeline-to]").SetValueAsync("33");
        await wrapper.Get("[data-testid=devtools-timeline-zoom]").TriggerAsync("click");
        wrapper.FindAll("[data-testid=devtools-timeline-event]").Count.ShouldBe(4);
        await wrapper.FindAll("[data-testid=devtools-timeline-event]")[0].TriggerAsync("click");
        IReadOnlyList<ElementWrapper> visibleEvents = wrapper.FindAll("[data-testid=devtools-timeline-event]");
        visibleEvents.Count(row => Equals(row.Attribute("data-related"), "true")).ShouldBe(3);
        visibleEvents.Count(row => Equals(row.Attribute("data-related"), "false")).ShouldBe(1);
        visibleEvents[0].Attribute("aria-pressed").ShouldBe("true");
        visibleEvents.Skip(1).ShouldAllBe(row => Equals(row.Attribute("aria-pressed"), "false"));
        wrapper.Get("[data-testid=devtools-timeline-selection]").Text().ShouldContain("3 related events");

        // A new accepted handshake resets local pane selection/range even when it shares one batch
        // with reused sequence identities and never exposes a disconnected frame to the UI.
        await transport.Receive(ProtocolCodec.SerializeBatch([
            ProtocolCodec.CreateEnvelope("handshake.response", new HandshakeResponsePayload(true, 1, null),
                DevToolsJsonSerializerContext.Default.HandshakeResponsePayload),
            ProtocolCodec.CreateEnvelope("timeline.event", new TimelineEventPayload(31, 900, 1, "reactivity", "state.write", "Reloaded"),
                DevToolsJsonSerializerContext.Default.TimelineEventPayload)]));
        scheduler.Drain();
        await wrapper.FlushAsync();
        wrapper.FindAll("[data-testid=devtools-timeline-event]").Count.ShouldBe(1);
        wrapper.Get("[data-testid=devtools-timeline-selection]").Text().ShouldContain("Select an event");
    }

    [Fact]
    public async Task SnapshotPane_ExplicitExpansionAndEdit_SendsExactPathAndTypedValue()
    {
        // [DVT-13], [DVT-14] Rendering never performs an eager expansion and the setter stays remote.
        PaneScheduler scheduler = new();
        PaneTransport transport = new();
        await using DevToolsClientSession session = new(transport, scheduler);
        await Connect(session, transport, scheduler);
        session.SelectComponent(1);
        scheduler.Drain();
        DevToolsValuePayload reference = new("reference", "Reference", null, true, ["count"], []);
        await transport.Receive(ProtocolCodec.SerializeBatch([
            ProtocolCodec.CreateEnvelope("component.snapshot",
                new ComponentSnapshotResponsePayload(1, [], [new("count", reference)], [], null),
                DevToolsJsonSerializerContext.Default.ComponentSnapshotResponsePayload)]));
        scheduler.Drain();
        using ComponentWrapper wrapper = Mount("DevToolsSnapshotPane", session);
        transport.Sent.Any(frame => frame.Contains("component.expand.request", StringComparison.Ordinal)).ShouldBeFalse();

        await wrapper.Get("[data-testid=devtools-expand]").TriggerAsync("click");
        scheduler.Drain();
        ProtocolEnvelope expansionRequest = ProtocolCodec.DeserializeBatch(transport.Sent[^1])!.Messages.Single();
        expansionRequest.Type.ShouldBe("component.expand.request");
        expansionRequest.Payload.GetProperty("depth").GetInt32().ShouldBe(1);
        DevToolsValuePayload scalar = new("scalar", "System.Int32", "1", false, ["count", "value"], []);
        await transport.Receive(ProtocolCodec.SerializeBatch([
            ProtocolCodec.CreateEnvelope("component.expand",
                new ComponentExpansionResponsePayload(1, "state", ["count"], reference with { Children = [new("value", scalar)] }, null),
                DevToolsJsonSerializerContext.Default.ComponentExpansionResponsePayload)]));
        scheduler.Drain();
        await wrapper.FlushAsync();
        await wrapper.Get("[data-testid=devtools-edit-select]").TriggerAsync("click");
        wrapper.Get("[data-testid=devtools-edit-value]").Element.EventListeners["input"].ShouldBeOfType<Action<IElementEvent>>();
        await wrapper.Get("[data-testid=devtools-edit-value]").SetValueAsync("42");
        await wrapper.Get("[data-testid=devtools-edit-apply]").TriggerAsync("click");
        scheduler.Drain();

        ProtocolEnvelope editRequest = ProtocolCodec.DeserializeBatch(transport.Sent[^1])!.Messages.Single();
        editRequest.Type.ShouldBe("state.edit.request");
        editRequest.Payload.GetProperty("value").GetInt32().ShouldBe(42);
        editRequest.Payload.GetProperty("path").EnumerateArray().Select(value => value.GetString()).ShouldBe(["count", "value"]);
    }

    [Fact]
    public async Task InspectorsPane_UnknownProvider_RendersOnlyProtocolLabelsAndValues()
    {
        // [DVT-7], [DVT-14] No provider type is known to the pane or its assembly.
        PaneScheduler scheduler = new();
        PaneTransport transport = new();
        await using DevToolsClientSession session = new(transport, scheduler);
        await Connect(session, transport, scheduler);
        await transport.Receive(ProtocolCodec.SerializeBatch([
            ProtocolCodec.CreateEnvelope("inspector.registered", new InspectorRegistrationPayload("unknown-provider", "Remote inventory"),
                DevToolsJsonSerializerContext.Default.InspectorRegistrationPayload)]));
        scheduler.Drain();
        using ComponentWrapper wrapper = Mount("DevToolsInspectorsPane", session);
        wrapper.Get("[data-testid=devtools-inspector-select]").Attribute("aria-pressed").ShouldBe("false");
        await wrapper.Get("[data-testid=devtools-inspector-select]").TriggerAsync("click");
        wrapper.Get("[data-testid=devtools-inspector-select]").Attribute("aria-pressed").ShouldBe("true");
        scheduler.Drain();
        await transport.Receive(ProtocolCodec.SerializeBatch([
            ProtocolCodec.CreateEnvelope("inspector.tree", new InspectorTreeResponsePayload("unknown-provider", [new("warehouse", "Warehouse north", [])], null),
                DevToolsJsonSerializerContext.Default.InspectorTreeResponsePayload)]));
        scheduler.Drain();
        await wrapper.FlushAsync();
        wrapper.Get("[data-testid=devtools-inspector-node]").Text().ShouldBe("Warehouse north");
        await wrapper.Get("[data-testid=devtools-inspector-node]").TriggerAsync("click");
        scheduler.Drain();
        await transport.Receive(ProtocolCodec.SerializeBatch([
            ProtocolCodec.CreateEnvelope("inspector.state", new InspectorStateResponsePayload("unknown-provider", "warehouse",
                [new("available", new("scalar", "System.Int32", "12", false, ["available"], []))], null),
                DevToolsJsonSerializerContext.Default.InspectorStateResponsePayload)]));
        scheduler.Drain();
        await wrapper.FlushAsync();
        wrapper.Get("[data-testid=devtools-inspector-state]").Text().ShouldContain("available");
        wrapper.Get("[data-testid=devtools-inspector-state]").Text().ShouldContain("12");
    }

    private static ComponentWrapper Mount(string name, DevToolsClientSession session)
    {
        ComponentFactory components = new();
        GeneratedViuComponents.Register(components);
        return ComponentTest.Mount(components.Resolve(ComponentReference.ForName(name)), new ComponentMountOptions
        {
            Arguments = new Dictionary<string, object?> { ["session"] = session },
            Components = components,
        });
    }

    private static async Task Connect(DevToolsClientSession session, PaneTransport transport, PaneScheduler scheduler)
    {
        await session.StartAsync();
        scheduler.Drain();
        await transport.Receive("""{"version":1,"type":"handshake.response","payload":{"accepted":true,"version":1}}""");
        scheduler.Drain();
    }

    private sealed class PaneScheduler : IDevToolsClientScheduler
    {
        private readonly Queue<Action> _callbacks = new();

        public void Schedule(Action callback) => _callbacks.Enqueue(callback);

        internal void Drain()
        {
            while (_callbacks.TryDequeue(out Action? callback))
            {
                callback();
            }
        }
    }

    private sealed class PaneTransport : IDevToolsClientTransport
    {
        private Func<string, ValueTask>? _receiver;

        internal List<string> Sent { get; } = [];

        public ValueTask StartAsync(Func<string, ValueTask> receiver, Action connected, CancellationToken cancellationToken = default)
        {
            _receiver = receiver;
            connected();
            return ValueTask.CompletedTask;
        }

        public ValueTask SendAsync(string message, CancellationToken cancellationToken = default)
        {
            Sent.Add(message);
            return ValueTask.CompletedTask;
        }

        internal ValueTask Receive(string message) => _receiver!(message);

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
