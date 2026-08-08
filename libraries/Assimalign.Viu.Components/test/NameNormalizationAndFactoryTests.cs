using System;

using Shouldly;
using Xunit;

using Assimalign.Viu.Components;

namespace Assimalign.Viu.Components.Tests;

public sealed class NameNormalizationAndFactoryTests
{
    [Theory]
    [InlineData("my-widget", "myWidget")]
    [InlineData("alreadyCamel", "alreadyCamel")]
    [InlineData("", "")]
    [InlineData("two--words", "twoWords")]
    public void Camelize_Input_UsesOrdinalHyphenRemoval(string input, string expected)
    {
        NameNormalization.Camelize(input).ShouldBe(expected);
    }

    [Theory]
    [InlineData("my-widget", "MyWidget")]
    [InlineData("myWidget", "MyWidget")]
    [InlineData("MyWidget", "MyWidget")]
    [InlineData("ǅuro", "Ǆuro")]
    [InlineData("", "")]
    public void Pascalize_Input_CamelizesThenCapitalizes(string input, string expected)
    {
        NameNormalization.Pascalize(input).ShouldBe(expected);
    }

    [Theory]
    [InlineData("modelValue", "model-value")]
    [InlineData("WebkitTransition", "webkit-transition")]
    [InlineData("already-hyphenated", "already-hyphenated")]
    [InlineData("", "")]
    public void Hyphenate_Input_UsesInvariantUppercaseBoundaries(string input, string expected)
    {
        NameNormalization.Hyphenate(input).ShouldBe(expected);
    }

    [Fact]
    public void TryResolve_HyphenatedRequest_UsesExactThenCamelThenPascalLadder()
    {
        ComponentFactory factory = new();
        ComponentRegistration raw = Registration("raw-widget");
        ComponentRegistration camel = Registration("camelWidget");
        ComponentRegistration pascal = Registration("PascalWidget");
        factory.Register(raw);
        factory.Register(camel);
        factory.Register(pascal);

        factory.Resolve(ComponentReference.ForName("raw-widget")).ShouldBeSameAs(raw);
        factory.Resolve(ComponentReference.ForName("camel-widget")).ShouldBeSameAs(camel);
        factory.Resolve(ComponentReference.ForName("pascal-widget")).ShouldBeSameAs(pascal);
    }

    [Fact]
    public void Resolve_AliasEquivalentRegistrations_PrefersExactName()
    {
        ComponentFactory factory = new();
        ComponentRegistration raw = Registration("my-widget");
        ComponentRegistration camel = Registration("myWidget");
        ComponentRegistration pascal = Registration("MyWidget");
        factory.Register(raw);
        factory.Register(camel);
        factory.Register(pascal);

        factory.Resolve(ComponentReference.ForName("my-widget")).ShouldBeSameAs(raw);
        factory.Resolve(ComponentReference.ForName("myWidget")).ShouldBeSameAs(camel);
        factory.Resolve(ComponentReference.ForName("MyWidget")).ShouldBeSameAs(pascal);
    }

    [Fact]
    public void Register_DuplicateReference_ThrowsArgumentException()
    {
        ComponentFactory factory = new();
        factory.Register(Registration("duplicate"));

        Should.Throw<ArgumentException>(() => factory.Register(Registration("duplicate")));
    }

    [Fact]
    public void Resolve_UnregisteredReference_ThrowsInvalidOperationException()
    {
        ComponentFactory factory = new();

        Should.Throw<InvalidOperationException>(
            () => factory.Resolve(ComponentReference.ForName("missing")));
    }

    [Fact]
    public void TryResolve_NameNormalizesToEmpty_ReturnsFalseWithoutThrowing()
    {
        ComponentFactory factory = new();
        ComponentReference reference = ComponentReference.ForName("-");

        factory.TryResolve(reference, out ComponentRegistration? registration).ShouldBeFalse();
        registration.ShouldBeNull();
        Should.Throw<InvalidOperationException>(() => factory.Resolve(reference));
    }

    private static ComponentRegistration Registration(string name) =>
        ComponentRegistration.Define(
            name,
            new ComponentContract(),
            _ => _ => new CommentNode(name));
}
