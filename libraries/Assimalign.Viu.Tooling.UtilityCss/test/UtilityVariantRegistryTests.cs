using System;
using System.Linq;

using Shouldly;
using Xunit;

namespace Assimalign.Viu.Tooling.UtilityCss.Tests;

public sealed class UtilityVariantRegistryTests
{
    [Theory]
    [InlineData("hover", UtilityVariantKind.Static, UtilityVariantCategory.State)]
    [InlineData("before", UtilityVariantKind.Static, UtilityVariantCategory.PseudoElement)]
    [InlineData("nth-last-of-type", UtilityVariantKind.Functional, UtilityVariantCategory.Structural)]
    [InlineData("group", UtilityVariantKind.Compound, UtilityVariantCategory.Compound)]
    [InlineData("peer", UtilityVariantKind.Compound, UtilityVariantCategory.Compound)]
    [InlineData("in", UtilityVariantKind.Compound, UtilityVariantCategory.Compound)]
    [InlineData("has", UtilityVariantKind.Compound, UtilityVariantCategory.Compound)]
    [InlineData("not", UtilityVariantKind.Compound, UtilityVariantCategory.Compound)]
    [InlineData("aria", UtilityVariantKind.Functional, UtilityVariantCategory.Attribute)]
    [InlineData("data", UtilityVariantKind.Functional, UtilityVariantCategory.Attribute)]
    [InlineData("supports", UtilityVariantKind.Functional, UtilityVariantCategory.Supports)]
    [InlineData("min", UtilityVariantKind.Functional, UtilityVariantCategory.Responsive)]
    [InlineData("max", UtilityVariantKind.Functional, UtilityVariantCategory.Responsive)]
    [InlineData("@", UtilityVariantKind.Functional, UtilityVariantCategory.ContainerQuery)]
    [InlineData("@min", UtilityVariantKind.Functional, UtilityVariantCategory.ContainerQuery)]
    [InlineData("@max", UtilityVariantKind.Functional, UtilityVariantCategory.ContainerQuery)]
    [InlineData("*", UtilityVariantKind.Static, UtilityVariantCategory.Child)]
    [InlineData("**", UtilityVariantKind.Static, UtilityVariantCategory.Descendant)]
    public void BuiltIn_RegisteredRoot_ExposesExpectedGrammarAndCategory(
        string name,
        UtilityVariantKind expectedKind,
        UtilityVariantCategory expectedCategory)
    {
        UtilityVariantRegistry.BuiltIn.TryGetDefinition(name, out var definition)
            .ShouldBeTrue();

        definition.ShouldNotBeNull();
        definition.Kind.ShouldBe(expectedKind);
        definition.Category.ShouldBe(expectedCategory);
    }

    [Fact]
    public void BuiltIn_Definitions_AreUniqueAndOrdinallyOrdered()
    {
        var definitions = UtilityVariantRegistry.BuiltIn.Definitions;

        definitions.Select(definition => definition.Name).Distinct(StringComparer.Ordinal).Count()
            .ShouldBe(definitions.Count);
        definitions.Select(definition => definition.Name)
            .ShouldBe(
                definitions
                    .Select(definition => definition.Name)
                    .OrderBy(name => name, StringComparer.Ordinal));
    }

    [Fact]
    public void Registry_EquivalentDefinitions_UsesStructuralEqualityAndHashing()
    {
        var definitions = UtilityVariantRegistry.BuiltIn.Definitions.ToArray();
        var first = new UtilityVariantRegistry(definitions);
        var second = new UtilityVariantRegistry(definitions.Reverse());

        first.ShouldBe(second);
        first.GetHashCode().ShouldBe(second.GetHashCode());
    }
}
