using System;
using System.Threading.Tasks;

using Assimalign.Viu;
using Assimalign.Viu.Browser;

namespace EndToEndHydrationApp;

internal static class Program
{
    internal static async Task Main()
    {
        await using BrowserApplication application = new BrowserApplicationBuilder()
            .ConfigureApplication(
                options =>
                {
                    options.RootComponent = HydrationFixture.CreateRoot();
                    options.Components = HydrationFixture.CreateComponents();
                    options.ErrorHandler = (exception, _, source) =>
                        Console.Error.WriteLine(
                            $"Viu hydration error ({source}): {exception}");
                    options.WarnHandler = message =>
                        Console.WriteLine($"Viu hydration warning: {message}");
                })
            .ConfigureBrowser(options => options.Hydrate = true)
            .Build();

        await application.RunAsync();
    }
}
