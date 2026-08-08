using System.Linq;

using Shouldly;
using Xunit;

namespace Assimalign.Viu.Generators.Syntax.Tests;

/// <summary>
/// Pins the generated surface selected by the approved Components/Reactivity redesign.
/// </summary>
public sealed class SingleFileComponentRedesignTests
{
    private const string ProjectDirectory = "C:/proj";
    private const string RootNamespace = "Demo";

    [Fact]
    public void Template_GeneratesComponentContextAndSynchronousSetupHook()
    {
        const string source =
            "<template>\n" +
            "    <div>ready</div>\n" +
            "</template>\n" +
            "@script {\n" +
            "    partial void OnSetup()\n" +
            "    {\n" +
            "        _ = Context.Services;\n" +
            "    }\n" +
            "}\n";

        var outcome = GeneratorTestHarness.Run(
            $"{ProjectDirectory}/Panel.viu",
            source,
            RootNamespace,
            ProjectDirectory);

        outcome.Diagnostics.ShouldBeEmpty();
        var generated = GeneratorTestHarness.GeneratedSource(
            outcome,
            "Panel.SingleFileComponent.g.cs");

        // [SFC-CG-4] The base class carries Context and the root-level lifecycle registration surface;
        // the interface stays on the partial because the declaration members are explicit implementations.
        generated.ShouldContain(
            "partial class Panel : global::Assimalign.Viu.Components.ComponentBase, "
            + "global::Assimalign.Viu.Components.IComponent");
        generated.ShouldNotContain("IComponentContext Context { get; set; }");
        generated.ShouldContain("partial void OnSetup();");
        generated.ShouldContain("Context = context;\n            OnSetup();");
        generated.ShouldContain(
            "global::Assimalign.Viu.Components.ComponentRenderer " +
            "global::Assimalign.Viu.Components.IComponent.Setup(");
    }

    [Fact]
    public void HostNeutralTemplate_DoesNotImportBrowserHelpers()
    {
        const string source =
            "<template>\n" +
            "    <div>host neutral</div>\n" +
            "</template>\n";

        var outcome = GeneratorTestHarness.Run(
            $"{ProjectDirectory}/Neutral.viu",
            source,
            RootNamespace,
            ProjectDirectory);

        outcome.Diagnostics.ShouldBeEmpty();
        var generated = GeneratorTestHarness.GeneratedSource(
            outcome,
            "Neutral.SingleFileComponent.g.cs");
        generated.ShouldNotContain("using static");
        generated.ShouldNotContain("RenderHelpers");
        generated.ShouldNotContain("Assimalign.Viu.Browser");
    }

    [Fact]
    public void DomModifierTemplate_QualifiesBrowserCapability()
    {
        const string source =
            "<template>\n" +
            "    <button @click.stop=\"Save\">Save</button>\n" +
            "</template>\n" +
            "@script {\n" +
            "    private void Save() { }\n" +
            "}\n";

        var outcome = GeneratorTestHarness.Run(
            $"{ProjectDirectory}/BrowserButton.viu",
            source,
            RootNamespace,
            ProjectDirectory);

        outcome.Diagnostics.ShouldBeEmpty();
        var generated = GeneratorTestHarness.GeneratedSource(
            outcome,
            "BrowserButton.SingleFileComponent.g.cs");
        generated.ShouldNotContain("using static");
        generated.ShouldContain("global::Assimalign.Viu.Browser.BrowserEvents.WithModifiers");
    }

    [Fact]
    public void SlotOutlet_ReadsCurrentSlotsFromGeneratedContext()
    {
        const string source =
            "<template>\n" +
            "    <slot />\n" +
            "</template>\n";

        var outcome = GeneratorTestHarness.Run(
            $"{ProjectDirectory}/SlotHost.viu",
            source,
            RootNamespace,
            ProjectDirectory);

        outcome.Diagnostics.ShouldBeEmpty();
        var generated = GeneratorTestHarness.GeneratedSource(
            outcome,
            "SlotHost.SingleFileComponent.g.cs");
        generated.ShouldContain("component.Context!.Bindings.Slots.TryGetValue");
    }

    [Fact]
    public void ReactiveReferenceInterface_IsUnwrappedInTemplate()
    {
        const string source =
            "<template>\n" +
            "    <div>{{ Count }}</div>\n" +
            "</template>\n" +
            "@script {\n" +
            "    public global::Assimalign.Viu.Reactivity.IReactiveReference<int> Count = default!;\n" +
            "}\n";

        var outcome = GeneratorTestHarness.Run(
            $"{ProjectDirectory}/Counter.viu",
            source,
            RootNamespace,
            ProjectDirectory);

        outcome.Diagnostics.ShouldBeEmpty();
        GeneratorTestHarness.GeneratedSource(outcome, "Counter.SingleFileComponent.g.cs")
            .ShouldContain("component.Count.Value");
    }

