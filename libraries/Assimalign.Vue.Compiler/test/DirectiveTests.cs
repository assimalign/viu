using System.Linq;

using Shouldly;

using Xunit;

namespace Assimalign.Vue.Compiler;

// Ported from vuejs/core packages/compiler-core/__tests__/parse.spec.ts, the directive cases in
// describe('Element').
public class DirectiveTests
{
    [Fact]
    public void Parse_DirectiveWithNoValue_HasNoExpression()
    {
        var directive = SingleDirective("<div v-if></div>");

        directive.Name.ShouldBe("if");
        directive.RawName.ShouldBe("v-if");
        directive.Expression.ShouldBeNull();
        directive.Argument.ShouldBeNull();
        directive.Modifiers.Count.ShouldBe(0);
    }

    [Fact]
    public void Parse_DirectiveWithValue_HasExpression()
    {
        var directive = SingleDirective("<div v-if=\"ok\"></div>");

        directive.Name.ShouldBe("if");
        var expression = directive.Expression.ShouldBeOfType<SimpleExpressionNode>();
        expression.Content.ShouldBe("ok");
        expression.IsStatic.ShouldBeFalse();
        expression.Location.Source.ShouldBe("ok");
    }

    [Fact]
    public void Parse_DirectiveWithStaticArgument_HasStaticArg()
    {
        var root = TestHelpers.Parse("<div v-bind:id=\"x\"></div>");
        var directive = SingleDirective(root);

        directive.Name.ShouldBe("bind");
        var argument = directive.Argument.ShouldBeOfType<SimpleExpressionNode>();
        argument.Content.ShouldBe("id");
        argument.IsStatic.ShouldBeTrue();
        argument.ConstantType.ShouldBe(ConstantType.CanStringify);
        directive.Expression.ShouldBeOfType<SimpleExpressionNode>().Content.ShouldBe("x");
        TestHelpers.AssertAllLocationsExact(root);
    }

    [Fact]
    public void Parse_DirectiveWithDynamicArgument_HasDynamicArg()
    {
        var directive = SingleDirective("<div v-bind:[key]=\"x\"></div>");

        var argument = directive.Argument.ShouldBeOfType<SimpleExpressionNode>();
        argument.Content.ShouldBe("key");
        argument.IsStatic.ShouldBeFalse();
        argument.ConstantType.ShouldBe(ConstantType.NotConstant);
    }

    [Fact]
    public void Parse_DirectiveWithModifier_CollectsModifier()
    {
        var directive = SingleDirective("<div v-on:click.stop=\"x\"></div>");

        directive.Name.ShouldBe("on");
        directive.Argument.ShouldBeOfType<SimpleExpressionNode>().Content.ShouldBe("click");
        directive.Modifiers.ShouldHaveSingleItem().Content.ShouldBe("stop");
        directive.Expression.ShouldBeOfType<SimpleExpressionNode>().Content.ShouldBe("x");
    }

    [Fact]
    public void Parse_DirectiveWithTwoModifiers_CollectsBoth()
    {
        var directive = SingleDirective("<div v-on:click.stop.prevent=\"x\"></div>");

        directive.Modifiers.Select(m => m.Content).ShouldBe(new[] { "stop", "prevent" });
    }

    [Fact]
    public void Parse_VBindShorthand_ResolvesToBind()
    {
        var root = TestHelpers.Parse("<div :id=\"x\"></div>");
        var directive = SingleDirective(root);

        directive.Name.ShouldBe("bind");
        directive.RawName.ShouldBe(":id");
        directive.Argument.ShouldBeOfType<SimpleExpressionNode>().Content.ShouldBe("id");
        TestHelpers.AssertAllLocationsExact(root);
    }

    [Fact]
    public void Parse_VBindPropShorthand_AddsPropModifier()
    {
        var directive = SingleDirective("<div .id=\"x\"></div>");

        directive.Name.ShouldBe("bind");
        directive.Argument.ShouldBeOfType<SimpleExpressionNode>().Content.ShouldBe("id");
        directive.Modifiers.ShouldHaveSingleItem().Content.ShouldBe("prop");
    }

