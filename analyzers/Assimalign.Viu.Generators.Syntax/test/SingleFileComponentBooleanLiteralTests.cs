using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;

using Microsoft.CodeAnalysis;

using Shouldly;
using Xunit;

using Assimalign.Viu.Components;
using Assimalign.Viu.Reactivity;
using Assimalign.Viu.Router;

using RoslynDiagnosticSeverity = Microsoft.CodeAnalysis.DiagnosticSeverity;

namespace Assimalign.Viu.Generators.Syntax.Tests;

/// <summary>
/// Pins literal expression emission for referenced and generated components and native elements.
/// Boolean arguments retain their literal value under <c>[SFC-6]</c> ([V01.01.05.04.02]).
/// </summary>
public sealed class SingleFileComponentBooleanLiteralTests
{
    private const string ProjectDirectory = "C:/proj";

    [Theory]
    [InlineData("<RouterLink to=\"/\" :replace=\"true\">x</RouterLink>", "replace", "true")]
    [InlineData("<RouterLink to=\"/\" :replace=\"false\">x</RouterLink>", "replace", "false")]
    [InlineData("<BooleanTarget :replace=\"true\" />", "replace", "true")]
    [InlineData("<BooleanTarget :replace=\"false\" />", "replace", "false")]
    [InlineData("<button :disabled=\"true\" />", "disabled", "true")]
    [InlineData("<button :disabled=\"false\" />", "disabled", "false")]
    public void BoundBooleanLiteral_ComponentOrNativeElement_CompilesWithoutWarningsOrErrors(
        string usage,
        string argumentName,
        string literal)
    {
        // [V01.01.05.04.02] RouterLink comes from real Router metadata. BooleanTarget exercises the
        // generated [Parameter] contract in the same compilation, and the native cases share binding.
        var compilation = GeneratorTestHarness.CreateCompilation(CreateReferences());
        var consumer = new InMemoryAdditionalText(
            $"{ProjectDirectory}/BooleanLiteralConsumer.viu",
            "<template>\n    " + usage + "\n</template>\n");
        var target = new InMemoryAdditionalText(
            $"{ProjectDirectory}/BooleanTarget.viu",
            """
            <template><span /></template>
            @script {
                using Assimalign.Viu.Components;

                [Parameter] public bool Replace { get; set; }
            }
            """);
        var driver = GeneratorTestHarness.CreateDriver(
            ImmutableArray.Create<AdditionalText>(consumer, target),
            "Demo",
            ProjectDirectory,
            emitHotReloadMetadata: "false");

        driver = driver.RunGeneratorsAndUpdateCompilation(
            compilation,
            out var consumerCompilation,
            out var diagnostics);

        diagnostics.ShouldBeEmpty();
        var result = driver.GetRunResult().Results.ShouldHaveSingleItem();
        result.Diagnostics.ShouldBeEmpty();
        consumerCompilation.GetDiagnostics()
            .Where(diagnostic => diagnostic.Severity is RoslynDiagnosticSeverity.Warning
                or RoslynDiagnosticSeverity.Error)
            .ShouldBeEmpty();
        var generated = result.GeneratedSources
            .Single(source => source.HintName.EndsWith(
                "BooleanLiteralConsumer.SingleFileComponent.g.cs",
                StringComparison.Ordinal))
            .SourceText.ToString();
        generated.ShouldContain("[\"" + argumentName + "\"] = " + literal);
        generated.ShouldNotContain("component." + literal);
    }

    private static IReadOnlyList<MetadataReference> CreateReferences()
    {
        var paths = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!)
            .Split(Path.PathSeparator)
            .Where(path => path.Length > 0
                && !Path.GetFileName(path).StartsWith("Assimalign.Viu.", StringComparison.OrdinalIgnoreCase)
                && !string.Equals(path, typeof(object).Assembly.Location, StringComparison.OrdinalIgnoreCase))
            .Concat(
            [
                typeof(ComponentBase).Assembly.Location,
                typeof(Reactive).Assembly.Location,
                typeof(RouterLink).Assembly.Location,
            ]);

        return paths.Select(path => (MetadataReference)MetadataReference.CreateFromFile(path)).ToArray();
    }
}
