using System;

namespace Assimalign.Viu;

/// <summary>Defers a leave transition until an incoming transition allows it to begin.</summary>
/// <param name="element">The platform element represented as an opaque object.</param>
/// <param name="earlyRemove">Immediately removes an obsolete delayed element.</param>
/// <param name="delayedLeave">Begins the deferred leave transition.</param>
public delegate void TransitionDelayLeave(
    object element,
    Action earlyRemove,
    Action delayedLeave);
