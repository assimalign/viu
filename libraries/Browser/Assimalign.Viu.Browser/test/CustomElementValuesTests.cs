using System;

using Shouldly;
using Xunit;

using Assimalign.Viu.Components;

namespace Assimalign.Viu.Browser.Tests;

// Pins the reflection-free name and value conversions at the custom-element host boundary.
// Specified by [CEL-1], [CEL-3], and [CEL-4] ([V01.01.04.08]).
public sealed class CustomElementValuesTests
{
    [Fact]
    public void CreateDefinition_GeneratedParameterTable_ProducesDefaultMetadataSnapshot()
    {
        ComponentReference component = ComponentReference.ForName("counter");
        ComponentContract contract = new(parameters:
        [
            new ComponentParameter("initialCount", parameterType: typeof(int)),
            new ComponentParameter("displayHTML", parameterType: typeof(string)),
            new ComponentParameter("payload", parameterType: typeof(object)),
        ]);
        CustomElementOptions options = new() { UseShadowRoot = false };

        CustomElementDefinition definition = CustomElementDefinition.Create(
            component,
            contract,
            options);
        options.UseShadowRoot = true;
        options.AttributeNameMapper = static _ => "changed";

        definition.Component.ShouldBeSameAs(component);
        definition.Contract.ShouldBeSameAs(contract);
        definition.UseShadowRoot.ShouldBeFalse();
        definition.ParameterNames.ShouldBe(["initialCount", "displayHTML", "payload"]);
        definition.AttributeNames.ShouldBe(["initial-count", "display-html", ""]);
        definition.Parameters.Keys.ShouldBe(["initialCount", "displayHTML", "payload"]);
    }

    [Fact]
    public void CreateDefinition_CustomMapper_MapsOnlyReflectableParametersAndAllowsEmptyName()
    {
        int invocationCount = 0;
        ComponentContract contract = new(parameters:
        [
            new ComponentParameter("count", parameterType: typeof(int)),
            new ComponentParameter("label", parameterType: typeof(string)),
            new ComponentParameter("payload", parameterType: typeof(object)),
        ]);
        CustomElementOptions options = new()
        {
            AttributeNameMapper = name =>
            {
                invocationCount++;
                return name == "label" ? string.Empty : $"data-{name}";
            },
        };

        CustomElementDefinition definition = CustomElementDefinition.Create(
            ComponentReference.ForName("counter"),
            contract,
            options);

        invocationCount.ShouldBe(2);
        definition.AttributeNames.ShouldBe(["data-count", "", ""]);
    }

    [Theory]
    [InlineData("uppercase", "Count")]
    [InlineData("space", "data count")]
    [InlineData("slash", "data/count")]
    public void CreateDefinition_InvalidMappedAttribute_Throws(
        string parameterName,
        string attributeName)
    {
        ComponentContract contract = new(parameters:
        [
            new ComponentParameter(parameterName, parameterType: typeof(string)),
        ]);
        CustomElementOptions options = new()
        {
            AttributeNameMapper = _ => attributeName,
        };

        ArgumentException exception = Should.Throw<ArgumentException>(() =>
            CustomElementDefinition.Create(
                ComponentReference.ForName("counter"),
                contract,
                options));

        exception.ParamName.ShouldBe("options");
    }

    [Fact]
    public void CreateDefinition_NullMappedAttribute_Throws()
    {
        ComponentContract contract = new(parameters:
        [
            new ComponentParameter("count", parameterType: typeof(int)),
        ]);
        CustomElementOptions options = new()
        {
            AttributeNameMapper = _ => null!,
        };

        ArgumentException exception = Should.Throw<ArgumentException>(() =>
            CustomElementDefinition.Create(
                ComponentReference.ForName("counter"),
                contract,
                options));

        exception.ParamName.ShouldBe("options");
        exception.Message.ShouldContain("returned null");
    }

    [Fact]
    public void CreateDefinition_CollidingMappedAttributes_Throws()
    {
        ComponentContract contract = new(parameters:
        [
            new ComponentParameter("first", parameterType: typeof(string)),
            new ComponentParameter("second", parameterType: typeof(string)),
        ]);
        CustomElementOptions options = new()
        {
            AttributeNameMapper = static _ => "value",
        };

        ArgumentException exception = Should.Throw<ArgumentException>(() =>
            CustomElementDefinition.Create(
                ComponentReference.ForName("counter"),
                contract,
                options));

        exception.ParamName.ShouldBe("options");
        exception.Message.ShouldContain("duplicate");
    }

