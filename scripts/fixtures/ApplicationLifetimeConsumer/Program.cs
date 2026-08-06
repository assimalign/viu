using System;

using Assimalign.Viu;
using Assimalign.Viu.Browser;
using Assimalign.Viu.Browser.Router;
using Assimalign.Viu.Components;
using Assimalign.Viu.Router;

ComponentFactory components = new(
[
    new ComponentRegistration(
        typeof(App),
        static () => new App()),
]);
EmptyServiceProvider services = new();
using Router router = new(
    RouterHistory.CreateWebHash(),
    [new RouteRecord("/")]);

await new BrowserApplicationBuilder()
    .ConfigureApplication(options =>
    {
        options.RootComponent = ComponentTree.Template<App>();
        options.Components = components;
        options.Services = services;
        options.ErrorHandler = RecordError;
    })
    .Build()
    .UseRouter(router)
    .RunAsync();

static void RecordError(
    Exception exception,
    IComponentContext? context,
    string source)
{
    _ = exception;
    _ = context;
    _ = source;
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
