using System;

using Assimalign.Viu.Reactivity;

namespace Assimalign.Viu.State;

/// <summary>
/// Provides the reactive lifetime and application dependencies available while setting up one
/// registry-owned state store. Specified by <c>[STA-1]</c> through <c>[STA-3]</c> and
/// <c>[STA-7]</c>.
/// </summary>
public interface IStateContext
{
    /// <summary>
    /// Gets the store-owned reactive scope. It is a child of the registry's detached root and is
    /// never a child of the component scope that first resolves the store. Specified by
    /// <c>[STA-2]</c> and <c>[STA-3]</c>.
    /// </summary>
    IReactiveEffectScope Scope { get; }

    /// <summary>
    /// Gets the optional externally owned application service provider available during setup.
    /// State does not prescribe how the provider is composed. Specified by <c>[STA-4]</c>.
    /// </summary>
    IServiceProvider? Services { get; }

    /// <summary>
    /// Gets the application watch scheduler, or <see langword="null"/> for Reactivity's
    /// synchronous fallback. Specified by <c>[STA-7]</c>.
    /// </summary>
    IReactiveWatchScheduler? WatchScheduler { get; }
}
