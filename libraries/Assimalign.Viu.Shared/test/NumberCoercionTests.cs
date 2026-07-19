using Shouldly;
using Xunit;

namespace Assimalign.Viu.Shared.Tests;

// Pins Vue's numeric coercion (looseToNumber/toNumber in @vue/shared general.ts) — the semantics
// behind v-model's .number modifier (https://vuejs.org/guide/essentials/forms.html#lazy).
public class NumberCoercionTests
{
    [Theory]
    [InlineData("12", 12d)]
    [InlineData("12.5", 12.5d)]
    [InlineData("-0.5", -0.5d)]
    [InlineData("+7", 7d)]
    [InlineData(".5", 0.5d)]
    [InlineData("5.", 5d)]
    [InlineData("1e3", 1000d)]
    [InlineData("2.5E2", 250d)]
    [InlineData("  3.14  ", 3.14d)] // parseFloat skips leading whitespace, ignores the trailing tail
    [InlineData("12abc", 12d)]      // longest numeric prefix wins
    [InlineData("3.14.15", 3.14d)]  // stops at the second decimal point
    [InlineData("0x10", 0d)]        // parseFloat('0x10') === 0 (stops before 'x')
    [InlineData("1e", 1d)]          // dangling exponent is rolled back
    public void LooseToNumber_ParsesTheLeadingNumericPrefix(string input, double expected)
        => NumberCoercion.LooseToNumber(input).ShouldBe(expected);

    [Theory]
    [InlineData("abc")]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("e5")]
    [InlineData("+")]
    [InlineData(".")]
    public void LooseToNumber_LeavesNonNumericInputUntouched(string input)
        // Upstream: isNaN(parseFloat(val)) ? val : n — the original string is returned unchanged.
        => NumberCoercion.LooseToNumber(input).ShouldBe(input);

    [Fact]
    public void LooseToNumber_SupportsInfinity()
    {
        NumberCoercion.LooseToNumber("Infinity").ShouldBe(double.PositiveInfinity);
        NumberCoercion.LooseToNumber("-Infinity").ShouldBe(double.NegativeInfinity);
    }

    [Fact]
    public void LooseToNumber_PassesNonStringsThrough()
    {
        NumberCoercion.LooseToNumber(null).ShouldBeNull();
        var marker = new object();
        NumberCoercion.LooseToNumber(marker).ShouldBeSameAs(marker);
    }

    [Theory]
    [InlineData("42", 42d)]
    [InlineData("  42  ", 42d)]
    [InlineData("3.5", 3.5d)]
    public void ToNumber_ParsesWhollyNumericStrings(string input, double expected)
        => NumberCoercion.ToNumber(input).ShouldBe(expected);

    [Fact]
    public void ToNumber_EmptyStringIsZero()
        // Number('') === 0 in JavaScript, unlike parseFloat('') which is NaN.
        => NumberCoercion.ToNumber("").ShouldBe(0d);

    [Theory]
    [InlineData("12abc")] // Number('12abc') is NaN — the whole string must parse, unlike looseToNumber
    [InlineData("abc")]
    public void ToNumber_LeavesPartialOrNonNumericInputUntouched(string input)
        => NumberCoercion.ToNumber(input).ShouldBe(input);
}
