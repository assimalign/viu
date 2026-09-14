using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using System.Threading;
using System.Threading.Tasks;

using Assimalign.Viu.DevTools;

namespace Assimalign.Viu.DevTools.Client;

/// <summary>Runs the protocol-only inspection client on one event loop. Not thread-safe. Specified by <c>[DVT-13]</c> and <c>[DVT-14]</c>.</summary>
/// <remarks>Transport callbacks only queue frames. A deferred scheduler applies bounded messages and sends requests after reception returns.</remarks>
public sealed class DevToolsClientSession : IAsyncDisposable
{
    private readonly IDevToolsClientTransport _transport;
    private readonly IDevToolsClientScheduler _scheduler;
    private readonly int _messagesPerFrame;
    private readonly Queue<ProtocolEnvelope> _incoming = new();
    private readonly Queue<ProtocolEnvelope> _outgoing = new();
    private readonly Dictionary<string, InspectorRegistrationPayload> _inspectors = new(StringComparer.Ordinal);
    private readonly List<ComponentExpansionResponsePayload> _expansions = [];
    private readonly CancellationTokenSource _cancellation = new();
    private bool _scheduled;
    private bool _started;
    private bool _disposed;
    private bool _sending;

    // Pane invalidation is separate from transport frames: timeline traffic must not traverse trees.
    internal int TreeRevision { get; private set; }
    internal int SnapshotRevision { get; private set; }
    internal int TimelineRevision { get; private set; }
    internal int InspectorRevision { get; private set; }
    private string? _selectedInspectorIdentifier;
    private string? _selectedInspectorNodeIdentifier;

    /// <summary>Creates a session with a bounded message budget per deferred frame and a bounded timeline.</summary>
    public DevToolsClientSession(IDevToolsClientTransport transport, IDevToolsClientScheduler? scheduler = null, int messagesPerFrame = 128, int timelineCapacity = 2048)
    {
        ArgumentNullException.ThrowIfNull(transport);
        ArgumentOutOfRangeException.ThrowIfLessThan(messagesPerFrame, 1);
        _transport = transport;
        _scheduler = scheduler ?? new ClientFrameScheduler();
        _messagesPerFrame = messagesPerFrame;
        Timeline = new(timelineCapacity);
    }
    /// <summary>Notifies once per applied frame, never once per incoming timeline event.</summary>
    public event Action? Changed;
    /// <summary>Gets whether version 1 has been accepted.</summary>
    public bool IsConnected { get; private set; }
    /// <summary>Gets the reset generation so panes discard local selections even when a new handshake is accepted within one frame.</summary>
    public int ConnectionGeneration { get; private set; }
    /// <summary>Gets the last connection or request diagnostic.</summary>
    public string? Error { get; private set; }
    /// <summary>Gets the live incrementally updated component tree.</summary>
    public DevToolsComponentTreeStore Tree { get; } = new();
    /// <summary>Gets the bounded timeline store.</summary>
    public DevToolsTimelineStore Timeline { get; }
    /// <summary>Gets the selected connection-local identity.</summary>
    public int? SelectedComponentIdentifier { get; private set; }
    /// <summary>Gets the latest shallow selected snapshot.</summary>
    public ComponentSnapshotResponsePayload? Snapshot { get; private set; }
    /// <summary>Gets explicitly requested lazy expansions for the selected node.</summary>
    public IReadOnlyList<ComponentExpansionResponsePayload> Expansions => _expansions;
    /// <summary>Gets the last edit acknowledgement, including rejection reasons.</summary>
    public StateEditResponsePayload? LastEdit { get; private set; }
    /// <summary>Gets generic inspector registrations.</summary>
    public IReadOnlyDictionary<string, InspectorRegistrationPayload> Inspectors => _inspectors;
    /// <summary>Gets the requested generic inspector tree.</summary>
    public InspectorTreeResponsePayload? InspectorTree { get; private set; }
    /// <summary>Gets the requested generic inspector state.</summary>
    public InspectorStateResponsePayload? InspectorState { get; private set; }
    /// <summary>Gets queued message count for deterministic frame-budget verification.</summary>
    public int PendingMessageCount => _incoming.Count;

