using System;
using System.Collections.Generic;
using System.Linq;

using Shouldly;
using Xunit;

using Assimalign.Viu.Components;

namespace Assimalign.Viu.Components.Tests;

public sealed class ComponentBindingsResolveTests
{
    [Fact]
    public void Resolve_DeclaredAndUndeclaredArguments_SplitsParametersFromFallthrough()
    {
        ComponentContract contract = new(
            parameters: [new ComponentParameter("name")]);
        ComponentInvocation invocation = new(
            arguments: new Dictionary<string, object?>
            {
                ["name"] = "Viu",
                ["class"] = "welcome",
            });

        ComponentBindings bindings = ComponentBindings.Resolve(contract, invocation);

        bindings.Parameters.Count.ShouldBe(1);
        bindings.Parameters["name"].ShouldBe("Viu");
        bindings.FallthroughBindings.Count.ShouldBe(1);
        bindings.FallthroughBindings["class"].ShouldBe("welcome");
    }

    [Fact]
    public void Resolve_HyphenatedParameterAlias_PublishesCanonicalDeclarationName()
    {
        ComponentContract contract = new(
            parameters: [new ComponentParameter("modelValue")]);
        ComponentInvocation invocation = new(
            arguments: new Dictionary<string, object?>
            {
                ["model-value"] = 42,
            });

        ComponentBindings bindings = ComponentBindings.Resolve(contract, invocation);

        bindings.Parameters.ShouldHaveSingleItem();
        bindings.Parameters["modelValue"].ShouldBe(42);
        bindings.Parameters.ContainsKey("model-value").ShouldBeFalse();
    }

    [Fact]
    public void Resolve_CamelizedParameterAlias_PublishesCanonicalDeclarationName()
    {
        // A declaration's exact, camelized, and hyphenated aliases are one ordinal table [CMP-13].
        ComponentContract contract = new(
            parameters: [new ComponentParameter("model-value")]);
        ComponentInvocation invocation = new(
            arguments: new Dictionary<string, object?>
            {
                ["modelValue"] = 42,
            });

        ComponentBindings bindings = ComponentBindings.Resolve(contract, invocation);

        bindings.Parameters.ShouldHaveSingleItem();
        bindings.Parameters["model-value"].ShouldBe(42);
    }

    [Fact]
    public void Resolve_TwoSuppliedAliasesForOneParameter_ReportsDuplicateAndUsesLaterValue()
    {
        ComponentContract contract = new(
            parameters: [new ComponentParameter("modelValue")]);
        ComponentInvocation invocation = new(
            arguments: new Dictionary<string, object?>
            {
                ["modelValue"] = "first",
                ["model-value"] = "second",
            });
        List<ComponentBindingDiagnostic> diagnostics = [];

        ComponentBindings bindings = ComponentBindings.Resolve(contract, invocation, diagnostics);

        bindings.Parameters["modelValue"].ShouldBe("second");
        ComponentBindingDiagnostic diagnostic = diagnostics.ShouldHaveSingleItem();
        diagnostic.Kind.ShouldBe(ComponentBindingDiagnosticKind.DuplicateAlias);
        diagnostic.Name.ShouldBe("modelValue");
    }

    [Fact]
    public void Resolve_TwoDeclarationsShareAlias_ReportsDuplicateAndUsesFirstDeclaration()
    {
        ComponentContract contract = new(
            parameters:
            [
                new ComponentParameter("model-value"),
                new ComponentParameter("modelValue"),
            ]);
        ComponentInvocation invocation = new(
            arguments: new Dictionary<string, object?>
            {
                ["modelValue"] = "value",
            });
        List<ComponentBindingDiagnostic> diagnostics = [];

        ComponentBindings bindings = ComponentBindings.Resolve(contract, invocation, diagnostics);

        bindings.Parameters["model-value"].ShouldBe("value");
        diagnostics.Count.ShouldBe(2);
        diagnostics.ShouldAllBe(
            diagnostic => diagnostic.Kind == ComponentBindingDiagnosticKind.DuplicateAlias);
        diagnostics.Select(diagnostic => diagnostic.Name).ShouldBe(
            ["modelValue", "model-value"],
            ignoreOrder: true);
    }

