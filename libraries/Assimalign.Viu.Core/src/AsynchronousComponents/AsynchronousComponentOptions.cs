using Assimalign.Viu.Components;

namespace Assimalign.Viu;

/// <summary>
/// Configures an asynchronous component definition.
/// </summary>
/// <remarks>
/// Mirrors Vue 3.5's <c>AsyncComponentOptions</c>. Render callbacks create fresh immutable tree
/// values for each mount; definitions and activated templates are never reused as tree values.
/// </remarks>
public sealed class AsynchronousComponentOptions
{
    /// <summary>Gets the loader that resolves a registered component identity.</summary>
    public required AsynchronousComponentLoader Loader { get; init; }

    /// <summary>
    /// Gets the optional callback that creates loading content after <see cref="Delay"/>.
    /// </summary>
    public ComponentRenderer? LoadingComponent { get; init; }

    /// <summary>Gets the optional callback that creates error content.</summary>
    public AsynchronousComponentErrorRenderer? ErrorComponent { get; init; }

    /// <summary>Gets the delay in milliseconds before loading content appears.</summary>
    public int Delay { get; init; } = 200;

    /// <summary>Gets the optional timeout in milliseconds.</summary>
    public int? Timeout { get; init; }

    /// <summary>
    /// Gets whether an enclosing Suspense boundary owns the loading presentation.
    /// </summary>
    public bool Suspensible { get; init; } = true;

    /// <summary>Gets the optional loader retry policy.</summary>
    public AsynchronousComponentErrorHandler? OnError { get; init; }
}
