using System;
using System.Diagnostics;
using System.Runtime.Versioning;
using System.Threading;
using System.Threading.Tasks;

using Assimalign.Viu;
using Assimalign.Viu.Components;

namespace Assimalign.Viu.Browser;

/// <summary>
/// A Viu application hosted by a browser DOM.
/// </summary>
/// <remarks>
/// Browser nodes are opaque integer handles. All component, service, state, plugin, and mounted
/// component contracts remain platform-neutral through <see cref="Application{TNode}"/> and
/// <see cref="IApplicationContext"/>, allowing another host such as WebView2 to supply its own
/// application without depending on this assembly. The application borrows every resolver in its
/// context and never disposes them. Not thread-safe.
/// </remarks>
[SupportedOSPlatform("browser")]
public sealed class BrowserApplication : Application<int>
{
    private readonly Renderer<int> _renderer;
    private readonly BufferedBrowserNodeOperations? _bufferedOperations;
    private readonly bool _hydrate;
    private readonly Func<CancellationToken, Task> _initialize;
    private readonly Action<int> _clearContainer;
    private readonly Func<string, int> _resolveContainer;
    private Task? _initialization;
    private int _container;

    internal BrowserApplication(
        Renderer<int> renderer,
        IApplicationContext context,
        BufferedBrowserNodeOperations? bufferedOperations = null,
        bool hydrate = false,
        Func<CancellationToken, Task>? initialize = null,
        Action<int>? clearContainer = null,
        Func<string, int>? resolveContainer = null)
        : base(context)
    {
        ArgumentNullException.ThrowIfNull(renderer);
        _renderer = renderer;
        _bufferedOperations = bufferedOperations;
        _hydrate = hydrate;
        _initialize = initialize ?? BrowserRuntime.EnsureBridgeAsync;
        _clearContainer = clearContainer ?? BrowserRuntime.ClearContainer;
        _resolveContainer = resolveContainer ?? BrowserRuntime.QuerySelector;

        void HandleEventError(Exception exception)
        {
            Action<Exception, IComponentContext?, string>? handler =
                Context.ErrorHandler;
            if (handler is not null)
            {
                handler(exception, null, "browser event handler");
                return;
            }

            Debug.WriteLine(
                $"[Viu warn] Unhandled error in browser event handler: {exception}");
        }

        BrowserNodeOperations.ErrorSink = HandleEventError;
        if (bufferedOperations is not null)
        {
            bufferedOperations.ErrorSink = HandleEventError;
        }
    }

    /// <summary>
    /// Gets whether this application was configured to hydrate server-rendered markup.
    /// </summary>
    public bool IsHydrating => _hydrate;

    /// <summary>Creates an unconfigured browser application builder.</summary>
    /// <param name="useCommandBuffer">
    /// Whether host mutations should be serialized into one command frame per explicit render
    /// boundary.
    /// </param>
    /// <returns>The browser application builder.</returns>
    public static BrowserApplicationBuilder CreateBuilder(bool useCommandBuffer = false)
    {
        return new BrowserApplicationBuilder(useCommandBuffer, hydrate: false);
    }

    /// <summary>Creates a browser application builder with its root tree configured.</summary>
    /// <param name="rootComponent">The root value in the unified component tree.</param>
    /// <param name="useCommandBuffer">
    /// Whether host mutations should be serialized into one command frame per explicit render
    /// boundary.
    /// </param>
    /// <returns>The browser application builder.</returns>
    public static BrowserApplicationBuilder CreateBuilder(
        IComponent rootComponent,
        bool useCommandBuffer = false)
    {
        ArgumentNullException.ThrowIfNull(rootComponent);
        BrowserApplicationBuilder builder =
            new(useCommandBuffer, hydrate: false);
        builder.UseRootComponent(rootComponent);
        return builder;
    }

    /// <summary>
    /// Creates a builder reserved for hydration of server-rendered browser markup.
    /// </summary>
    /// <param name="rootComponent">The root value in the unified component tree.</param>
    /// <returns>The browser application builder.</returns>
    public static BrowserApplicationBuilder CreateServerRendererBuilder(
        IComponent rootComponent)
    {
        ArgumentNullException.ThrowIfNull(rootComponent);
        BrowserApplicationBuilder builder =
            new(useCommandBuffer: false, hydrate: true);
        builder.UseRootComponent(rootComponent);
        return builder;
    }

    /// <summary>
    /// Initializes the browser bridge, resolves a CSS selector, and mounts the root tree.
    /// </summary>
    /// <param name="selector">The CSS selector for the mount container.</param>
    /// <param name="cancellationToken">Cancels bridge initialization.</param>
    /// <returns>The mounted root template context, when the root is a template.</returns>
    public async ValueTask<IComponentContext?> MountAsync(
        string selector,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(selector);

        // Selector resolution itself requires the bridge. The generic base owns plugin ordering
        // and awaits this same cached initialization again before it invokes MountCore.
        await OnInitializeAsync(cancellationToken).ConfigureAwait(false);
        int container = _resolveContainer(selector);
        return await base.MountAsync(container, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    protected override ValueTask OnInitializeAsync(CancellationToken cancellationToken)
    {
        return new ValueTask(
            _initialization ??= _initialize(cancellationToken));
    }

    /// <inheritdoc/>
    protected override IComponentContext? MountCore(int container)
    {
        _container = container;
        _bufferedOperations?.ObserveForeignHandle(container);
        IComponentContext? rootContext;
        if (_hydrate)
        {
            rootContext =
                _renderer.Hydrate(Context.RootComponent, container, Context);
        }
        else
        {
            _clearContainer(container);
            rootContext =
                _renderer.Render(Context.RootComponent, container, Context);
        }

        _bufferedOperations?.ApplyPending();
        return rootContext;
    }

    /// <inheritdoc/>
    protected override void UnmountCore()
    {
        if (_container == default)
        {
            return;
        }

        _renderer.Render(null, _container, Context);
        _bufferedOperations?.ApplyPending();
        _bufferedOperations?.Deactivate();
        _container = default;
    }

    internal static BrowserApplication Create(
        IApplicationContext context,
        bool useCommandBuffer,
        bool hydrate)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (useCommandBuffer)
        {
            BufferedBrowserNodeOperations operations =
                BufferedBrowserNodeOperations.CreateProduction();
            Renderer<int> renderer =
                RendererFactory.CreateRenderer(operations.Create());
            return new BrowserApplication(renderer, context, operations, hydrate);
        }

        return new BrowserApplication(
            RendererFactory.CreateRenderer(BrowserNodeOperations.Create()),
            context,
            bufferedOperations: null,
            hydrate);
    }
}
