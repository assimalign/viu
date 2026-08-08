using Shouldly;

using Xunit;

namespace Assimalign.Viu.Syntax.Templates;

// These cases ARE the contract for event-name resolution, inline-statement wrapping,
// and key/system/event-option modifier handling.
public class VOnTransformTests
{
    [Fact]
    public void VOn_StaticEvent_ResolvesHandlerKey()
    {
        var result = TransformTestHelpers.Transform("<div @click=\"handler\"></div>");

        var props = result.CodegenNode.ShouldBeOfType<VirtualNodeCall>().Properties.ShouldBeOfType<ObjectExpression>();
        var handler = props.ObjectProperty("onClick");
        handler.Value.ShouldBeOfType<SimpleExpressionNode>().Content.ShouldBe("handler");
    }

    [Fact]
    public void VOn_InlineStatement_WrapsInEventArrow()
    {
        var result = TransformTestHelpers.Transform("<div @click=\"count++\"></div>");

        var props = result.CodegenNode.ShouldBeOfType<VirtualNodeCall>().Properties.ShouldBeOfType<ObjectExpression>();
        var compound = props.ObjectProperty("onClick").Value.ShouldBeOfType<CompoundExpressionNode>();
        compound.Parts[0].ShouldBe("$event => (");
    }

    [Fact]
    public void VOn_SystemModifier_WrapsWithModifiers()
    {
        var result = TransformTestHelpers.Transform("<div @click.stop=\"fn\"></div>");

        var props = result.CodegenNode.ShouldBeOfType<VirtualNodeCall>().Properties.ShouldBeOfType<ObjectExpression>();
        var call = props.ObjectProperty("onClick").Value.ShouldBeOfType<CallExpression>();
        call.Callee.ShouldBeOfType<RuntimeHelper>().Name.ShouldBe("withModifiers");
        call.Arguments[1].ShouldBe("[\"stop\"]");
        result.ShouldUseHelper("withModifiers");
    }

    [Fact]
    public void VOn_KeyModifierOnKeyboardEvent_WrapsWithKeys()
    {
        var result = TransformTestHelpers.Transform("<input @keyup.enter=\"fn\"/>");

        var props = result.CodegenNode.ShouldBeOfType<VirtualNodeCall>().Properties.ShouldBeOfType<ObjectExpression>();
        var call = props.ObjectProperty("onKeyup").Value.ShouldBeOfType<CallExpression>();
        call.Callee.ShouldBeOfType<RuntimeHelper>().Name.ShouldBe("withKeys");
        call.Arguments[1].ShouldBe("[\"enter\"]");
    }

    [Fact]
    public void VOn_EventOptionModifier_AppendsSuffix()
    {
        var result = TransformTestHelpers.Transform("<div @click.once=\"fn\"></div>");

        var props = result.CodegenNode.ShouldBeOfType<VirtualNodeCall>().Properties.ShouldBeOfType<ObjectExpression>();
        props.ObjectProperty("onClickOnce").ShouldNotBeNull();
    }

    [Fact]
    public void VOn_RightModifier_RemapsToContextmenu()
    {
        var result = TransformTestHelpers.Transform("<div @click.right=\"fn\"></div>");

        var props = result.CodegenNode.ShouldBeOfType<VirtualNodeCall>().Properties.ShouldBeOfType<ObjectExpression>();
        props.ObjectProperty("onContextmenu").ShouldNotBeNull();
    }

    [Fact]
    public void VOn_NoExpressionAndNoModifiers_ReportsError()
    {
        TransformTestHelpers.Transform("<div @click></div>", out var errors);

        errors.ShouldContain(e => e.Code == CompilerErrorCode.XVOnNoExpression);
    }
}