    [Fact]
    public void GeneratedMemberConflicts_ReportReservedMemberDiagnostic()
    {
        const string source =
            "<template>\n" +
            "    <div />\n" +
            "</template>\n" +
            "@script {\n" +
            "    private object Context = new();\n" +
            "    private void OnSetup() { }\n" +
            "}\n";

        var outcome = GeneratorTestHarness.Run(
            $"{ProjectDirectory}/Conflict.viu",
            source,
            RootNamespace,
            ProjectDirectory);

        var conflicts = outcome.Diagnostics
            .Where(diagnostic => diagnostic.Id == "VIU1204")
            .ToArray();
        conflicts.Length.ShouldBe(2);
        conflicts.ShouldAllBe(
            diagnostic => diagnostic.Location.GetLineSpan().Path == $"{ProjectDirectory}/Conflict.viu");
    }

    [Fact]
    public void AuthoredLifecycleField_OwnsItsName_AndSuppressesGeneratedOverloads()
    {
        const string source =
            "<template>\n" +
            "    <div />\n" +
            "</template>\n" +
            "@script {\n" +
            "    private global::System.Action<global::System.Action> OnMounted = callback => callback();\n" +
            "}\n";

        var outcome = GeneratorTestHarness.Run(
            $"{ProjectDirectory}/LifecycleOwner.viu",
            source,
            RootNamespace,
            ProjectDirectory);

        outcome.Diagnostics.ShouldBeEmpty();
        var generated = GeneratorTestHarness.GeneratedSource(
            outcome,
            "LifecycleOwner.SingleFileComponent.g.cs");
        generated.ShouldContain("Action<global::System.Action> OnMounted");
        generated.ShouldNotContain("protected void OnMounted(");
    }

    [Fact]
    public void RenderlessFile_DoesNotReserveComponentBridgeMembers()
    {
        const string source =
            "@script {\n" +
            "    private object Context = new();\n" +
            "    private void OnSetup() { }\n" +
            "}\n";

        var outcome = GeneratorTestHarness.Run(
            $"{ProjectDirectory}/ScriptOnly.viu",
            source,
            RootNamespace,
            ProjectDirectory);

        outcome.Diagnostics.ShouldBeEmpty();
    }

    [Fact]
    public void AsynchronousVoidScriptMethod_ReportsUnobservableCallbackDiagnostic()
    {
        const string source =
            "@script {\n" +
            "    private async void SaveAsync()\n" +
            "    {\n" +
            "        await global::System.Threading.Tasks.Task.Yield();\n" +
            "    }\n" +
            "}\n";

        var outcome = GeneratorTestHarness.Run(
            $"{ProjectDirectory}/InvalidAsync.viu",
            source,
            RootNamespace,
            ProjectDirectory);

        var diagnostic = outcome.Diagnostics.ShouldHaveSingleItem();
        diagnostic.Id.ShouldBe("VIU1205");
        diagnostic.GetMessage().ShouldContain("Return Task instead");
        var span = diagnostic.Location.GetLineSpan();
        span.Path.ShouldBe($"{ProjectDirectory}/InvalidAsync.viu");
        span.StartLinePosition.Line.ShouldBe(1);
    }

    [Fact]
    public void TaskReturningNamedEventHandler_RemainsADelegateForCoreToObserve()
    {
        const string source =
            "<template>\n" +
            "    <button @click=\"SaveAsync\">Save</button>\n" +
            "</template>\n" +
            "@script {\n" +
            "    private global::System.Threading.Tasks.Task SaveAsync()\n" +
            "        => global::System.Threading.Tasks.Task.CompletedTask;\n" +
            "}\n";

        var outcome = GeneratorTestHarness.Run(
            $"{ProjectDirectory}/AsyncButton.viu",
            source,
            RootNamespace,
            ProjectDirectory);

        outcome.Diagnostics.ShouldBeEmpty();
        var generated = GeneratorTestHarness.GeneratedSource(
            outcome,
            "AsyncButton.SingleFileComponent.g.cs");
        generated.ShouldContain("RenderGlue.Handler(component.SaveAsync)");
        generated.ShouldNotContain("component.SaveAsync();");
    }

    [Fact]
    public void TaskReturningInlineLambda_RemainsTaskReturningForCoreToObserve()
    {
        const string source =
            "<template>\n" +
            "    <button @click=\"async () => await SaveAsync()\">Save</button>\n" +
            "</template>\n" +
            "@script {\n" +
            "    private global::System.Threading.Tasks.Task SaveAsync()\n" +
            "        => global::System.Threading.Tasks.Task.CompletedTask;\n" +
            "}\n";

        var outcome = GeneratorTestHarness.Run(
            $"{ProjectDirectory}/InlineAsyncButton.viu",
            source,
            RootNamespace,
            ProjectDirectory);

        outcome.Diagnostics.ShouldBeEmpty();
        var generated = GeneratorTestHarness.GeneratedSource(
            outcome,
            "InlineAsyncButton.SingleFileComponent.g.cs");
        generated.ShouldContain("RenderGlue.Handler(async () => await component.SaveAsync())");
    }
}