    [Fact]
    public void CreateDefinition_DuplicateCanonicalParameter_Throws()
    {
        ComponentContract contract = new(parameters:
        [
            new ComponentParameter("value", parameterType: typeof(string)),
            new ComponentParameter("value", parameterType: typeof(int)),
        ]);

        ArgumentException exception = Should.Throw<ArgumentException>(() =>
            CustomElementDefinition.Create(
                ComponentReference.ForName("counter"),
                contract,
                options: null));

        exception.ParamName.ShouldBe("component");
        exception.Message.ShouldContain("Duplicate custom-element parameter");
    }

    [Theory]
    [InlineData("initialCount", "initial-count")]
    [InlineData("HTMLLabel", "html-label")]
    [InlineData("version2Value", "version2-value")]
    [InlineData("AlreadyNamed", "already-named")]
    [InlineData("already-named", "already-named")]
    public void Hyphenate_ParameterName_UsesLowercaseWordBoundaries(
        string name,
        string expected)
    {
        CustomElementValues.Hyphenate(name).ShouldBe(expected);
    }

    [Theory]
    [InlineData("viu-counter")]
    [InlineData("x-1")]
    [InlineData("a-b.c_d")]
    [InlineData("a-é")]
    [InlineData("a-𐀀")]
    public void ValidateTagName_PotentialCustomElementName_AcceptsName(string tagName)
    {
        Should.NotThrow(() => CustomElementValues.ValidateTagName(tagName));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("counter")]
    [InlineData("Viu-counter")]
    [InlineData("-counter")]
    [InlineData("1-counter")]
    [InlineData("viu counter")]
    [InlineData("viu/counter")]
    [InlineData("annotation-xml")]
    [InlineData("color-profile")]
    [InlineData("font-face")]
    [InlineData("font-face-src")]
    [InlineData("font-face-uri")]
    [InlineData("font-face-format")]
    [InlineData("font-face-name")]
    [InlineData("missing-glyph")]
    public void ValidateTagName_NonconformingOrReservedName_Throws(string? tagName)
    {
        ArgumentException exception = Should.Throw<ArgumentException>(
            () => CustomElementValues.ValidateTagName(tagName!));

        exception.ParamName.ShouldBe("tagName");
    }

    [Theory]
    [InlineData(typeof(string))]
    [InlineData(typeof(bool))]
    [InlineData(typeof(bool?))]
    [InlineData(typeof(int))]
    [InlineData(typeof(int?))]
    [InlineData(typeof(Half))]
    [InlineData(typeof(decimal?))]
    public void IsReflectable_StringBooleanOrNumericType_ReturnsTrue(Type parameterType)
    {
        CustomElementValues.IsReflectable(parameterType).ShouldBeTrue();
    }

    [Theory]
    [InlineData(null)]
    [InlineData(typeof(object))]
    [InlineData(typeof(char))]
    [InlineData(typeof(DateTime))]
    public void IsReflectable_UnsupportedOrMissingType_ReturnsFalse(Type? parameterType)
    {
        CustomElementValues.IsReflectable(parameterType).ShouldBeFalse();
    }

    [Theory]
    [InlineData(null, false)]
    [InlineData("", true)]
    [InlineData("false", false)]
    [InlineData("False", true)]
    [InlineData("0", true)]
    public void TryAttribute_BooleanAttribute_OnlyLowercaseLiteralFalseNegatesPresence(
        string? text,
        bool expected)
    {
        bool accepted = CustomElementValues.TryAttribute(
            typeof(bool),
            text,
            out object? value);

        accepted.ShouldBeTrue();
        value.ShouldBe(expected);
    }

    [Fact]
    public void TryAttribute_StringAttribute_PreservesTextVerbatim()
    {
        bool accepted = CustomElementValues.TryAttribute(
            typeof(string),
            "  value  ",
            out object? value);

        accepted.ShouldBeTrue();
        value.ShouldBe("  value  ");
    }

    [Theory]
    [InlineData(typeof(string))]
    [InlineData(typeof(int))]
    [InlineData(typeof(int?))]
    public void TryAttribute_RemovedNonBooleanAttribute_ProducesNoValue(Type parameterType)
    {
        bool accepted = CustomElementValues.TryAttribute(
            parameterType,
            null,
            out object? value);

        accepted.ShouldBeFalse();
        value.ShouldBeNull();
    }

