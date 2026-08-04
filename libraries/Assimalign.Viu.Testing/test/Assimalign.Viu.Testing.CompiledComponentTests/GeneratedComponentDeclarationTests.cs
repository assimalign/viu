using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading.Tasks;

using Shouldly;
using Xunit;

using Assimalign.Viu;
using Assimalign.Viu.Components;
using Assimalign.Viu.Reactivity;

namespace Assimalign.Viu.Testing.Tests;

/// <summary>
/// The runtime half of the attribute-declared component surface ([CMP-26]–[CMP-31]): a real
/// <c>.viu</c> is compiled by the real source generator, loaded, and mounted in the in-memory host, so
/// these tests assert what a developer observes rather than what the emitter wrote. The central claim is
/// <b>substitutability</b> — an attribute-declared component and the imperative component it replaces
/// resolve arguments identically — because that is what makes the new form adoptable without a behavior
/// review of every component.
/// </summary>
public sealed class GeneratedComponentDeclarationTests
{
    private const string CardTemplate =
        "<template>\n" +
        "    <article>\n" +
        "        <span>{{ Eyebrow }}</span>\n" +
        "        <h2>{{ Title }}</h2>\n" +
        "    </article>\n" +
        "</template>\n";

    // [CMP-26] The declarative form: the property that receives the argument carries the declaration,
    // and the authored initializer is the default [CMP-29].
    private const string AttributedCard =
        CardTemplate +
        "\n" +
        "@script {\n" +
        "    using Assimalign.Viu.Components;\n" +
        "\n" +
        "    [Parameter(IsRequired = true)]\n" +
        "    public string Title { get; set; } = \"Untitled\";\n" +
        "\n" +
        "    [Parameter]\n" +
        "    public string Eyebrow { get; set; } = \"Feature\";\n" +
        "}\n";

    // The imperative form the attributed one must be substitutable for — the shape every existing
    // component uses.
    private const string ImperativeCard =
        CardTemplate +
        "\n" +
        "@script {\n" +
        "    using System.Collections.Generic;\n" +
        "    using Assimalign.Viu.Components;\n" +
        "\n" +
        "    public IReadOnlyList<IComponentParameter> Parameters { get; } =\n" +
        "    [\n" +
        "        new ComponentParameter(\"title\", isRequired: true),\n" +
        "        new ComponentParameter(\"eyebrow\", defaultFactory: static () => \"Feature\"),\n" +
        "    ];\n" +
        "\n" +
        "    public string Title => Context.Arguments.Get<string>(\"title\") ?? \"Untitled\";\n" +
        "\n" +
        "    public string Eyebrow => Context.Arguments.Get<string>(\"eyebrow\") ?? \"Feature\";\n" +
        "}\n";

    [Theory]
    [InlineData("title")]   // the canonical derived name
    [InlineData("Title")]   // an unrecognized spelling: neither form resolves it
    public void AttributedAndImperativeComponents_ResolveTheSameArguments(string suppliedName)
    {
        // The substitutability claim. Both components are mounted with the identical argument snapshot
        // and must render identical markup — including for a spelling neither declares, where both fall
        // back to their default rather than one of them silently matching.
        IComponentArguments arguments = Arguments((suppliedName, "Compiled templates"));

        Render("AttributedCard", AttributedCard, arguments)
            .ShouldBe(Render("ImperativeCard", ImperativeCard, arguments));
    }

    [Fact]
    public void AttributedComponent_RendersTheSuppliedArgument_AndTheAuthoredDefault()
    {
        // [CMP-29] A supplied argument wins; an omitted one falls back to the property's authored
        // initializer, captured once per mounted instance.
        Render("AttributedCard", AttributedCard, Arguments(("title", "Compiled templates")))
            .ShouldBe("<article><span>Feature</span><h2>Compiled templates</h2></article>");
    }

