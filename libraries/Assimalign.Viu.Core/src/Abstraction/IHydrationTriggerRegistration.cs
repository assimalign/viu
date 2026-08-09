using System;

namespace Assimalign.Viu;

/// <summary>Owns one host trigger registered for a deferred hydration boundary.</summary>
/// <remarks>
/// Disposal cancels an unfired trigger and releases host observers or listeners. Trigger delivery
/// occurs after registration on the host event loop; Core still schedules activation post-flush.
/// After activation, <see cref="Complete"/> lets an interaction host queue asynchronous replay of
/// its captured event rather than delivering it inside component setup. Specified by
/// <c>[HYD-LAZY-3]</c> through <c>[HYD-LAZY-5]</c>.
/// </remarks>
public interface IHydrationTriggerRegistration : IDisposable
{
    /// <summary>
    /// Signals that activation and host writes are scheduled, allowing the host to complete any
    /// deferred asynchronous interaction replay after its commit boundary.
    /// </summary>
    void Complete();
}
