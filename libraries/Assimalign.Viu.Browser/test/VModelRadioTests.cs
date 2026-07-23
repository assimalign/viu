using System;
using System.Collections.Generic;

using Shouldly;
using Xunit;

using Assimalign.Viu.Components;

namespace Assimalign.Viu.Browser.Tests;

// Pins the Browser implementation to Vue's vModelRadio behavior:
// https://github.com/vuejs/core/blob/main/packages/runtime-dom/src/directives/vModel.ts.
public sealed class VModelRadioTests : IDisposable
{
    private readonly BrowserDirectiveTestHarness _harness = new();
    private object? _model;

    public void Dispose() => _harness.Dispose();

    [Fact]
    public void ChecksTheRadioMatchingTheModel()
    {
        _model = "a";

        _harness.Render(RadioGroup("a", "b"));

        IReadOnlyList<int> radios = _harness.FindElements("input");
        _harness.Checked(radios[0]).ShouldBeTrue();
        _harness.Checked(radios[1]).ShouldBeFalse();
    }

    [Fact]
    public void ChangeAssignsTheRadiosValueToTheModel()
    {
        _model = "a";
        _harness.Render(RadioGroup("a", "b"));
        IReadOnlyList<int> radios = _harness.FindElements("input");

        _harness.FireChange(radios[1], "b");

        _model.ShouldBe("b");
    }

    [Fact]
    public void ObjectValues_RoundTripByIdentity()
    {
        object first = new();
        object second = new();
        _model = first;
        _harness.Render(RadioGroup(first, second));
        IReadOnlyList<int> radios = _harness.FindElements("input");
        _harness.Checked(radios[0]).ShouldBeTrue();

        _harness.FireChange(radios[1], string.Empty);

        _model.ShouldBeSameAs(second);
    }

    [Fact]
    public void UsesLooseEquality_ForTheCheckedComparison()
    {
        _model = "1";

        _harness.Render(RadioGroup(1, 2));

        _harness.Checked(
            _harness.FindElements("input")[0]).ShouldBeTrue();
    }

    [Fact]
    public void ProgrammaticChange_UpdatesCheckedStateOnRerender()
    {
        _model = "a";
        _harness.Render(RadioGroup("a", "b"));
        IReadOnlyList<int> radios = _harness.FindElements("input");

        _model = "b";
        _harness.Render(RadioGroup("a", "b"));

        _harness.Checked(radios[0]).ShouldBeFalse();
        _harness.Checked(radios[1]).ShouldBeTrue();
    }

    private IElementComponent RadioGroup(object valueA, object valueB)
    {
        IElementComponent Radio(object value)
        {
            ComponentAttributes attributes =
                new(
                [
                    new ComponentAttribute("type", "radio"),
                    new ComponentAttribute("value", value),
                ]);
            return ComponentTree.Element(
                "input",
                attributes,
                directives:
                [
                    new ComponentDirectiveBinding(
                        "modelRadio",
                        new ViuModelBinding(
                            _model,
                            next => _model = next)),
                ]);
        }

        return ComponentTree.Element(
            "div",
            children: [Radio(valueA), Radio(valueB)]);
    }
}
