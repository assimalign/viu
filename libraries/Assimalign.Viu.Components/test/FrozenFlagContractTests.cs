using Shouldly;
using Xunit;

using Assimalign.Viu.Components;

namespace Assimalign.Viu.Components.Tests;

public sealed class FrozenFlagContractTests
{
    [Fact]
    public void PatchFlags_Values_PreserveFrozenCompilerRuntimeLayout()
    {
        ((int)PatchFlags.None).ShouldBe(0);
        ((int)PatchFlags.Text).ShouldBe(1);
        ((int)PatchFlags.Class).ShouldBe(2);
        ((int)PatchFlags.Style).ShouldBe(4);
        ((int)PatchFlags.Properties).ShouldBe(8);
        ((int)PatchFlags.FullProperties).ShouldBe(16);
        ((int)PatchFlags.NeedsHydration).ShouldBe(32);
        ((int)PatchFlags.StableFragment).ShouldBe(64);
        ((int)PatchFlags.KeyedFragment).ShouldBe(128);
        ((int)PatchFlags.UnkeyedFragment).ShouldBe(256);
        ((int)PatchFlags.NeedPatch).ShouldBe(512);
        ((int)PatchFlags.DynamicSlots).ShouldBe(1024);
        ((int)PatchFlags.DevelopmentRootFragment).ShouldBe(2048);
        ((int)PatchFlags.Cached).ShouldBe(-1);
        ((int)PatchFlags.Bail).ShouldBe(-2);
    }

    [Fact]
    public void ShapeFlags_Values_PreserveFrozenCompilerRuntimeLayout()
    {
        ((int)ShapeFlags.Element).ShouldBe(1);
        ((int)ShapeFlags.FunctionalComponent).ShouldBe(2);
        ((int)ShapeFlags.StatefulComponent).ShouldBe(4);
        ((int)ShapeFlags.TextChildren).ShouldBe(8);
        ((int)ShapeFlags.ArrayChildren).ShouldBe(16);
        ((int)ShapeFlags.SlotsChildren).ShouldBe(32);
        ((int)ShapeFlags.Teleport).ShouldBe(64);
        ((int)ShapeFlags.Suspense).ShouldBe(128);
        ((int)ShapeFlags.ComponentShouldKeepAlive).ShouldBe(256);
        ((int)ShapeFlags.ComponentKeptAlive).ShouldBe(512);
        ((int)ShapeFlags.Component).ShouldBe(6);
    }

    [Fact]
    public void SlotStability_Values_PreservePlainEnumerationLayout()
    {
        ((int)SlotStability.Stable).ShouldBe(1);
        ((int)SlotStability.Dynamic).ShouldBe(2);
        ((int)SlotStability.Forwarded).ShouldBe(3);
    }
}
