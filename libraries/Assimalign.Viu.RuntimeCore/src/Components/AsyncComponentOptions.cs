namespace Assimalign.Viu.RuntimeCore;

/// <summary>
/// The options bag for <see cref="AsyncComponents.DefineAsyncComponent(AsyncComponentOptions)"/> —
/// the C# port of upstream's <c>AsyncComponentOptions</c>
/// (<c>packages/runtime-core/src/apiAsyncComponent.ts</c>,
/// https://vuejs.org/guide/components/async.html). Mirrors the object form of
/// <c>defineAsyncComponent({ loader, loadingComponent, errorComponent, delay, timeout, suspensible,
/// onError })</c>. Not thread-safe (single-threaded JS event-loop model).
/// </summary>
public sealed class AsyncComponentOptions
{
    /// <summary>
    /// The loader that produces the real component definition (upstream: <c>loader</c>). Runs at most
    /// once for a successful load; its result is cached and reused by later mounts.
    /// </summary>
    public required AsyncComponentLoader Loader { get; init; }

    /// <summary>
    /// The component shown after <see cref="Delay"/> while the loader is pending (upstream:
    /// <c>loadingComponent</c>), or null to show nothing (a comment placeholder) while loading.
    /// </summary>
    public IComponentDefinition? LoadingComponent { get; init; }

    /// <summary>
    /// The component shown on loader failure or after <see cref="Timeout"/> elapses (upstream:
    /// <c>errorComponent</c>), or null. When present it receives an <c>"error"</c> prop carrying the
    /// failure, and its presence keeps a load failure from crashing the flush.
    /// </summary>
    public IComponentDefinition? ErrorComponent { get; init; }

    /// <summary>
    /// Milliseconds to wait before showing <see cref="LoadingComponent"/> (upstream: <c>delay</c>,
    /// default 200). 0 shows the loading component immediately on first render.
    /// </summary>
    public int Delay { get; init; } = 200;

    /// <summary>
    /// Milliseconds before an unresolved load settles to the error state (upstream: <c>timeout</c>),
    /// or null to never time out.
    /// </summary>
    public int? Timeout { get; init; }

    /// <summary>
    /// Whether an enclosing <c>&lt;Suspense&gt;</c> boundary controls this component's loading state
    /// (upstream: <c>suspensible</c>, default true). With no boundary present it has no effect and the
    /// component renders its own loading/error UI; with a boundary the component registers its pending
    /// load through <see cref="ISuspenseBoundary"/> and defers display to the boundary. The real
    /// enclosing-Suspense integration completes in [V01.01.03.20].
    /// </summary>
    public bool Suspensible { get; init; } = true;

    /// <summary>
    /// The loader-failure retry policy (upstream: <c>onError</c>), or null to fail on the first error.
    /// </summary>
    public AsyncComponentErrorHandler? OnError { get; init; }
}
