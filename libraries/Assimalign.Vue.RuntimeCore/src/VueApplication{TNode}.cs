using System;

namespace Assimalign.Vue.RuntimeCore;

/// <summary>
/// The minimal application shell — the C# port of the app object produced by
/// <c>createAppAPI(render)</c> in <c>@vue/runtime-core</c>
/// (<c>packages/runtime-core/src/apiCreateApp.ts</c>, https://vuejs.org/api/application.html):
/// one root component mounted into one container, unmounted as a whole. Plugins, app-level
/// provides, and error-handler configuration land with [V01.01.03.12]. Platform packages wrap
/// this with container resolution (the browser's <c>CreateApp(...).Mount("#app")</c> is
/// [V01.01.04.04]). Not thread-safe (single-threaded JS event-loop model).
/// </summary>
/// <typeparam name="TNode">The platform node type.</typeparam>
public sealed class VueApplication<TNode>
    where TNode : notnull
{
    private readonly Renderer<TNode> _renderer;
    private readonly IComponentDefinition _rootComponent;
    private readonly VirtualNodeProperties? _rootProperties;
    private VirtualNode? _rootVirtualNode;
    private TNode? _container;

    internal VueApplication(Renderer<TNode> renderer, IComponentDefinition rootComponent, VirtualNodeProperties? rootProperties)
    {
        _renderer = renderer;
        _rootComponent = rootComponent;
        _rootProperties = rootProperties;
    }

    /// <summary>Whether the app is currently mounted.</summary>
    public bool IsMounted { get; private set; }

    /// <summary>The root component instance after mounting, or null.</summary>
    public ComponentInstance? RootInstance => _rootVirtualNode?.Component as ComponentInstance;

    /// <summary>
    /// Mounts the root component into <paramref name="container"/> (upstream:
    /// <c>app.mount</c>). A second call warns and no-ops, returning the existing instance
    /// (upstream parity).
    /// </summary>
    /// <param name="container">The platform container node.</param>
    /// <returns>The root component instance.</returns>
    public ComponentInstance? Mount(TNode container)
    {
        if (IsMounted)
        {
            RuntimeWarnings.Warn(
                "App has already been mounted. Create a new app instance to mount again, or call Unmount() first.");
            return RootInstance;
        }
        _rootVirtualNode = VirtualNodeFactory.Component(_rootComponent, _rootProperties);
        _renderer.Render(_rootVirtualNode, container);
        _container = container;
        IsMounted = true;
        return RootInstance;
    }

    /// <summary>
    /// Unmounts the app (upstream: <c>app.unmount</c>): runs the component teardown
    /// lifecycles and removes the rendered tree from the container.
    /// </summary>
    public void Unmount()
    {
        if (!IsMounted)
        {
            return;
        }
        _renderer.Render(null, _container!);
        _rootVirtualNode = null;
        _container = default;
        IsMounted = false;
    }
}
