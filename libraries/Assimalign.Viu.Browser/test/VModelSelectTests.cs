using System;
using System.Collections.Generic;

using Shouldly;
using Xunit;

using Assimalign.Viu.Components;

namespace Assimalign.Viu.Browser.Tests;

// Pins the Browser implementation to Vue's vModelSelect behavior:
// https://github.com/vuejs/core/blob/v3.5.29/packages/runtime-dom/src/directives/vModel.ts.
public sealed class VModelSelectTests : IDisposable
{
    private readonly BrowserDirectiveTestHarness _harness = new();
    private object? _model;

    public void Dispose() => _harness.Dispose();

    [Fact]
    public void SingleSelect_ReflectsModelOntoSelectedOption()
    {
        _model = "b";

        _harness.Render(
            Select(
                multiple: false,
                number: false,
                ("a", "A"),
                ("b", "B")));
        _harness.RunUntilIdle();

        IReadOnlyList<int> options = _harness.FindElements("option");
        _harness.Selected(options[0]).ShouldBeFalse();
        _harness.Selected(options[1]).ShouldBeTrue();
    }

    [Fact]
    public void SingleSelect_ChangeAssignsSelectedValue()
    {
        _model = "a";
        _harness.Render(
            Select(
                multiple: false,
                number: false,
                ("a", "A"),
                ("b", "B")));
        _harness.RunUntilIdle();

        _harness.FireSelectChange(
            _harness.FindElement("select"),
            singleValue: "b");

        _model.ShouldBe("b");
    }

    [Fact]
    public void MultipleSelect_AssignsListOfSelectedValues()
    {
        _model = new List<object?> { "a" };
        _harness.Render(
            Select(
                multiple: true,
                number: false,
                ("a", "A"),
                ("b", "B"),
                ("c", "C")));
        _harness.RunUntilIdle();

        _harness.FireSelectChange(
            _harness.FindElement("select"),
            singleValue: string.Empty,
            selectedValues: ["b", "c"]);

        _model.ShouldBeOfType<List<object?>>()
            .ShouldBe(["b", "c"]);
    }

    [Fact]
    public void MultipleSelect_WithSetModel_AssignsSet()
    {
        _model = new HashSet<object?> { "a" };
        _harness.Render(
            Select(
                multiple: true,
                number: false,
                ("a", "A"),
                ("b", "B")));
        _harness.RunUntilIdle();

        _harness.FireSelectChange(
            _harness.FindElement("select"),
            singleValue: string.Empty,
            selectedValues: ["a", "b"]);

        HashSet<object?> assigned =
            _model.ShouldBeOfType<HashSet<object?>>();
        assigned.ShouldBe(
            new HashSet<object?> { "a", "b" },
            ignoreOrder: true);
    }

    [Fact]
    public void MultipleSelect_ReflectsModelMembershipOntoOptions()
    {
        _model = new List<object?> { "a", "c" };
        _harness.Render(
            Select(
                multiple: true,
                number: false,
                ("a", "A"),
                ("b", "B"),
                ("c", "C")));
        _harness.RunUntilIdle();

        IReadOnlyList<int> options = _harness.FindElements("option");
        _harness.Selected(options[0]).ShouldBeTrue();
        _harness.Selected(options[1]).ShouldBeFalse();
        _harness.Selected(options[2]).ShouldBeTrue();
    }

    [Fact]
    public void NumberModifier_CoercesSelectedValue()
    {
        _model = "1";
        _harness.Render(
            Select(
                multiple: false,
                number: true,
                ("1", "One"),
                ("2", "Two")));
        _harness.RunUntilIdle();

        _harness.FireSelectChange(
            _harness.FindElement("select"),
            singleValue: "2");

        _model.ShouldBe(2d);
    }

    [Fact]
    public void ObjectOptionValues_RoundTripByIdentity()
    {
        Fruit apple = new("apple");
        Fruit banana = new("banana");
        _model = apple;
        _harness.Render(
            Select(
                multiple: false,
                number: false,
                (apple, "Apple"),
                (banana, "Banana")));
        _harness.RunUntilIdle();

        _harness.FireSelectChange(
            _harness.FindElement("select"),
            singleValue: "banana");

        _model.ShouldBeSameAs(banana);
    }

