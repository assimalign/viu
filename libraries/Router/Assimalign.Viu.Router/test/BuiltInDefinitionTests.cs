using Shouldly;
using Xunit;

using Assimalign.Viu.Components;

namespace Assimalign.Viu.Router.Tests;

public sealed class BuiltInDefinitionTests
{
    [Fact]
    public void TypedDescriptions_RouterBuiltIns_UseTheirExistingStaticRegistration()
    {
        // [CMP-35], [V01.01.06.18]: no activation or runtime metadata discovery is needed.
        Verify<RouterView>(RouterView.Registration);
        Verify<RouterLink>(RouterLink.Registration);
    }

    private static void Verify<TDefinition>(ComponentRegistration registration)
        where TDefinition : IComponentDefinition
    {
        TDefinition.Registration.ShouldBeSameAs(registration);
        ComponentNode.For<TDefinition>().Component.ShouldBeSameAs(registration.Reference);
    }
}
