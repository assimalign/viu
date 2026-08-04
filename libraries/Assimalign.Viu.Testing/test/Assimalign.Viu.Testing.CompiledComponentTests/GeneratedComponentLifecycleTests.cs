using System;
using System.Collections.Generic;
using System.Linq;

using Shouldly;
using Xunit;

using Assimalign.Viu.Components;

namespace Assimalign.Viu.Testing.Tests;

/// <summary>
/// The runtime half of root-level lifecycle registration ([CMP-32]): a real <c>.viu</c> whose
/// <c>@script</c> writes <c>OnMounted(...)</c> at the root of the class is compiled by the real source
/// generator and mounted in the in-memory host, so these tests assert what a developer observes rather
/// than what the emitter wrote. The central claim is that the root and <c>Context.Lifecycle</c> forms are
/// interchangeable — same callbacks, same phases, one shared registration order — which is what makes the
/// shorter form adoptable without re-reviewing a component's behavior.
/// </summary>
public sealed class GeneratedComponentLifecycleTests
{
    // Every callback reports through Context.Emit, because an emitted-event log is ordered and survives
    // unmount — the phases that run after the last render are otherwise unobservable in the markup.
    private const string LifecycleProbe =
        "<template>\n" +
        "    <p>probe</p>\n" +
        "</template>\n" +
        "\n" +
        "@script {\n" +
        "    partial void OnSetup()\n" +
        "    {\n" +
        "        OnBeforeMount(() => Context.Emit(\"phase\", \"before-mount\"));\n" +
        "        OnMounted(() => Context.Emit(\"phase\", \"root-mounted\"));\n" +
        "        Context.Lifecycle.OnMounted(() => Context.Emit(\"phase\", \"context-mounted\"));\n" +
        "        OnMounted(() => Context.Emit(\"phase\", \"root-mounted-again\"));\n" +
        "        OnBeforeUnmount(() => Context.Emit(\"phase\", \"before-unmount\"));\n" +
        "        OnUnmounted(() => Context.Emit(\"phase\", \"unmounted\"));\n" +
        "    }\n" +
        "}\n";

    // [CMP-32] The collision case: a component declares a member with the inherited wrapper's exact
    // signature. C# hiding rules make the authored member win, and the compiler says so with a warning.
    private const string HidingProbe =
        "<template>\n" +
        "    <p>{{ Phase }}</p>\n" +
        "</template>\n" +
        "\n" +
        "@script {\n" +
        "    using System;\n" +
        "\n" +
        "    public string Phase { get; private set; } = \"authored member never ran\";\n" +
        "\n" +
        "    private void OnMounted(Action callback) => Phase = \"captured by the component\";\n" +
        "\n" +
        "    partial void OnSetup()\n" +
        "    {\n" +
        "        OnMounted(() => Phase = \"registered with the lifecycle\");\n" +
        "    }\n" +
        "}\n";

    [Fact]
    public void RootAndContextRegistrations_RunInOneSharedOrder_AcrossTheMountedLifetime()
    {
        // [CMP-32] The two forms register into the same per-phase list, so the mounted phase runs
        // root, context, root — the order the registrations were made, not the form they used. Run counts
        // are pinned by the exact sequence: a wrapper that registered twice would show a duplicate.
        using ComponentWrapper wrapper = Mount("LifecycleProbe", LifecycleProbe);

        Phases(wrapper).ShouldBe(["before-mount", "root-mounted", "context-mounted", "root-mounted-again"]);

        wrapper.Unmount();

        Phases(wrapper).ShouldBe(
        [
            "before-mount",
            "root-mounted",
            "context-mounted",
            "root-mounted-again",
            "before-unmount",
            "unmounted",
        ]);
    }

    [Fact]
    public void ComponentDeclaringTheSameMember_HidesTheRootWrapper_WithAWarningNotAnError()
    {
        // [CMP-32] The collision outcome is benign end to end: the component still compiles — CS0108 is a
        // hiding WARNING, and it is the only diagnostic — and the authored member is what the component's
        // own call site reaches, exactly as if the root wrapper had never existed.
        GeneratedComponentSupport.CompileDiagnostics("HidingProbe", HidingProbe).ShouldBe(["CS0108"]);

        using ComponentWrapper wrapper = Mount("HidingProbe", HidingProbe, requireNoWarnings: false);

        wrapper.Html().ShouldBe("<p>captured by the component</p>");
        wrapper.Emitted().ShouldBeEmpty(); // nothing was registered, so no lifecycle callback ever ran
    }

    private static ComponentWrapper Mount(string componentName, string source, bool requireNoWarnings = true)
    {
        var type = GeneratedComponentSupport.CompileToType(componentName, source, requireNoWarnings);
        return ViuTest.Mount(
            (IComponentTemplate)Activator.CreateInstance(type)!,
            new ComponentMountOptions
            {
                // "phase" is emitted without a declaration, which Core warns about; the warning is not
                // what these tests are about.
                ConfigureApplication = application => application.WarnHandler = _ => { },
            });
    }

    private static IReadOnlyList<string> Phases(ComponentWrapper wrapper) =>
        wrapper.Emitted("phase")
            .Select(occurrence => (string)occurrence.ShouldHaveSingleItem()!)
            .ToList();
}
