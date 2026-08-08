using System;

using Shouldly;
using Xunit;

using Assimalign.Viu.Components;

namespace Assimalign.Viu.Components.Tests;

// Pins the exact compiler render-cache size channel specified by [SFC-OPT-1].
public sealed class ComponentContractTests
{
    [Fact]
    public void Constructor_OmittedCompilerCacheSize_DefaultsPropertyToZero()
    {
        ComponentContract contract = new();

        contract.RenderCacheSize.ShouldBe(0);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(3)]
    public void Constructor_ExplicitCompilerCacheSize_PreservesExactValue(int renderCacheSize)
    {
        ComponentContract contract = new(renderCacheSize: renderCacheSize);

        contract.RenderCacheSize.ShouldBe(renderCacheSize);
    }

    [Fact]
    public void Constructor_NegativeCompilerCacheSize_ThrowsArgumentOutOfRangeException()
    {
        Should.Throw<ArgumentOutOfRangeException>(
            () => new ComponentContract(renderCacheSize: -1));
    }
}