    [Fact]
    public void Parse_VOnShorthand_ResolvesToOn()
    {
        var directive = SingleDirective("<div @click=\"x\"></div>");

        directive.Name.ShouldBe("on");
        directive.RawName.ShouldBe("@click");
        directive.Argument.ShouldBeOfType<SimpleExpressionNode>().Content.ShouldBe("click");
    }

    [Fact]
    public void Parse_VSlotShorthand_ResolvesToSlot()
    {
        var directive = SingleDirective("<template #header></template>");

        directive.Name.ShouldBe("slot");
        directive.RawName.ShouldBe("#header");
        directive.Argument.ShouldBeOfType<SimpleExpressionNode>().Content.ShouldBe("header");
    }

    [Fact]
    public void Parse_VSlotArgumentWithDots_FoldsIntoArgument()
    {
        // parse.spec.ts 'v-slot arg containing dots': slot has no modifiers, so ".bar.baz" folds in.
        var directive = SingleDirective("<template #foo.bar.baz></template>");

        directive.Name.ShouldBe("slot");
        directive.Argument.ShouldBeOfType<SimpleExpressionNode>().Content.ShouldBe("foo.bar.baz");
    }

    [Fact]
    public void Parse_ShorthandSlotWithNoArgument_HasUndefinedArgument()
    {
        // parse.spec.ts 'arg should be undefined on shorthand dirs with no arg'
        var directive = SingleDirective("<template #></template>");

        directive.Name.ShouldBe("slot");
        directive.Argument.ShouldBeNull();
        directive.Expression.ShouldBeNull();
    }

    [Fact]
    public void Parse_DirectiveWithNoName_ReportsErrorAndTreatsAsAttribute()
    {
        // parse.spec.ts 'directive with no name'
        var root = TestHelpers.Parse("<div v-></div>", out var errors);

        errors.ShouldContain(e => e.Code == CompilerErrorCode.XMissingDirectiveName);
        var property = root.Children.ShouldHaveSingleItem().ShouldBeOfType<ElementNode>()
            .Properties.ShouldHaveSingleItem();
        property.ShouldBeOfType<AttributeNode>().Name.ShouldBe("v-");
    }

    [Fact]
    public void Parse_DynamicArgumentMissingEnd_ReportsError()
    {
        var errors = TestHelpers.Errors("<div v-bind:[key></div>");

        errors.ShouldContain(e => e.Code == CompilerErrorCode.XMissingDynamicDirectiveArgumentEnd);
    }

    [Fact]
    public void Parse_VPre_ConvertsInnerDirectivesToAttributes()
    {
        // parse.spec.ts 'v-pre': directives on and after v-pre become plain attributes.
        var root = TestHelpers.Parse("<div v-pre :id=\"foo\"><Comp v-if=\"x\"/></div>");

        var element = root.Children.ShouldHaveSingleItem().ShouldBeOfType<ElementNode>();
        // v-pre itself is dropped; :id is kept as a raw attribute.
        var attribute = element.Properties.ShouldHaveSingleItem().ShouldBeOfType<AttributeNode>();
        attribute.Name.ShouldBe(":id");
        attribute.Value!.Content.ShouldBe("foo");

        // Inside v-pre, <Comp> is not treated as a component and v-if is a raw attribute.
        var child = element.Children.ShouldHaveSingleItem().ShouldBeOfType<ElementNode>();
        child.ElementType.ShouldBe(ElementType.Element);
        child.Properties.ShouldHaveSingleItem().ShouldBeOfType<AttributeNode>().Name.ShouldBe("v-if");
    }

    private static DirectiveNode SingleDirective(string source) => SingleDirective(TestHelpers.Parse(source));

    private static DirectiveNode SingleDirective(RootNode root)
        => root.Children.ShouldHaveSingleItem().ShouldBeOfType<ElementNode>()
            .Properties.ShouldHaveSingleItem().ShouldBeOfType<DirectiveNode>();
}
