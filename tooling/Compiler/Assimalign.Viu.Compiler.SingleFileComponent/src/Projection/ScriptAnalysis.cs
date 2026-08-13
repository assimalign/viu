namespace Assimalign.Viu.Compiler.SingleFileComponent;

/// <summary>
/// The result of analyzing an <c>@script</c> block ([V01.01.06.03]/[V01.01.06.03.01]): the
/// <see cref="Regions"/> to emit (the using-hoist + class-body member split) and the classified
/// <see cref="Bindings"/> the template compiler consumes for ref-unwrapping. Both are value-equatable, so
/// the analysis rides inside the incremental generator's cached model without defeating the cache.
/// </summary>
/// <param name="Regions">The using-hoist and class-body member regions to emit.</param>
/// <param name="Bindings">The classified top-level script members, for the template compiler's ref-unwrapping decisions.</param>
/// <param name="Declarations">The attribute-declared component parameters and events ([CMP-26], [CMP-30]) the scaffold synthesizes.</param>
public readonly record struct ScriptAnalysis(
    ScriptRegions Regions,
    EquatableArray<ScriptBinding> Bindings,
    ScriptDeclarations Declarations);
