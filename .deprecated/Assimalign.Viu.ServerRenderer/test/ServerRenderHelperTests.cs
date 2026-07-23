using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

using Shouldly;

using Xunit;

using Assimalign.Viu;

namespace Assimalign.Viu.ServerRenderer.Tests;

/// <summary>
/// Exercises the push-based helpers the compiler-generated <c>ssrRender</c> bodies ([V01.01.07.02])
/// will call, driving each directly over a render state — the same surface the runtime walker uses.
/// </summary>
public class ServerRenderHelperTests
{
    private static (SsrRenderState State, SsrWriter Writer) NewState()
    {
        var writer = new SsrWriter();
        return (new SsrRenderState(writer, new SsrContext(), CancellationToken.None), writer);
    }

    [Fact]
    public async Task SsrRenderListAsync_Enumerable_YieldsItemAndIndex()
    {
        var (state, writer) = NewState();
        await ServerRender.SsrRenderListAsync(
            new List<string> { "a", "b" },
            (value, key) =>
            {
                state.Push($"[{key}:{value}]");
                return Task.CompletedTask;
            });
        writer.ToStringResult().ShouldBe("[0:a][1:b]");
    }

    [Fact]
    public async Task SsrRenderListAsync_IntegerCount_IsOneBased()
    {
        var (state, writer) = NewState();
        await ServerRender.SsrRenderListAsync(3, (value, _) =>
        {
            state.Push(value!.ToString()!);
            return Task.CompletedTask;
        });
        writer.ToStringResult().ShouldBe("123");
    }

    [Fact]
    public async Task SsrRenderListAsync_Dictionary_YieldsValueAndKey()
    {
        var (state, writer) = NewState();
        await ServerRender.SsrRenderListAsync(
            new Dictionary<string, int> { ["x"] = 1 },
            (value, key) =>
            {
                state.Push($"{key}={value}");
                return Task.CompletedTask;
            });
        writer.ToStringResult().ShouldBe("x=1");
    }

    [Fact]
    public async Task SsrRenderComponentAsync_RendersChild()
    {
        var (state, writer) = NewState();
        var child = new InlineComponent((_, _) => () => VirtualNodeFactory.Element("span", "x"));
        await ServerRender.SsrRenderComponentAsync(state, child);
        writer.ToStringResult().ShouldBe("<span>x</span>");
    }

    [Fact]
    public async Task SsrRenderSlotAsync_WrapsContentInFragmentAnchors()
    {
        var (state, writer) = NewState();
        var slots = new ComponentSlots();
        slots["default"] = _ => [VirtualNodeFactory.Element("b", "hi")];
        await ServerRender.SsrRenderSlotAsync(state, slots, "default");
        writer.ToStringResult().ShouldBe("<!--[--><b>hi</b><!--]-->");
    }

    [Fact]
    public async Task SsrRenderSuspenseAsync_RendersDefaultBranchAwaitingAsyncDependencies()
    {
        var (state, writer) = NewState();
        await ServerRender.SsrRenderSuspenseAsync(state, async branchState =>
        {
            branchState.Push("<main>");
            await Task.Yield();
            branchState.Push("</main>");
        });
        writer.ToStringResult().ShouldBe("<main></main>");
    }

    [Fact]
    public async Task SsrRenderTeleportAsync_BuffersContentByTarget()
    {
        var (state, writer) = NewState();
        await ServerRender.SsrRenderTeleportAsync(
            state,
            contentState =>
            {
                contentState.Push("<p>hi</p>");
                return Task.CompletedTask;
            },
            "#modal",
            disabled: false);
        // Buffers resolve into Context.Teleports at the end of a render; do that step explicitly here since
        // this drives the helper in isolation.
        state.Context.ResolveTeleports();

        writer.ToStringResult().ShouldBe("<!--teleport start--><!--teleport end-->");
        state.Context.Teleports["#modal"].ShouldBe("<p>hi</p><!--teleport anchor-->");
    }
}
