using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

using Shouldly;
using Xunit;

using Assimalign.Viu.Components;
using Assimalign.Viu.Reactivity;

using RoslynDiagnosticSeverity = Microsoft.CodeAnalysis.DiagnosticSeverity;

namespace Assimalign.Viu.Generators.Syntax.Tests;

public sealed class SingleFileComponentPortableEventTests
{
    [Fact]
    public void NativeHandlers_PortableEventMethodGroups_PreserveHostNeutralDelegateTypes()
    {
        // [DVT-14], [SFC-CG-2]: a host-neutral library must reach the Browser host's designed
        // Action/IElementEvent dispatch without requiring a Browser-specific object-delegate shim.
        const string projectDirectory = "C:/proj";
        const string source = """
            <template>
                <section>
                    <button @click="() => Click()">Click</button>
                    <input @input="SetValue" @change="CommitAsync" />
                </section>
            </template>
            @script {
                using System.Threading.Tasks;
                using Assimalign.Viu.Components;

                private void Click() { }
                private void SetValue(IElementEvent input) { }
                private Task CommitAsync(IElementEvent input) => Task.CompletedTask;
            }
            """;
        var compilation = GeneratorTestHarness.CreateCompilation(CreateReferences());
        var driver = GeneratorTestHarness.CreateDriver(
            ImmutableArray.Create<AdditionalText>(new InMemoryAdditionalText(projectDirectory + "/PortableEvents.viu", source)),
            "Demo", projectDirectory, emitHotReloadMetadata: "false");
        driver = driver.RunGeneratorsAndUpdateCompilation(compilation, out var consumerCompilation, out var diagnostics);

        diagnostics.ShouldBeEmpty();
        driver.GetRunResult().Results.ShouldHaveSingleItem().Diagnostics.ShouldBeEmpty();
        consumerCompilation.GetDiagnostics().Where(diagnostic => diagnostic.Severity is
            RoslynDiagnosticSeverity.Warning or RoslynDiagnosticSeverity.Error).ShouldBeEmpty();
        List<string> handlerTypes = [];
        foreach (SyntaxTree tree in consumerCompilation.SyntaxTrees)
        {
            SemanticModel model = consumerCompilation.GetSemanticModel(tree);
            foreach (InvocationExpressionSyntax invocation in tree.GetRoot().DescendantNodes().OfType<InvocationExpressionSyntax>())
            {
                if (model.GetSymbolInfo(invocation).Symbol is IMethodSymbol method
                    && method.Name == "Handler" && method.ContainingType.Name == "RenderGlue")
                {
                    handlerTypes.Add(method.ReturnType.ToDisplayString());
                }
            }
        }
        handlerTypes.Order().ShouldBe(new[]
        {
            "System.Action",
            "System.Action<Assimalign.Viu.Components.IElementEvent>",
            "System.Func<Assimalign.Viu.Components.IElementEvent, System.Threading.Tasks.Task>",
        }.Order());
    }

    private static IReadOnlyList<MetadataReference> CreateReferences()
    {
        return ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!)
            .Split(Path.PathSeparator)
            .Where(path => path.Length > 0
                && !Path.GetFileName(path).StartsWith("Assimalign.Viu.", StringComparison.OrdinalIgnoreCase)
                && !string.Equals(path, typeof(object).Assembly.Location, StringComparison.OrdinalIgnoreCase))
            .Concat([typeof(ComponentBase).Assembly.Location, typeof(Reactive).Assembly.Location])
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Select(path => (MetadataReference)MetadataReference.CreateFromFile(path))
            .ToArray();
    }
}