    [Fact]
    public void NestedAndFragmentOptions_AreResolvedInDocumentOrder()
    {
        _model = "b";
        IElementComponent select =
            ComponentTree.Element(
                "select",
                children:
                [
                    ComponentTree.Element(
                        "optgroup",
                        children:
                        [
                            Option("a", "A"),
                            ComponentTree.Fragment(
                                [Option("b", "B")]),
                        ]),
                ],
                directives: [ModelDirective(number: false)]);

        _harness.Render(select);
        _harness.RunUntilIdle();

        IReadOnlyList<int> options = _harness.FindElements("option");
        options.Count.ShouldBe(2);
        _harness.Selected(options[0]).ShouldBeFalse();
        _harness.Selected(options[1]).ShouldBeTrue();
    }

    [Fact]
    public void ChildTemplateOptions_AreResolvedToMountedHosts()
    {
        _model = "b";
        OptionTemplate template = new();
        IComponentFactory factory =
            new ComponentFactory(
            [
                new ComponentRegistration(
                    typeof(OptionTemplate),
                    () => template),
            ]);
        using BrowserDirectiveTestHarness harness = new(factory);
        IElementComponent select =
            ComponentTree.Element(
                "select",
                children: [ComponentTree.Template<OptionTemplate>()],
                directives: [ModelDirective(number: false)]);

        harness.Render(select);
        harness.RunUntilIdle();

        IReadOnlyList<int> options = harness.FindElements("option");
        options.Count.ShouldBe(2);
        harness.Selected(options[0]).ShouldBeFalse();
        harness.Selected(options[1]).ShouldBeTrue();
    }

    [Fact]
    public void OptionWithoutValue_UsesItsText()
    {
        _model = "Second";
        IElementComponent select =
            ComponentTree.Element(
                "select",
                children:
                [
                    OptionWithoutValue("First"),
                    OptionWithoutValue("Second"),
                ],
                directives: [ModelDirective(number: false)]);

        _harness.Render(select);
        _harness.RunUntilIdle();

        IReadOnlyList<int> options = _harness.FindElements("option");
        _harness.Selected(options[1]).ShouldBeTrue();
        _harness.FireSelectChange(
            _harness.FindElement("select"),
            singleValue: "First");
        _model.ShouldBe("First");
    }

    [Fact]
    public void ProgrammaticModelChange_UpdatesSelectionOnRerender()
    {
        _model = "a";
        _harness.Render(
            Select(
                multiple: false,
                number: false,
                ("a", "A"),
                ("b", "B")));
        _harness.RunUntilIdle();
        IReadOnlyList<int> options = _harness.FindElements("option");

        _model = "b";
        _harness.Render(
            Select(
                multiple: false,
                number: false,
                ("a", "A"),
                ("b", "B")));
        _harness.RunUntilIdle();

        _harness.Selected(options[0]).ShouldBeFalse();
        _harness.Selected(options[1]).ShouldBeTrue();
    }

    private IElementComponent Select(
        bool multiple,
        bool number,
        params (object? Value, string Text)[] options)
    {
        List<IComponentAttribute> attributes = [];
        if (multiple)
        {
            attributes.Add(new ComponentAttribute("multiple", true));
        }

        IComponent[] optionComponents =
            new IComponent[options.Length];
        for (int index = 0; index < options.Length; index++)
        {
            optionComponents[index] =
                Option(options[index].Value, options[index].Text);
        }

        return ComponentTree.Element(
            "select",
            new ComponentAttributes(attributes),
            optionComponents,
            directives: [ModelDirective(number)]);
    }

    private ComponentDirectiveBinding ModelDirective(bool number)
    {
        IReadOnlyDictionary<string, bool>? modifiers =
            number
                ? new Dictionary<string, bool>(StringComparer.Ordinal)
                {
                    ["number"] = true,
                }
                : null;
        return new ComponentDirectiveBinding(
            "modelSelect",
            new ViuModelBinding(
                _model,
                value => _model = value),
            modifiers: modifiers);
    }

    private static IElementComponent Option(
        object? value,
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

    private static IElementComponent OptionWithoutValue(string text)
    {
        return ComponentTree.Element(
            "option",
            children: [ComponentTree.Text(text)]);
    }

    private sealed record Fruit(string Name)
    {
        public override string ToString() => Name;
    }

    private sealed class OptionTemplate : IComponentTemplate
    {
        public ComponentRenderer Setup(IComponentContext context)
        {
            ArgumentNullException.ThrowIfNull(context);
            return static () =>
                ComponentTree.Fragment(
                [
                    Option("a", "A"),
                    Option("b", "B"),
                ]);
        }
    }
}
