using System;

namespace Assimalign.Vue.Reactivity;

/// <summary>
/// State flags shared by every subscriber (effects and computeds). Mirrors Vue 3.5's
/// <c>EffectFlags</c> bit field.
/// </summary>
[Flags]
internal enum SubscriberFlags
{
    /// <summary>No flags set.</summary>
    None = 0,

    /// <summary>The subscriber has not been stopped.</summary>
    Active = 1 << 0,

    /// <summary>The subscriber's function is currently executing.</summary>
    Running = 1 << 1,

    /// <summary>The subscriber is linked into the subscriber lists of its dependencies (it will be notified).</summary>
    Tracking = 1 << 2,

    /// <summary>The subscriber has been queued in the current batch and must not be queued again.</summary>
    Notified = 1 << 3,

    /// <summary>A dependency has triggered since the last evaluation (computeds only).</summary>
    Dirty = 1 << 4,

    /// <summary>The subscriber may re-trigger itself by mutating its own dependencies.</summary>
    AllowRecurse = 1 << 5,

    /// <summary>Notifications are deferred until <c>Resume()</c>.</summary>
    Paused = 1 << 6,

    /// <summary>
    /// The computed's cached value comes from a completed getter run (cleared again when a
    /// re-evaluation throws, so the next read retries instead of serving a stale value).
    /// </summary>
    Evaluated = 1 << 7,
}
