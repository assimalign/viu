using Shouldly;
using Xunit;

namespace Assimalign.Viu.Compiler.SingleFileComponent.Tests;

/// <summary>
/// Pins the public projection-record construction and deconstruction surface while additive compiler
/// knowledge remains outside each primary constructor ([SFC-USE-5], [V01.01.05.11]).
/// </summary>
public sealed class ProjectionCompatibilityTests
{
    [Fact]
    public void ProjectionRecords_ExistingConstructorsAndDeconstructionRemainAvailable()
    {
        var entry = new ComponentDeclarationEntry(
            "Button",
            EquatableArray<ComponentParameterDeclaration>.Empty);
        var declarations = new ScriptDeclarations(
            EquatableArray<ComponentParameterDeclaration>.Empty,
            EquatableArray<ComponentEventDeclaration>.Empty,
            false,
            false);

        var (componentName, parameters) = entry;
        var (scriptParameters, events, declaresRequiredMember, declaresConstructor) = declarations;

        componentName.ShouldBe("Button");
        parameters.Count.ShouldBe(0);
        entry.IsParameterSurfaceKnown.ShouldBeTrue();
        scriptParameters.Count.ShouldBe(0);
        events.Count.ShouldBe(0);
        declaresRequiredMember.ShouldBeFalse();
        declaresConstructor.ShouldBeFalse();
        declarations.DeclaresImperativeParameters.ShouldBeFalse();
    }
}