    [Fact]
    public void AttributedComponent_ResolvesTheKebabCaseSpelling_OfTheDerivedName()
    {
        // [CMP-13]/[CMP-27] The derived name is canonical, and Core's alias table still accepts the
        // parent's kebab-case spelling — the attribute form changes how a parameter is DECLARED, not how
        // an argument is RESOLVED.
        var type = GeneratedComponentSupport.CompileToType("ModelCard", ModelCard);

        Render(type, Arguments(("model-value", 4))).ShouldBe("<p>4</p>");
        Render(type, Arguments(("modelValue", 7))).ShouldBe("<p>7</p>");
    }

    [Fact]
    public async Task AttributedComponent_RebindsOnEveryRender_AndRestoresTheDefaultWhenTheParentStops()
    {
        // [CMP-29] The binding is per render, not per mount: a parent that changes an argument updates
        // the child, and a parent that STOPS supplying one restores the captured default rather than
        // leaving the last supplied value behind. The second half is what a setup-time-only binding
        // silently gets wrong.
        var childType = GeneratedComponentSupport.CompileToType("AttributedCard", AttributedCard);
        Reference<string?> title = Reactive.Reference<string?>("First");
        ParentTemplate parent = new(childType, () => title.Value);

        using ComponentWrapper wrapper = ViuTest.Mount(
            parent,
            new ComponentMountOptions
            {
                Components = new ComponentFactory(
                    [new ComponentRegistration(
                        childType,
                        () => (IComponentTemplate)Activator.CreateInstance(childType)!)]),
                ConfigureApplication = application => application.WarnHandler = _ => { },
            });
        wrapper.Html().ShouldBe("<article><span>Feature</span><h2>First</h2></article>");

        title.Value = "Second";
        await wrapper.FlushAsync();
        wrapper.Html().ShouldBe("<article><span>Feature</span><h2>Second</h2></article>");

        title.Value = null; // the parent stops supplying the argument entirely
        await wrapper.FlushAsync();
        wrapper.Html().ShouldBe("<article><span>Feature</span><h2>Untitled</h2></article>");
    }

    [Fact]
    public void AttributedRequiredParameter_Omitted_WarnsWithoutDiscarding()
    {
        // [CMP-12]/[CMP-28] Requiredness declared by attribute reaches Core as the same
        // ComponentParameter.IsRequired an imperative declaration produces, so the mount-time warning is
        // unchanged — the component still renders with its authored default.
        List<string> warnings = [];
        var instance = (IComponentTemplate)Activator.CreateInstance(
            GeneratedComponentSupport.CompileToType("AttributedCard", AttributedCard))!;

        using ComponentWrapper wrapper = ViuTest.Mount(
            instance,
            new ComponentMountOptions
            {
                ConfigureApplication = application =>
                    application.WarnHandler = message => warnings.Add(message),
            });

        warnings.ShouldContain(warning => warning.Contains("\"title\""));
        wrapper.Html().ShouldBe("<article><span>Feature</span><h2>Untitled</h2></article>");
    }

    [Fact]
    public void AttributedEvent_EmitsTheDeclaredName_WithTheOrderedPayload()
    {
        // [CMP-30] The attributed `partial void` is implemented as the emit of the declared name: calling
        // the strongly typed method is what reaches the parent, and the declaration is what stops Core
        // warning about an undeclared event.
        var type = GeneratedComponentSupport.CompileToType("Rating", RatingControl);
        var instance = (IComponentTemplate)Activator.CreateInstance(type)!;
        List<string> warnings = [];

        using ComponentWrapper wrapper = ViuTest.Mount(
            instance,
            new ComponentMountOptions
            {
                ConfigureApplication = application =>
                    application.WarnHandler = message => warnings.Add(message),
            });

        type.GetMethod("Changed", BindingFlags.Public | BindingFlags.Instance)!
            .Invoke(instance, [4]);
        type.GetMethod("Dismissed", BindingFlags.Public | BindingFlags.Instance)!
            .Invoke(instance, []);

        wrapper.Emitted("changed").ShouldHaveSingleItem().ShouldBe([4]);
        wrapper.Emitted("dismissed").ShouldHaveSingleItem().ShouldBeEmpty();
        warnings.ShouldBeEmpty(); // both events are declared, so neither is an undeclared emission
    }

