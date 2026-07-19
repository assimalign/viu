using System;
using System.Collections.Generic;

using Assimalign.Viu.Syntax.Templates;

namespace Assimalign.Viu.Syntax.Generators;

/// <summary>
/// The value-equatable description of the partial-class scaffold the generator emits for one <c>.viu</c>
/// component. Deliberately free of syntax nodes, symbols, and parser records so the incremental pipeline
/// caches on it: a <c>.viu</c> edit that re-parses to an equal descriptor shape re-emits nothing. The
/// block-presence counts summarize the parsed <see cref="Assimalign.Viu.Syntax.SingleFileComponent.SingleFileComponentDescriptor"/>
/// so the scaffold reflects what the composed parse produced; the render body itself is [V01.01.05.05]'s
/// output. The merged <c>@script</c> C# ([V01.01.06.03]/[V01.01.06.03.01]) rides here as its
/// <see cref="Script"/> regions — the hoisted using directives and the class-body members, each emitted
/// under its own <c>#line</c> map — plus the classified <see cref="Bindings"/> the template compiler
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
/// <param name="Script">The <c>@script</c> block's two emission regions — hoisted usings and class-body members, each with its <c>#line</c> anchor; <see cref="ScriptRegions.None"/> when the component declares no script.</param>
/// <param name="Bindings">The classified top-level script members, for the template compiler's ref-unwrapping decisions.</param>
/// <param name="RenderBody">
/// The compiled <c>@template</c> render-method body emitted by the template compiler's
/// <c>RenderFunctionEmitter</c> ([V01.01.05.05]), pre-indented for the render method's nesting depth,
/// or <see langword="null"/> when the component has no <c>@template</c> block. A plain string, so the
/// pipeline stays value-equatable.
/// </param>
/// <param name="RenderCacheSize">
/// The number of <c>_cache</c> slots the render function uses (<c>v-once</c> subtrees and cached
/// handlers); surfaced as a generated constant because C# arrays cannot grow on assignment the way
/// upstream's JavaScript render cache does.
/// </param>
/// <param name="ScopeId">
/// The scoped-CSS scope id (<c>data-v-&lt;hash&gt;</c>) when the component declares at least one
/// <c>scoped</c> <c>@style</c> block, otherwise <see langword="null"/> ([V01.01.06.04]). Emitted as a
/// generated constant so the renderer can stamp the matching <c>data-v-&lt;hash&gt;</c> attribute on the
/// component's elements — the scope-id propagation contract the runtime side implements.
/// </param>
/// <param name="ExtractedStyles">
/// The component's compiled CSS — scoped <c>@style</c> blocks rewritten with <see cref="ScopeId"/> and
/// non-scoped blocks passed through unmodified, concatenated in source order — or <see langword="null"/>
/// when the component declares no <c>@style</c> block. Emitted as a generated string constant; the
/// physical static-web-asset bundling is the MSBuild-side follow-up.
/// </param>
/// <param name="ModuleClasses">
/// The CSS Modules class map ([V01.01.06.06]) — one entry per <c>original → hashed</c> class, grouped by
/// its accessor — emitted as the typed <c>$style</c>-equivalent nested class(es). Empty when no
/// <c>@style module</c> block is declared.
/// </param>
/// <param name="CssVariableBindings">
/// The <c>v-bind()</c> CSS bindings ([V01.01.06.06]) — one entry per distinct <c>(hash, expression)</c> —
/// emitted as the <c>ApplyCssVariables</c> seam the <c>UseCssVars</c> runtime consumes. Empty when no
/// <c>@style</c> block uses <c>v-bind()</c>.
/// </param>
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
    ScriptRegions Script,
    EquatableArray<ScriptBinding> Bindings,
    string? RenderBody,
    int RenderCacheSize,
    string? ScopeId,
    string? ExtractedStyles,
    EquatableArray<CssModuleClassEntry> ModuleClasses,
    EquatableArray<CssVariableBindingEntry> CssVariableBindings)
{
    /// <summary>
    /// Materializes the template compiler's <see cref="BindingMetadata"/> from the classified
    /// <see cref="Bindings"/>. This is the consumable form the render-code-generation path
    /// ([V01.01.05.04]/[V01.01.05.05]) reads to decide where a <c>Reference&lt;T&gt;.Value</c> unwrap
    /// belongs. <see cref="BindingMetadata.IsScriptSetup"/> is set whenever the component declares a
    /// script, mirroring Vue's <c>__isScriptSetup</c> for a <c>&lt;script setup&gt;</c> block.
    /// </summary>
    /// <returns>The binding metadata, or <see cref="BindingMetadata.Empty"/> for a scriptless component.</returns>
    public BindingMetadata ToBindingMetadata() => BuildBindingMetadata(HasScript, Bindings);

    /// <summary>
    /// Builds the template compiler's <see cref="BindingMetadata"/> from the classified script bindings — the
    /// standalone form the generator needs before the model exists (the <c>v-bind()</c> CSS compile
    /// [V01.01.06.06.01] rewrites its expressions with the same metadata the render path uses).
    /// </summary>
    /// <param name="hasScript">Whether the component declares a script block.</param>
    /// <param name="bindings">The classified script bindings.</param>
    /// <returns>The binding metadata, or <see cref="BindingMetadata.Empty"/> for a scriptless component.</returns>
    public static BindingMetadata BuildBindingMetadata(bool hasScript, EquatableArray<ScriptBinding> bindings)
    {
        if (!hasScript)
        {
            return BindingMetadata.Empty;
        }

        if (bindings.Count == 0)
        {
            return new BindingMetadata(isScriptSetup: true);
        }

        var map = new Dictionary<string, BindingType>(bindings.Count, StringComparer.Ordinal);
        foreach (var binding in bindings)
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
