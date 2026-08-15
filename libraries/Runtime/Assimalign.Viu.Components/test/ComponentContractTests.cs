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

    [Fact]
    public void RenderCacheSize_ProviderChanges_ReturnsCurrentCompiledSize()
    {
        // [V01.01.06.14]/[SFC-CG-9] The contract retains one stable delegate while an accepted
        // metadata update replaces only its method body.
        int currentSize = 1;
        int providerRuns = 0;
        ComponentContract contract = new(displayName: null, renderCacheSizeProvider: () =>
        {
            providerRuns++;
            return currentSize;
        });

        int first = contract.RenderCacheSize;
        currentSize = 3;
        int second = contract.RenderCacheSize;

        first.ShouldBe(1);
        second.ShouldBe(3);
        providerRuns.ShouldBe(2);
    }

    [Fact]
    public void RenderCacheSize_ProviderReturnsNegative_ThrowsArgumentOutOfRangeException()
    {
        ComponentContract contract = new(
            displayName: null,
            renderCacheSizeProvider: static () => -1);

        Should.Throw<ArgumentOutOfRangeException>(() => _ = contract.RenderCacheSize);
    }

    [Fact]
    public void Constructor_NullRenderCacheSizeProvider_ThrowsArgumentNullException()
    {
        Should.Throw<ArgumentNullException>(
            () => new ComponentContract(displayName: null, renderCacheSizeProvider: null!));
    }
}
