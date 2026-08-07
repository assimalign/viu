using System;

using Assimalign.Viu.Reactivity;

namespace Assimalign.Viu.State;

/// <summary>
/// Provides the reactive lifetime and application dependencies available while creating a store.
/// </summary>
public interface IStateContext
{
    /// <summary>Gets the store-owned detached reactive scope.</summary>
    EffectScope Scope { get; }

    /// <summary>Gets the optional externally owned application service provider.</summary>
    IServiceProvider? Services { get; }

    /// <summary>Gets the watch delivery policy selected by the registry owner.</summary>
    IReactiveWatchScheduler WatchScheduler { get; }
}
