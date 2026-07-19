using System.Collections.Generic;

using Shouldly;

using Xunit;

namespace Assimalign.Vue.Syntax.Templates;

// The standalone instance-member expression compile ([V01.01.06.06.01], issue #149): the v-bind() CSS getter
// routes its expressions through the template compiler's binding-metadata rewriting, but as an instance member
// (no _ctx receiver, definite references unwrap to .Value, everything else bare). Malformed expressions surface
// diagnostics rather than throwing.
public class TemplateExpressionCompilerTests
{
    [Fact]
    public void CompileInstanceExpression_Reference_UnwrapsToValue_WithoutContextReceiver()
    {
        // The pinned acceptance shape: `v-bind(count)` with a script Reference<T> member unwraps to `count.Value`,
        // read through the implicit `this` (no `_ctx.`) because the getter is an instance member.
        var result = Compile("count", ("count", BindingType.SetupReference));

        result.Diagnostics.ShouldBeEmpty();
        result.Code.ShouldBe("count.Value");
    }

    [Fact]
    public void CompileInstanceExpression_NonReference_StaysBare()
    {
        // A non-reference binding (a plain field) is provably not reactive, so it is never unwrapped and stays bare.
        var result = Compile("color", ("color", BindingType.SetupLet));

        result.Diagnostics.ShouldBeEmpty();
        result.Code.ShouldBe("color");
    }

    [Fact]
    public void CompileInstanceExpression_ReferenceMemberAccess_UnwrapsTheReferenceOnly()
    {
        // Only the reference identifier unwraps; the member access rides on the unwrapped value.
        var result = Compile("theme.color", ("theme", BindingType.SetupReference));

        result.Diagnostics.ShouldBeEmpty();
        result.Code.ShouldBe("theme.Value.color");
    }

    [Fact]
    public void CompileInstanceExpression_Malformed_SurfacesDiagnostic_NeverThrows()
    {
        var result = Compile("1 +", ("count", BindingType.SetupReference));

        var error = result.Diagnostics.ShouldHaveSingleItem();
        error.Code.ShouldBe(CompilerErrorCode.XInvalidExpression);
    }

    [Fact]
    public void CompileInstanceExpression_UnresolvedUnderPermissiveMetadata_StaysBare_NoDiagnostic()
    {
        // Permissive metadata (the generator's choice, matching the render path so hand-written members are not
        // false-flagged) leaves an unknown identifier bare; the C# compiler is the backstop.
        var result = Compile("mystery");

        result.Diagnostics.ShouldBeEmpty();
        result.Code.ShouldBe("mystery");
    }

    private static ExpressionCompileResult Compile(string expression, params (string Name, BindingType Type)[] bindings)
    {
        var map = new Dictionary<string, BindingType>();
        foreach (var (name, type) in bindings)
        {
            map[name] = type;
        }

        var metadata = new BindingMetadata(map, isScriptSetup: true);
        var location = new SourceLocation(
            new Position(0, 1, 1),
            new Position(expression.Length, 1, expression.Length + 1),
            expression);
        return TemplateExpressionCompiler.CompileInstanceExpression(expression, metadata, location);
    }
}
