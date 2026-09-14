using System;
using System.Collections.Generic;
using System.Runtime.InteropServices.JavaScript;
using System.Threading.Tasks;

using Assimalign.Viu;
using Assimalign.Viu.Browser;
using Assimalign.Viu.Components;
using Assimalign.Viu.DevTools;
using Assimalign.Viu.DevTools.Client;

namespace EndToEndDevToolsApp;

internal static partial class Program
{
    internal static async Task Main()
    {
        if (Imports.IsPanel())
        {
            await RunPanelAsync();
            return;
        }

        await using DevToolsSession inspection = new(
            await PostMessageDevToolsTransport.CreateAsync(Imports.GetOrigin()));
        using IDisposable inspector = inspection.RegisterInspector(new SampleInspector());
        await inspection.StartAsync();
        ComponentFactory components = new();
        GeneratedViuComponents.Register(components);
        await using BrowserApplication application = CreateApplication(
            components,
            new ComponentNode(ComponentReference.ForName("InspectedCounter")));
        await application.RunAsync();
    }

    private static async Task RunPanelAsync()
    {
        string endpoint = Imports.GetEndpoint();
        IDevToolsClientTransport transport = endpoint.Length == 0
            ? await PostMessageDevToolsClientTransport.CreateAsync(Imports.GetOrigin())
            : new WebSocketDevToolsClientTransport(new Uri(endpoint, UriKind.Absolute));
        await using DevToolsClientSession session = new(transport);
        ComponentFactory components = new();
        Assimalign.Viu.DevTools.Client.GeneratedViuComponents.Register(components);
        await using BrowserApplication application = CreateApplication(
            components,
            new ComponentNode(
                ComponentReference.ForName("DevToolsPanel"),
                new ComponentInvocation(new Dictionary<string, object?> { ["session"] = session })));
        await session.StartAsync();
        await application.RunAsync();
    }

    private static BrowserApplication CreateApplication(
        ComponentFactory components,
        ComponentNode root) => new BrowserApplicationBuilder()
        .ConfigureApplication(options =>
        {
            options.RootComponent = root;
            options.Components = components;
            options.ErrorHandler = static (exception, _, source) =>
                Console.Error.WriteLine($"Viu DevTools fixture error ({source}): {exception}");
            options.WarnHandler = static message =>
                Console.Error.WriteLine($"Viu DevTools fixture warning: {message}");
        })
        .Build();

    private static partial class Imports
    {
        [JSImport("isPanel", "EndToEndDevToolsApp")]
        internal static partial bool IsPanel();

        [JSImport("getOrigin", "EndToEndDevToolsApp")]
        internal static partial string GetOrigin();

        [JSImport("getEndpoint", "EndToEndDevToolsApp")]
        internal static partial string GetEndpoint();
    }
}
