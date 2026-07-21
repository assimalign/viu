using System;

using Shouldly;
using Xunit;

using Assimalign.Viu;

namespace Assimalign.Viu.RuntimeDom.Tests;

// Pins VModelRadio against @vue/runtime-dom's vModelRadio
// (https://github.com/vuejs/core/blob/main/packages/runtime-dom/src/directives/vModel.ts,
// https://vuejs.org/guide/essentials/forms.html#radio): loose-equality checked state and object
// value round-trip, through the DOM-free directive pipeline.
public sealed class VModelRadioTests : IDisposable
{
    private readonly BrowserDirectiveTestHarness _harness = new();
    private readonly Reference<object?> _model = Reactive.Reference<object?>(null);

    public void Dispose() => _harness.Dispose();

    private RenderComponent RadioGroup(object valueA, object valueB)
        => new((_, _) => () =>
        {
            VirtualNode Radio(object value)
            {
                var properties = new VirtualNodeProperties();
                properties.Set("type", "radio");
                properties.Set("value", value);
                return Directives.WithDirectives(
                    VirtualNodeFactory.Element("input", properties),
                    VModelRadio.Instance,
                    new ViuModelBinding(_model.Value, next => _model.Value = next));
            }

            return VirtualNodeFactory.Element("div", (VirtualNodeProperties?)null, Radio(valueA), Radio(valueB));
        });

    [Fact]
    public void ChecksTheRadioMatchingTheModel()
    {
        _model.Value = "a";
        _harness.Render(RadioGroup("a", "b"));
        _harness.RunUntilIdle();
        var radios = _harness.FindElements("input");

        _harness.Checked(radios[0]).ShouldBeTrue();
        _harness.Checked(radios[1]).ShouldBeFalse();
    }

    [Fact]
    public void ChangeAssignsTheRadiosValueToTheModel()
    {
        _model.Value = "a";
        _harness.Render(RadioGroup("a", "b"));
        _harness.RunUntilIdle();
        var radios = _harness.FindElements("input");

        _harness.FireChange(radios[1], "b");

        _model.Value.ShouldBe("b");
    }

    [Fact]
    public void ObjectValues_RoundTripByIdentity()
    {
        var first = new object();
        var second = new object();
        _model.Value = first;
        _harness.Render(RadioGroup(first, second));
        _harness.RunUntilIdle();
        var radios = _harness.FindElements("input");
        _harness.Checked(radios[0]).ShouldBeTrue(); // identity match, no string coercion

        _harness.FireChange(radios[1], string.Empty);

        _model.Value.ShouldBeSameAs(second); // the raw object round-trips
    }

    [Fact]
    public void UsesLooseEquality_ForTheCheckedComparison()
    {
        // Vue's looseEqual: numeric 1 loosely equals string "1".
        _model.Value = "1";
        _harness.Render(RadioGroup(1, 2));
        _harness.RunUntilIdle();

        _harness.Checked(_harness.FindElements("input")[0]).ShouldBeTrue();
    }
}
