using Assimalign.Viu.Components;

namespace Assimalign.Viu;

/// <summary>Configures an asynchronous component definition.</summary>
/// <remarks>
/// Definitions share loader state while every mount owns its wrapper instance and immutable output
/// nodes. Specified by <c>[BLT-14]</c>.
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

    /// <summary>Gets whether an active Suspense boundary owns the pending presentation.</summary>
    public bool Suspensible { get; init; } = true;

    /// <summary>Gets the optional retry-or-fail policy.</summary>
    public AsynchronousComponentErrorHandler? OnError { get; init; }
}
