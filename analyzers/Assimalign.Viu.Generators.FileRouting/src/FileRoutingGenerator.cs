using System;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;

namespace Assimalign.Viu.Generators.FileRouting;

/// <summary>
/// Emits an eager, declarative route table from the consumer's page additional files. The add-on
/// produces ordinary route records under <c>[RTR-1]</c> and <c>[RTR-7]</c>; it introduces no core
/// routing semantics or lazy assembly loading (<c>[RTR-8]</c>, <c>[RTR-11]</c>).
/// </summary>
/// <remarks>
/// Inputs are only additional-file paths and text plus the four relevant build properties. No
/// compilation, filesystem discovery, reflection, or runtime activation participates in generation.
/// Identical input values reuse the incremental pipeline across unrelated C# edits.
/// </remarks>
[Generator(LanguageNames.CSharp)]
public sealed class FileRoutingGenerator : IIncrementalGenerator
{
    /// <inheritdoc />
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var options = context.AnalyzerConfigOptionsProvider
            .Select(static (provider, _) => FileRoutingOptions.Read(provider.GlobalOptions))
            .WithTrackingName("FileRoutingOptions");

        var files = context.AdditionalTextsProvider
            .Where(static file => file.Path.EndsWith(".viu", StringComparison.OrdinalIgnoreCase)
                || file.Path.EndsWith(".vue", StringComparison.OrdinalIgnoreCase))
            .Combine(options)
            .Where(static pair => pair.Right.Enabled && pair.Right.Contains(pair.Left.Path))
            .Select(static (pair, cancellationToken) => new FileRoutingInput(
                FileRoutingOptions.NormalizePath(pair.Left.Path),
                pair.Left.GetText(cancellationToken)?.ToString() ?? string.Empty))
            .WithTrackingName("FileRoutingFiles");

        var output = files.Collect().Combine(options)
            .Select(static (pair, cancellationToken) => FileRoutingTable.Build(pair.Left, pair.Right, cancellationToken))
            .WithTrackingName("FileRoutingTable");

        context.RegisterSourceOutput(output, static (production, result) =>
        {
            foreach (var diagnostic in result.Diagnostics)
            {
                production.ReportDiagnostic(diagnostic);
            }

            if (result.Source is not null)
            {
                production.AddSource("Viu.FileRoutes.g.cs", SourceText.From(result.Source, System.Text.Encoding.UTF8));
            }
        });
    }
}
