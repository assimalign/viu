using System;

using Shouldly;
using Xunit;

using Assimalign.Viu.Components;

namespace Assimalign.Viu.Components.Tests;

// [V01.01.14.08] Runtime registries and generated helper calls share these exact, culture-invariant
// name aliases instead of carrying private implementations that can drift.
public sealed class NameNormalizationTests
{
    [Theory]
    [InlineData("my-widget", "myWidget")]
    [InlineData("-my-widget", "MyWidget")]
    [InlineData("my--widget", "myWidget")]
    [InlineData("my-widget-", "myWidget")]
    [InlineData("alreadyCamel", "alreadyCamel")]
    [InlineData("", "")]
    public void Camelize_Name_ReturnsInvariantCamelCase(string value, string expected)
    {
        NameNormalization.Camelize(value).ShouldBe(expected);
    }

    [Fact]
    public void Camelize_NameWithoutHyphen_ReturnsOriginalInstance()
    {
        string value = new(new[] { 'n', 'a', 'm', 'e' });

        NameNormalization.Camelize(value).ShouldBeSameAs(value);
    }

    [Theory]
    [InlineData("widget", "Widget")]
    [InlineData("Widget", "Widget")]
    [InlineData("", "")]
    public void Pascalize_Name_ReturnsInvariantInitialCapital(string value, string expected)
    {
        NameNormalization.Pascalize(value).ShouldBe(expected);
    }

    [Fact]
    public void Camelize_NullValue_ThrowsArgumentNullException()
    {
        Should.Throw<ArgumentNullException>(() => NameNormalization.Camelize(null!));
    }

    [Fact]
    public void Pascalize_NullValue_ThrowsArgumentNullException()
    {
        Should.Throw<ArgumentNullException>(() => NameNormalization.Pascalize(null!));
    }
}
