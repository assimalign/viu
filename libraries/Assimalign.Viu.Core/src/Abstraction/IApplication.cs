using System;
using System.Threading;
using System.Threading.Tasks;

namespace Assimalign.Viu;
/*
 TODO's:

1. Convert all reactive interfaces to concrete classes. Make Reactivity a first class citizen
2. Remove Dependency injection setup and utilize IApplicationServiceContainer


 */




/// <summary>
/// The platform-neutral application contract — the C# port of the app object produced by
/// <c>createAppAPI(render)</c> in <c>@vue/runtime-core</c>
/// (<c>packages/runtime-core/src/apiCreateApp.ts</c>, https://vuejs.org/api/application.html),
/// reduced to the surface that does not need the platform node type: component/directive
/// registration, app-level <see cref="Provide{T}(InjectionKey{T}, T)"/>, plugin installation
/// (<see cref="Use"/>), configuration, and teardown. It is the face a plugin's
/// <see cref="IApplicationPlugin.Install"/> receives and the type an <see cref="IApplicationBuilder"/> produces,
/// so plugins and generic hosting code work against any platform.
/// <para>
/// <c>Mount</c> is deliberately absent: the container type is platform-specific (a browser CSS
/// selector or node handle, a Core <c>TNode</c>), so mounting lives on the concrete platform
/// applications (<see cref="Application{TNode}.Mount(TNode)"/>,
/// <c>BrowserApplication.MountAsync(string)</c>). Introspection that needs no node type
/// (<see cref="IsMounted"/>, <see cref="RootInstance"/>) and <see cref="Unmount"/> stay here.
/// </para>
/// Not thread-safe (single-threaded JS event-loop model).
/// </summary>
public interface IApplication
{
    /// <summary>
    /// Whether the application is currently mounted.
    /// </summary>
    bool IsMounted { get; }

    /// <summary>
    /// 
    /// </summary>
    IApplicationContext Context { get; }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="component"></param>
    /// <returns></returns>
    IApplication UseComponent(IComponent component);

    /// <summary>
    /// Registers a component under <paramref name="name"/> so descendants of the root can resolve it
    /// by name — including <c>&lt;component :is="name"&gt;</c> dynamic components (upstream:
    /// <c>app.component(name, definition)</c>). Registering a duplicate name, or registering after
    /// mount, warns in dev. Returns the application for chaining.
    /// </summary>
    /// <param name="name">The component name (resolved case-insensitively at render).</param>
    /// <param name="definition">The component definition.</param>
    /// <returns>This application, for chaining.</returns>
    IApplication UseComponent(string name, IComponent definition);

    /// <summary>
    /// 
    /// </summary>
    /// <param name="name"></param>
    /// <param name="factory"></param>
    /// <returns></returns>
    IApplication UseComponent(string name, Func<ComponentProperties, ComponentSetupContext, ComponentSetup> factory);


    /// <summary>
    /// Returns the directive registered under <paramref name="name"/>, or null (upstream:
    /// <c>app.directive(name)</c> getter — an exact-name lookup).
    /// </summary>
    /// <param name="name">The registered name.</param>
    /// <returns>The registered directive, or null.</returns>
    //IApplication UseDirective(string name);

    /// <summary>
    /// Registers a directive under <paramref name="name"/> so descendants of the root can resolve it
    /// by name (upstream: <c>app.directive(name, directive)</c>). Registering a duplicate name, or
    /// registering after mount, warns in dev. Returns the application for chaining.
    /// </summary>
    /// <param name="name">The directive name (resolved case-insensitively at render).</param>
    /// <param name="directive">The directive definition.</param>
    /// <returns>This application, for chaining.</returns>
    IApplication UseDirective(string name, IDirective directive);


    /// <summary>
    /// 
    /// </summary>
    /// <param name="selector"></param>
    void Mount(string selector);

    /// <summary>
    /// 
    /// </summary>
    /// <param name="selector"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    Task MountAsync(string selector, CancellationToken cancellationToken = default);

    /// <summary>
    /// Unmounts the application (upstream: <c>app.unmount</c>): runs component teardown lifecycles and
    /// releases the rendered tree. A no-op when not mounted.
    /// </summary>
    void Unmount();

    /// <summary>
    /// 
    /// </summary>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    Task UnmountAsync(CancellationToken cancellationToken = default);
}
