using Assimalign.Viu.Compiler.SingleFileComponent;

using Shouldly;

using Xunit;

namespace Assimalign.Viu.Compiler.SingleFileComponent.Tests;

public sealed class SingleFileComponentCompilerTests
{
    [Fact]
    public void Project_PublicFacade_ReturnsEditorNeutralProjectionResult()
    {
        var request = new SingleFileComponentProjectionRequest(
            SingleFileComponentFormat.Viu,
            "Components/Greeting.viu",
            "<template>Hello</template>",
            "C:/Example",
            "Example.Components");

        var result = SingleFileComponentCompiler.Project(request);

        result.ClassName.ShouldBe("Greeting");
        result.HintName.ShouldBe("Greeting.g.cs");
        result.ComponentNamespace.ShouldBe("Example.Components");
        result.Diagnostics.ShouldBeEmpty();
    }
}