    /// <summary>Starts transport reception and schedules the initial handshake.</summary>
    public async ValueTask StartAsync(CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (_started)
        {
            return;
        }

        _started = true;
        try { await _transport.StartAsync(ReceiveAsync, Reconnect, cancellationToken); }
        catch { _started = false; throw; }
    }

    /// <summary>Queues a fresh handshake and discards all previous connection-local data and requests.</summary>
    public void Reconnect()
    {
        if (_disposed)
        {
            return;
        }

        _incoming.Clear();
        _outgoing.Clear();
        Reset();
        Queue("handshake.request", new HandshakeRequestPayload([1]), DevToolsJsonSerializerContext.Default.HandshakeRequestPayload);
    }

    /// <summary>Selects a node and requests depth zero; deeper state is fetched only on expansion.</summary>
    public void SelectComponent(int identifier)
    {
        TreeRevision++;
        SnapshotRevision++;
        SelectedComponentIdentifier = identifier;
        Snapshot = null;
        LastEdit = null;
        _expansions.Clear();
        RequestSnapshot(identifier);
    }
    /// <summary>Loads one additional level for a selected state or parameter path.</summary>
    public void Expand(string section, IReadOnlyList<string> path)
    {
        if (SelectedComponentIdentifier is not int identifier)
        {
            return;
        }

        Queue("component.expand.request", new ComponentExpansionRequestPayload(identifier, section, [.. path], 1), DevToolsJsonSerializerContext.Default.ComponentExpansionRequestPayload);
    }
    /// <summary>Sends a JSON value for a selected state path; the runtime decides editability and coercion.</summary>
    public void EditState(string section, IReadOnlyList<string> path, string jsonValue)
    {
        if (SelectedComponentIdentifier is not int identifier)
        {
            return;
        }

        using JsonDocument document = JsonDocument.Parse(jsonValue);
        Queue("state.edit.request", new StateEditRequestPayload(identifier, section, [.. path], document.RootElement.Clone()), DevToolsJsonSerializerContext.Default.StateEditRequestPayload);
    }
    /// <summary>Requests any registered inspector's tree without inspector-specific client code.</summary>
    public void SelectInspector(string identifier)
    {
        InspectorRevision++;
        _selectedInspectorIdentifier = identifier;
        _selectedInspectorNodeIdentifier = null;
        InspectorTree = null;
        InspectorState = null;
        Queue("inspector.tree.request", new InspectorTreeRequestPayload(identifier), DevToolsJsonSerializerContext.Default.InspectorTreeRequestPayload);
    }
    /// <summary>Requests shallow state for a generic inspector node.</summary>
    public void SelectInspectorNode(string inspectorIdentifier, string nodeIdentifier)
    {
        InspectorRevision++;
        if (_selectedInspectorIdentifier != inspectorIdentifier)
        {
            InspectorTree = null;
        }
        _selectedInspectorIdentifier = inspectorIdentifier;
        _selectedInspectorNodeIdentifier = nodeIdentifier;
        InspectorState = null;
        Queue("inspector.state.request", new InspectorStateRequestPayload(inspectorIdentifier, nodeIdentifier, 1), DevToolsJsonSerializerContext.Default.InspectorStateRequestPayload);
    }

