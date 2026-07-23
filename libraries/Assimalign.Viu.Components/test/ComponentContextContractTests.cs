using System;
using System.Collections.Generic;

using Shouldly;
using Xunit;

using Assimalign.Viu.Components;

namespace Assimalign.Viu.Components.Tests;

public sealed class ComponentContextContractTests
{
    [Fact]
    public void Context_ExposesCurrentSlotsAndFallthroughAttributes()
    {
        ComponentSlot defaultSlot = _ => ComponentTree.Text("content");
        Dictionary<string, ComponentSlot> slots = new()
        {
            ["default"] = defaultSlot,
        };
        ComponentAttributes attributes = new(
        [
            new ComponentAttribute("class", "panel"),
        ]);
        TestComponentContext context = new(slots, attributes);

        context.Slots["default"].ShouldBeSameAs(defaultSlot);
        context.Attributes.TryGetValue("class", out object? value).ShouldBeTrue();
        value.ShouldBe("panel");
    }

    [Fact]
    public void Template_ScopeIdentifier_DefaultsToNullAndSupportsGeneratedValue()
    {
        IComponentTemplate defaultTemplate = new DefaultScopeTemplate();
        IComponentTemplate scopedTemplate = new ScopedTemplate();

        defaultTemplate.ScopeIdentifier.ShouldBeNull();
        scopedTemplate.ScopeIdentifier.ShouldBe("data-viu-a1b2c3");
    }

    private sealed class TestComponentContext : IComponentContext
    {
        internal TestComponentContext(
            IReadOnlyDictionary<string, ComponentSlot> slots,
            IComponentAttributeCollection attributes)
        {
            Slots = slots;
            Attributes = attributes;
        }

        public IComponentArguments Arguments { get; } = new ComponentArguments();

        public IReadOnlyDictionary<string, ComponentSlot> Slots { get; }

        public IComponentAttributeCollection Attributes { get; }

        public IComponentFactory Components => null!;

        public IServiceProvider Services => null!;

        public IComponentLifecycle Lifecycle => null!;

        public void Emit(string eventName, params object?[] arguments)
        {
        }
    }

    private sealed class DefaultScopeTemplate : IComponentTemplate
    {
        public ComponentRenderer Setup(IComponentContext context)
        {
            return () => null;
        }
    }

    private sealed class ScopedTemplate : IComponentTemplate
    {
        public string? ScopeIdentifier => "data-viu-a1b2c3";

        public ComponentRenderer Setup(IComponentContext context)
        {
            return () => null;
        }
    }
}
