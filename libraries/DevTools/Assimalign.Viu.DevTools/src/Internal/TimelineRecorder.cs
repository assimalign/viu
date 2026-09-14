using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Runtime.CompilerServices;

namespace Assimalign.Viu.DevTools;

internal sealed class TimelineRecorder
{
    private readonly TimelineEventPayload[] _events;
    private readonly int _samplingInterval;
    private readonly int _maximumEventsPerSecond;
    private readonly ConditionalWeakTable<object, Identity> _identities = new();
    private readonly long _startedTimestamp = Stopwatch.GetTimestamp();
    private int _nextIdentifier;
    private int _head;
    private int _count;
    private int _samplingPosition;
    private long _rateWindow;
    private int _rateCount;
    private long _droppedCount;
    private long? _firstDroppedSequence;

    internal TimelineRecorder(DevToolsSessionOptions options)
    {
        _events = new TimelineEventPayload[options.TimelineCapacity];
        _samplingInterval = options.TimelineSamplingInterval;
        _maximumEventsPerSecond = options.MaximumTimelineEventsPerSecond;
    }

    internal bool HasEvents => _count > 0 || _droppedCount > 0;

    internal long GetTimestamp() =>
        Stopwatch.GetElapsedTime(_startedTimestamp).Ticks / 10;

    internal int GetIdentifier(object value)
    {
        if (!_identities.TryGetValue(value, out Identity? identity))
        {
            identity = new Identity(++_nextIdentifier);
            _identities.Add(value, identity);
        }

        return identity.Identifier;
    }

    internal string GetLabel(object dependency, object? memberKey, string? debugLabel)
    {
        if (!string.IsNullOrEmpty(debugLabel))
        {
            return debugLabel;
        }

        // Never call user-defined ToString: inspection must not execute application code.
        string? memberName = memberKey switch
        {
            string name => name,
            int index => index.ToString(CultureInfo.InvariantCulture),
            long index => index.ToString(CultureInfo.InvariantCulture),
            _ => null,
        };
        return !string.IsNullOrEmpty(memberName)
            ? memberName
            : "dependency-" + GetIdentifier(dependency).ToString(CultureInfo.InvariantCulture);
    }

    internal void Record(in TimelineEventPayload payload)
    {
        bool sampled = _samplingPosition == 0;
        _samplingPosition = (_samplingPosition + 1) % _samplingInterval;
        if (!sampled)
        {
            return;
        }

        long window = payload.Timestamp / 1_000_000;
        if (window != _rateWindow)
        {
            _rateWindow = window;
            _rateCount = 0;
        }

        if (_rateCount >= _maximumEventsPerSecond)
        {
            Drop(payload.Sequence);
            return;
        }

        _rateCount++;
        if (_count == _events.Length)
        {
            Drop(_events[_head].Sequence);
            _events[_head] = default;
            _head = (_head + 1) % _events.Length;
            _count--;
        }

        _events[(_head + _count) % _events.Length] = payload;
        _count++;
    }

    internal List<SequencedProtocolEnvelope> Drain()
    {
        List<SequencedProtocolEnvelope> result = new(_count + 1);
        if (_firstDroppedSequence.HasValue)
        {
            result.Add(new SequencedProtocolEnvelope(
                _firstDroppedSequence.Value,
                ProtocolCodec.CreateEnvelope(
                    "timeline.dropped",
                    new TimelineDroppedPayload(_droppedCount),
                    DevToolsJsonSerializerContext.Default.TimelineDroppedPayload)));
        }

        while (_count > 0)
        {
            TimelineEventPayload payload = _events[_head];
            _events[_head] = default;
            _head = (_head + 1) % _events.Length;
            _count--;
            result.Add(new SequencedProtocolEnvelope(
                payload.Sequence,
                ProtocolCodec.CreateEnvelope(
                    "timeline.event",
                    payload,
                    DevToolsJsonSerializerContext.Default.TimelineEventPayload)));
        }

        _droppedCount = 0;
        _firstDroppedSequence = null;
        result.Sort(static (left, right) => left.Sequence.CompareTo(right.Sequence));
        return result;
    }

    internal void Clear()
    {
        Array.Clear(_events);
        _head = 0;
        _count = 0;
        _samplingPosition = 0;
        _rateWindow = 0;
        _rateCount = 0;
        _droppedCount = 0;
        _firstDroppedSequence = null;
    }

    private void Drop(long sequence)
    {
        _firstDroppedSequence = _firstDroppedSequence.HasValue
            ? Math.Min(_firstDroppedSequence.Value, sequence)
            : sequence;
        if (_droppedCount < long.MaxValue)
        {
            _droppedCount++;
        }
    }

    private sealed record Identity(int Identifier);
}