    [Fact]
    public void AttributedComponent_DeclarationCarriesTheDeclaredParameterType()
    {
        // [SFC-USE-1] The declared type reaches the runtime declaration, and through the [Parameter]
        // attribute that produced it, a consumer's build-time check. This is the metadata that an
        // imperative `new ComponentParameter("title")` never carried.
        var template = (IComponentTemplate)Activator.CreateInstance(
            GeneratedComponentSupport.CompileToType("ModelCard", ModelCard))!;

        var parameter = template.Parameters.ShouldHaveSingleItem();
        parameter.Name.ShouldBe("modelValue");
        parameter.ParameterType.ShouldBe(typeof(int));
    }

    [Fact]
    public void RequiredModifierParameter_ActivatesAndBinds_ThroughTheParameterlessActivator()
    {
        // [CMP-28] The C# `required` modifier declares requiredness to Viu without breaking activation:
        // Viu's activator is `new T()`, which C# would otherwise reject for a type with required members.
        // The generated [SetsRequiredMembers] constructor is what keeps that legal, and the scaffold still
        // compiles with zero warnings (asserted by the compile harness).
        var type = GeneratedComponentSupport.CompileToType("RequiredCard", RequiredCard);

        Render(type, Arguments(("title", "Bound"))).ShouldBe("<h2>Bound</h2>");
    }

    private const string RequiredCard =
        "<template>\n" +
        "    <h2>{{ Title }}</h2>\n" +
        "</template>\n" +
        "\n" +
        "@script {\n" +
        "    using Assimalign.Viu.Components;\n" +
        "\n" +
        "    [Parameter]\n" +
        "    public required string Title { get; set; }\n" +
        "}\n";

    private const string ModelCard =
        "<template>\n" +
        "    <p>{{ ModelValue }}</p>\n" +
        "</template>\n" +
        "\n" +
        "@script {\n" +
        "    using Assimalign.Viu.Components;\n" +
        "\n" +
        "    [Parameter]\n" +
        "    public int ModelValue { get; set; }\n" +
        "}\n";

    private const string RatingControl =
        "<template>\n" +
        "    <div></div>\n" +
        "</template>\n" +
        "\n" +
        "@script {\n" +
        "    using Assimalign.Viu.Components;\n" +
        "\n" +
        "    [Event]\n" +
        "    public partial void Changed(int rating);\n" +
        "\n" +
        "    [Event]\n" +
        "    public partial void Dismissed();\n" +
        "}\n";

    private static string Render(string componentName, string source, IComponentArguments arguments)
        => Render(GeneratedComponentSupport.CompileToType(componentName, source), arguments);

    private static string Render(Type componentType, IComponentArguments arguments)
    {
        using ComponentWrapper wrapper = ViuTest.Mount(
            (IComponentTemplate)Activator.CreateInstance(componentType)!,
            new ComponentMountOptions
            {
                Arguments = arguments,
                ConfigureApplication = application => application.WarnHandler = _ => { },
            });
        return wrapper.Html();
    }

    private static IComponentArguments Arguments(params (string Name, object? Value)[] values)
    {
        Dictionary<string, object?> map = new(StringComparer.Ordinal);
        foreach ((string name, object? value) in values)
        {
            map[name] = value;
        }

        return new ComponentArguments(map);
    }

    /// <summary>
    /// A hand-authored parent that re-renders the compiled child with a reactive argument, so the
    /// per-render rebinding contract is exercised through a real parent update rather than a second mount.
    /// </summary>
    private sealed class ParentTemplate(Type childType, Func<string?> title) : IComponentTemplate
    {
        public ComponentRenderer Setup(IComponentContext context)
        {
            return () =>
            {
                Dictionary<string, object?> arguments = new(StringComparer.Ordinal);
                if (title() is { } value)
                {
                    arguments["title"] = value;
                }

                return ComponentTree.Template(childType, new ComponentArguments(arguments));
            };
        }
    }
}
