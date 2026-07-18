using System;

using Assimalign.Vue.RuntimeCore;

namespace Assimalign.Vue.RuntimeDom.Tests;

// A minimal component definition whose setup returns a supplied render function — the RuntimeDom
// test stand-in for the source-generated component the compiler emits.
internal sealed class RenderComponent : IComponentDefinition
{
    private readonly Func<ComponentProperties, ComponentSetupContext, Func<VirtualNode?>> _setup;

    public RenderComponent(Func<ComponentProperties, ComponentSetupContext, Func<VirtualNode?>> setup) => _setup = setup;

    // Convenience for a render that needs no setup state.
    public RenderComponent(Func<VirtualNode?> render) => _setup = (_, _) => render;

    public Func<VirtualNode?> Setup(ComponentProperties properties, ComponentSetupContext context) => _setup(properties, context);
}
