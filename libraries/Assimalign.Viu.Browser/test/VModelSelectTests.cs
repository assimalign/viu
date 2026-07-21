using System;
using System.Collections.Generic;

using Shouldly;
using Xunit;

using Assimalign.Viu;

namespace Assimalign.Viu.Browser.Tests;

// Pins VModelSelect against @vue/runtime-dom's vModelSelect
// (https://github.com/vuejs/core/blob/main/packages/runtime-dom/src/directives/vModel.ts,
// https://vuejs.org/guide/essentials/forms.html#select): single/multiple, list/set, .number, and
// object option values, through the DOM-free directive pipeline. The real DOM selectedOptions read
// is exercised by the e2e harness ([V01.01.11.03]); here the selection rides the event payload.
public sealed class VModelSelectTests : IDisposable
{
    private readonly BrowserDirectiveTestHarness _harness = new();
    private readonly Reference<object?> _model = Reactive.Reference<object?>(null);

    public void Dispose() => _harness.Dispose();

    private RenderComponent Select(bool multiple, bool number, params (object Value, string Text)[] options)
        => new((_, _) => () =>
        {
            var selectProperties = new VirtualNodeProperties();
            if (multiple)
            {
                selectProperties.Set("multiple", true);
            }
            var optionNodes = new VirtualNode?[options.Length];
            for (var index = 0; index < options.Length; index++)
            {
                var optionProperties = new VirtualNodeProperties();
                optionProperties.Set("value", options[index].Value);
                optionNodes[index] = VirtualNodeFactory.Element("option", optionProperties, options[index].Text);
            }
            var modifiers = number
                ? new Dictionary<string, bool>(StringComparer.Ordinal) { ["number"] = true }
                : null;
            return Directives.WithDirectives(
                VirtualNodeFactory.Element("select", selectProperties, optionNodes),
                VModelSelect.Instance,
                new ViuModelBinding(_model.Value, value => _model.Value = value),
                argument: null,
                modifiers);
        });

    [Fact]
    public void SingleSelect_ReflectsTheModelOntoTheSelectedOption()
    {
        _model.Value = "b";
        _harness.Render(Select(multiple: false, number: false, ("a", "A"), ("b", "B")));
        _harness.RunUntilIdle();
        var options = _harness.FindElements("option");

        _harness.Selected(options[0]).ShouldBeFalse();
        _harness.Selected(options[1]).ShouldBeTrue();
    }

    [Fact]
    public void SingleSelect_ChangeAssignsTheSelectedValue()
    {
        _model.Value = "a";
        _harness.Render(Select(multiple: false, number: false, ("a", "A"), ("b", "B")));
        _harness.RunUntilIdle();

        _harness.FireSelectChange(_harness.FindElement("select"), singleValue: "b");

        _model.Value.ShouldBe("b");
    }

    [Fact]
    public void MultipleSelect_AssignsAListOfSelectedValues()
    {
        _model.Value = new List<object?> { "a" };
        _harness.Render(Select(multiple: true, number: false, ("a", "A"), ("b", "B"), ("c", "C")));
        _harness.RunUntilIdle();

        _harness.FireSelectChange(_harness.FindElement("select"), singleValue: string.Empty, selectedValues: ["b", "c"]);

        ((List<object?>)_model.Value!).ShouldBe(["b", "c"]);
    }

    [Fact]
    public void MultipleSelect_WithSetModel_AssignsASet()
    {
        _model.Value = new HashSet<object?> { "a" };
        _harness.Render(Select(multiple: true, number: false, ("a", "A"), ("b", "B")));
        _harness.RunUntilIdle();

        _harness.FireSelectChange(_harness.FindElement("select"), singleValue: string.Empty, selectedValues: ["a", "b"]);

        _model.Value.ShouldBeOfType<HashSet<object?>>();
        ((HashSet<object?>)_model.Value!).ShouldBe(new HashSet<object?> { "a", "b" }, ignoreOrder: true);
    }

    [Fact]
    public void MultipleSelect_ReflectsModelMembershipOntoOptions()
    {
        _model.Value = new List<object?> { "a", "c" };
        _harness.Render(Select(multiple: true, number: false, ("a", "A"), ("b", "B"), ("c", "C")));
        _harness.RunUntilIdle();
        var options = _harness.FindElements("option");

        _harness.Selected(options[0]).ShouldBeTrue();
        _harness.Selected(options[1]).ShouldBeFalse();
        _harness.Selected(options[2]).ShouldBeTrue();
    }

    [Fact]
    public void NumberModifier_CoercesSelectedValues()
    {
        _model.Value = "1";
        _harness.Render(Select(multiple: false, number: true, ("1", "One"), ("2", "Two")));
        _harness.RunUntilIdle();

        _harness.FireSelectChange(_harness.FindElement("select"), singleValue: "2");

        _model.Value.ShouldBe(2d);
    }

    [Fact]
    public void ObjectOptionValues_RoundTripByIdentity()
    {
        var apple = new Fruit("apple");
        var banana = new Fruit("banana");
        _model.Value = apple;
        _harness.Render(Select(multiple: false, number: false, (apple, "Apple"), (banana, "Banana")));
        _harness.RunUntilIdle();

        // The dispatched selection string maps back to the raw object via the option-value snapshot.
        _harness.FireSelectChange(_harness.FindElement("select"), singleValue: "banana");

        _model.Value.ShouldBeSameAs(banana);
    }

    private sealed record Fruit(string Name)
    {
        public override string ToString() => Name;
    }
}
