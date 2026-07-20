using System;

namespace Assimalign.Viu.RuntimeCore;

/// <summary>
/// Defines async components — the C# port of upstream's <c>defineAsyncComponent</c>
/// (<c>packages/runtime-core/src/apiAsyncComponent.ts</c>,
/// https://vuejs.org/guide/components/async.html). A loader returns the real component definition
/// asynchronously; the returned wrapper renders a loading component after <c>Delay</c>, an error
/// component on failure or <c>Timeout</c>, and the resolved component in place once loaded. The
/// resolved definition is cached on the wrapper, so subsequent mounts reuse it without re-invoking
/// the loader, and concurrent mounts share one in-flight load.
/// </summary>
public static class AsyncComponents
{
    /// <summary>
    /// Defines an async component from a bare loader (upstream: <c>defineAsyncComponent(loader)</c>),
    /// with default delay (200ms) and no loading/error/timeout options.
    /// </summary>
    /// <param name="loader">The loader producing the real component definition.</param>
    /// <returns>The async component wrapper definition; mount it like any other component.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="loader"/> is null.</exception>
    public static IComponentDefinition DefineAsyncComponent(AsyncComponentLoader loader)
    {
        ArgumentNullException.ThrowIfNull(loader);
        return new AsyncComponentWrapper(new AsyncComponentOptions { Loader = loader });
    }

    /// <summary>
    /// Defines an async component from an options bag (upstream:
    /// <c>defineAsyncComponent(options)</c>).
    /// </summary>
    /// <param name="options">The loader plus the loading/error/delay/timeout/suspensible policy.</param>
    /// <returns>The async component wrapper definition; mount it like any other component.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="options"/> or its loader is null.</exception>
    public static IComponentDefinition DefineAsyncComponent(AsyncComponentOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(options.Loader);
        return new AsyncComponentWrapper(options);
    }
}
