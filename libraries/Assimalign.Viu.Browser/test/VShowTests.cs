using System;
using System.Collections.Generic;

using Shouldly;
using Xunit;

using Assimalign.Viu.Components;

namespace Assimalign.Viu.Browser.Tests;

// Pins the Browser implementation to Vue's vShow behavior:
// https://github.com/vuejs/core/blob/main/packages/runtime-dom/src/directives/vShow.ts.
public sealed class VShowTests : IDisposable
{
    private readonly BrowserDirectiveTestHarness _harness = new();
    private object? _condition = true;

    public void Dispose() => _harness.Dispose();

    [Fact]
    public void InitiallyFalsy_IsHiddenBeforeRenderReturns()
    {
        _condition = false;

        _harness.Render(Shown());

        _harness.Display(
            _harness.FindElement("div")).ShouldBe("none");
    }

    [Fact]
    public void Toggling_HidesAndReveals()
    {
        _condition = true;
        _harness.Render(Shown());
        int element = _harness.FindElement("div");
        _harness.Display(element).ShouldBeNull();

        _condition = false;
        _harness.Render(Shown());
        _harness.Display(element).ShouldBe("none");

        _condition = true;
        _harness.Render(Shown());
        _harness.Display(element).ShouldBeNull();
    }

    [Fact]
    public void AuthorSuppliedInlineDisplay_SurvivesRepeatedToggles()
    {
        _condition = true;
        _harness.Render(Shown(inlineDisplay: "flex"));
        int element = _harness.FindElement("div");
        _harness.Display(element).ShouldBe("flex");

        _condition = false;
        _harness.Render(Shown(inlineDisplay: "flex"));
        _harness.Display(element).ShouldBe("none");

        _condition = true;
        _harness.Render(Shown(inlineDisplay: "flex"));
        _harness.Display(element).ShouldBe("flex");

        _condition = false;
        _harness.Render(Shown(inlineDisplay: "flex"));
        _condition = true;
        _harness.Render(Shown(inlineDisplay: "flex"));
        _harness.Display(element).ShouldBe("flex");
    }

    [Fact]
    public void TruthinessUnchangedUpdate_DoesNotTouchDisplay()
    {
        _condition = "first";
        _harness.Render(Shown());
        int element = _harness.FindElement("div");

        _condition = "second";
        _harness.Render(Shown());

        _harness.Display(element).ShouldBeNull();
    }

    [Fact]
    public void InlineDisplayChange_WhileHidden_RemainsHiddenAndBecomesNewRestoreValue()
    {
        _condition = false;
        _harness.Render(Shown(inlineDisplay: "flex"));
        int element = _harness.FindElement("div");
        _harness.Display(element).ShouldBe("none");

        _harness.Render(Shown(inlineDisplay: "grid"));

        _harness.Display(element).ShouldBe("none");

        _condition = true;
        _harness.Render(Shown(inlineDisplay: "grid"));
        _harness.Display(element).ShouldBe("grid");
    }

    [Fact]
    public void JavaScriptFalsyValues_Hide()
    {
        _condition = 0;
        _harness.Render(Shown());
        int element = _harness.FindElement("div");
        _harness.Display(element).ShouldBe("none");

        _condition = "0";
        _harness.Render(Shown());

        _harness.Display(element).ShouldBeNull();
    }

    private IElementComponent Shown(string? inlineDisplay = null)
    {
        IComponentAttributeCollection? attributes = null;
        if (inlineDisplay is not null)
        {
            attributes = new ComponentAttributes(
            [
                new ComponentAttribute(
                    "style",
                    new Dictionary<string, object?>(
                        StringComparer.Ordinal)
                    {
                        ["display"] = inlineDisplay,
                    }),
            ]);
        }

        return ComponentTree.Element(
            "div",
            attributes,
            children: [ComponentTree.Text("content")],
            directives:
            [
                new ComponentDirectiveBinding("show", _condition),
            ]);
    }
}
