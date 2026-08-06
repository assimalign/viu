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
/// Browser nodes are opaque integer handles. All component, service, state, middleware, and mounted
/// component contracts remain platform-neutral through <see cref="Application{TNode}"/> and
/// <see cref="IApplicationContext"/>, allowing another host such as WebView2 to supply its own
/// application without depending on this assembly. The application borrows every resolver in its
/// context and never disposes them. Top-level execution resolves the configured mount selector only
/// after browser initialization and keeps middleware active until the application stops. Not
/// thread-safe. Specified by <c>[APP-4]</c> and <c>[APP-6]</c>.
/// </remarks>
[SupportedOSPlatform("browser")]
public sealed class BrowserApplication : Application<int>
{
    internal const string DefaultMountTargetSelector = "#app";

    private readonly Renderer<int> _renderer;
    private readonly BufferedBrowserNodeOperations? _bufferedOperations;
    private readonly bool _hydrate;
    private readonly Func<CancellationToken, Task> _initialize;
    private readonly Action<int> _clearContainer;
    private readonly Func<string, int> _resolveContainer;
    private readonly string _mountTargetSelector;
    private Task? _initialization;
    private int _container;

    internal BrowserApplication(
        Renderer<int> renderer,
        IApplicationContext context,
        BufferedBrowserNodeOperations? bufferedOperations = null,
        bool hydrate = false,
        Func<CancellationToken, Task>? initialize = null,
        Action<int>? clearContainer = null,
        Func<string, int>? resolveContainer = null,
        string mountTargetSelector = DefaultMountTargetSelector)
        : base(context)
    {
        ArgumentNullException.ThrowIfNull(renderer);
        ArgumentException.ThrowIfNullOrEmpty(mountTargetSelector);
        _renderer = renderer;
        _bufferedOperations = bufferedOperations;
        _hydrate = hydrate;
        _initialize = initialize ?? BrowserRuntime.EnsureBridgeAsync;
        _clearContainer = clearContainer ?? BrowserRuntime.ClearContainer;
        _resolveContainer = resolveContainer ?? BrowserRuntime.QuerySelector;
        _mountTargetSelector = mountTargetSelector;

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
        builder.AddRootComponent(rootComponent);
        return builder;
    }

    /// <summary>
    /// Initializes the browser bridge, resolves a CSS selector, and mounts the root tree.
    /// </summary>
    /// <param name="selector">The CSS selector for the mount container.</param>
    /// <param name="cancellationToken">Cancels bridge initialization.</param>
    /// <returns>The mounted root template context, when the root is a template.</returns>
    /// <remarks>
    /// This is a lower-level embedding API. It bypasses top-level application middleware; ordinary
    /// browser applications use <see cref="IApplication.RunAsync(CancellationToken)"/> instead.
    /// Specified by <c>[APP-7]</c>.
    /// </remarks>
    public ValueTask<IComponentContext?> MountAsync(
        string selector,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(selector);
        return MountResolvedAsync(
            stopping =>
            {
                stopping.ThrowIfCancellationRequested();
                return ValueTask.FromResult(_resolveContainer(selector));
            },
            cancellationToken);
    }

    /// <inheritdoc/>
    protected override ValueTask OnInitializeAsync(CancellationToken cancellationToken)
    {
        return new ValueTask(
            _initialization ??= _initialize(cancellationToken));
    }

    /// <inheritdoc/>
    protected override ValueTask<int> ResolveMountTargetAsync(
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return ValueTask.FromResult(
            _resolveContainer(_mountTargetSelector));
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
        bool hydrate,
        string mountTargetSelector)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (useCommandBuffer)
        {
            BufferedBrowserNodeOperations operations =
                BufferedBrowserNodeOperations.CreateProduction();
            Renderer<int> renderer =
                RendererFactory.CreateRenderer(operations.Create());
            return new BrowserApplication(
                renderer,
                context,
                operations,
                hydrate,
                mountTargetSelector: mountTargetSelector);
        }

        return new BrowserApplication(
            RendererFactory.CreateRenderer(BrowserNodeOperations.Create()),
            context,
            bufferedOperations: null,
            hydrate,
            mountTargetSelector: mountTargetSelector);
    }
}
