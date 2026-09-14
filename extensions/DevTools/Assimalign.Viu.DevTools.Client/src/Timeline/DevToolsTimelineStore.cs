using System;
using System.Collections.Generic;

using Assimalign.Viu.DevTools;

namespace Assimalign.Viu.DevTools.Client;

/// <summary>Keeps a bounded timeline and returns only requested visible rows. Single-threaded. Specified by <c>[DVT-14]</c>.</summary>
public sealed class DevToolsTimelineStore
{
    private readonly Queue<TimelineEventPayload> _events = new();
    private readonly Queue<TimelineDroppedPayload> _gaps = new();
    private readonly Dictionary<string, TimelineLayerPayload> _layers = new(StringComparer.Ordinal);
    private readonly int _capacity;

    /// <summary>Creates a bounded window; each insertion does constant work.</summary>
    public DevToolsTimelineStore(int capacity = 2048)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(capacity, 1);
        _capacity = capacity;
    }
    /// <summary>Gets a copy of retained events in arrival order.</summary>
    public IReadOnlyList<TimelineEventPayload> Events => _events.ToArray();
    /// <summary>Gets retained drop notices, bounded independently by the same capacity.</summary>
    public IReadOnlyList<TimelineDroppedPayload> Gaps => _gaps.ToArray();
    /// <summary>Gets built-in and custom layer metadata.</summary>
    public IReadOnlyDictionary<string, TimelineLayerPayload> Layers => _layers;
    /// <summary>Gets the number of retained events without allocating a view.</summary>
    public int Count => _events.Count;

    /// <summary>Appends one event, evicting the oldest when the configured window is full.</summary>
    public void Apply(TimelineEventPayload timelineEvent)
    {
        if (_events.Count == _capacity)
        {
            _events.Dequeue();
        }

        _events.Enqueue(timelineEvent);
    }
    /// <summary>Appends a loss notice, retaining a bounded history of gaps.</summary>
    public void Apply(TimelineDroppedPayload gap)
    {
        ArgumentNullException.ThrowIfNull(gap);
        if (_gaps.Count == _capacity)
        {
            _gaps.Dequeue();
        }

        _gaps.Enqueue(gap);
    }
    /// <summary>Registers or refreshes a layer's protocol metadata.</summary>
    public void RegisterLayer(TimelineLayerPayload layer) => _layers[layer.Identifier] = layer;
    /// <summary>Removes a layer's registration without erasing historical events.</summary>
    public void RemoveLayer(string identifier) => _layers.Remove(identifier);

    /// <summary>Returns at most count matching rows after offset, using inclusive microsecond bounds.</summary>
    public IReadOnlyList<TimelineEventPayload> GetWindow(int offset, int count, string? layer = null, long? from = null, long? to = null)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(offset);
        ArgumentOutOfRangeException.ThrowIfNegative(count);
        List<TimelineEventPayload> rows = new(Math.Min(count, _capacity));
        foreach (TimelineEventPayload value in _events)
        {
            if (rows.Count == count)
            {
                break;
            }

            if (layer is not null && value.LayerIdentifier != layer || from.HasValue && value.Timestamp < from || to.HasValue && value.Timestamp > to)
            {
                continue;
            }

            if (offset > 0) { offset--; continue; }
            rows.Add(value);
        }
        return rows;
    }

    /// <summary>Finds the retained causal chain through correlation, dependency, effect, and component identities.</summary>
    public IReadOnlySet<long> GetRelatedSequences(long sequence)
    {
        Dictionary<long, List<long>> correlations = [];
        Dictionary<int, List<long>> dependencies = [];
        Dictionary<int, List<long>> effects = [];
        Dictionary<int, List<long>> components = [];
        Dictionary<long, TimelineEventPayload> events = [];
        foreach (TimelineEventPayload value in _events)
        {
            events[value.Sequence] = value;
            if (value.CorrelationIdentifier != 0)
            {
                Index(correlations, value.CorrelationIdentifier, value.Sequence);
            }

            if (value.DependencyIdentifier is int dependency)
            {
                Index(dependencies, dependency, value.Sequence);
            }

            if (value.EffectIdentifier is int effect)
            {
                Index(effects, effect, value.Sequence);
            }

            if (value.ComponentIdentifier is int component)
            {
                Index(components, component, value.Sequence);
            }
        }
        HashSet<long> related = [];
        Queue<long> pending = new();
        if (events.ContainsKey(sequence)) { related.Add(sequence); pending.Enqueue(sequence); }
        while (pending.TryDequeue(out long current))
        {
            TimelineEventPayload value = events[current];
            Visit(correlations, value.CorrelationIdentifier, related, pending);
            if (value.DependencyIdentifier is int dependency)
            {
                Visit(dependencies, dependency, related, pending);
            }

            if (value.EffectIdentifier is int effect)
            {
                Visit(effects, effect, related, pending);
            }

            if (value.ComponentIdentifier is int component)
            {
                Visit(components, component, related, pending);
            }
        }
        return related;
    }

    /// <summary>Clears events, gaps, and registrations on a new handshake.</summary>
    public void Clear() { _events.Clear(); _gaps.Clear(); _layers.Clear(); }

    private static void Index<TKey>(Dictionary<TKey, List<long>> index, TKey key, long sequence) where TKey : notnull
    {
        if (!index.TryGetValue(key, out List<long>? values))
        {
            index[key] = values = [];
        }

        values.Add(sequence);
    }
    private static void Visit<TKey>(Dictionary<TKey, List<long>> index, TKey key, HashSet<long> related, Queue<long> pending) where TKey : notnull
    {
        if (!index.Remove(key, out List<long>? values))
        {
            return;
        }

        foreach (long value in values)
        {
            if (related.Add(value))
            {
                pending.Enqueue(value);
            }
        }
    }
}