    [Fact]
    public void Resolve_DuplicateExactDeclarations_ReportsOneDiagnosticForTheUniqueAlias()
    {
        ComponentContract contract = new(
            parameters:
            [
                new ComponentParameter("name"),
                new ComponentParameter("name"),
            ]);
        List<ComponentBindingDiagnostic> diagnostics = [];

        ComponentBindings.Resolve(contract, ComponentInvocation.Empty, diagnostics);

        ComponentBindingDiagnostic diagnostic = diagnostics.ShouldHaveSingleItem();
        diagnostic.Kind.ShouldBe(ComponentBindingDiagnosticKind.DuplicateAlias);
        diagnostic.Name.ShouldBe("name");
    }

    [Fact]
    public void Resolve_DeclaredListenersAndNodeLifecycleBindings_DoNotFallThrough()
    {
        ComponentContract contract = new(events: [new ComponentEvent("save")]);
        ComponentInvocation invocation = new(
            arguments: new Dictionary<string, object?>
            {
                ["onSave"] = new Action(() => { }),
                ["onSaveOnce"] = new Action(() => { }),
                ["onVnodeMounted"] = new Action(() => { }),
                ["onClick"] = new Action(() => { }),
            });

        ComponentBindings bindings = ComponentBindings.Resolve(contract, invocation);

        bindings.FallthroughBindings.ShouldHaveSingleItem();
        bindings.FallthroughBindings.ContainsKey("onClick").ShouldBeTrue();
    }

    [Fact]
    public void Resolve_ValidatorRejectsSuppliedValue_ReportsValidationDiagnosticWithoutDiscardingValue()
    {
        ComponentContract contract = new(
            parameters:
            [
                new ComponentParameter(
                    "count",
                    validator: value => value is int count && count > 0),
            ]);
        ComponentInvocation invocation = new(
            arguments: new Dictionary<string, object?> { ["count"] = -1 });
        List<ComponentBindingDiagnostic> diagnostics = [];

        ComponentBindings bindings = ComponentBindings.Resolve(contract, invocation, diagnostics);

        bindings.Parameters["count"].ShouldBe(-1);
        diagnostics.ShouldHaveSingleItem().Kind.ShouldBe(
            ComponentBindingDiagnosticKind.ParameterValidationFailed);
    }

    [Fact]
    public void Resolve_MissingRequiredParameter_ReportsMissingRequiredParameterDiagnostic()
    {
        ComponentContract contract = new(
            parameters: [new ComponentParameter("name", isRequired: true)]);
        List<ComponentBindingDiagnostic> diagnostics = [];

        ComponentBindings bindings = ComponentBindings.Resolve(
            contract,
            ComponentInvocation.Empty,
            diagnostics);

        bindings.Parameters.ShouldBeEmpty();
        ComponentBindingDiagnostic diagnostic = diagnostics.ShouldHaveSingleItem();
        diagnostic.Kind.ShouldBe(ComponentBindingDiagnosticKind.MissingRequiredParameter);
        diagnostic.Name.ShouldBe("name");
    }

    [Fact]
    public void Resolve_MissingParameterWithDefault_DoesNotEvaluateOrReportDefault()
    {
        int factoryRuns = 0;
        ComponentContract contract = new(
            parameters:
            [
                new ComponentParameter(
                    "name",
                    isRequired: true,
                    defaultFactory: () =>
                    {
                        factoryRuns++;
                        return "default";
                    }),
            ]);
        List<ComponentBindingDiagnostic> diagnostics = [];

        ComponentBindings bindings = ComponentBindings.Resolve(
            contract,
            ComponentInvocation.Empty,
            diagnostics);

        factoryRuns.ShouldBe(0);
        bindings.Parameters.ShouldBeEmpty();
        diagnostics.ShouldBeEmpty();
    }

    [Fact]
    public void Resolve_SourceMutationAfterResolution_DoesNotChangeBindingSnapshots()
    {
        Dictionary<string, object?> arguments = new() { ["name"] = "before" };
        ComponentContract contract = new(
            parameters: [new ComponentParameter("name")]);
        ComponentInvocation invocation = new(arguments: arguments);

        ComponentBindings bindings = ComponentBindings.Resolve(contract, invocation);
        arguments["name"] = "after";

        bindings.Parameters["name"].ShouldBe("before");
    }

    [Fact]
    public void Resolve_NullContractOrInvocation_ThrowsArgumentNullException()
    {
        Should.Throw<ArgumentNullException>(
            () => ComponentBindings.Resolve(null!, ComponentInvocation.Empty));
        Should.Throw<ArgumentNullException>(
            () => ComponentBindings.Resolve(new ComponentContract(), null!));
    }
}
