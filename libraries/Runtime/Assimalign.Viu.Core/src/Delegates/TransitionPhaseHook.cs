using System;

namespace Assimalign.Viu;

/// <summary>
/// Runs one host-provided enter, appear, or leave phase and signals when that phase completes.
/// </summary>
/// <param name="element">The opaque host element.</param>
/// <param name="complete">The idempotent callback that completes the phase.</param>
/// <remarks>Specified by <c>[BLT-7]</c> and <c>[BLT-8]</c>.</remarks>
public delegate void TransitionPhaseHook(object element, Action complete);
