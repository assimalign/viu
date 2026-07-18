using Shouldly;

using Xunit;

namespace Assimalign.Vue.Syntax.Templates;

// Ported from vuejs/core packages/compiler-core/__tests__/transforms/vOn.spec.ts and
// packages/compiler-dom/__tests__/transforms/vOn.spec.ts: event-name resolution, inline-statement wrapping,
// and key/system/event-option modifier handling.
public class VOnTransformTests
{
    [Fact]
    public void VOn_StaticEvent_ResolvesHandlerKey()
    {
        var result = TransformTestHelpers.Transform("<div @click=\"handler\"></div>");

        var props = result.CodegenNode.ShouldBeOfType<VNodeCall>().Props.ShouldBeOfType<ObjectExpression>();
        var handler = props.Property("onClick");
        handler.Value.ShouldBeOfType<SimpleExpressionNode>().Content.ShouldBe("handler");
    }

    [Fact]
    public void VOn_InlineStatement_WrapsInEventArrow()
    {
        var result = TransformTestHelpers.Transform("<div @click=\"count++\"></div>");

        var props = result.CodegenNode.ShouldBeOfType<VNodeCall>().Props.ShouldBeOfType<ObjectExpression>();
        var compound = props.Property("onClick").Value.ShouldBeOfType<CompoundExpressionNode>();
        compound.Parts[0].ShouldBe("$event => (");
    }

    [Fact]
    public void VOn_SystemModifier_WrapsWithModifiers()
    {
        var result = TransformTestHelpers.Transform("<div @click.stop=\"fn\"></div>");

        var props = result.CodegenNode.ShouldBeOfType<VNodeCall>().Props.ShouldBeOfType<ObjectExpression>();
        var call = props.Property("onClick").Value.ShouldBeOfType<CallExpression>();
        call.Callee.ShouldBeOfType<RuntimeHelper>().Name.ShouldBe("withModifiers");
        call.Arguments[1].ShouldBe("[\"stop\"]");
        result.ShouldUseHelper("withModifiers");
    }

    [Fact]
    public void VOn_KeyModifierOnKeyboardEvent_WrapsWithKeys()
    {
        var result = TransformTestHelpers.Transform("<input @keyup.enter=\"fn\"/>");

        var props = result.CodegenNode.ShouldBeOfType<VNodeCall>().Props.ShouldBeOfType<ObjectExpression>();
        var call = props.Property("onKeyup").Value.ShouldBeOfType<CallExpression>();
        call.Callee.ShouldBeOfType<RuntimeHelper>().Name.ShouldBe("withKeys");
        call.Arguments[1].ShouldBe("[\"enter\"]");
    }

    [Fact]
    public void VOn_EventOptionModifier_AppendsSuffix()
    {
        var result = TransformTestHelpers.Transform("<div @click.once=\"fn\"></div>");

        var props = result.CodegenNode.ShouldBeOfType<VNodeCall>().Props.ShouldBeOfType<ObjectExpression>();
        props.Property("onClickOnce").ShouldNotBeNull();
    }

    [Fact]
    public void VOn_RightModifier_RemapsToContextmenu()
    {
        var result = TransformTestHelpers.Transform("<div @click.right=\"fn\"></div>");

        var props = result.CodegenNode.ShouldBeOfType<VNodeCall>().Props.ShouldBeOfType<ObjectExpression>();
        props.Property("onContextmenu").ShouldNotBeNull();
    }

    [Fact]
    public void VOn_NoExpressionAndNoModifiers_ReportsError()
    {
        TransformTestHelpers.Transform("<div @click></div>", out var errors);

        errors.ShouldContain(e => e.Code == CompilerErrorCode.XVOnNoExpression);
    }
}
