using System;

using Shouldly;

using Xunit;

namespace Assimalign.Viu.Compiler.SingleFileComponent.Tests;

/// <summary>
/// Pins the value-equality contract of <c>EquatableArray{T}</c> directly. The incremental generator's
/// pipeline cache and the language service's per-content caching both key on these semantics, and the
/// service side has no Roslyn driver to catch an equality flip — so the contract is pinned here, at
/// the library boundary, per the [V01.01.06.11] conformance plan.
/// </summary>
public class EquatableArrayTests
{
    [Fact]
    public void Equals_SameElementsInOrder_AreEqualWithEqualHashCodes()
    {
        var left = new EquatableArray<int>([1, 2, 3]);
        var right = new EquatableArray<int>([1, 2, 3]);

        left.Equals(right).ShouldBeTrue();
        left.GetHashCode().ShouldBe(right.GetHashCode());
    }

    [Fact]
    public void Equals_DifferentOrderOrLength_AreNotEqual()
    {
        var ordered = new EquatableArray<int>([1, 2, 3]);

        ordered.Equals(new EquatableArray<int>([3, 2, 1])).ShouldBeFalse();
        ordered.Equals(new EquatableArray<int>([1, 2])).ShouldBeFalse();
    }

    [Fact]
    public void Empty_DefaultAndEmptySingleton_AreEqual()
    {
        // A defaulted struct field (null backing array) and the Empty singleton must be one value:
        // model records containing either must cache-hit each other.
        var defaulted = default(EquatableArray<int>);

        defaulted.Equals(EquatableArray<int>.Empty).ShouldBeTrue();
        EquatableArray<int>.Empty.Equals(defaulted).ShouldBeTrue();
        defaulted.GetHashCode().ShouldBe(EquatableArray<int>.Empty.GetHashCode());
        defaulted.Count.ShouldBe(0);
    }

    [Fact]
    public void DescribeMembers_EqualScriptContent_ProducesEqualArrays()
    {
        // The end-to-end pin the caches rely on: describing the same script text twice yields
        // value-equal arrays of value-equal records across independent parses.
        const string script =
            "    public int Count;\n" +
            "    public void Increment() => Count++;\n";

        var first = ScriptBlockAnalyzer.DescribeMembers(script);
        var second = ScriptBlockAnalyzer.DescribeMembers(script);

        first.Equals(second).ShouldBeTrue();
        first.GetHashCode().ShouldBe(second.GetHashCode());
    }
}
