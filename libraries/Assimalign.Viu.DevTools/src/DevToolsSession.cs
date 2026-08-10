using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using System.Threading;
using System.Threading.Tasks;

using Assimalign.Viu;
using Assimalign.Viu.Reactivity;

namespace Assimalign.Viu.DevTools;

/// <summary>
/// Connects Core's optional runtime hook to one versioned diagnostics-protocol transport.
/// </summary>
/// <remarks>
/// Hook calls only update weak identity metadata and enqueue bounded telemetry. Reliable control
/// messages use a separate queue; a session-wide sequence preserves their shared enqueue order at
/// drain time. One scheduler callback drains both queues after rendering and one asynchronous send
/// carries the whole batch, so transport work never blocks or re-enters the renderer. The session
/// is single-threaded by design. Specified by <c>[DVT-1]</c> through <c>[DVT-7]</c>.
/// </remarks>
public sealed class DevToolsSession : IAsyncDisposable, IRuntimeInspectionHook
{
    private readonly IDevToolsTransport _transport;
    private readonly DevToolsSessionOptions _options;
    private readonly BoundedMessageBuffer _telemetryBuffer;
    private readonly Queue<SequencedProtocolEnvelope> _controlMessages = [];
    private readonly ComponentInspectionRegistry _components = new();
    private readonly SnapshotValueEncoder _valueEncoder;
    private readonly Dictionary<string, IDevToolsInspector> _inspectors =
        new(StringComparer.Ordinal);
    private readonly Dictionary<string, DevToolsTimelineLayer> _timelineLayers =
        new(StringComparer.Ordinal);
    private readonly CancellationTokenSource _sessionCancellation = new();
    private readonly SchedulerJob _flushJob;
    private IDisposable? _runtimeHookRegistration;
    private Task _activeSend = Task.CompletedTask;
    private long _nextMessageSequence;
    private int? _negotiatedVersion;
    private bool _flushQueued;
    private bool _started;
    private bool _disposed;

    /// <summary>Initializes a session over a caller-owned transport configuration.</summary>
    /// <param name="transport">The transport disposed with this session.</param>
    /// <param name="options">Optional bounded-buffer and snapshot limits.</param>
    public DevToolsSession(
        IDevToolsTransport transport,
        DevToolsSessionOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(transport);
        _transport = transport;
        _options = options ?? new DevToolsSessionOptions();
        _telemetryBuffer = new BoundedMessageBuffer(_options.BufferCapacity);
        _valueEncoder = new SnapshotValueEncoder(_options.MaximumCollectionEntries);
        _flushJob = new SchedulerJob(Flush)
        {
            Name = "runtime inspection protocol flush",
        };
    }

    /// <summary>
    /// Starts inbound transport delivery and attaches this session to Core's feature-switched
    /// runtime hook.
    /// </summary>
    /// <param name="cancellationToken">Cancels startup before the session is attached.</param>
    /// <returns>A value task that completes when the session is ready.</returns>
    public async ValueTask StartAsync(CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (_started)
        {
            return;
        }

        IDisposable hookRegistration = RuntimeInspection.Use(this);
        try
        {
            await _transport.StartAsync(ProcessIncomingAsync, cancellationToken)
                .ConfigureAwait(false);
        }
        catch
        {
            hookRegistration.Dispose();
            throw;
        }

        _runtimeHookRegistration = hookRegistration;
        _started = true;
    }

    /// <summary>Registers a named custom inspector until the returned lease is disposed.</summary>
    /// <param name="inspector">The reflection-free tree and state provider.</param>
    /// <returns>A lease that unregisters this exact inspector.</returns>
    public IDisposable RegisterInspector(IDevToolsInspector inspector)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(inspector);
        ArgumentException.ThrowIfNullOrEmpty(inspector.Identifier);
        ArgumentException.ThrowIfNullOrEmpty(inspector.DisplayName);
        if (!_inspectors.TryAdd(inspector.Identifier, inspector))
        {
            throw new InvalidOperationException(
                $"A custom inspector named '{inspector.Identifier}' is already registered.");
        }

