using System;

using Assimalign.Viu;
using Assimalign.Viu.Browser;
using Assimalign.Viu.Browser.Router;
using Assimalign.Viu.Components;
using Assimalign.Viu.Router;

ComponentReference appReference = ComponentReference.ForType(typeof(App));
ComponentFactory components = new();
components.Register(
    new ComponentRegistration(
        appReference,
        new ComponentContract(renderCacheSize: 0, displayName: nameof(App)),
        static _ => new App()));
EmptyServiceProvider services = new();
using IRouterHistory history = RouterHistory.CreateWebHash();
using Router router = new(
    history,
    [new RouteRecord("/")]);

await new BrowserApplicationBuilder()
    .ConfigureApplication(options =>
    {
        options.RootComponent = new ComponentNode(appReference);
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

internal sealed class App : IComponent
{
    public ComponentRenderer Setup(ComponentContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        return static (ComponentRenderFrame frame) =>
        {
            ArgumentNullException.ThrowIfNull(frame);
            return new ElementNode(new QualifiedName("main"));
        };
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
