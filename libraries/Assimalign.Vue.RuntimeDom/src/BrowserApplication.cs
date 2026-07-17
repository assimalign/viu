using System;
using System.Runtime.Versioning;

using Assimalign.Vue.RuntimeCore;

namespace Assimalign.Vue.RuntimeDom;

/// <summary>
/// A browser-mounted Vuecs application — the C# port of the app object
/// <c>createApp</c> returns in <c>@vue/runtime-dom</c>
/// (https://vuejs.org/api/application.html, <c>packages/runtime-dom/src/index.ts</c>). Wraps
/// the platform-agnostic <see cref="VueApplication{TNode}"/> with browser container concerns:
/// selector resolution, clearing existing content before a non-hydrating client mount
/// (upstream parity), and full interop cleanup on <see cref="Unmount"/> — the bridge registry
/// returns to its pre-mount baseline. Create through
/// <see cref="BrowserRuntime.CreateApp"/>. Not thread-safe (browser main thread only).
/// </summary>
[SupportedOSPlatform("browser")]
public sealed class BrowserApplication
{
    private readonly VueApplication<int> _application;

    internal BrowserApplication(VueApplication<int> application)
    {
        _application = application;
    }

    /// <summary>Whether the app is currently mounted.</summary>
    public bool IsMounted => _application.IsMounted;

    /// <summary>The root component instance after mounting, or null.</summary>
    public ComponentInstance? RootInstance => _application.RootInstance;

    /// <summary>
    /// Resolves <paramref name="selector"/> and mounts there (upstream:
    /// <c>app.mount('#app')</c>). A selector matching nothing throws a
    /// <see cref="BrowserDomException"/> naming the selector.
    /// </summary>
    /// <param name="selector">The CSS selector of the container.</param>
    /// <returns>The root component instance.</returns>
    /// <exception cref="BrowserDomException">No element matches <paramref name="selector"/>.</exception>
    public ComponentInstance? Mount(string selector)
    {
        ArgumentException.ThrowIfNullOrEmpty(selector);
        return Mount(BrowserRuntime.QuerySelector(selector));
    }

    /// <summary>Mounts into an already-resolved container handle.</summary>
    /// <param name="containerHandle">The container's node handle.</param>
    /// <returns>The root component instance.</returns>
    public ComponentInstance? Mount(int containerHandle)
    {
        if (!_application.IsMounted)
        {
            // Non-hydrating client mount clears existing container content (upstream parity);
            // one interop call that also releases any registered child handles.
            BrowserRuntime.ClearContainer(containerHandle);
        }
        return _application.Mount(containerHandle);
    }

    /// <summary>
    /// Unmounts the app (upstream: <c>app.unmount()</c>): runs component teardown lifecycles,
    /// removes the rendered DOM, and releases every JS-side handle and listener the app
    /// created.
    /// </summary>
    public void Unmount() => _application.Unmount();
}
