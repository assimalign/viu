using System;
using System.Collections.Generic;

using Assimalign.Vue.Syntax.Templates;

namespace Assimalign.Vue.Syntax.Generators;

/// <summary>
/// The value-equatable description of the partial-class scaffold the generator emits for one <c>.viu</c>
/// component. Deliberately free of syntax nodes, symbols, and parser records so the incremental pipeline
/// caches on it: a <c>.viu</c> edit that re-parses to an equal descriptor shape re-emits nothing. The
/// block-presence counts summarize the parsed <see cref="Assimalign.Vue.Syntax.SingleFileComponent.SingleFileComponentDescriptor"/>
/// so the scaffold reflects what the composed parse produced; the render body itself is [V01.01.05.05]'s
/// output. The merged <c>@script</c> C# ([V01.01.06.03]) rides here as its verbatim
/// <see cref="ScriptContent"/> (emitted under a <c>#line</c> map anchored at
/// <see cref="ScriptContentStartLine"/>) plus the classified <see cref="Bindings"/> the template compiler
/// consumes for ref-unwrapping — all value-equatable, so an unchanged component still caches.
/// </summary>
/// <param name="Namespace">The containing namespace, or <see langword="null"/> for the global namespace.</param>
/// <param name="ClassName">The generated partial class name.</param>
/// <param name="FileName">The originating <c>.viu</c> leaf file name.</param>
/// <param name="HintName">The stable <c>AddSource</c> hint name.</param>
/// <param name="HasTemplate">Whether the component declares an <c>@template</c> block.</param>
/// <param name="HasScript">Whether the component declares an <c>@script</c> block.</param>
/// <param name="StyleCount">The number of <c>@style</c> blocks.</param>
/// <param name="CustomBlockCount">The number of custom blocks.</param>
/// <param name="FilePath">The originating <c>.viu</c> file path — the <c>#line</c> directive target that lands script errors and debugger stepping in the source.</param>
/// <param name="ScriptContent">The <c>@script</c> block's verbatim C# body to merge into the partial class, or <see langword="null"/> when the component declares no script.</param>
/// <param name="ScriptContentStartLine">The one-based <c>.viu</c> line where the script content begins (the <c>#line</c> anchor); <c>0</c> when there is no script.</param>
/// <param name="Bindings">The classified top-level script members, for the template compiler's ref-unwrapping decisions.</param>
internal readonly record struct SingleFileComponentModel(
    string? Namespace,
    string ClassName,
    string FileName,
    string HintName,
    bool HasTemplate,
    bool HasScript,
    int StyleCount,
    int CustomBlockCount,
    string FilePath,
    string? ScriptContent,
    int ScriptContentStartLine,
    EquatableArray<ScriptBinding> Bindings)
{
    /// <summary>
    /// Materializes the template compiler's <see cref="BindingMetadata"/> from the classified
    /// <see cref="Bindings"/>. This is the consumable form the render-code-generation path
    /// ([V01.01.05.04]/[V01.01.05.05]) reads to decide where a <c>Reference&lt;T&gt;.Value</c> unwrap
    /// belongs. <see cref="BindingMetadata.IsScriptSetup"/> is set whenever the component declares a
    /// script, mirroring Vue's <c>__isScriptSetup</c> for a <c>&lt;script setup&gt;</c> block.
    /// </summary>
    /// <returns>The binding metadata, or <see cref="BindingMetadata.Empty"/> for a scriptless component.</returns>
    public BindingMetadata ToBindingMetadata()
    {
        if (!HasScript)
        {
            return BindingMetadata.Empty;
        }

        if (Bindings.Count == 0)
        {
            return new BindingMetadata(isScriptSetup: true);
        }

        var map = new Dictionary<string, BindingType>(Bindings.Count, StringComparer.Ordinal);
        foreach (var binding in Bindings)
        {
            // A later declaration of the same name wins (e.g. method overloads share a name); an
            // illegal duplicate-member script is already surfaced as a script diagnostic.
            map[binding.Name] = binding.Type;
        }

        return new BindingMetadata(map, isScriptSetup: true);
    }
}

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
