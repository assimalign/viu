namespace Assimalign.Viu.DevTools;

/// <summary>Pairs one queued protocol envelope with its session-wide enqueue order.</summary>
internal readonly struct SequencedProtocolEnvelope
{
    internal SequencedProtocolEnvelope(long sequence, ProtocolEnvelope envelope)
    {
        Sequence = sequence;
        Envelope = envelope;
    }

    internal long Sequence { get; }

    internal ProtocolEnvelope Envelope { get; }
}
