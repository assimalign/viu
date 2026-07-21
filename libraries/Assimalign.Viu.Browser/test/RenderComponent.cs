using System;

using Assimalign.Viu;

namespace Assimalign.Viu.Browser.Tests;

// A minimal component definition whose setup returns a supplied render function — the Browser
// test stand-in for the source-generated component the compiler emits.
internal sealed class RenderComponent : IComponent
{
    private readonly Func<ComponentProperties, ComponentSetupContext, ComponentSetup> _setup;

    public RenderComponent(Func<ComponentProperties, ComponentSetupContext, ComponentSetup> setup) => _setup = setup;

    // Convenience for a render that needs no setup state.
    public RenderComponent(ComponentSetup render) => _setup = (_, _) => render;

    public ComponentSetup Setup(ComponentProperties properties, ComponentSetupContext context) => _setup(properties, context);
}
