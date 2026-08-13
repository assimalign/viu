using System.Linq;

using Shouldly;

using Xunit;

namespace Assimalign.Viu.Syntax.Templates;

// These cases ARE the contract for the runtime model
// directive selected by element and input type, and the component modelValue/onUpdate pair.
public class VModelTransformTests
{
    [Theory]
    [InlineData("<input v-model=\"text\"/>", "vModelText")]
    [InlineData("<input type=\"checkbox\" v-model=\"c\"/>", "vModelCheckbox")]
    [InlineData("<input type=\"radio\" v-model=\"r\"/>", "vModelRadio")]
    [InlineData("<select v-model=\"s\"></select>", "vModelSelect")]
    [InlineData("<textarea v-model=\"t\"></textarea>", "vModelText")]
    [InlineData("<input :type=\"dyn\" v-model=\"d\"/>", "vModelDynamic")]
    public void VModel_NativeElement_SelectsRuntimeDirectiveByType(string template, string expectedHelper)
    {
        var result = TransformTestHelpers.Transform(template);

        result.ShouldUseHelper(expectedHelper);
        result.CodegenNode.ShouldBeOfType<VirtualNodeCall>().Directives.ShouldNotBeNull();
    }

    [Fact]
    public void VModel_NativeElement_EmitsUpdateHandlerButNotModelValueProp()
    {
        var result = TransformTestHelpers.Transform("<input v-model=\"text\"/>");

        var props = result.CodegenNode.ShouldBeOfType<VirtualNodeCall>().Properties.ShouldBeOfType<ObjectExpression>();
        props.ObjectProperty("onUpdate:modelValue").ShouldNotBeNull();
        // native v-model does not carry the modelValue prop; it is passed as binding.value.
        props.Properties.Any(p => p.Key is SimpleExpressionNode { Content: "modelValue" }).ShouldBeFalse();
    }

    [Fact]
    public void VModel_Component_EmitsModelValueAndUpdatePair()
    {
        var result = TransformTestHelpers.Transform("<MyInput v-model=\"value\"></MyInput>");

        var props = result.CodegenNode.ShouldBeOfType<VirtualNodeCall>().Properties.ShouldBeOfType<ObjectExpression>();
        props.ObjectProperty("modelValue").ShouldNotBeNull();
        props.ObjectProperty("onUpdate:modelValue").ShouldNotBeNull();
    }

    [Fact]
    public void VModel_ComponentWithArgumentAndModifiers_EmitsNamedModelAndModifiers()
    {
        var result = TransformTestHelpers.Transform("<MyInput v-model:title.trim=\"value\"></MyInput>");

        var props = result.CodegenNode.ShouldBeOfType<VirtualNodeCall>().Properties.ShouldBeOfType<ObjectExpression>();
        props.ObjectProperty("title").ShouldNotBeNull();
        props.ObjectProperty("onUpdate:title").ShouldNotBeNull();
        props.ObjectProperty("titleModifiers").ShouldNotBeNull();
    }

    [Fact]
    public void VModel_OnInvalidElement_ReportsError()
    {
        TransformTestHelpers.Transform("<div v-model=\"x\"></div>", out var errors);

        errors.ShouldContain(e => e.Code == CompilerErrorCode.XVModelOnInvalidElement);
    }

    [Fact]
    public void VModel_OnFileInput_ReportsError()
    {
        TransformTestHelpers.Transform("<input type=\"file\" v-model=\"f\"/>", out var errors);

        errors.ShouldContain(e => e.Code == CompilerErrorCode.XVModelOnFileInputElement);
    }

    [Fact]
    public void VModel_MalformedExpression_ReportsError()
    {
        TransformTestHelpers.Transform("<input v-model=\"a + b\"/>", out var errors);

        errors.ShouldContain(e => e.Code == CompilerErrorCode.XVModelMalformedExpression);
    }
}
