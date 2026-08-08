using System;
using System.Collections.Generic;
using System.Threading.Tasks;

using Shouldly;
using Xunit;

using Assimalign.Viu;
using Assimalign.Viu.Components;
using Assimalign.Viu.ServerRenderer;
using Assimalign.Viu.Testing;

using ViuServerRenderer = Assimalign.Viu.ServerRenderer.ServerRenderer;

namespace Assimalign.Viu.Router.Tests;

// Item 2e close-out: memory-history routing reaches SSR through a public render lease, and Testing
// parses and adopts the exact hydration marker vocabulary. Specified by [RTR-3], [RTR-4], [SSR-2],
// [SSR-6], and [HYD-2].
public sealed class RoutedSsrHydrationIntegrationTests
{
    [Fact]
    public async Task MemoryRoute_ServerRendersAndHydratesThroughPublicSeams()
    {
        ComponentRegistration page = ComponentRegistration.Define(
            "RoutedPage",
            new ComponentContract(
                renderCacheSize: 0,
                displayName: "RoutedPage",
                parameters: [new ComponentParameter("id")]),
            context => _ => new FragmentNode(
            [
                new ElementNode(
                    new QualifiedName("main"),
                    [
                        ElementBinding.Attribute(
                            new QualifiedName("data-route"),
                            ReadString(context.Bindings.Parameters, "id")),
                    ],
                    [new TextNode("routed")]),
            ]));
        Router router = new(
            RouterHistory.CreateMemory(),
            [
                new RouteRecord(
                    "/items/:id",
                    component: new ComponentNode(page.Reference),
                    argumentsResolver: RouteComponentArguments.FromParameters()),
            ]);
        (await router.Push("/items/42")).ShouldBeNull();
        ComponentFactory components = new();
        components.Register(RouterView.Registration);
        components.Register(page);
        ComponentNode root = new(RouterView.Registration.Reference);
        IServiceProvider services = new RouterServiceProvider(router);
        ServerRenderApplication serverApplication = new(root, components, services);

        string markup = await ViuServerRenderer.RenderToStringAsync(serverApplication);

        markup.ShouldBe(
            HydrationMarkers.FragmentStart
            + "<main data-route=\"42\">routed</main>"
            + HydrationMarkers.FragmentEnd);
        TestElement container = TestServerMarkup.Parse(markup);
        container.Children.Count.ShouldBe(3);
        ((TestComment)container.Children[0]).Text.ShouldBe("[");
        ((TestComment)container.Children[2]).Text.ShouldBe("]");

        ApplicationOptions options = new()
        {
            RootComponent = root,
            Components = components,
            Services = services,
        };
        ApplicationContext clientApplication = new(options);
        TestRenderer renderer = new(snapshotSemantics: true);
        Scheduler.Reset();
        try
        {
            renderer.Hydrate(root, container, clientApplication);
            renderer.OperationLog.Count(TestNodeOperationType.CreateElement).ShouldBe(0);
            renderer.OperationLog.Count(TestNodeOperationType.CreateText).ShouldBe(0);
            renderer.OperationLog.Count(TestNodeOperationType.Commit).ShouldBe(1);
        }
        finally
        {
            Scheduler.Reset();
        }
    }

    private static string? ReadString(
        IReadOnlyDictionary<string, object?> values,
        string name)
    {
        return values.TryGetValue(name, out object? value) ? value as string : null;
    }

    private sealed class RouterServiceProvider : IServiceProvider
    {
        private readonly Router _router;

        internal RouterServiceProvider(Router router)
        {
            _router = router;
        }

        public object? GetService(Type serviceType)
        {
            return serviceType == typeof(Router) ? _router : null;
        }
    }
}
