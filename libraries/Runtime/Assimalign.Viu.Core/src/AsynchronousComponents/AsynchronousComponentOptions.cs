using Assimalign.Viu.Components;

namespace Assimalign.Viu;

/// <summary>Configures an asynchronous component definition.</summary>
/// <remarks>
/// Definitions share loader state while every mount owns its wrapper instance and immutable output
/// nodes. A definition-level hydration strategy applies only to its outer wrapper boundary.
/// Specified by <c>[BLT-14]</c> and <c>[HYD-LAZY-1]</c>.
/// </remarks>
public sealed class AsynchronousComponentOptions
{
    /// <summary>Gets the loader that resolves a registered component identity.</summary>
    public required AsynchronousComponentLoader Loader { get; init; }

    /// <summary>Gets the optional render closure used after the loading delay elapses.</summary>
    public ComponentRenderer? LoadingComponent { get; init; }

    /// <summary>Gets the optional failure-tree factory.</summary>
    public AsynchronousComponentErrorRenderer? ErrorComponent { get; init; }

    /// <summary>Gets the loading-presentation delay in milliseconds.</summary>
    public int Delay { get; init; } = 200;

    /// <summary>Gets the optional load timeout in milliseconds.</summary>
    public int? Timeout { get; init; }

    /// <summary>
    /// Gets whether the nearest Suspense boundary, while still pending, counts this loader and owns
    /// its pending presentation. False uses this definition's loading, delay, timeout, and error presentation
    /// independently; it does not delay the boundary's reveal. Specified by <c>[BLT-20]</c>.
    /// </summary>
    public bool Suspensible { get; init; } = true;

    /// <summary>Gets the optional retry-or-fail policy.</summary>
    public AsynchronousComponentErrorHandler? OnError { get; init; }

    /// <summary>
    /// Gets the default client hydration strategy copied to invocation nodes that do not declare
    /// one explicitly. The default is eager. Specified by <c>[HYD-LAZY-1]</c>.
    /// </summary>
    public HydrationStrategy HydrationStrategy { get; init; } =
        HydrationStrategy.Immediate;
}
