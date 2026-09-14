using System;
using System.Runtime.InteropServices.JavaScript;
using System.Threading.Tasks;

using Assimalign.Viu;
using Assimalign.Viu.Browser;
using Assimalign.Viu.Browser.Router;
using Assimalign.Viu.Router;
using Assimalign.Viu.State;

using ViuRouter = Assimalign.Viu.Router.Router;

namespace EndToEndPrerenderApp;

internal static partial class Program
{
    internal static async Task Main()
    {
        using IRouterHistory history = BrowserRouterHistory.CreateWeb();
        using ViuRouter router = PrerenderFixture.CreateRouter(history);
        using IStateStoreRegistry state = StateStores.CreateRegistry();
        PrerenderFixture.Mounted = MarkHydrated;
        await using BrowserApplication application = new BrowserApplicationBuilder()
            .ConfigureApplication(options =>
            {
                options.RootComponent = PrerenderFixture.CreateRoot();
                options.Components = PrerenderFixture.CreateComponents();
                options.Services = new PrerenderServices(router);
                options.State = state;
                options.ErrorHandler = (exception, _, source) =>
                    Console.Error.WriteLine($"Prerender hydration error ({source}): {exception}");
                options.WarnHandler = message => Console.Error.WriteLine(message);
            })
            .ConfigureBrowser(options => options.Hydrate = true)
            .Build();
        await application.UseRouter(router).RunAsync();
    }

    [JSImport("markHydrated", "prerender-fixture")]
    internal static partial void MarkHydrated();
}
