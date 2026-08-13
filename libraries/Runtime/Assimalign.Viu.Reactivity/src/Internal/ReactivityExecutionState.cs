using System.Collections.Generic;

namespace Assimalign.Viu.Reactivity;

/// <summary>Owns ambient reactivity bookkeeping for one logical execution flow.</summary>
internal sealed class ReactivityExecutionState
{
    internal Subscriber? ActiveSubscriber { get; set; }

    internal bool ShouldTrack { get; set; } = true;

    internal int GlobalVersion { get; set; }

    internal Stack<bool> TrackStack { get; } = new();

    internal int BatchDepth { get; set; }

    internal Subscriber? BatchedSubscriber { get; set; }

    internal Subscriber? BatchedComputed { get; set; }

    internal EffectScope? CurrentScope { get; set; }
}
