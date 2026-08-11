using System;
using System.Threading.Tasks;

using Assimalign.Viu;
using Assimalign.Viu.Browser;
using Assimalign.Viu.Components;

namespace EndToEndHotReloadApp;

internal static class Program
{
    internal static async Task Main()
    {
        ComponentFactory components = new();
        GeneratedViuComponents.Register(components);

        await using BrowserApplication application = new BrowserApplicationBuilder()
            .ConfigureApplication(
                options =>
                {
                    options.RootComponent = new ComponentNode(
                        ComponentReference.ForName("HotReloadPage"));
                    options.Components = components;
                    options.ErrorHandler = ReportError;
                    options.WarnHandler = message =>
                        Console.WriteLine($"Viu hot-reload warning: {message}");
                })
            .Build();

        await application.RunAsync();
    }

    private static void ReportError(
        Exception exception,
        ComponentContext? context,
        string source)
    {
        _ = context;
        Console.Error.WriteLine($"Viu hot-reload error ({source}): {exception}");
    }
}
