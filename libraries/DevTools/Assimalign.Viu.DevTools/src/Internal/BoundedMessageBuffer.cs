using System.Collections.Generic;

namespace Assimalign.Viu.DevTools;

internal sealed class BoundedMessageBuffer
{
    private readonly Queue<SequencedProtocolEnvelope> _messages;
    private readonly int _capacity;
    private int _droppedCount;
    private long? _firstDroppedSequence;

    internal BoundedMessageBuffer(int capacity)
    {
        _capacity = capacity;
        _messages = new Queue<SequencedProtocolEnvelope>(capacity);
    }

    internal int Count => _messages.Count;

    internal void Enqueue(SequencedProtocolEnvelope message)
    {
        if (_messages.Count == _capacity)
        {
            SequencedProtocolEnvelope droppedMessage = _messages.Dequeue();
            _firstDroppedSequence ??= droppedMessage.Sequence;
            if (_droppedCount < int.MaxValue)
            {
                _droppedCount++;
            }
        }

        _messages.Enqueue(message);
    }

    internal List<SequencedProtocolEnvelope> Drain(
        out int droppedCount,
        out long? firstDroppedSequence)
    {
        droppedCount = _droppedCount;
        firstDroppedSequence = _firstDroppedSequence;
        _droppedCount = 0;
        _firstDroppedSequence = null;
        List<SequencedProtocolEnvelope> drained = new(_messages.Count);
        while (_messages.TryDequeue(out SequencedProtocolEnvelope message))
        {
            drained.Add(message);
        }

        return drained;
    }

    internal void Clear()
    {
        _messages.Clear();
        _droppedCount = 0;
        _firstDroppedSequence = null;
    }
}
