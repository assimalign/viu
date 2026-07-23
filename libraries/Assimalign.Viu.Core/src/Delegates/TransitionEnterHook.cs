using System;

namespace Assimalign.Viu;

/// <summary>
/// Runs an asynchronous enter, appear, or leave transition phase and signals when it is complete.
/// </summary>
/// <param name="element">The platform element represented as an opaque object.</param>
/// <param name="done">The callback that completes the transition phase.</param>
public delegate void TransitionEnterHook(object element, Action done);
