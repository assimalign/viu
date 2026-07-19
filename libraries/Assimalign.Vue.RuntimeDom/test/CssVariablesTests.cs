using System;
using System.Collections.Generic;

using Shouldly;
using Xunit;

using Assimalign.Vue.Reactivity;
using Assimalign.Vue.RuntimeCore;

namespace Assimalign.Vue.RuntimeDom.Tests;

/// <summary>
/// Pins the <c>UseCssVars</c> runtime (<see cref="CssVariables"/>, [V01.01.06.06]) — the C# port of Vue
/// 3.5's <c>useCssVars</c> (<c>@vue/runtime-dom</c> <c>helpers/useCssVars.ts</c>,
/// https://vuejs.org/api/sfc-css-features.html#v-bind-in-css). Exercised DOM-free through the in-memory
/// adapter (<see cref="BrowserDirectiveTestHarness"/>): the evaluated <c>v-bind()</c> values are applied as
/// custom properties on the component root after mount, re-applied reactively on the next post-flush pass
/// without re-rendering, batched into one interop crossing per element per pass, and torn down on unmount.
/// </summary>
public sealed class CssVariablesTests : IDisposable
{
    private readonly BrowserDirectiveTestHarness _harness = new();

    public void Dispose() => _harness.Dispose();

    [Fact]
    public void UseCssVars_AppliesCustomProperty_OnRootElement_AfterMount()
    {
        var color = Reactive.Reference("red");
        _harness.Render(Component(() => new Dictionary<string, string> { ["abc12345"] = color.Value }));
        _harness.RunUntilIdle();

        var div = _harness.FindElement("div");
        // Applied with the leading '--' (upstream setVarsOnNode: style.setProperty('--' + key, value)).
        _harness.CssVariable(div, "--abc12345").ShouldBe("red");
        // One crossing on mount — the initial application (upstream's watch source runs once in onMounted).
        _harness.CssVariableCrossings.ShouldBe(1);
    }

    [Fact]
    public void UseCssVars_UpdatesReactively_OnNextFlush_WithoutReRendering()
    {
        var color = Reactive.Reference("red");
        var renderCount = 0;
        _harness.Render(Component(
            () => new Dictionary<string, string> { ["abc12345"] = color.Value },
            onRender: () => renderCount++));
        _harness.RunUntilIdle();

        renderCount.ShouldBe(1);
        _harness.CssVariableCrossings.ShouldBe(1);

        // A bound value changes: the property updates on the next flush and the crossing count ticks by one,
        // but the render function does not re-run — the AC's "without re-rendering the component".
        color.Value = "blue";
        _harness.RunUntilIdle();

        var div = _harness.FindElement("div");
        _harness.CssVariable(div, "--abc12345").ShouldBe("blue");
        _harness.CssVariableCrossings.ShouldBe(2);
        renderCount.ShouldBe(1);
    }

    [Fact]
    public void UseCssVars_DoesNotReapply_WhenNoBoundValueChanged()
    {
        var color = Reactive.Reference("red");
        var unrelated = Reactive.Reference(0);
        _harness.Render(Component(() => new Dictionary<string, string> { ["abc12345"] = color.Value }));
        _harness.RunUntilIdle();
        _harness.CssVariableCrossings.ShouldBe(1);

        // Mutating state the getter never reads triggers no post-flush re-application (dependency tracking,
        // not a blanket post-flush hook) — the run-count guard the testing rules require.
        unrelated.Value = 5;
        _harness.RunUntilIdle();

        _harness.CssVariableCrossings.ShouldBe(1);
    }

    [Fact]
    public void UseCssVars_BatchesEveryProperty_IntoOneCrossingPerPass()
    {
        var color = Reactive.Reference("red");
        var size = Reactive.Reference("10px");
        _harness.Render(Component(() => new Dictionary<string, string>
        {
            ["c"] = color.Value,
            ["s"] = size.Value,
        }));
        _harness.RunUntilIdle();

        var div = _harness.FindElement("div");
        _harness.CssVariable(div, "--c").ShouldBe("red");
        _harness.CssVariable(div, "--s").ShouldBe("10px");
        // Both properties applied in a single crossing — never one interop call per property (the batching AC).
        _harness.CssVariableCrossings.ShouldBe(1);
    }

    [Fact]
    public void UseCssVars_AppliesToEveryRoot_OfAFragmentComponent()
    {
        var color = Reactive.Reference("red");
        var component = new RenderComponent((_, _) =>
        {
            CssVariables.UseCssVars(() => new Dictionary<string, string> { ["v"] = color.Value });
            return () => VirtualNodeFactory.Fragment(
                VirtualNodeFactory.Element("div", "a"),
                VirtualNodeFactory.Element("span", "b"));
        });

        _harness.Render(component);
        _harness.RunUntilIdle();

        // A multi-root component applies the vars to each root element (upstream setVarsOnVNode descends the
        // Fragment) — one crossing per root element.
        _harness.CssVariable(_harness.FindElement("div"), "--v").ShouldBe("red");
        _harness.CssVariable(_harness.FindElement("span"), "--v").ShouldBe("red");
        _harness.CssVariableCrossings.ShouldBe(2);
    }

    [Fact]
    public void UseCssVars_StopsApplying_AfterUnmount()
    {
        var color = Reactive.Reference("red");
        _harness.Render(Component(() => new Dictionary<string, string> { ["abc12345"] = color.Value }));
        _harness.RunUntilIdle();
        _harness.CssVariableCrossings.ShouldBe(1);

        _harness.Unmount();
        _harness.RunUntilIdle();
        var afterUnmount = _harness.CssVariableCrossings;

        // The watcher stops with the component (onUnmounted -> handle.Stop), so a later mutation applies nothing.
        color.Value = "blue";
        _harness.RunUntilIdle();

        _harness.CssVariableCrossings.ShouldBe(afterUnmount);
    }

    [Fact]
    public void UseCssVars_WithoutActiveInstance_IsNoOp()
    {
        // Called outside a component Setup there is no subtree to apply to; upstream warns and returns.
        Should.NotThrow(() => CssVariables.UseCssVars(() => new Dictionary<string, string> { ["x"] = "1" }));
    }

    [Fact]
    public void UseCssVars_NullGetter_Throws()
        => Should.Throw<ArgumentNullException>(() => CssVariables.UseCssVars(null!));

    // A component whose setup registers UseCssVars and whose root is a single <div>, mirroring the shape the
    // generator's ApplyCssVariables seam produces.
    private static RenderComponent Component(Func<IReadOnlyDictionary<string, string>> getter, Action? onRender = null)
        => new((_, _) =>
        {
            CssVariables.UseCssVars(getter);
            return () =>
            {
                onRender?.Invoke();
                return VirtualNodeFactory.Element("div", "content");
            };
        });
}
