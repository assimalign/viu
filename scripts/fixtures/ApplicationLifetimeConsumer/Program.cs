using System;
using System.Threading.Tasks;

using Assimalign.Viu;
using Assimalign.Viu.Browser;
using Assimalign.Viu.Browser.Router;
using Assimalign.Viu.Components;
using Assimalign.Viu.DevTools;
using Assimalign.Viu.Router;

ComponentFactory components = new();
ComponentLibraryConsumer.GeneratedViuComponents.Register(components);
EmptyServiceProvider services = new();
using IRouterHistory history = BrowserRouterHistory.CreateWebHash();
using Router router = new(
    history,
    [new RouteRecord("/")]);

IAsyncDisposable? runtimeInspectionSession = null;
try
{
    if (RuntimeInspection.IsSupported)
    {
        runtimeInspectionSession = await StartRuntimeInspectionAsync();
    }

    await new BrowserApplicationBuilder()
        .ConfigureApplication(options =>
        {
            options.RootComponent = new ComponentNode(
                ComponentReference.ForName("LibraryGreeting"));
            options.Components = components;
            options.Services = services;
            options.ErrorHandler = RecordError;
        })
        .Build()
        .UseRouter(router)
        .RunAsync();
}
finally
{
    if (runtimeInspectionSession is not null)
    {
        await runtimeInspectionSession.DisposeAsync();
    }
}

static async ValueTask<IAsyncDisposable> StartRuntimeInspectionAsync()
{
    PostMessageDevToolsTransport transport =
        await PostMessageDevToolsTransport.CreateAsync("*");
    DevToolsSession session = new(transport);
    try
    {
        await session.StartAsync();
        return session;
    }
    catch
    {
        await session.DisposeAsync();
        throw;
    }
}

static void RecordError(
    Exception exception,
    ComponentContext? context,
    string source)
{
    _ = exception;
    _ = context;
    _ = source;
}

internal sealed class EmptyServiceProvider : IServiceProvider
{
    public object? GetService(Type serviceType)
    {
        ArgumentNullException.ThrowIfNull(serviceType);
        return null;
    }
}
