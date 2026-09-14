namespace Assimalign.Viu.DevTools;

/// <summary>The timeline event payload exchanged by the inspection protocol.</summary>
/// <remarks>Data-only contract; mutable collections are not thread-safe. Specified by <c>[DVT-2]</c>, <c>[DVT-13]</c>, and <c>[DVT-14]</c>.</remarks>
/// <param name="Sequence">The common session enqueue sequence.</param>
/// <param name="Timestamp">Monotonic elapsed microseconds since session construction.</param>
/// <param name="CorrelationIdentifier">The upcoming or current scheduler flush-chain identity.</param>
/// <param name="LayerIdentifier">The registered timeline-layer identity.</param>
/// <param name="Kind">The event or value discriminator.</param>
/// <param name="Label">The safely encoded display label.</param>
/// <param name="DependencyIdentifier">The causal dependency identity when known.</param>
/// <param name="EffectIdentifier">The correlated effect identity when known.</param>
/// <param name="OwnerIdentifier">The weakly assigned owner identity when known.</param>
/// <param name="ComponentIdentifier">The correlated component identity when known.</param>
/// <param name="Version">The protocol or recorded dependency version.</param>
/// <param name="PreFlushCount">The attempted pre-flush job count.</param>
/// <param name="RenderCount">The attempted render job count.</param>
/// <param name="PostFlushCount">The attempted post-flush callback count.</param>
/// <param name="Succeeded">Whether the recorded operation completed successfully.</param>
public readonly record struct TimelineEventPayload(
    long Sequence,
    long Timestamp,
    long CorrelationIdentifier,
    string LayerIdentifier,
    string Kind,
    string Label,
    int? DependencyIdentifier = null,
    int? EffectIdentifier = null,
    int? OwnerIdentifier = null,
    int? ComponentIdentifier = null,
    int? Version = null,
    int? PreFlushCount = null,
    int? RenderCount = null,
    int? PostFlushCount = null,
    bool? Succeeded = null);
