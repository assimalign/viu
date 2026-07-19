using System;
using System.Collections.Generic;

using Shouldly;
using Xunit;

using Assimalign.Viu.Reactivity;
using Assimalign.Viu.RuntimeCore;

namespace Assimalign.Viu.RuntimeDom.Tests;

// Pins VModelCheckbox against @vue/runtime-dom's vModelCheckbox
// (https://github.com/vuejs/core/blob/main/packages/runtime-dom/src/directives/vModel.ts,
// https://vuejs.org/guide/essentials/forms.html#checkbox): boolean, array, and set bindings with
// true-value/false-value, through the DOM-free directive pipeline.
public sealed class VModelCheckboxTests : IDisposable
{
    private readonly BrowserDirectiveTestHarness _harness = new();
    private readonly Reference<object?> _model = Reactive.Reference<object?>(false);

    public void Dispose() => _harness.Dispose();

    private RenderComponent Checkbox(object? elementValue = null, (object? True, object? False)? trueFalse = null)
        => new((_, _) => () =>
        {
            var properties = new VirtualNodeProperties();
            properties.Set("type", "checkbox");
            if (elementValue is not null)
            {
                properties.Set("value", elementValue);
            }
            if (trueFalse is { } pair)
            {
                properties.Set("true-value", pair.True);
                properties.Set("false-value", pair.False);
            }
            return Directives.WithDirectives(
                VirtualNodeFactory.Element("input", properties),
                VModelCheckbox.Instance,
                new ViuModelBinding(_model.Value, value => _model.Value = value));
        });

    [Fact]
    public void BooleanBinding_TogglesTheModel()
    {
        _model.Value = false;
        _harness.Render(Checkbox());
        _harness.RunUntilIdle();
        var input = _harness.FindElement("input");
        _harness.Checked(input).ShouldBeFalse();

        _harness.FireCheckboxChange(input, isChecked: true);
        _model.Value.ShouldBe(true);

        _harness.FireCheckboxChange(input, isChecked: false);
        _model.Value.ShouldBe(false);
    }

    [Fact]
    public void TrueFalseValue_AssignsTheConfiguredValues()
    {
        _model.Value = "no";
        _harness.Render(Checkbox(trueFalse: ("yes", "no")));
        _harness.RunUntilIdle();
        var input = _harness.FindElement("input");
        _harness.Checked(input).ShouldBeFalse(); // "no" != true-value "yes"

        _harness.FireCheckboxChange(input, isChecked: true);
        _model.Value.ShouldBe("yes");

        _harness.FireCheckboxChange(input, isChecked: false);
        _model.Value.ShouldBe("no");
    }

    [Fact]
    public void ArrayBinding_AddsAndRemovesTheElementValue()
    {
        _model.Value = new List<object?>();
        _harness.Render(Checkbox(elementValue: "a"));
        _harness.RunUntilIdle();
        var input = _harness.FindElement("input");

        _harness.FireCheckboxChange(input, isChecked: true);
        _harness.RunUntilIdle();
        ((List<object?>)_model.Value!).ShouldBe(["a"]);

        _harness.FireCheckboxChange(input, isChecked: false);
        ((List<object?>)_model.Value!).ShouldBeEmpty();
    }

    [Fact]
    public void SetBinding_AddsAndRemovesTheElementValue()
    {
        _model.Value = new HashSet<object?>();
        _harness.Render(Checkbox(elementValue: "x"));
        _harness.RunUntilIdle();
        var input = _harness.FindElement("input");

        _harness.FireCheckboxChange(input, isChecked: true);
        _harness.RunUntilIdle();
        ((HashSet<object?>)_model.Value!).ShouldContain("x");

        _harness.FireCheckboxChange(input, isChecked: false);
        ((HashSet<object?>)_model.Value!).ShouldNotContain("x");
    }

    [Fact]
    public void ArrayBinding_ReflectsMembershipOntoCheckedAtMount()
    {
        _model.Value = new List<object?> { "a", "b" };
        _harness.Render(Checkbox(elementValue: "b"));
        _harness.RunUntilIdle();

        _harness.Checked(_harness.FindElement("input")).ShouldBeTrue(); // "b" is in the bound list
    }

    [Fact]
    public void ArrayBinding_UsesLooseEquality_ForMembership()
    {
        // Vue's looseEqual: numeric 1 loosely equals string "1" for array membership.
        _model.Value = new List<object?> { 1 };
        _harness.Render(Checkbox(elementValue: "1"));
        _harness.RunUntilIdle();

        _harness.Checked(_harness.FindElement("input")).ShouldBeTrue();
    }
}
