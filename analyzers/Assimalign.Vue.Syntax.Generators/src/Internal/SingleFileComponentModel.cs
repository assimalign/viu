namespace Assimalign.Vue.Syntax.Generators;

/// <summary>
/// The value-equatable description of the partial-class scaffold the generator emits for one <c>.viu</c>
/// component. Deliberately free of syntax nodes, symbols, and parser records so the incremental pipeline
/// caches on it: a <c>.viu</c> edit that re-parses to an equal descriptor shape re-emits nothing. The
/// block-presence counts summarize the parsed <see cref="Assimalign.Vue.Syntax.SingleFileComponent.SingleFileComponentDescriptor"/>
/// so the scaffold reflects what the composed parse produced — the render body itself is
/// [V01.01.05.05]'s output and the merged <c>@script</c> C# is [V01.01.06.03]'s.
/// </summary>
/// <param name="Namespace">The containing namespace, or <see langword="null"/> for the global namespace.</param>
/// <param name="ClassName">The generated partial class name.</param>
/// <param name="FileName">The originating <c>.viu</c> leaf file name.</param>
/// <param name="HintName">The stable <c>AddSource</c> hint name.</param>
/// <param name="HasTemplate">Whether the component declares an <c>@template</c> block.</param>
/// <param name="HasScript">Whether the component declares an <c>@script</c> block.</param>
/// <param name="StyleCount">The number of <c>@style</c> blocks.</param>
/// <param name="CustomBlockCount">The number of custom blocks.</param>
internal readonly record struct SingleFileComponentModel(
    string? Namespace,
    string ClassName,
    string FileName,
    string HintName,
    bool HasTemplate,
    bool HasScript,
    int StyleCount,
    int CustomBlockCount);

/// <summary>
/// One <c>.viu</c> file's pipeline result: the scaffold model to emit (always present — the descriptor
/// is produced even for malformed input) and the mapped diagnostics to report. Both are value-equatable
/// so the pipeline stays cacheable.
/// </summary>
/// <param name="Model">The scaffold model to emit.</param>
/// <param name="Diagnostics">The mapped Roslyn diagnostics to report.</param>
internal readonly record struct SingleFileComponentGeneratorResult(
    SingleFileComponentModel Model,
    EquatableArray<DiagnosticInfo> Diagnostics);
