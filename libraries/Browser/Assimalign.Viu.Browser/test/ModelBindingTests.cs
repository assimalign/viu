using System;
using System.Collections.Generic;

using Shouldly;
using Xunit;

namespace Assimalign.Viu.Browser.Tests;

// Pins the reflection-free native-control model carrier specified by [SFC-CG-7].
public sealed class ModelBindingTests
{
    [Fact]
    public void Constructor_CopiesModifierNamesAndRetainsExplicitSetter()
    {
        object? assigned = null;
        List<string> modifiers = ["trim", "number"];
        var binding = new ModelBinding(
            "initial",
            value => assigned = value,
            modifiers);

        modifiers[0] = "changed";
        binding.Value.ShouldBe("initial");
        binding.Modifiers.ShouldBe(["trim", "number"]);
        binding.Setter("updated");
        assigned.ShouldBe("updated");
    }

    [Fact]
    public void Constructor_NullSetterOrEmptyModifier_Throws()
    {
        Should.Throw<ArgumentNullException>(
            () => new ModelBinding(null, null!));
        Should.Throw<ArgumentException>(
            () => new ModelBinding(null, _ => { }, [string.Empty]));
    }
}
