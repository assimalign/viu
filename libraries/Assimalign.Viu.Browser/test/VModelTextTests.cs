using System;
using System.Collections.Generic;

using Shouldly;
using Xunit;

using Assimalign.Viu.Components;

namespace Assimalign.Viu.Browser.Tests;

// Pins the Browser implementation to Vue's vModelText behavior:
// https://github.com/vuejs/core/blob/main/packages/runtime-dom/src/directives/vModel.ts.
public sealed class VModelTextTests : IDisposable
{
    private readonly BrowserDirectiveTestHarness _harness = new();
    private object? _model = string.Empty;

    public void Dispose() => _harness.Dispose();

    [Fact]
    public void Mounted_WritesTheModelOntoTheElementValue()
    {
        _model = "hello";

        _harness.Render(Input());

        _harness.Value(_harness.FindElement("input")).ShouldBe("hello");
    }

    [Fact]
    public void Input_WritesTheDomValueBackToTheModel()
    {
        _harness.Render(Input());
        int input = _harness.FindElement("input");

        _harness.FireInput(input, "typed");

        _model.ShouldBe("typed");
    }

    [Fact]
    public void NumberModifier_CoercesNumericInput_AndLeavesNonNumericUntouched()
    {
        _harness.Render(Input(modifiers: "number"));
        int input = _harness.FindElement("input");

        _harness.FireInput(input, "42");
        _model.ShouldBe(42d);

        _harness.FireInput(input, "abc");
        _model.ShouldBe("abc");
    }

    [Fact]
    public void NumberType_CoercesEvenWithoutTheModifier()
    {
        _harness.Render(Input(type: "number"));

        _harness.FireInput(_harness.FindElement("input"), "7");

        _model.ShouldBe(7d);
    }

    [Fact]
    public void TrimModifier_TrimsTheModel_AndReSyncsTheElementOnChange()
    {
        _harness.Render(Input(modifiers: "trim"));
        int input = _harness.FindElement("input");

        _harness.FireInput(input, "  spaced  ");
        _model.ShouldBe("spaced");

        _harness.FireChange(input, "  spaced  ");
        _harness.Value(input).ShouldBe("spaced");
    }

    [Fact]
    public void LazyModifier_CommitsOnChange_NotOnInput()
    {
        _harness.Render(Input(modifiers: "lazy"));
        int input = _harness.FindElement("input");

        _harness.FireInput(input, "ignored");
        _model.ShouldBe(string.Empty);

        _harness.FireChange(input, "committed");
        _model.ShouldBe("committed");
    }

    [Fact]
    public void Composition_SuppressesInputUpdates_UntilCompositionEnd()
    {
        _harness.Render(Input());
        int input = _harness.FindElement("input");

        _harness.FireCompositionStart(input);
        _harness.FireInput(input, "partial");
        _model.ShouldBe(string.Empty);

        _harness.FireCompositionEnd(input, "final");
        _model.ShouldBe("final");
    }

    [Fact]
    public void FocusedElement_WhoseValueEqualsTheModel_IsNotRewrittenOnRerender()
    {
        _harness.Render(Input());
        int input = _harness.FindElement("input");
        _harness.FireFocus(input);
        _harness.FireInput(input, "hello");
        int writesBefore = _harness.ValueWriteCount(input);

        _harness.Render(Input());

        _harness.ValueWriteCount(input).ShouldBe(writesBefore);
        _harness.Value(input).ShouldBe("hello");
    }

    [Fact]
    public void ProgrammaticModelChange_UpdatesTheElement()
    {
        _harness.Render(Input());
        int input = _harness.FindElement("input");

        _model = "programmatic";
        _harness.Render(Input());

        _harness.Value(input).ShouldBe("programmatic");
    }

    [Fact]
    public void Unmount_ReleasesTheDirectiveListeners()
    {
        _harness.Render(Input());
        _harness.InvokerCount.ShouldBeGreaterThan(0);

        _harness.Unmount();

        _harness.InvokerCount.ShouldBe(0);
    }

    private IElementComponent Input(
        string? type = null,
        params string[] modifiers)
    {
        List<IComponentAttribute> attributes = [];
        if (type is not null)
        {
            attributes.Add(new ComponentAttribute("type", type));
        }

        Dictionary<string, bool> modifierMap =
            new(StringComparer.Ordinal);
        foreach (string modifier in modifiers)
        {
            modifierMap[modifier] = true;
        }

        return ComponentTree.Element(
            "input",
            new ComponentAttributes(attributes),
            directives:
            [
                new ComponentDirectiveBinding(
                    "modelText",
                    new ViuModelBinding(
                        _model,
                        value => _model = value),
                    modifiers: modifierMap),
            ]);
    }
}
