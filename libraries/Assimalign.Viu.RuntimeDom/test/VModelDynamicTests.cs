using System;

using Shouldly;
using Xunit;

using Assimalign.Viu.Reactivity;
using Assimalign.Viu.RuntimeCore;

namespace Assimalign.Viu.RuntimeDom.Tests;

// Pins VModelDynamic against @vue/runtime-dom's vModelDynamic/resolveDynamicModel
// (https://github.com/vuejs/core/blob/main/packages/runtime-dom/src/directives/vModel.ts): each
// hook resolves the concrete directive from the element's current tag/type, so behavior switches
// at runtime when <input :type> changes.
public sealed class VModelDynamicTests : IDisposable
{
    private readonly BrowserDirectiveTestHarness _harness = new();
    private readonly Reference<object?> _model = Reactive.Reference<object?>(null);
    private readonly Reference<string> _type = Reactive.Reference("text");

    public void Dispose() => _harness.Dispose();

    private RenderComponent DynamicInput()
        => new((_, _) => () =>
        {
            var properties = new VirtualNodeProperties();
            properties.Set("type", _type.Value);
            return Directives.WithDirectives(
                VirtualNodeFactory.Element("input", properties),
                VModelDynamic.Instance,
                new ViuModelBinding(_model.Value, value => _model.Value = value));
        });

    private RenderComponent Tagged(string tag)
        => new((_, _) => () => Directives.WithDirectives(
            VirtualNodeFactory.Element(tag),
            VModelDynamic.Instance,
            new ViuModelBinding(_model.Value, value => _model.Value = value)));

    [Fact]
    public void TextType_BehavesAsVModelText()
    {
        _model.Value = "typed";
        _type.Value = "text";
        _harness.Render(DynamicInput());
        _harness.RunUntilIdle();
        var input = _harness.FindElement("input");
        _harness.Value(input).ShouldBe("typed"); // text mount reflects the value

        _harness.FireInput(input, "edited");
        _model.Value.ShouldBe("edited");
    }

    [Fact]
    public void CheckboxType_BehavesAsVModelCheckbox()
    {
        _model.Value = true;
        _type.Value = "checkbox";
        _harness.Render(DynamicInput());
        _harness.RunUntilIdle();
        var input = _harness.FindElement("input");
        _harness.Checked(input).ShouldBeTrue();

        _harness.FireCheckboxChange(input, isChecked: false);
        _model.Value.ShouldBe(false);
    }

    [Fact]
    public void RadioType_BehavesAsVModelRadio()
    {
        _model.Value = "other";
        _type.Value = "radio";
        _harness.Render(DynamicInput());
        _harness.RunUntilIdle();

        // Radio created reflects looseEqual(model, :value); no :value prop -> unchecked.
        _harness.Checked(_harness.FindElement("input")).ShouldBeFalse();
    }

    [Fact]
    public void TextareaTag_ResolvesToVModelText()
    {
        _model.Value = "long form";
        _harness.Render(Tagged("textarea"));
        _harness.RunUntilIdle();

        _harness.Value(_harness.FindElement("textarea")).ShouldBe("long form");
    }

    [Fact]
    public void RuntimeTypeSwitch_SwitchesTheResolvedBehavior()
    {
        // Upstream resolveDynamicModel runs per hook, so a type change re-routes the update pass.
        _model.Value = "hello";
        _type.Value = "text";
        _harness.Render(DynamicInput());
        _harness.RunUntilIdle();
        var input = _harness.FindElement("input");
        _harness.Value(input).ShouldBe("hello");

        _type.Value = "checkbox";
        _model.Value = true;
        _harness.RunUntilIdle();

        // The beforeUpdate pass resolved VModelCheckbox and reflected checked from the model.
        _harness.Checked(input).ShouldBeTrue();
    }
}
