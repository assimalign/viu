using System;
using System.Threading;
using System.Threading.Tasks;

using Assimalign.Viu;
using Assimalign.Viu.Browser;
using Assimalign.Viu.Components;
using Assimalign.Viu.Router;
using Assimalign.Viu.Router.Browser;
using Assimalign.Viu.State;

ComponentFactory components = new(
[
    new ComponentRegistration(
        typeof(App),
        static () => new App()),
]);
EmptyServiceProvider services = new();
using EmptyStateStoreRegistry state = new();
using Router router = new(
    RouterHistory.CreateWebHash(),
    [new RouteRecord("/")]);

await Application
    .CreateBuilder()
    .AddRootComponent(ComponentTree.Template<App>())
    .AddComponentFactory(components)
    .AddServiceProvider(services)
    .AddStateRegistry(state)
    .ConfigureApplication(options =>
    {
        options.WarnHandler = static _ => { };
        options.ErrorHandler = static (_, _, _) => { };
    })
    .Build()
    .UseRouter(router)
    .Use(async (execution, next) =>
    {
        await RestoreSessionAsync(execution.Stopping);
        await next(execution);
    })
    .RunAsync();

static ValueTask RestoreSessionAsync(CancellationToken cancellationToken)
{
    cancellationToken.ThrowIfCancellationRequested();
    return ValueTask.CompletedTask;
}

internal sealed class App : IComponentTemplate
{
    public ComponentRenderer Setup(IComponentContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        return static () => ComponentTree.Element("main");
    }
}

internal sealed class EmptyServiceProvider : IServiceProvider
{
    public object? GetService(Type serviceType)
    {
        ArgumentNullException.ThrowIfNull(serviceType);
        return null;
    }
}

internal sealed class EmptyStateStoreRegistry : IStateStoreRegistry
{
    public int Count => 0;

    public bool IsDisposed { get; private set; }

    public TStore GetOrCreate<TStore>(
        StateStoreDefinition<TStore> definition,
        IComponentContext? owner = null)
        where TStore : class
    {
        ArgumentNullException.ThrowIfNull(definition);
        throw new InvalidOperationException("The compile fixture does not create state stores.");
    }

    public bool Remove<TStore>(StateStoreDefinition<TStore> definition)
        where TStore : class
    {
        ArgumentNullException.ThrowIfNull(definition);
        return false;
    }

    public void Dispose()
    {
        IsDisposed = true;
    }
}
