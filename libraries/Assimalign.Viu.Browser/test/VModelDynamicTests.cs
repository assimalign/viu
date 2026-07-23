using System;
using System.Collections.Generic;

using Shouldly;
using Xunit;

using Assimalign.Viu.Components;

namespace Assimalign.Viu.Browser.Tests;

// Pins the Browser implementation to Vue's vModelDynamic/resolveDynamicModel behavior:
// https://github.com/vuejs/core/blob/v3.5.29/packages/runtime-dom/src/directives/vModel.ts.
public sealed class VModelDynamicTests : IDisposable
{
    private readonly BrowserDirectiveTestHarness _harness = new();
    private object? _model;
    private string _type = "text";

    public void Dispose() => _harness.Dispose();

    [Fact]
    public void TextType_BehavesAsVModelText()
    {
        _model = "typed";
        _type = "text";
        _harness.Render(DynamicInput());
        _harness.RunUntilIdle();
        int input = _harness.FindElement("input");

        _harness.Value(input).ShouldBe("typed");
        _harness.FireInput(input, "edited");

        _model.ShouldBe("edited");
    }

    [Fact]
    public void CheckboxType_BehavesAsVModelCheckbox()
    {
        _model = true;
        _type = "checkbox";
        _harness.Render(DynamicInput());
        _harness.RunUntilIdle();
        int input = _harness.FindElement("input");

        _harness.Checked(input).ShouldBeTrue();
        _harness.FireCheckboxChange(input, isChecked: false);

        _model.ShouldBe(false);
    }

    [Fact]
    public void RadioType_BehavesAsVModelRadio()
    {
        _model = "selected";
        _type = "radio";
        _harness.Render(DynamicInput("selected"));
        _harness.RunUntilIdle();

        _harness.Checked(_harness.FindElement("input")).ShouldBeTrue();
    }

    [Fact]
    public void TextareaTag_ResolvesToVModelText()
    {
        _model = "long form";
        _harness.Render(Tagged("textarea"));
        _harness.RunUntilIdle();

        _harness.Value(_harness.FindElement("textarea"))
            .ShouldBe("long form");
    }

    [Fact]
    public void SelectTag_ResolvesToVModelSelect()
    {
        _model = "b";
        IElementComponent select =
            ComponentTree.Element(
                "select",
                children:
                [
                    Option("a", "A"),
                    Option("b", "B"),
                ],
                directives: [ModelDirective()]);

        _harness.Render(select);
        _harness.RunUntilIdle();

        IReadOnlyList<int> options = _harness.FindElements("option");
        _harness.Selected(options[0]).ShouldBeFalse();
        _harness.Selected(options[1]).ShouldBeTrue();
    }

    [Fact]
    public void RuntimeTypeSwitch_ReroutesUpdateHook()
    {
        _model = "hello";
        _type = "text";
        _harness.Render(DynamicInput());
        _harness.RunUntilIdle();
        int input = _harness.FindElement("input");
        _harness.Value(input).ShouldBe("hello");

        _type = "checkbox";
        _model = true;
        _harness.Render(DynamicInput());
        _harness.RunUntilIdle();

        _harness.Checked(input).ShouldBeTrue();
    }

    private IElementComponent DynamicInput(object? elementValue = null)
    {
        List<IComponentAttribute> attributes =
        [
            new ComponentAttribute("type", _type),
        ];
        if (elementValue is not null)
        {
            attributes.Add(
                new ComponentAttribute("value", elementValue));
        }

        return ComponentTree.Element(
            "input",
            new ComponentAttributes(attributes),
            directives: [ModelDirective()]);
    }

    private IElementComponent Tagged(string tag)
    {
        return ComponentTree.Element(
            tag,
            directives: [ModelDirective()]);
    }

    private ComponentDirectiveBinding ModelDirective()
    {
        return new ComponentDirectiveBinding(
            "modelDynamic",
            new ViuModelBinding(
                _model,
                value => _model = value));
    }

    private static IElementComponent Option(
        object value,
        string text)
    {
        return ComponentTree.Element(
            "option",
            new ComponentAttributes(
            [
                new ComponentAttribute("value", value),
            ]),
            [ComponentTree.Text(text)]);
    }
}
