using System;

using Assimalign.Viu;
using Assimalign.Viu.Components;
using Assimalign.Viu.Reactivity;

#if SERVER_MARKUP
namespace EndToEndServerMarkup;
#else
namespace EndToEndHydrationApp;
#endif

internal static class HydrationFixture
{
    private static readonly ComponentReference RootReference =
        ComponentReference.ForName("HydrationRoot");
    private static readonly ComponentReference LazyReference =
        ComponentReference.ForName("VisibleLazyCard");

    internal static ComponentFactory CreateComponents()
    {
        ComponentFactory components = new();
        components.Register(
            new ComponentRegistration(
                RootReference,
                new ComponentContract(displayName: "HydrationRoot"),
                static _ => new HydrationRootComponent()));
        components.Register(
            new ComponentRegistration(
                LazyReference,
                new ComponentContract(displayName: "VisibleLazyCard"),
                static _ => new VisibleLazyCardComponent()));
        return components;
    }

    internal static ComponentNode CreateRoot() => new(RootReference);

    private static ElementBinding Attribute(string name, object? value) =>
        ElementBinding.Attribute(new QualifiedName(name), value);

    private sealed class HydrationRootComponent : IComponent
    {
        public ComponentRenderer Setup(ComponentContext context)
        {
            ArgumentNullException.ThrowIfNull(context);
            return static _ => new ElementNode(
                new QualifiedName("main"),
                bindings:
                [
                    Attribute("id", "hydration-root"),
                    Attribute("data-testid", "hydration-root"),
                ],
                children:
                [
                    new ElementNode(
                        new QualifiedName("section"),
                        bindings: [Attribute("style", "min-height: 1800px")],
                        children:
                        [
                            new ElementNode(
                                new QualifiedName("h1"),
                                bindings: [Attribute("data-testid", "hydrated-heading")],
                                children: [new TextNode("SSR hydrated route")]),
                            new ElementNode(
                                new QualifiedName("p"),
                                children:
                                [
                                    new TextNode(
                                        "This markup was streamed through ServerRenderAdaptor before Browser hydration."),
                                ]),
                        ]),
                    new ComponentNode(
                        LazyReference,
                        new ComponentInvocation(
                            hydrationStrategy: HydrationStrategy.OnVisible())),
                ]);
        }
    }

    private sealed class VisibleLazyCardComponent : IComponent
    {
        private readonly Reference<bool> _isActive = Reactive.Reference(false);
        private readonly Reference<int> _activationCount = Reactive.Reference(0);

        public ComponentRenderer Setup(ComponentContext context)
        {
            ArgumentNullException.ThrowIfNull(context);
            context.Lifecycle.OnMounted(() => _isActive.Value = true);

            void Activate(IElementEvent payload)
            {
                _ = payload;
                _activationCount.Value++;
            }

            return _ => new ElementNode(
                new QualifiedName("button"),
                bindings:
                [
                    Attribute("type", "button"),
                    Attribute("data-testid", "lazy-action"),
                    ElementBinding.Event("click", (Action<IElementEvent>)Activate),
                ],
                children:
                [
                    new TextNode(
                        _isActive.Value
                            ? $"Lazy ready: {_activationCount.Value}"
                            : $"Lazy waiting: {_activationCount.Value}"),
                ]);
        }
    }
}
