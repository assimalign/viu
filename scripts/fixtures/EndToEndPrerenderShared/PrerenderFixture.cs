using System;
using System.Threading.Tasks;

using Assimalign.Viu;
using Assimalign.Viu.Components;
using Assimalign.Viu.Reactivity;
using Assimalign.Viu.Router;
using Assimalign.Viu.State;

using ViuRouter = Assimalign.Viu.Router.Router;

#if STATIC_PRERENDER_HOST
namespace EndToEndPrerenderHost;
#else
namespace EndToEndPrerenderApp;
#endif

// [V01.01.07.05], #68: the same explicit registrations and state schema serve both hosts.
internal static class PrerenderFixture
{
    private static readonly ComponentReference PageReference = ComponentReference.ForName("PrerenderPage");

    internal static readonly StateStoreDefinition<PrerenderStore> Store = StateStores.Define(
        "prerender-page",
        static _ => new PrerenderStore(),
        new StateStoreJsonSerializer<PrerenderStore, PrerenderState>(
            static store => store.State,
            static (store, state) => store.State = state,
            PrerenderJsonContext.Default.PrerenderState));

    internal static Action? Mounted { get; set; }

    internal static ComponentNode CreateRoot() => new(RouterView.Registration.Reference);

    internal static ComponentFactory CreateComponents()
    {
        ComponentFactory components = new();
        components.Register(RouterView.Registration);
        components.Register(new ComponentRegistration(
            PageReference,
            new ComponentContract(
                displayName: "PrerenderPage",
                parameters: [new ComponentParameter("slug")]),
            static _ => new PageComponent()));
        return components;
    }

    internal static ViuRouter CreateRouter(IRouterHistory history)
    {
        ViuRouter router = new(history,
        [
            new RouteRecord("/", component: new ComponentNode(PageReference)),
            new RouteRecord(
                "/guide/:slug",
                component: new ComponentNode(PageReference),
                argumentsResolver: RouteComponentArguments.FromParameters()),
        ]);
        router.BeforeEach(static (destination, _, _) => Task.FromResult(
            destination.Matched.Count > 0
                ? NavigationGuardResult.Allow
                : NavigationGuardResult.Abort));
        return router;
    }

    private static ElementBinding Attribute(string name, object value) =>
        ElementBinding.Attribute(new QualifiedName(name), value);

    private sealed class PageComponent : IComponent
    {
        public ComponentRenderer Setup(ComponentContext context)
        {
            PrerenderStore store = Store.Use(context);
            Reference<int> count = Reactive.Reference(0);
            string heading = context.Bindings.Parameters.TryGetValue("slug", out object? slug)
                ? $"Guide: {slug}"
                : "Static home";
            context.Lifecycle.OnMounted(() => Mounted?.Invoke());
            return _ => new FragmentNode(
            [
                new ElementNode(new QualifiedName("main"),
                    bindings: [Attribute("data-testid", "prerender-root")],
                    children:
                    [
                        new ElementNode(new QualifiedName("h1"),
                            bindings: [Attribute("data-testid", "prerender-heading")],
                            children: [new TextNode(heading)]),
                        new ElementNode(new QualifiedName("p"),
                            bindings: [Attribute("data-testid", "prerender-state")],
                            children: [new TextNode(store.State.Message)]),
                        new ElementNode(new QualifiedName("button"),
                            bindings:
                            [
                                Attribute("type", "button"),
                                Attribute("data-testid", "prerender-action"),
                                ElementBinding.Event("click", (Action<IElementEvent>)(payload => count.Value++)),
                            ],
                            children: [new TextNode($"Activated: {count.Value}")]),
                    ]),
            ]);
        }
    }
}
