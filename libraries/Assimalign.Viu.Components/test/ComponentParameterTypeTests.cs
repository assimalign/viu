using System;
using System.Collections.Generic;

using Shouldly;
using Xunit;

using Assimalign.Viu.Components;

namespace Assimalign.Viu.Components.Tests;

/// <summary>
/// Pins <see cref="IComponentParameter.ParameterType"/>: the declared value type a declaration carries
/// so a consumer's template can be checked at build time ([SFC-USE-1]). It is descriptive only — Core
/// never converts a supplied argument to it — and it is default-implemented, so the addition cannot
/// break an existing implementor.
/// </summary>
public sealed class ComponentParameterTypeTests
{
    [Fact]
    public void ComponentParameter_CarriesTheDeclaredType_AndDefaultsToNull()
    {
        new ComponentParameter("rating", parameterType: typeof(int)).ParameterType.ShouldBe(typeof(int));
        new ComponentParameter("title").ParameterType.ShouldBeNull();
    }

    [Fact]
    public void ComponentParameter_ExistingPositionalDeclarations_KeepTheirMeaning()
    {
        // The type argument was appended last, so every declaration written before it compiles and means
        // exactly what it meant: the validator stays the fourth argument, not the type.
        Func<object?, bool> validator = static value => value is string;
        ComponentParameter parameter = new(
            "title",
            isRequired: true,
            defaultFactory: static () => "Untitled",
            validator: validator);

        parameter.IsRequired.ShouldBeTrue();
        parameter.DefaultFactory!().ShouldBe("Untitled");
        parameter.Validator.ShouldBeSameAs(validator);
        parameter.ParameterType.ShouldBeNull();
    }

    [Fact]
    public void CustomImplementor_WithoutTheMember_ReportsNoDeclaredType()
    {
        // The member is default-implemented, so an implementor written before it exists still compiles
        // and simply declares no type — the "no information" answer the checker treats as silence.
        IComponentParameter parameter = new LegacyParameter();

        parameter.ParameterType.ShouldBeNull();
        parameter.Name.ShouldBe("title");
    }

    private sealed class LegacyParameter : IComponentParameter
    {
        public string Name => "title";

        public bool IsRequired => false;

        public Func<object?>? DefaultFactory => null;

        public Func<object?, bool>? Validator => null;
    }
}
