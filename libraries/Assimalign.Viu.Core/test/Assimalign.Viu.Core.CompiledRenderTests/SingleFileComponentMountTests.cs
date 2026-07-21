using System;
using System.Reflection;
using System.Threading.Tasks;

using Shouldly;
using Xunit;

using Assimalign.Viu;
using Assimalign.Viu.Testing;

namespace Assimalign.Viu.CompiledRenderTests;

/// <summary>
/// [V01.01.06.07] The <c>.viu</c> component-runtime proof of life — the criterion the epic
/// ([V01.01.06], #56) waited on. A real single-file component (<c>@template</c> + reactive
/// <c>@script</c> state + an event handler) is compiled by the ACTUAL
/// <see cref="Assimalign.Viu.Syntax.Generators.SingleFileComponentGenerator"/>, the generated partial —
/// now a mountable <see cref="IComponentDefinition"/> through the generated Name + Setup bridge — is
/// Roslyn-compiled against the runtime helper surface (as in <see cref="CompiledRenderEndToEndTests"/>),
/// and the reflectively-loaded definition is mounted through the shipping <see cref="ViuTest"/> renderer:
/// the real <c>MountComponent → SetupComponent → Setup → render effect</c> path. This proves the closed
/// loop the compiler investment always implied: <c>.viu</c> → generator → generated <c>Setup</c> →
/// runtime → a mounted, reactive component, with NO hand-written wiring beyond authoring the file. Upstream
/// parity: Vue SFC + Composition API <c>setup()</c> (https://vuejs.org/api/sfc-spec.html).
/// </summary>
public sealed class SingleFileComponentMountTests
{
    // A fully self-contained .viu — no hand-written partial half. @script holds all of the state: a
    // readonly Reference<int> (classified SETUP_REF, so {{ Count }} unwraps to _ctx.Count.Value and its
    // reads are tracked by the render effect), a readonly string (SETUP_CONST -> _ctx.Message), and the
    // click handler that mutates the ref. The `using Assimalign.Viu;` is hoisted above the
    // namespace by the generator ([V01.01.06.03.01]).
    private const string GreetingViu =
        "@template {\n" +
        "    <div class=\"greeting\">\n" +
        "        <p class=\"message\">{{ Message }}</p>\n" +
        "        <button class=\"counter\" @click=\"Increment\">{{ Count }}</button>\n" +
        "    </div>\n" +
        "}\n" +
        "\n" +
        "@script {\n" +
        "    using Assimalign.Viu;\n" +
        "    public readonly string Message = \"hello\";\n" +
        "    public readonly Reference<int> Count = Reactive.Reference(0);\n" +
        "    public void Increment() => Count.Value++;\n" +
        "}\n";

    [Fact]
    public async Task SingleFileComponent_MountsThroughTheRuntime_AndReactsToAClick()
    {
        var definition = CompileSingleFileComponent("Greeting", GreetingViu);

        // The generated bridge makes the .viu partial a real, named component definition.
        definition.ShouldBeAssignableTo<IComponentDefinition>();
        definition.Name.ShouldBe("Greeting"); // the generated IComponentDefinition.Name, inferred from the file

        // Mount through the shipping Testing renderer: the real MountComponent -> SetupComponent ->
        // Definition.Setup(...) -> render-effect path, exactly as a hand-written component mounts.
        using var wrapper = ViuTest.Mount(definition);

        // Initial render: the static @script field and the reactive ref, both read through _ctx.
        wrapper.Get(".message").Text().ShouldBe("hello");
        wrapper.Get(".counter").Text().ShouldBe("0");

        // A DOM click runs the @script Increment(), whose Count.Value++ triggers the ref the render read,
        // enqueuing a scheduled re-render (the wrapper awaits the flush) — a .viu component reacting to
        // state with no hand-written wiring.
        await wrapper.Get(".counter").Trigger("click");
        wrapper.Get(".counter").Text().ShouldBe("1");
        wrapper.Get(".message").Text().ShouldBe("hello"); // the TEXT patch-flag update never touched the sibling

        await wrapper.Get(".counter").Trigger("click");
        wrapper.Get(".counter").Text().ShouldBe("2"); // exactly one increment per click — the reactive contract
    }

    [Fact]
    public void SingleFileComponent_WithoutInteraction_RendersItsInitialTree()
    {
        // The html() shape pins that the compiled render produced the whole subtree, not just a fragment,
        // through the generated Setup's NormalizeRoot(Render(this, _cache)) bridge.
        var definition = CompileSingleFileComponent("Greeting", GreetingViu);
        using var wrapper = ViuTest.Mount(definition);

        wrapper.Html().ShouldBe(
            "<div class=\"greeting\"><p class=\"message\">hello</p><button class=\"counter\">0</button></div>");
    }

    // v-bind() in @style makes the generator emit ApplyCssVariables(), and the [V01.01.06.07] Setup calls it
    // during setup. Under the DOM-free test renderer UseCssVars applies to no host element (no browser
    // operations), so this pins that the Setup CSS-var wiring runs without throwing and the component still
    // mounts and renders — AC: "invokes ApplyCssVariables() when the component declares v-bind() CSS vars".
    private const string ThemedViu =
        "@template {\n" +
        "    <div class=\"themed\">{{ Label }}</div>\n" +
        "}\n" +
        "\n" +
        "@script {\n" +
        "    public readonly string Label = \"themed\";\n" +
        "    public readonly string Accent = \"#0a0\";\n" +
        "}\n" +
        "\n" +
        "@style {\n" +
        "    .themed { color: v-bind(Accent); }\n" +
        "}\n";

    [Fact]
    public void SingleFileComponent_WithVBindCssVariable_MountsThroughApplyCssVariablesDuringSetup()
    {
        var generated = Assimalign.Viu.CompiledRenderTests.CompiledRenderSupport.Generate("Themed", ThemedViu);
        // The generator emitted the CSS-var seam AND the Setup call to it.
        generated.ShouldContain("internal void ApplyCssVariables()");
        generated.ShouldContain("ApplyCssVariables();");

        var definition = LoadDefinition("Themed", generated);
        using var wrapper = ViuTest.Mount(definition);

        // Mounting exercised the generated Setup, which called ApplyCssVariables() with the instance current;
        // it did not throw and the component rendered.
        wrapper.Get(".themed").Text().ShouldBe("themed");
    }

    private static IComponentDefinition CompileSingleFileComponent(string name, string viu)
    {
        var generated = CompiledRenderSupport.Generate(name, viu);
        // The generated partial is now a mountable component — the [V01.01.06.07] bridge is present.
        generated.ShouldContain(": global::Assimalign.Viu.IComponentDefinition");
        generated.ShouldContain("global::Assimalign.Viu.IComponentDefinition.Setup(");
        generated.ShouldContain("NormalizeRoot(Render(this, _cache))");
        return LoadDefinition(name, generated);
    }

    private static IComponentDefinition LoadDefinition(string name, string generated)
    {
        // The .viu is self-contained, so the second Roslyn tree is empty (no hand-written half).
        var assembly = Assembly.Load(CompiledRenderSupport.CompileToAssembly(generated, "// self-contained .viu component\n"));
        var type = assembly.GetType($"Demo.{name}")
            ?? throw new InvalidOperationException($"The compiled assembly did not contain Demo.{name}.");
        return (IComponentDefinition)Activator.CreateInstance(type, nonPublic: true)!;
    }
}