    /// <summary>Cancels reception, releases transport listeners, and makes scheduled callbacks inert.</summary>
    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _cancellation.Cancel();
        await _transport.DisposeAsync();
        _incoming.Clear(); _outgoing.Clear(); Reset(); Changed = null;
        _cancellation.Dispose();
    }

    private ValueTask ReceiveAsync(string json)
    {
        if (_disposed)
        {
            return ValueTask.CompletedTask;
        }

        try
        {
            // The transport's 1 MiB frame bound limits JSON parse cost; application is frame-budgeted.
            if (json.Length > 1024 * 1024)
            {
                return ValueTask.CompletedTask;
            }

            using JsonDocument document = JsonDocument.Parse(json);
            JsonElement root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
            {
                return ValueTask.CompletedTask;
            }

            List<ProtocolEnvelope>? messages;
            if (root.TryGetProperty("messages", out _))
            {
                ProtocolBatch? batch = ProtocolCodec.DeserializeBatch(json);
                if (batch?.Protocol != DevToolsProtocol.Name)
                {
                    return ValueTask.CompletedTask;
                }

                messages = batch.Messages;
            }
            else
            {
                ProtocolEnvelope? envelope = root.Deserialize(DevToolsJsonSerializerContext.Default.ProtocolEnvelope);
                messages = envelope is null ? null : [envelope];
            }
            if (messages is null)
            {
                return ValueTask.CompletedTask;
            }

            foreach (ProtocolEnvelope envelope in messages)
            {
                if (envelope is null || envelope.Type is null || envelope.Payload.ValueKind != JsonValueKind.Object)
                {
                    continue;
                }

                if (_incoming.Count >= 16384)
                {
                    Reconnect();
                    Error = "The client receive window overflowed; requesting a fresh connection snapshot.";
                    return ValueTask.CompletedTask;
                }
                _incoming.Enqueue(envelope);
            }
            Schedule();
        }
        catch (JsonException) { }
        return ValueTask.CompletedTask;
    }

    private void Queue<T>(string type, T payload, JsonTypeInfo<T> information)
    {
        if (_disposed || !IsConnected && type != "handshake.request")
        {
            return;
        }

        _outgoing.Enqueue(ProtocolCodec.CreateEnvelope(type, payload, information));
        Schedule();
    }
    private void Schedule()
    {
        if (_scheduled || _disposed)
        {
            return;
        }

        _scheduled = true;
        _scheduler.Schedule(ApplyFrame);
    }
    private void ApplyFrame()
    {
        _scheduled = false;
        if (_disposed)
        {
            return;
        }

        int count = 0;
        while (count++ < _messagesPerFrame && _incoming.TryDequeue(out ProtocolEnvelope? envelope))
        {
            try { Apply(envelope); }
            catch (JsonException) { }
            catch (ArgumentException) { }
        }
        Changed?.Invoke();
        if (!_sending && _outgoing.Count > 0)
        {
            _ = SendAsync();
        }

        if (_incoming.Count > 0)
        {
            Schedule();
        }
    }
    private async Task SendAsync()
    {
        _sending = true;
        List<ProtocolEnvelope> messages = [];
        while (_outgoing.TryDequeue(out ProtocolEnvelope? envelope))
        {
            messages.Add(envelope);
        }

        try { await _transport.SendAsync(ProtocolCodec.SerializeBatch(messages), _cancellation.Token); }
        catch (Exception exception) when (exception is not OutOfMemoryException)
        {
            if (!_disposed)
            {
                IsConnected = false; Error = exception.Message;
                Schedule();
            }
        }
        finally
        {
            _sending = false;
            if (_outgoing.Count > 0)
            {
                Schedule();
            }
        }
    }
    private void RequestSnapshot(int identifier) => Queue("component.snapshot.request", new ComponentSnapshotRequestPayload(identifier, 0), DevToolsJsonSerializerContext.Default.ComponentSnapshotRequestPayload);
    private void RequestTree() => Queue("tree.snapshot.request", new EmptyPayload(), DevToolsJsonSerializerContext.Default.EmptyPayload);
    private void Reset()
    {
        ConnectionGeneration++;
        TreeRevision++;
        SnapshotRevision++;
        TimelineRevision++;
        InspectorRevision++;
        IsConnected = false; Error = null; SelectedComponentIdentifier = null; Snapshot = null; LastEdit = null;
        Tree.Clear(); Timeline.Clear(); _inspectors.Clear(); _expansions.Clear(); InspectorTree = null; InspectorState = null;
        _selectedInspectorIdentifier = null; _selectedInspectorNodeIdentifier = null;
    }
    private void Apply(ProtocolEnvelope envelope)
    {
        if (envelope.Version != DevToolsProtocol.CurrentVersion)
        {
            return;
        }
        JsonElement payload = envelope.Payload;
        if (envelope.Type == "handshake.response")
        {
            HandshakeResponsePayload? response = ReadPayload(payload, DevToolsJsonSerializerContext.Default.HandshakeResponsePayload);
            if (response is null)
            {
                return;
            }

            _outgoing.Clear(); Reset();
            IsConnected = response.Accepted && response.Version == DevToolsProtocol.CurrentVersion;
            Error = IsConnected ? null : response.Reason ?? "Unsupported protocol version.";
            if (IsConnected)
            {
                RequestTree();
            }

            return;
        }
        if (!IsConnected)
        {
            return;
        }

        switch (envelope.Type)
        {
            case "component.mounted":
            case "component.updated":
            case "component.reordered":
            case "tree.snapshot":
                TreeRevision++;
                break;
            case "component.unmounted":
                TreeRevision++;
                SnapshotRevision++;
                break;
            case "component.snapshot":
            case "component.expand":
            case "state.edit.response":
                SnapshotRevision++;
                break;
            case "timeline.event":
            case "timeline.dropped":
            case "timeline.layer.registered":
            case "timeline.layer.unregistered":
                TimelineRevision++;
                break;
            case "inspector.registered":
            case "inspector.unregistered":
            case "inspector.tree":
            case "inspector.state":
                InspectorRevision++;
                break;
        }

        switch (envelope.Type)
        {
            case "component.mounted":
            case "component.updated":
                if (ReadPayload(payload, DevToolsJsonSerializerContext.Default.ComponentChangePayload) is { } change)
                {
                    if (!CreatesParentCycle(change.Identifier, change.ParentIdentifier))
                    {
                        Tree.Apply(change);
                    }
                }

                break;
            case "component.unmounted":
                if (ReadPayload(payload, DevToolsJsonSerializerContext.Default.ComponentIdentifierPayload) is { } removed)
                {
                    Tree.Remove(removed.Identifier);
                    if (SelectedComponentIdentifier is int selected && !Tree.Nodes.ContainsKey(selected))
                    { SelectedComponentIdentifier = null; Snapshot = null; _expansions.Clear(); }
                }
                break;
            case "component.reordered":
                if (ReadPayload(payload, DevToolsJsonSerializerContext.Default.ComponentReorderPayload) is { } reordered)
                {
                    Tree.Reorder(reordered.Identifier, reordered.Index);
                }

                break;
            case "tree.snapshot":
                if (ReadPayload(payload, DevToolsJsonSerializerContext.Default.ComponentTreeSnapshotPayload) is { } tree)
                {
                    Tree.Replace(tree);
                    if (SelectedComponentIdentifier is int selected && !Tree.Nodes.ContainsKey(selected))
                    {
                        SelectedComponentIdentifier = null;
                        Snapshot = null;
                        LastEdit = null;
                        _expansions.Clear();
                        SnapshotRevision++;
                    }
                }

                break;
            case "telemetry.dropped": RequestTree(); break;
            case "component.snapshot":
                if (ReadPayload(payload, DevToolsJsonSerializerContext.Default.ComponentSnapshotResponsePayload) is { } snapshot && snapshot.Identifier == SelectedComponentIdentifier)
                {
                    Snapshot = snapshot;
                }

                break;
            case "component.expand":
                if (ReadPayload(payload, DevToolsJsonSerializerContext.Default.ComponentExpansionResponsePayload) is { } expansion && expansion.Identifier == SelectedComponentIdentifier)
                {
                    _expansions.RemoveAll(value => value.Section == expansion.Section && PathsEqual(value.Path, expansion.Path));
                    _expansions.Add(expansion);
                }
                break;
            case "state.edit.response":
                if (ReadPayload(payload, DevToolsJsonSerializerContext.Default.StateEditResponsePayload) is { } edit && edit.Identifier == SelectedComponentIdentifier)
                { LastEdit = edit; if (edit.Accepted) { _expansions.Clear(); RequestSnapshot(edit.Identifier); } }
                break;
            case "timeline.event":
                TimelineEventPayload observation = payload.Deserialize(DevToolsJsonSerializerContext.Default.TimelineEventPayload);
                if (ClientProtocolValidation.IsValid(observation))
                {
                    Timeline.Apply(observation);
                }
                break;
            case "timeline.dropped":
                if (ReadPayload(payload, DevToolsJsonSerializerContext.Default.TimelineDroppedPayload) is { } dropped)
                {
                    Timeline.Apply(dropped);
                }

                break;
            case "timeline.layer.registered":
                if (ReadPayload(payload, DevToolsJsonSerializerContext.Default.TimelineLayerPayload) is { } layer)
                {
                    Timeline.RegisterLayer(layer);
                }

                break;
            case "timeline.layer.unregistered":
                if (ReadPayload(payload, DevToolsJsonSerializerContext.Default.InspectorIdentifierPayload) is { } removedLayer)
                {
                    Timeline.RemoveLayer(removedLayer.Identifier);
                }

                break;
            case "inspector.registered":
                if (ReadPayload(payload, DevToolsJsonSerializerContext.Default.InspectorRegistrationPayload) is { } inspector)
                {
                    _inspectors[inspector.Identifier] = inspector;
                }

                break;
            case "inspector.unregistered":
                if (ReadPayload(payload, DevToolsJsonSerializerContext.Default.InspectorIdentifierPayload) is { } removedInspector)
                {
                    _inspectors.Remove(removedInspector.Identifier);
                    if (_selectedInspectorIdentifier == removedInspector.Identifier)
                    {
                        _selectedInspectorIdentifier = null; _selectedInspectorNodeIdentifier = null;
                        InspectorTree = null; InspectorState = null;
                    }
                }
                break;
            case "inspector.tree":
                if (ReadPayload(payload, DevToolsJsonSerializerContext.Default.InspectorTreeResponsePayload) is { } inspectorTree
                    && inspectorTree.InspectorIdentifier == _selectedInspectorIdentifier)
                {
                    InspectorTree = inspectorTree;
                }
                break;
            case "inspector.state":
                if (ReadPayload(payload, DevToolsJsonSerializerContext.Default.InspectorStateResponsePayload) is { } inspectorState
                    && inspectorState.InspectorIdentifier == _selectedInspectorIdentifier
                    && inspectorState.NodeIdentifier == _selectedInspectorNodeIdentifier)
                {
                    InspectorState = inspectorState;
                }
                break;
        }
    }

    private static T? ReadPayload<T>(JsonElement payload, JsonTypeInfo<T> information) where T : class
    {
        T? value = payload.Deserialize(information);
        return ClientProtocolValidation.IsValid(value) ? value : null;
    }

    private bool CreatesParentCycle(int identifier, int? parentIdentifier)
    {
        int remaining = Tree.Nodes.Count + 1;
        while (parentIdentifier is int parent)
        {
            if (parent == identifier || remaining-- == 0)
            {
                return true;
            }
            if (!Tree.Nodes.TryGetValue(parent, out DevToolsComponentNode? node))
            {
                return false;
            }
            parentIdentifier = node.ParentIdentifier;
        }
        return false;
    }
    private static bool PathsEqual(List<string> left, List<string> right)
    {
        if (left.Count != right.Count)
        {
            return false;
        }

        for (int index = 0; index < left.Count; index++)
        {
            if (left[index] != right[index])
            {
                return false;
            }
        }

        return true;
    }
}
