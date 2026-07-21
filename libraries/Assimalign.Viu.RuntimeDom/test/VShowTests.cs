using System;
using System.Collections.Generic;

using Shouldly;
using Xunit;

using Assimalign.Viu;

namespace Assimalign.Viu.RuntimeDom.Tests;

// Pins VShow against @vue/runtime-dom's vShow
// (https://github.com/vuejs/core/blob/main/packages/runtime-dom/src/directives/vShow.ts,
// https://vuejs.org/guide/essentials/conditional.html#v-show): display toggling that preserves the
// original inline display, hidden-from-first-paint timing, and stylesheet-value restoration. The
// <Transition> coordination clause is a documented-inert seam until [V01.01.04.07].
public sealed class VShowTests : IDisposable
{
    private readonly BrowserDirectiveTestHarness _harness = new();
    private readonly Reference<object?> _condition = Reactive.Reference<object?>(true);

    public void Dispose() => _harness.Dispose();

    private RenderComponent Shown(string? inlineDisplay = null)
        => new((_, _) => () =>
        {
            VirtualNodeProperties? properties = null;
            if (inlineDisplay is not null)
            {
                properties = new VirtualNodeProperties();
                properties.Set("style", new Dictionary<string, object?>(StringComparer.Ordinal)
                {
                    ["display"] = inlineDisplay,
                });
            }
            return Directives.WithDirectives(
                VirtualNodeFactory.Element("div", properties, "content"),
                VShow.Instance,
                _condition.Value);
        });

    [Fact]
    public void InitiallyFalsy_IsHiddenFromTheFirstPaint()
    {
        _condition.Value = false;
        _harness.Render(Shown());

        // Asserted before RunUntilIdle: beforeMount (sync, pre-insert) already applied
        // display:none — no flash of visible content while post-flush hooks are still queued.
        _harness.Display(_harness.FindElement("div")).ShouldBe("none");
    }

    [Fact]
    public void Toggling_HidesAndReveals()
    {
        _condition.Value = true;
        _harness.Render(Shown());
        _harness.RunUntilIdle();
        var div = _harness.FindElement("div");
        _harness.Display(div).ShouldBeNull(); // no inline display while shown

        _condition.Value = false;
        _harness.RunUntilIdle();
        _harness.Display(div).ShouldBe("none");

        _condition.Value = true;
        _harness.RunUntilIdle();
        // Restoring with no author-supplied inline display removes the inline property, so a
        // stylesheet-supplied display value wins (upstream parity).
        _harness.Display(div).ShouldBeNull();
    }

    [Fact]
    public void AuthorSuppliedInlineDisplay_SurvivesTheToggle()
    {
        _condition.Value = true;
        _harness.Render(Shown(inlineDisplay: "flex"));
        _harness.RunUntilIdle();
        var div = _harness.FindElement("div");
        _harness.Display(div).ShouldBe("flex");

        _condition.Value = false;
        _harness.RunUntilIdle();
        _harness.Display(div).ShouldBe("none");

        _condition.Value = true;
        _harness.RunUntilIdle();
        _harness.Display(div).ShouldBe("flex"); // the original inline display is restored

        // Repeated toggling never loses the saved original.
        _condition.Value = false;
        _harness.RunUntilIdle();
        _condition.Value = true;
        _harness.RunUntilIdle();
        _harness.Display(div).ShouldBe("flex");
    }

    [Fact]
    public void TruthinessUnchangedUpdate_DoesNotTouchDisplay()
    {
        _condition.Value = "first";
        _harness.Render(Shown());
        _harness.RunUntilIdle();
        var div = _harness.FindElement("div");
        _harness.Display(div).ShouldBeNull();

        // truthy -> truthy: upstream's `if (!value === !oldValue) return` — nothing to toggle.
        _condition.Value = "second";
        _harness.RunUntilIdle();
        _harness.Display(div).ShouldBeNull();
    }

    [Fact]
    public void JavaScriptFalsyValues_Hide()
    {
        // JS truthiness: 0, "", null are falsy; "0" is truthy (upstream coercion semantics).
        _condition.Value = 0;
        _harness.Render(Shown());
        _harness.RunUntilIdle();
        var div = _harness.FindElement("div");
        _harness.Display(div).ShouldBe("none");

        _condition.Value = "0";
        _harness.RunUntilIdle();
        _harness.Display(div).ShouldBeNull();
    }
}
