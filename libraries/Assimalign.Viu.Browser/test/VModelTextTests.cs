using System;

using Shouldly;
using Xunit;

using Assimalign.Viu;

namespace Assimalign.Viu.Browser.Tests;

// Pins VModelText against @vue/runtime-dom's vModelText
// (https://github.com/vuejs/core/blob/main/packages/runtime-dom/src/directives/vModel.ts,
// https://vuejs.org/guide/essentials/forms.html) through the DOM-free directive pipeline. Real IME
// composition and real focus are exercised by the e2e harness ([V01.01.11.03]); here composition and
// focus are simulated via their events, which is what the directive actually reacts to.
public sealed class VModelTextTests : IDisposable
{
    private readonly BrowserDirectiveTestHarness _harness = new();
    private readonly Reference<object?> _model = Reactive.Reference<object?>(string.Empty);
    private readonly Reference<int> _tick = Reactive.Reference(0);

    public void Dispose() => _harness.Dispose();

    // Forces an unrelated re-render of the same component without touching the model.
    private void ReRender()
    {
        _tick.Value++;
        _harness.RunUntilIdle();
    }

    private RenderComponent Input(string? type = null, params string[] modifiers)
    {
        var modifierMap = new System.Collections.Generic.Dictionary<string, bool>(StringComparer.Ordinal);
        foreach (var modifier in modifiers)
        {
            modifierMap[modifier] = true;
        }
        return new RenderComponent((_, _) => () =>
        {
            _ = _tick.Value; // establish a re-render dependency independent of the model
            var properties = new VirtualNodeProperties();
            if (type is not null)
            {
                properties.Set("type", type);
            }
            return Directives.WithDirectives(
                VirtualNodeFactory.Element("input", properties),
                VModelText.Instance,
                new ViuModelBinding(_model.Value, value => _model.Value = value),
                argument: null,
                modifierMap);
        });
    }

    [Fact]
    public void Mounted_WritesTheModelOntoTheElementValue()
    {
        _model.Value = "hello";
        _harness.Render(Input());
        _harness.RunUntilIdle();

        _harness.Value(_harness.FindElement("input")).ShouldBe("hello");
    }

    [Fact]
    public void Input_WritesTheDomValueBackToTheModel()
    {
        _harness.Render(Input());
        _harness.RunUntilIdle();
        var input = _harness.FindElement("input");

        _harness.FireInput(input, "typed");

        _model.Value.ShouldBe("typed");
    }

    [Fact]
    public void NumberModifier_CoercesNumericInput_AndLeavesNonNumericUntouched()
    {
        _harness.Render(Input(modifiers: "number"));
        _harness.RunUntilIdle();
        var input = _harness.FindElement("input");

        _harness.FireInput(input, "42");
        _model.Value.ShouldBe(42d);

        _harness.FireInput(input, "abc");
        _model.Value.ShouldBe("abc"); // non-numeric input is left untouched (looseToNumber)
    }

    [Fact]
    public void NumberType_CoercesEvenWithoutTheModifier()
    {
        _harness.Render(Input(type: "number"));
        _harness.RunUntilIdle();

        _harness.FireInput(_harness.FindElement("input"), "7");

        _model.Value.ShouldBe(7d);
    }

    [Fact]
    public void TrimModifier_TrimsTheModel_AndReSyncsTheElementOnChange()
    {
        _harness.Render(Input(modifiers: "trim"));
        _harness.RunUntilIdle();
        var input = _harness.FindElement("input");

        _harness.FireInput(input, "  spaced  ");
        _model.Value.ShouldBe("spaced"); // model is trimmed

        _harness.FireChange(input, "  spaced  ");
        _harness.Value(input).ShouldBe("spaced"); // element re-synced to the trimmed value on change
    }

    [Fact]
    public void LazyModifier_CommitsOnChange_NotOnInput()
    {
        _harness.Render(Input(modifiers: "lazy"));
        _harness.RunUntilIdle();
        var input = _harness.FindElement("input");

        _harness.FireInput(input, "ignored"); // no 'input' listener under .lazy
        _model.Value.ShouldBe(string.Empty);

        _harness.FireChange(input, "committed");
        _model.Value.ShouldBe("committed");
    }

    [Fact]
    public void Composition_SuppressesInputUpdates_UntilCompositionEnd()
    {
        _harness.Render(Input());
        _harness.RunUntilIdle();
        var input = _harness.FindElement("input");

        _harness.FireCompositionStart(input);
        _harness.FireInput(input, "partial"); // suppressed while composing (IME safety)
        _model.Value.ShouldBe(string.Empty);

        _harness.FireCompositionEnd(input, "final"); // commits the final composed value
        _model.Value.ShouldBe("final");
    }

    [Fact]
    public void FocusedElement_WhoseValueEqualsTheModel_IsNotRewrittenOnReRender()
    {
        _harness.Render(Input());
        _harness.RunUntilIdle();
        var input = _harness.FindElement("input");

        _harness.FireFocus(input);
        _harness.FireInput(input, "hello"); // model and element value now agree
        var writesBefore = _harness.ValueWriteCount(input);

        // An unrelated re-render must not clobber the focused element whose value already equals the model.
        ReRender();

        _harness.ValueWriteCount(input).ShouldBe(writesBefore); // no redundant write
        _harness.Value(input).ShouldBe("hello");
    }

    [Fact]
    public void ProgrammaticModelChange_UpdatesTheElement()
    {
        _harness.Render(Input());
        _harness.RunUntilIdle();
        var input = _harness.FindElement("input");

        _model.Value = "programmatic";
        _harness.RunUntilIdle();

        _harness.Value(input).ShouldBe("programmatic");
    }

    [Fact]
    public void Unmount_ReleasesTheDirectiveListeners()
    {
        _harness.Render(Input());
        _harness.RunUntilIdle();
        _harness.InvokerCount.ShouldBeGreaterThan(0);

        _harness.Unmount();

        _harness.InvokerCount.ShouldBe(0);
    }
}
