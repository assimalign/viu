using System;

using Assimalign.Viu;
using Assimalign.Viu.Browser;
using Assimalign.Viu.Browser.Router;
using Assimalign.Viu.Components;
using Assimalign.Viu.Router;

ComponentFactory components = new();
ComponentLibraryConsumer.GeneratedViuComponents.Register(components);
EmptyServiceProvider services = new();
using IRouterHistory history = RouterHistory.CreateWebHash();
using Router router = new(
    history,
    [new RouteRecord("/")]);

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
