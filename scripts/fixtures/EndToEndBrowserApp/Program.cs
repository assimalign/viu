using System;
using System.Threading.Tasks;

using Assimalign.Viu;
using Assimalign.Viu.Browser;
using Assimalign.Viu.Browser.Router;
using Assimalign.Viu.Components;
using Assimalign.Viu.Router;

using ViuRouter = Assimalign.Viu.Router.Router;

namespace EndToEndBrowserApp;

internal static class Program
{
    internal static async Task Main()
    {
        ComponentFactory components = new();
        GeneratedViuComponents.Register(components);
        components.Register(RouterView.Registration);
        components.Register(RegisterByName("RouterView", RouterView.Registration));
        components.Register(RouterLink.Registration);
        components.Register(RegisterByName("RouterLink", RouterLink.Registration));

        using IRouterHistory history = BrowserRouterHistory.CreateWebHash();
        using ViuRouter router = new(
            history,
            [
                new RouteRecord(
                    "/",
                    component: new ComponentNode(ComponentReference.ForName("HomePage"))),
                new RouteRecord(
                    "/second",
                    component: new ComponentNode(ComponentReference.ForName("SecondPage"))),
            ]);
        router.ScrollBehavior = static (_, _, savedPosition) =>
            Task.FromResult<ScrollTarget?>(
                new ScrollTarget(savedPosition ?? new ScrollPosition(0, 0)));
        RouterServiceProvider services = new(router);

        await using BrowserApplication application = new BrowserApplicationBuilder()
            .ConfigureApplication(
                options =>
                {
                    options.RootComponent = new ComponentNode(RouterView.Registration.Reference);
                    options.Components = components;
                    options.Services = services;
                    options.ErrorHandler = ReportError;
                    options.WarnHandler = ReportWarning;
                })
            .Build();

        await application
            .UseRouter(router)
            .RunAsync();
    }

    private static ComponentRegistration RegisterByName(
        string name,
        ComponentRegistration registration) => new(
        ComponentReference.ForName(name),
        registration.Contract,
        registration.Activator);

    private static void ReportError(
        Exception exception,
        ComponentContext? context,
        string source)
    {
        _ = context;
        Console.Error.WriteLine($"Viu application error ({source}): {exception}");
    }

    private static void ReportWarning(string message) =>
        Console.WriteLine($"Viu application warning: {message}");

    private sealed class RouterServiceProvider : IServiceProvider
    {
        private readonly ViuRouter _router;

        internal RouterServiceProvider(ViuRouter router)
        {
            _router = router;
        }

        public object? GetService(Type serviceType)
        {
            ArgumentNullException.ThrowIfNull(serviceType);
            return serviceType == typeof(ViuRouter) ? _router : null;
        }
    }
}
