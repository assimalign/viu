using System;
using System.Collections.Generic;

using Shouldly;
using Xunit;

using Assimalign.Viu.Components;

namespace Assimalign.Viu.Browser.Tests;

// Pins the Browser implementation to Vue's vModelCheckbox behavior:
// https://github.com/vuejs/core/blob/main/packages/runtime-dom/src/directives/vModel.ts.
public sealed class VModelCheckboxTests : IDisposable
{
    private readonly BrowserDirectiveTestHarness _harness = new();
    private object? _model = false;

    public void Dispose() => _harness.Dispose();

    [Fact]
    public void BooleanBinding_TogglesTheModel()
    {
        _model = false;
        _harness.Render(Checkbox());
        int input = _harness.FindElement("input");
        _harness.Checked(input).ShouldBeFalse();

        _harness.FireCheckboxChange(input, isChecked: true);
        _model.ShouldBe(true);

        _harness.FireCheckboxChange(input, isChecked: false);
        _model.ShouldBe(false);
    }

    [Fact]
    public void TrueFalseValue_AssignsTheConfiguredValues()
    {
        _model = "no";
        _harness.Render(Checkbox(trueFalse: ("yes", "no")));
        int input = _harness.FindElement("input");
        _harness.Checked(input).ShouldBeFalse();

        _harness.FireCheckboxChange(input, isChecked: true);
        _model.ShouldBe("yes");

        _harness.FireCheckboxChange(input, isChecked: false);
        _model.ShouldBe("no");
    }

    [Fact]
    public void ArrayBinding_AddsAndRemovesTheElementValue()
    {
        _model = new List<object?>();
        _harness.Render(Checkbox(elementValue: "a"));
        int input = _harness.FindElement("input");

        _harness.FireCheckboxChange(input, isChecked: true);
        ((List<object?>)_model!).ShouldBe(["a"]);

        _harness.Render(Checkbox(elementValue: "a"));
        _harness.FireCheckboxChange(input, isChecked: false);
        ((List<object?>)_model!).ShouldBeEmpty();
    }

    [Fact]
    public void SetBinding_AddsAndRemovesTheElementValue()
    {
        _model = new HashSet<object?>();
        _harness.Render(Checkbox(elementValue: "x"));
        int input = _harness.FindElement("input");

        _harness.FireCheckboxChange(input, isChecked: true);
        ((HashSet<object?>)_model!).ShouldContain("x");

        _harness.Render(Checkbox(elementValue: "x"));
        _harness.FireCheckboxChange(input, isChecked: false);
        ((HashSet<object?>)_model!).ShouldNotContain("x");
    }

    [Fact]
    public void ArrayBinding_ReflectsMembershipOntoCheckedAtMount()
    {
        _model = new List<object?> { "a", "b" };

        _harness.Render(Checkbox(elementValue: "b"));

        _harness.Checked(_harness.FindElement("input")).ShouldBeTrue();
    }

    [Fact]
    public void ArrayBinding_UsesLooseEquality_ForMembership()
    {
        _model = new List<object?> { 1 };

        _harness.Render(Checkbox(elementValue: "1"));

        _harness.Checked(_harness.FindElement("input")).ShouldBeTrue();
    }

    [Fact]
    public void ProgrammaticBooleanChange_UpdatesCheckedOnRerender()
    {
        _model = false;
        _harness.Render(Checkbox());
        int input = _harness.FindElement("input");

        _model = true;
        _harness.Render(Checkbox());

        _harness.Checked(input).ShouldBeTrue();
    }

    private IElementComponent Checkbox(
        object? elementValue = null,
        (object? True, object? False)? trueFalse = null)
    {
        List<IComponentAttribute> attributes =
        [
            new ComponentAttribute("type", "checkbox"),
        ];
        if (elementValue is not null)
        {
            attributes.Add(new ComponentAttribute("value", elementValue));
        }

        if (trueFalse is { } pair)
        {
            attributes.Add(
                new ComponentAttribute("true-value", pair.True));
            attributes.Add(
                new ComponentAttribute("false-value", pair.False));
        }

        return ComponentTree.Element(
            "input",
            new ComponentAttributes(attributes),
            directives:
            [
                new ComponentDirectiveBinding(
                    "modelCheckbox",
                    new ViuModelBinding(
                        _model,
                        value => _model = value)),
            ]);
    }
}
