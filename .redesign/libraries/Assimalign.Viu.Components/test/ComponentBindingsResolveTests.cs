using System.Collections.Generic;

using Assimalign.Viu.Components;

using Shouldly;

using Xunit;

namespace Assimalign.Viu.Components.Tests;

public sealed class ComponentBindingsResolveTests
{
    [Fact]
    public void Resolve_DeclaredAndUndeclaredArguments_SplitParametersFromFallthrough()
    {
        var contract = new ComponentContract(
            parameters: new[] { new ComponentParameter("name") });
        var invocation = new ComponentInvocation(
            arguments: new Dictionary<string, object?>
            {
                ["name"] = "Viu",
                ["class"] = "welcome",
            });

        var bindings = ComponentBindings.Resolve(contract, invocation);

        bindings.Parameters.Count.ShouldBe(1);
        bindings.Parameters["name"].ShouldBe("Viu");
        bindings.FallthroughBindings.Count.ShouldBe(1);
        bindings.FallthroughBindings["class"].ShouldBe("welcome");
    }

    [Fact]
    public void Resolve_MissingRequiredParameter_ReportsMissingRequiredParameterDiagnostic()
    {
        var contract = new ComponentContract(
            parameters: new[] { new ComponentParameter("name", isRequired: true) });
        var diagnostics = new List<ComponentBindingDiagnostic>();

        var bindings = ComponentBindings.Resolve(contract, ComponentInvocation.Empty, diagnostics);

        bindings.Parameters.ShouldBeEmpty();
        var diagnostic = diagnostics.ShouldHaveSingleItem();
        diagnostic.Kind.ShouldBe(ComponentBindingDiagnosticKind.MissingRequiredParameter);
        diagnostic.Name.ShouldBe("name");
    }
}
