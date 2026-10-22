using Shouldly;
using Xunit;

using Assimalign.Viu.Components;

namespace Assimalign.Viu.Browser.Tests;

public sealed class BuiltInDefinitionTests
{
    [Fact]
    public void TypedDescriptions_BrowserBuiltIns_UseTheirExistingStaticRegistration()
    {
        // [CMP-35], [V01.01.06.18]: built-ins participate in the same static definition contract.
        Verify<Transition>(Transition.Registration);
        Verify<TransitionGroup>(TransitionGroup.Registration);
    }

    private static void Verify<TDefinition>(ComponentRegistration registration)
        where TDefinition : IComponentDefinition
    {
        TDefinition.Registration.ShouldBeSameAs(registration);
        ComponentNode.For<TDefinition>().Component.ShouldBeSameAs(registration.Reference);
    }
}