        if (_started)
        {
            EnqueueInspectorRegistration(inspector);
        }

        return new Registration(
            () => UnregisterInspector(inspector.Identifier, inspector));
    }

    /// <summary>
    /// Registers custom timeline-layer metadata without enabling timeline event recording.
    /// </summary>
    /// <param name="layer">The stable layer identity and presentation metadata.</param>
    /// <returns>A lease that unregisters this exact layer.</returns>
    public IDisposable RegisterTimelineLayer(DevToolsTimelineLayer layer)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(layer);
        if (!_timelineLayers.TryAdd(layer.Identifier, layer))
        {
            throw new InvalidOperationException(
                $"A timeline layer named '{layer.Identifier}' is already registered.");
        }

        if (_started)
        {
            EnqueueTimelineLayerRegistration(layer);
        }

        return new Registration(
            () => UnregisterTimelineLayer(layer.Identifier, layer));
    }

    /// <summary>Detaches runtime inspection, stops queued emission, and disposes the transport.</summary>
    /// <returns>A value task representing transport teardown.</returns>
    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _sessionCancellation.Cancel();
        _runtimeHookRegistration?.Dispose();
        _runtimeHookRegistration = null;
        _controlMessages.Clear();
        _telemetryBuffer.Clear();
        try
        {
            await _activeSend.ConfigureAwait(false);
        }
        catch
        {
        }

        await _transport.DisposeAsync().ConfigureAwait(false);
        _sessionCancellation.Dispose();
    }

    void IRuntimeInspectionHook.ComponentMounted(
        in RuntimeInspectionComponent component)
    {
        ComponentChangePayload payload;
        using (new UntrackedReactiveReadScope())
        {
            payload = _components.Mount(in component);
        }

        EnqueueTelemetry(
            "component.mounted",
            payload,
            DevToolsJsonSerializerContext.Default.ComponentChangePayload);
    }

    void IRuntimeInspectionHook.ComponentUpdated(
        in RuntimeInspectionComponent component)
    {
        ComponentChangePayload payload;
        using (new UntrackedReactiveReadScope())
        {
            payload = _components.Update(in component);
        }

        EnqueueTelemetry(
            "component.updated",
            payload,
            DevToolsJsonSerializerContext.Default.ComponentChangePayload);
    }

    void IRuntimeInspectionHook.ComponentUnmounted(object instance)
    {
        int? identifier = _components.Unmount(instance);
        if (identifier.HasValue)
        {
            EnqueueTelemetry(
                "component.unmounted",
                new ComponentIdentifierPayload(identifier.Value),
                DevToolsJsonSerializerContext.Default.ComponentIdentifierPayload);
        }
    }

    void IRuntimeInspectionHook.ComponentReordered(object instance, int index)
    {
        ComponentReorderPayload? payload = _components.Reorder(instance, index);
        if (payload is not null)
        {
            EnqueueTelemetry(
                "component.reordered",
                payload,
                DevToolsJsonSerializerContext.Default.ComponentReorderPayload);
        }
    }

    void IRuntimeInspectionHook.ComponentEvent(
        object instance,
        string name,
        IReadOnlyList<object?> arguments)
    {
        int? identifier = _components.FindIdentifier(instance);
        if (!identifier.HasValue)
        {
            return;
        }

        List<DevToolsValuePayload> encodedArguments;
        using (new UntrackedReactiveReadScope())
        {
            encodedArguments = new List<DevToolsValuePayload>(arguments.Count);
            for (int index = 0; index < arguments.Count; index++)
            {
                encodedArguments.Add(_valueEncoder.EncodeValue(
                    arguments[index],
                    depth: 1,
                    [index.ToString(System.Globalization.CultureInfo.InvariantCulture)]));
            }
        }

        EnqueueTelemetry(
            "component.event",
            new ComponentEventPayload(identifier.Value, name, encodedArguments),
            DevToolsJsonSerializerContext.Default.ComponentEventPayload);
    }

    private void EnqueueTelemetry<TPayload>(
        string type,
        TPayload payload,
        JsonTypeInfo<TPayload> typeInformation)
    {
        if (_disposed || !_negotiatedVersion.HasValue)
        {
            return;
        }

        _telemetryBuffer.Enqueue(new SequencedProtocolEnvelope(
            _nextMessageSequence++,
            ProtocolCodec.CreateEnvelope(type, payload, typeInformation)));
        QueueFlush();
    }

    private void EnqueueControl<TPayload>(
        string type,
        TPayload payload,
        JsonTypeInfo<TPayload> typeInformation,
        bool allowBeforeHandshake = false)
    {
        if (_disposed || !_negotiatedVersion.HasValue && !allowBeforeHandshake)
        {
            return;
        }

        _controlMessages.Enqueue(new SequencedProtocolEnvelope(
            _nextMessageSequence++,
            ProtocolCodec.CreateEnvelope(type, payload, typeInformation)));
        QueueFlush();
    }

    private void QueueFlush()
    {
        if (_flushQueued)
        {
            return;
        }

        _flushQueued = true;
        Scheduler.QueuePostFlushCallback(_flushJob);
    }

    private void Flush()
    {
        _flushQueued = false;
        if (_disposed || !_started || !HasQueuedMessages())
        {
            return;
        }

        if (!_activeSend.IsCompleted)
        {
            return;
        }

        string batch = ProtocolCodec.SerializeBatch(DrainMessages());
        _activeSend = SendBatchAsync(batch);
    }

    private async Task SendBatchAsync(string batch)
    {
        try
        {
            await Task.Yield();
            if (!_disposed)
            {
                await _transport.SendAsync(batch, _sessionCancellation.Token)
                    .ConfigureAwait(false);
            }
        }
        catch
        {
            // A diagnostics transport failure never changes application rendering or scheduling.
        }
        finally
        {
            if (!_disposed && HasQueuedMessages())
            {
                QueueFlush();
            }
        }
    }

    private bool HasQueuedMessages() =>
        _controlMessages.Count > 0 || _telemetryBuffer.Count > 0;

    private List<ProtocolEnvelope> DrainMessages()
    {
        List<SequencedProtocolEnvelope> telemetry = _telemetryBuffer.Drain(
            out int droppedCount,
            out long? firstDroppedSequence);
        if (droppedCount > 0 && firstDroppedSequence.HasValue)
        {
            telemetry.Insert(
                0,
                new SequencedProtocolEnvelope(
                    firstDroppedSequence.Value,
                    ProtocolCodec.CreateEnvelope(
                        "telemetry.dropped",
                        new TelemetryDroppedPayload(droppedCount),
                        DevToolsJsonSerializerContext.Default.TelemetryDroppedPayload)));
        }

        List<ProtocolEnvelope> messages = new(
            _controlMessages.Count + telemetry.Count);
        int telemetryIndex = 0;
        while (_controlMessages.Count > 0 || telemetryIndex < telemetry.Count)
        {
            bool hasControlMessage = _controlMessages.TryPeek(
                out SequencedProtocolEnvelope controlMessage);
            if (telemetryIndex >= telemetry.Count
                || hasControlMessage
                && controlMessage.Sequence <= telemetry[telemetryIndex].Sequence)
            {
                _controlMessages.Dequeue();
                messages.Add(controlMessage.Envelope);
            }
            else
            {
                messages.Add(telemetry[telemetryIndex].Envelope);
                telemetryIndex++;
            }
        }

        return messages;
    }

    private async ValueTask ProcessIncomingAsync(string message)
    {
        if (_disposed)
        {
            return;
        }

        try
        {
            using JsonDocument document = JsonDocument.Parse(message);
            JsonElement root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
            {
                return;
            }

            if (root.TryGetProperty("messages", out JsonElement messages))
            {
                if (root.TryGetProperty("protocol", out JsonElement protocol)
                    && (protocol.ValueKind != JsonValueKind.String
                        || !string.Equals(
                            protocol.GetString(),
                            DevToolsProtocol.Name,
                            StringComparison.Ordinal)))
                {
                    return;
                }

                if (messages.ValueKind != JsonValueKind.Array)
                {
                    return;
                }

                foreach (JsonElement envelope in messages.EnumerateArray())
                {
                    await ProcessEnvelopeAsync(envelope).ConfigureAwait(false);
                }
            }
            else
            {
                await ProcessEnvelopeAsync(root).ConfigureAwait(false);
            }
        }
        catch (JsonException)
        {
            // Malformed or unknown input is isolated to this frame; the session remains active.
        }
    }

    private async ValueTask ProcessEnvelopeAsync(JsonElement envelope)
    {
        if (envelope.ValueKind != JsonValueKind.Object
            || !envelope.TryGetProperty("type", out JsonElement typeElement)
            || typeElement.ValueKind != JsonValueKind.String
            || typeElement.GetString() is not { } type
            || !envelope.TryGetProperty("version", out JsonElement versionElement)
            || versionElement.ValueKind != JsonValueKind.Number
            || !versionElement.TryGetInt32(out int version)
            || !envelope.TryGetProperty("payload", out JsonElement payload))
        {
            return;
        }

        if (string.Equals(type, "handshake.request", StringComparison.Ordinal))
        {
            HandleHandshake(payload);
            return;
        }

        if (!_negotiatedVersion.HasValue)
        {
            return;
        }

        if (version != _negotiatedVersion.Value)
        {
            return;
        }

        switch (type)
        {
            case "tree.snapshot.request":
                EnqueueControl(
                    "tree.snapshot",
                    _components.CreateTreeSnapshot(),
                    DevToolsJsonSerializerContext.Default.ComponentTreeSnapshotPayload);
                break;
            case "component.snapshot.request":
                HandleComponentSnapshotRequest(payload);
                break;
            case "component.expand.request":
                HandleComponentExpansionRequest(payload);
                break;
            case "inspector.tree.request":
                await HandleInspectorTreeRequestAsync(payload).ConfigureAwait(false);
                break;
            case "inspector.state.request":
                await HandleInspectorStateRequestAsync(payload).ConfigureAwait(false);
                break;
            default:
                // Forward-compatible receivers ignore message types added by later versions.
                break;
        }
    }

    private void HandleHandshake(JsonElement payload)
    {
        HandshakeRequestPayload? request = payload.Deserialize(
            DevToolsJsonSerializerContext.Default.HandshakeRequestPayload);
        bool accepted = request?.SupportedVersions?.Contains(
            DevToolsProtocol.CurrentVersion) == true;
        _negotiatedVersion = accepted
            ? DevToolsProtocol.CurrentVersion
            : null;
        _controlMessages.Clear();
        _telemetryBuffer.Clear();
        EnqueueControl(
            "handshake.response",
            new HandshakeResponsePayload(
                accepted,
                _negotiatedVersion,
                accepted ? null : "No mutually supported protocol version."),
            DevToolsJsonSerializerContext.Default.HandshakeResponsePayload,
            allowBeforeHandshake: true);
        if (!accepted)
        {
            return;
        }

        foreach (IDevToolsInspector inspector in _inspectors.Values)
        {
            EnqueueInspectorRegistration(inspector);
        }

        foreach (DevToolsTimelineLayer layer in _timelineLayers.Values)
        {
            EnqueueTimelineLayerRegistration(layer);
        }
    }

    private void HandleComponentSnapshotRequest(JsonElement payload)
    {
        ComponentSnapshotRequestPayload? request = payload.Deserialize(
            DevToolsJsonSerializerContext.Default.ComponentSnapshotRequestPayload);
        if (request is null)
        {
            return;
        }

        int depth = Math.Clamp(request.Depth, 0, _options.MaximumSnapshotDepth);
        ComponentSnapshotResponsePayload response;
        using (new UntrackedReactiveReadScope())
        {
            _components.TryCreateSnapshot(
                request.Identifier,
                depth,
                _valueEncoder,
                out response);
        }

        EnqueueControl(
            "component.snapshot",
            response,
            DevToolsJsonSerializerContext.Default.ComponentSnapshotResponsePayload);
    }

    private void HandleComponentExpansionRequest(JsonElement payload)
    {
        ComponentExpansionRequestPayload? request = payload.Deserialize(
            DevToolsJsonSerializerContext.Default.ComponentExpansionRequestPayload);
        if (request is null
            || string.IsNullOrEmpty(request.Section)
            || request.Path is null
            || ContainsNullPathSegment(request.Path))
        {
            return;
        }

        int depth = Math.Clamp(request.Depth, 0, _options.MaximumSnapshotDepth);
        DevToolsValuePayload? value;
        string? error;
        using (new UntrackedReactiveReadScope())
        {
            _components.TryExpand(
                request.Identifier,
                request.Section,
                request.Path,
                depth,
                _valueEncoder,
                out value,
                out error);
        }

        EnqueueControl(
            "component.expand",
            new ComponentExpansionResponsePayload(
                request.Identifier,
                request.Section,
                request.Path,
                value,
                error),
            DevToolsJsonSerializerContext.Default.ComponentExpansionResponsePayload);
    }

    private async ValueTask HandleInspectorTreeRequestAsync(JsonElement payload)
    {
        InspectorTreeRequestPayload? request = payload.Deserialize(
            DevToolsJsonSerializerContext.Default.InspectorTreeRequestPayload);
        if (request is null || string.IsNullOrEmpty(request.InspectorIdentifier))
        {
            return;
        }

        List<InspectorNodePayload> roots = [];
        string? error = null;
        if (!_inspectors.TryGetValue(request.InspectorIdentifier, out IDevToolsInspector? inspector))
        {
            error = "The requested custom inspector is not registered.";
        }
        else
        {
            try
            {
                ValueTask<IReadOnlyList<DevToolsInspectorNode>> pendingNodes;
                using (new UntrackedReactiveReadScope())
                {
                    pendingNodes = inspector.GetTreeAsync(_sessionCancellation.Token);
                }

                IReadOnlyList<DevToolsInspectorNode> nodes =
                    await pendingNodes.ConfigureAwait(false);
                roots = ConvertInspectorNodes(nodes, depth: 0);
            }
            catch
            {
                error = "The custom inspector tree provider failed.";
            }
        }

        EnqueueControl(
            "inspector.tree",
            new InspectorTreeResponsePayload(
                request.InspectorIdentifier,
                roots,
                error),
            DevToolsJsonSerializerContext.Default.InspectorTreeResponsePayload);
    }

    private async ValueTask HandleInspectorStateRequestAsync(JsonElement payload)
    {
        InspectorStateRequestPayload? request = payload.Deserialize(
            DevToolsJsonSerializerContext.Default.InspectorStateRequestPayload);
        if (request is null
            || string.IsNullOrEmpty(request.InspectorIdentifier)
            || string.IsNullOrEmpty(request.NodeIdentifier))
        {
            return;
        }

        List<DevToolsNamedValuePayload> state = [];
        string? error = null;
        if (!_inspectors.TryGetValue(request.InspectorIdentifier, out IDevToolsInspector? inspector))
        {
            error = "The requested custom inspector is not registered.";
        }
        else
        {
            try
            {
                ValueTask<IReadOnlyDictionary<string, object?>> pendingValues;
                using (new UntrackedReactiveReadScope())
                {
                    pendingValues = inspector.GetStateAsync(
                        request.NodeIdentifier,
                        _sessionCancellation.Token);
                }

                IReadOnlyDictionary<string, object?> values =
                    await pendingValues.ConfigureAwait(false);
                using (new UntrackedReactiveReadScope())
                {
                    state = _valueEncoder.EncodeDictionary(
                        values,
                        Math.Clamp(request.Depth, 0, _options.MaximumSnapshotDepth));
                }
            }
            catch
            {
                error = "The custom inspector state provider failed.";
            }
        }

        EnqueueControl(
            "inspector.state",
            new InspectorStateResponsePayload(
                request.InspectorIdentifier,
                request.NodeIdentifier,
                state,
                error),
            DevToolsJsonSerializerContext.Default.InspectorStateResponsePayload);
    }

    private static List<InspectorNodePayload> ConvertInspectorNodes(
        IReadOnlyList<DevToolsInspectorNode> nodes,
        int depth)
    {
        if (depth == 64)
        {
            return [];
        }

        List<InspectorNodePayload> converted = new(nodes.Count);
        for (int index = 0; index < nodes.Count; index++)
        {
            DevToolsInspectorNode node = nodes[index];
            converted.Add(new InspectorNodePayload(
                node.Identifier,
                node.Label,
                ConvertInspectorNodes(node.Children, depth + 1)));
        }

        return converted;
    }

    private static bool ContainsNullPathSegment(IReadOnlyList<string> path)
    {
        for (int index = 0; index < path.Count; index++)
        {
            if (path[index] is null)
            {
                return true;
            }
        }

        return false;
    }

    private void EnqueueInspectorRegistration(IDevToolsInspector inspector) =>
        EnqueueControl(
            "inspector.registered",
            new InspectorRegistrationPayload(
                inspector.Identifier,
                inspector.DisplayName),
            DevToolsJsonSerializerContext.Default.InspectorRegistrationPayload);

    private void EnqueueTimelineLayerRegistration(DevToolsTimelineLayer layer) =>
        EnqueueControl(
            "timeline.layer.registered",
            new TimelineLayerPayload(
                layer.Identifier,
                layer.DisplayName,
                layer.Color),
            DevToolsJsonSerializerContext.Default.TimelineLayerPayload);

    private void UnregisterInspector(string identifier, IDevToolsInspector inspector)
    {
        if (!_inspectors.TryGetValue(identifier, out IDevToolsInspector? registered)
            || !ReferenceEquals(registered, inspector))
        {
            return;
        }

        _inspectors.Remove(identifier);
        if (_started && !_disposed)
        {
            EnqueueControl(
                "inspector.unregistered",
                new InspectorIdentifierPayload(identifier),
                DevToolsJsonSerializerContext.Default.InspectorIdentifierPayload);
        }
    }

    private void UnregisterTimelineLayer(string identifier, DevToolsTimelineLayer layer)
    {
        if (!_timelineLayers.TryGetValue(identifier, out DevToolsTimelineLayer? registered)
            || !ReferenceEquals(registered, layer))
        {
            return;
        }

        _timelineLayers.Remove(identifier);
        if (_started && !_disposed)
        {
            EnqueueControl(
                "timeline.layer.unregistered",
                new InspectorIdentifierPayload(identifier),
                DevToolsJsonSerializerContext.Default.InspectorIdentifierPayload);
        }
    }

    private sealed class Registration : IDisposable
    {
        private Action? _unregister;

        internal Registration(Action unregister) =>
            _unregister = unregister;

        public void Dispose()
        {
            Action? unregister = _unregister;
            _unregister = null;
            unregister?.Invoke();
        }
    }

    private readonly ref struct UntrackedReactiveReadScope
    {
        public UntrackedReactiveReadScope() => Reactive.PauseTracking();

        public void Dispose() => Reactive.ResetTracking();
    }
}
