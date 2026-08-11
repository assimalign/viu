using System;
using System.Collections.Generic;

using Assimalign.Viu.Syntax.Templates;

namespace Assimalign.Viu.Compiler.SingleFileComponent;

/// <summary>
/// The value-equatable description of the partial-class scaffold emitted for one <c>.viu</c>
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
/// <param name="HasTemplate">Whether the component declares a template block.</param>
/// <param name="HasScript">Whether the component declares an <c>@script</c> block.</param>
/// <param name="StyleCount">The number of style blocks.</param>
/// <param name="CustomBlockCount">The number of custom blocks.</param>
/// <param name="FilePath">The originating <c>.viu</c> file path — the <c>#line</c> directive target that lands script errors and debugger stepping in the source.</param>
/// <param name="Script">The <c>@script</c> block's two emission regions — hoisted usings and class-body members, each with its <c>#line</c> anchor; <see cref="ScriptRegions.None"/> when the component declares no script.</param>
/// <param name="Bindings">The classified top-level script members, for the template compiler's ref-unwrapping decisions.</param>
/// <param name="RenderBody">
/// The compiled template render-method body emitted by the template compiler's
/// <c>RenderFunctionEmitter</c> ([V01.01.05.05]), pre-indented for the render method's nesting depth,
/// or <see langword="null"/> when the component has no template block. A plain string, so the
/// pipeline stays value-equatable.
/// </param>
/// <param name="RenderCacheSize">
/// The exact number of per-mount cache slots the frame-based render function uses. Emitted through
/// <c>ComponentContract.RenderCacheSize</c> so Core creates the mount-owned frame at the compiled size.
/// </param>
/// <param name="ScopeId">
/// The scoped-CSS scope id (<c>data-v-&lt;hash&gt;</c>) when the component declares at least one
/// <c>scoped</c> style block, otherwise <see langword="null"/> ([V01.01.06.04]). The template transform
/// stamps this identifier on every emitted native element, keeping client and server output aligned.
/// </param>
/// <param name="ExtractedStyles">
/// The component's compiled CSS — scoped style blocks rewritten with <see cref="ScopeId"/> and
/// non-scoped blocks passed through unmodified, concatenated in source order — or <see langword="null"/>
/// when the component declares no style block. Emitted as a generated string constant; the
/// physical static-web-asset bundling is the MSBuild-side follow-up.
/// </param>
/// <param name="ModuleClasses">
/// The CSS Modules class map ([V01.01.06.06]) — one entry per <c>original → hashed</c> class, grouped by
/// its accessor — emitted as the typed <c>$style</c>-equivalent nested class(es). Empty when no
/// <c>module</c> style block is declared.
/// </param>
/// <param name="CssVariableBindings">
/// The <c>v-bind()</c> CSS bindings ([V01.01.06.06]) — one entry per distinct <c>(hash, expression)</c> —
/// retained for parsing and diagnostics while runtime CSS-variable application is deferred. Empty when
/// no style block uses <c>v-bind()</c>.
/// </param>
public readonly record struct SingleFileComponentModel(
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
    /// The compiled direct-markup render body when the project declares server rendering, or
    /// <see langword="null"/> for client-only projects and renderless components. The body is emitted
    /// alongside, never instead of, <see cref="RenderBody"/> so a dual-target project retains one
    /// deterministic client profile and one deterministic server profile. Specified by
    /// <c>[SSR-TARGET-2]</c>.
    /// </summary>
    public string? ServerRenderBody { get; init; }

    /// <summary>
    /// The tag-based C# <c>&lt;script setup&gt;</c> block's emission regions. Viu treats this explicitly
    /// C# block as partial-class member shorthand; JavaScript setup macros and runtime evaluation remain
    /// unsupported. <see cref="ScriptRegions.None"/> for canonical <c>.viu</c> files and tag-based
    /// components without a setup script.
    /// </summary>
    public ScriptRegions ScriptSetup { get; init; }

    /// <summary>
    /// Whether the tag-based setup script precedes the ordinary script in the authored source. The
    /// emitter preserves this order because C# field-initializer order is observable.
    /// </summary>
    public bool IsScriptSetupFirst { get; init; }

    /// <summary>
    /// The [V01.01.06.05] development identity and independent block hashes, or
    /// <see langword="null"/> when Debug metadata emission is disabled.
    /// </summary>
    public SingleFileComponentHotReloadMetadata? HotReloadMetadata { get; init; }

    /// <summary>
    /// The attribute-declared component surface merged from every script block ([CMP-26], [CMP-30]):
    /// the <c>[Parameter]</c> properties and <c>[Event]</c> methods the scaffold turns into the
    /// partial's <c>ComponentContract.Parameters</c>/<c>Events</c>, the per-render argument
    /// assignment, and the typed emit implementations. <see cref="ScriptDeclarations.None"/> for a
    /// component that declares its surface imperatively or not at all — the form that keeps compiling
    /// unchanged [CMP-31].
    /// </summary>
    public ScriptDeclarations Declarations { get; init; }

    /// <summary>
    /// Materializes the template compiler's <see cref="BindingMetadata"/> from the classified
    /// <see cref="Bindings"/>. This is the consumable form the render-code-generation path
    /// ([V01.01.05.04]/[V01.01.05.05]) reads to decide where a <c>Reference&lt;T&gt;.Value</c> unwrap
    /// belongs. <see cref="BindingMetadata.IsScriptSetup"/> is set whenever the component declares a
    /// script block at all, because a Viu component has exactly one script slot.
    /// </summary>
    /// <returns>The binding metadata, or <see cref="BindingMetadata.Empty"/> for a scriptless component.</returns>
    public BindingMetadata ToBindingMetadata() => BuildBindingMetadata(HasScript, Bindings);

    /// <summary>
    /// Builds the template compiler's <see cref="BindingMetadata"/> from the classified script bindings — the
    /// standalone form the projection needs before the model exists (the <c>v-bind()</c> CSS compile
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