    [Fact]
    public void TryAttribute_IntegralValues_ParsesInvariantTypeAndRange()
    {
        CustomElementValues.TryAttribute(typeof(byte), "255", out object? byteValue)
            .ShouldBeTrue();
        CustomElementValues.TryAttribute(typeof(int?), "-42", out object? integerValue)
            .ShouldBeTrue();
        CustomElementValues.TryAttribute(typeof(UInt128), "18446744073709551616", out object? wideValue)
            .ShouldBeTrue();

        byteValue.ShouldBe((byte)255);
        integerValue.ShouldBe(-42);
        wideValue.ShouldBe((UInt128)ulong.MaxValue + 1);
    }

    [Fact]
    public void TryAttribute_FloatingValues_ParsesInvariantFiniteValues()
    {
        CustomElementValues.TryAttribute(typeof(double), "1.25e2", out object? doubleValue)
            .ShouldBeTrue();
        CustomElementValues.TryAttribute(typeof(float), "-3.5", out object? singleValue)
            .ShouldBeTrue();
        CustomElementValues.TryAttribute(typeof(decimal?), "0.125", out object? decimalValue)
            .ShouldBeTrue();

        doubleValue.ShouldBe(125d);
        singleValue.ShouldBe(-3.5f);
        decimalValue.ShouldBe(0.125m);
    }

    [Theory]
    [InlineData(typeof(byte), "256")]
    [InlineData(typeof(int), "1.0")]
    [InlineData(typeof(uint), "-1")]
    [InlineData(typeof(double), "NaN")]
    [InlineData(typeof(double), "Infinity")]
    [InlineData(typeof(float), "1e100")]
    [InlineData(typeof(object), "4")]
    public void TryAttribute_InvalidNumericOrUnsupportedType_RejectsValue(
        Type parameterType,
        string text)
    {
        bool accepted = CustomElementValues.TryAttribute(
            parameterType,
            text,
            out object? value);

        accepted.ShouldBeFalse();
        value.ShouldBeNull();
    }

    [Fact]
    public void TryProperty_JavaScriptNumber_NormalizesToDeclaredNumericType()
    {
        CustomElementValues.TryProperty(typeof(int), 42d, out object? integerValue)
            .ShouldBeTrue();
        CustomElementValues.TryProperty(typeof(float?), 1.5d, out object? singleValue)
            .ShouldBeTrue();
        CustomElementValues.TryProperty(typeof(decimal), 0.125d, out object? decimalValue)
            .ShouldBeTrue();

        integerValue.ShouldBe(42);
        singleValue.ShouldBe(1.5f);
        decimalValue.ShouldBe(0.125m);
    }

    [Theory]
    [InlineData(typeof(int), 1.5d)]
    [InlineData(typeof(int), 9007199254740992d)]
    [InlineData(typeof(double), double.NaN)]
    [InlineData(typeof(double), double.PositiveInfinity)]
    [InlineData(typeof(byte), 256d)]
    public void TryProperty_UnrepresentableJavaScriptNumber_RejectsValue(
        Type parameterType,
        double input)
    {
        bool accepted = CustomElementValues.TryProperty(
            parameterType,
            input,
            out object? value);

        accepted.ShouldBeFalse();
        value.ShouldBeNull();
    }

    [Fact]
    public void TryProperty_StringAndBoolean_RequireMatchingTypedValue()
    {
        CustomElementValues.TryProperty(typeof(string), "3", out object? text)
            .ShouldBeTrue();
        CustomElementValues.TryProperty(typeof(bool), true, out object? condition)
            .ShouldBeTrue();
        CustomElementValues.TryProperty(typeof(int), "3", out _)
            .ShouldBeFalse();
        CustomElementValues.TryProperty(typeof(string), 3d, out _)
            .ShouldBeFalse();

        text.ShouldBe("3");
        condition.ShouldBe(true);
    }

    [Theory]
    [InlineData(null, true)]
    [InlineData(typeof(object), true)]
    [InlineData(typeof(string), true)]
    [InlineData(typeof(int?), true)]
    [InlineData(typeof(bool?), true)]
    [InlineData(typeof(int), false)]
    [InlineData(typeof(bool), false)]
    public void TryProperty_Null_UsesDeclaredNullabilityPolicy(
        Type? parameterType,
        bool expected)
    {
        bool accepted = CustomElementValues.TryProperty(
            parameterType,
            null,
            out object? value);

        accepted.ShouldBe(expected);
        value.ShouldBeNull();
    }
}
