using System;
using System.Threading.Tasks;

using Assimalign.Viu;
using Assimalign.Viu.Browser;
using Assimalign.Viu.Components;

namespace PackagedVueBrowserConsumer;

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
                        ComponentReference.ForName("PublishedVuePage"));
                    options.Components = components;
                    options.ErrorHandler = ReportError;
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
        Console.Error.WriteLine($"Viu packaged .vue error ({source}): {exception}");
    }
}
