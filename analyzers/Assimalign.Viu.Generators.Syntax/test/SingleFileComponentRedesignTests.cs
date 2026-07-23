using System.Linq;

using Shouldly;
using Xunit;

namespace Assimalign.Viu.Syntax.Generators.Tests;

/// <summary>
/// Pins the generated surface selected by the approved Components/Reactivity redesign.
/// </summary>
public sealed class SingleFileComponentRedesignTests
{
    private const string ProjectDirectory = "C:/proj";
    private const string RootNamespace = "Demo";

    [Fact]
    public void Template_GeneratesComponentTemplateContextAndSynchronousSetupHook()
    {
        const string source =
            "@template {\n" +
            "    <div>ready</div>\n" +
            "}\n" +
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

        generated.ShouldContain(
            "partial class Panel : global::Assimalign.Viu.Components.IComponentTemplate");
        generated.ShouldContain(
            "private global::Assimalign.Viu.Components.IComponentContext Context { get; set; } = null!;");
        generated.ShouldContain("partial void OnSetup();");
        generated.ShouldContain("Context = context;\n            OnSetup();");
        generated.ShouldContain(
            "global::Assimalign.Viu.Components.ComponentRenderer " +
            "global::Assimalign.Viu.Components.IComponentTemplate.Setup(");
    }

    [Fact]
    public void HostNeutralTemplate_DoesNotImportBrowserHelpers()
    {
        const string source =
            "@template {\n" +
            "    <div>host neutral</div>\n" +
            "}\n";

        var outcome = GeneratorTestHarness.Run(
            $"{ProjectDirectory}/Neutral.viu",
            source,
            RootNamespace,
            ProjectDirectory);

        outcome.Diagnostics.ShouldBeEmpty();
        var generated = GeneratorTestHarness.GeneratedSource(
            outcome,
            "Neutral.SingleFileComponent.g.cs");
        generated.ShouldContain("using static global::Assimalign.Viu.RenderHelpers;");
        generated.ShouldNotContain("Assimalign.Viu.Browser");
    }

    [Fact]
    public void DomModifierTemplate_ImportsBrowserHelpersAsARequiredCapability()
    {
        const string source =
            "@template {\n" +
            "    <button @click.stop=\"Save\">Save</button>\n" +
            "}\n" +
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
        generated.ShouldContain("using static global::Assimalign.Viu.Browser.DomRenderHelpers;");
        generated.ShouldContain("_withModifiers");
    }

    [Fact]
    public void SlotOutlet_ReadsCurrentSlotsFromGeneratedContext()
    {
        const string source =
            "@template {\n" +
            "    <slot />\n" +
            "}\n";

        var outcome = GeneratorTestHarness.Run(
            $"{ProjectDirectory}/SlotHost.viu",
            source,
            RootNamespace,
            ProjectDirectory);

        outcome.Diagnostics.ShouldBeEmpty();
        var generated = GeneratorTestHarness.GeneratedSource(
            outcome,
            "SlotHost.SingleFileComponent.g.cs");
        generated.ShouldContain(
            "IReadOnlyDictionary<string, global::Assimalign.Viu.Components.ComponentSlot> " +
            "__slots => Context.Slots;");
    }

    [Fact]
    public void ReactiveReferenceInterface_IsUnwrappedInTemplate()
    {
        const string source =
            "@template {\n" +
            "    <div>{{ Count }}</div>\n" +
            "}\n" +
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
            .ShouldContain("_toDisplayString(_ctx.Count.Value)");
    }

    [Fact]
    public void GeneratedMemberConflicts_ReportReservedMemberDiagnostic()
    {
        const string source =
            "@template {\n" +
            "    <div />\n" +
            "}\n" +
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
            "@template {\n" +
            "    <button @click=\"SaveAsync\">Save</button>\n" +
            "}\n" +
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
        generated.ShouldContain("_withHandler(_ctx.SaveAsync)");
        generated.ShouldNotContain("_ctx.SaveAsync();");
    }

    [Fact]
    public void TaskReturningInlineLambda_RemainsTaskReturningForCoreToObserve()
    {
        const string source =
            "@template {\n" +
            "    <button @click=\"async () => await SaveAsync()\">Save</button>\n" +
            "}\n" +
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
        generated.ShouldContain("_withHandler(async () => await _ctx.SaveAsync())");
    }
}
