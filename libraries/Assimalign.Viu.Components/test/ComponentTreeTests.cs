using System.Collections.Generic;
using System.Threading.Tasks;

using Shouldly;
using Xunit;

using Assimalign.Viu.Components;
using Assimalign.Viu.Shared;

namespace Assimalign.Viu.Components.Tests;

public sealed class ComponentTreeTests
{
    [Fact]
    public void Tree_AllRenderValuesShareComponentContract()
    {
        IComponent[] values =
        {
            ComponentTree.Element("main"),
            ComponentTree.Template<EmptyTemplate>(),
            ComponentTree.Text("text"),
            ComponentTree.Comment(),
            ComponentTree.Static("<span>static</span>"),
            ComponentTree.Fragment(),
            ComponentTree.Teleport("#target"),
        };

        foreach (IComponent component in values)
        {
            ((int)component.Kind).ShouldBeInRange(
                (int)ComponentKind.Element,
                (int)ComponentKind.Teleport);
        }
    }

    [Fact]
    public void Element_ChildrenAndAttributesAreReadOnlySnapshots()
    {
        List<IComponent> children = new() { ComponentTree.Text("first") };
        List<IComponentAttribute> attributes =
            new() { new ComponentAttribute("role", "main") };

        IElementComponent element = ComponentTree.Element(
            "main",
            new ComponentAttributes(attributes),
            children);
        children.Add(ComponentTree.Text("second"));
        attributes.Add(new ComponentAttribute("hidden", true));

        element.Children.Count.ShouldBe(1);
        element.Attributes.Count.ShouldBe(1);
        element.Attributes.TryGetValue("role", out object? role).ShouldBeTrue();
        role.ShouldBe("main");
    }

    [Fact]
    public void Fragment_CompilerMetadata_PreservesBlockTreeFastPathInputs()
    {
        ITextComponent dynamicText = ComponentTree.Text(
            "dynamic",
            new ComponentOptimization(PatchFlags.Text));
        IFragmentComponent block = ComponentTree.Fragment(
            new IComponent[] { ComponentTree.Static("<h1>fixed</h1>"), dynamicText },
            optimization: new ComponentOptimization(
                PatchFlags.StableFragment,
                dynamicChildren: new IComponent[] { dynamicText },
                hasOnce: true));

        block.Optimization.IsBlock.ShouldBeTrue();
        block.Optimization.PatchFlags.ShouldBe(PatchFlags.StableFragment);
        block.Optimization.DynamicChildren.ShouldBe(new IComponent[] { dynamicText });
        block.Optimization.HasOnce.ShouldBeTrue();
        dynamicText.Optimization.PatchFlags.ShouldBe(PatchFlags.Text);
    }

    [Fact]
    public void Template_RegisteredName_DefersResolutionAndActivation()
    {
        int activationCount = 0;
        ComponentFactory factory = new(
        [
            new ComponentRegistration(
                typeof(EmptyTemplate),
                () =>
                {
                    activationCount++;
                    return new EmptyTemplate();
                },
                "empty"),
        ]);

        ITemplateComponent request = ComponentTree.Template("empty");

        request.TemplateName.ShouldBe("empty");
        request.TemplateType.ShouldBeNull();
        activationCount.ShouldBe(0);

        factory.Create(request.TemplateName!).ShouldBeOfType<EmptyTemplate>();
        activationCount.ShouldBe(1);
    }

    [Fact]
    public void Template_RegisteredType_CarriesOnlyTypeIdentity()
    {
        ITemplateComponent request = ComponentTree.Template<EmptyTemplate>();

        request.TemplateType.ShouldBe(typeof(EmptyTemplate));
        request.TemplateName.ShouldBeNull();
    }

    [Fact]
    public void Template_Listeners_AreReadOnlySnapshots()
    {
        ComponentEventListener original =
            new((ComponentEventHandler)(_ => { }));
        Dictionary<string, ComponentEventListener> listeners = new()
        {
            ["changed"] = original,
        };

        ITemplateComponent request = ComponentTree.Template<EmptyTemplate>(
            listeners: listeners);
        listeners["changed"] =
            new ComponentEventListener(
                (AsynchronousComponentEventHandler)(_ => Task.CompletedTask));

        request.Listeners.ShouldNotBeNull();
        request.Listeners["changed"].ShouldBeSameAs(original);
    }

    [Fact]
    public void Template_Slots_PreserveStabilityInAnImmutableSnapshot()
    {
        ComponentSlots slots = new(SlotFlags.Dynamic)
        {
            ["default"] = _ => ComponentTree.Text("original"),
        };

        ITemplateComponent request = ComponentTree.Template<EmptyTemplate>(
            slots: slots);
        slots["default"] = _ => ComponentTree.Text("changed");

        IComponentSlotCollection snapshot =
            request.Slots.ShouldBeAssignableTo<IComponentSlotCollection>()!;
        snapshot.Flags.ShouldBe(SlotFlags.Dynamic);
        ITextComponent text = snapshot["default"](new ComponentArguments())
            .ShouldBeAssignableTo<ITextComponent>()!;
        text.Text.ShouldBe("original");
    }

    [Fact]
    public void Element_Directives_AreImmutableAuthoringMetadata()
    {
        Dictionary<string, bool> modifiers = new()
        {
            ["lazy"] = true,
        };
        ComponentDirectiveBinding binding = new(
            "model",
            value: "Ada",
            argument: "display-name",
            modifiers: modifiers);
        List<IComponentDirectiveBinding> directives = new() { binding };

        IElementComponent element = ComponentTree.Element(
            "input",
            directives: directives);
        directives.Clear();
        modifiers["trim"] = true;

        element.Directives.Count.ShouldBe(1);
        element.Directives[0].ShouldBeSameAs(binding);
        binding.DirectiveName.ShouldBe("model");
        binding.Value.ShouldBe("Ada");
        binding.Argument.ShouldBe("display-name");
        binding.Modifiers.ShouldContainKey("lazy");
        binding.Modifiers.ShouldNotContainKey("trim");
    }

    [Fact]
    public void Template_Directives_AreAttachedToRenderedRootMetadata()
    {
        ComponentDirectiveBinding binding = new("focus");

        ITemplateComponent request = ComponentTree.Template<EmptyTemplate>(
            directives: new IComponentDirectiveBinding[] { binding });

        request.Directives.ShouldBe(new IComponentDirectiveBinding[] { binding });
    }

    private sealed class EmptyTemplate : IComponentTemplate
    {
        public ComponentRenderer Setup(IComponentContext context)
        {
            return () => null;
        }
    }
}
