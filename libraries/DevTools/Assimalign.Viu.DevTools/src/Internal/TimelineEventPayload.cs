namespace Assimalign.Viu.DevTools;

// Buffered values never hold an application object. [DVT-9] through [DVT-11].
internal readonly record struct TimelineEventPayload(
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
