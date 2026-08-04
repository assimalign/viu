using System;
using System.Collections.Generic;
using System.Threading;

using Assimalign.Viu.Syntax;
using Assimalign.Viu.Syntax.SingleFileComponent;
using Assimalign.Viu.Syntax.Templates;
using Assimalign.Viu.Tooling.Css;

namespace Assimalign.Viu.Tooling.SingleFileComponent;

/// <summary>
/// The shared <c>.viu</c>/<c>.vue</c> → C# projection facade ([V01.01.06.11]) — the ONE
/// implementation of parse → analyze → compile → model that the Assimalign.Viu.Generators.Syntax
/// source generator runs at build time and the Assimalign.Viu.Tooling.LanguageService runs in the editor, so
/// scaffold names, render mapping, and diagnostics can never drift between the two hosts. Specified by
/// <c>[TOOL-2]</c>. Everything host-specific stays outside: the generator
/// owns the incremental pipeline, file reads, hot-reload gating, and <c>.vue</c>-shadowing; the
/// language service owns request caching and LSP mapping. Both feed a value-equatable
/// <see cref="SingleFileComponentProjectionInput"/> and receive a value-equatable
/// <see cref="SingleFileComponentProjectionResult"/>.
/// </summary>
internal static class SingleFileComponentProjection
{
    // The composed .viu parser is stateless and recoverable, so one shared instance serves every parse. The
    // composition lives in the shared Tooling core ([V01.01.12.12]) so the ViuBundleCss task builds the
    // identical parser and reproduces this projection's ExtractedStyles byte-for-byte.
    private static readonly SingleFileComponentSyntaxParser Parser = SingleFileComponentParserFactory.Create();
    private static readonly VueSingleFileComponentSyntaxParser VueParser = SingleFileComponentParserFactory.CreateVue();

    /// <summary>
    /// Projects one single-file-component source into its scaffold model and mapped diagnostics —
    /// deterministically and host-neutrally, so the generator's emitted scaffold and the editor's
    /// understanding of the same source are identical by construction.
    /// </summary>
    /// <param name="input">The value-equatable source read to project.</param>
    /// <param name="cancellationToken">The token cancelling the projection.</param>
    /// <returns>The value-equatable projection result.</returns>
    public static SingleFileComponentProjectionResult Project(
        SingleFileComponentProjectionInput input,
        CancellationToken cancellationToken)
    {
        if (input.Format == SingleFileComponentFormat.Vue)
        {
            var parse = VueParser.ParseComponent(input.Text, cancellationToken);
            var diagnostics = new List<DiagnosticInfo>();
            AddParseDiagnostics(input, parse, diagnostics);

            SingleFileComponentScriptBlock? script = null;
            if (parse.Descriptor.Script is { } ordinaryScript
                && ValidateVueScriptLanguage(input, ordinaryScript, diagnostics))
            {
                script = ordinaryScript;
            }

            SingleFileComponentScriptBlock? scriptSetup = null;
            if (parse.Descriptor.ScriptSetup is { } authoredScriptSetup
                && ValidateVueScriptLanguage(input, authoredScriptSetup, diagnostics))
            {
                scriptSetup = authoredScriptSetup;
            }

            return BuildResult(
                input,
                parse,
                parse.Descriptor.Template,
                script,
                scriptSetup,
                parse.Descriptor.Styles.Count,
                parse.Descriptor.CustomBlocks.Count,
                diagnostics,
                CreateHotReloadMetadata(
                    input,
                    parse.Descriptor.Template,
                    parse.Descriptor.Script,
                    parse.Descriptor.ScriptSetup,
                    parse.Descriptor.Styles),
                cancellationToken);
        }

        var viuParse = Parser.ParseComponent(input.Text, cancellationToken);
        var viuDiagnostics = new List<DiagnosticInfo>();
        AddParseDiagnostics(input, viuParse, viuDiagnostics);
        return BuildResult(
            input,
            viuParse,
            viuParse.Descriptor.Template,
            viuParse.Descriptor.Script,
            scriptSetup: null,
            viuParse.Descriptor.Styles.Count,
            viuParse.Descriptor.CustomBlocks.Count,
            viuDiagnostics,
            CreateHotReloadMetadata(
                input,
                viuParse.Descriptor.Template,
                viuParse.Descriptor.Script,
                scriptSetup: null,
                viuParse.Descriptor.Styles),
            cancellationToken);
    }

    private static SingleFileComponentProjectionResult BuildResult(
        SingleFileComponentProjectionInput input,
        AggregateSyntaxParserResult<SingleFileComponentBlock> parse,
        SingleFileComponentTemplateBlock? template,
        SingleFileComponentScriptBlock? script,
        SingleFileComponentScriptBlock? scriptSetup,
        int styleCount,
        int customBlockCount,
        List<DiagnosticInfo> diagnostics,
        SingleFileComponentHotReloadMetadata? hotReloadMetadata,
        CancellationToken cancellationToken)
    {
        var scriptRegions = ScriptRegions.None;
        var scriptBindings = EquatableArray<ScriptBinding>.Empty;
        var scriptDeclarations = ScriptDeclarations.None;
        if (script is not null)
        {
            var analysis = ScriptBlockAnalyzer.Analyze(
                input.FilePath,
                script,
                diagnostics,
                reservesGeneratedMembers: template is not null);
            scriptRegions = analysis.Regions;
            scriptBindings = analysis.Bindings;
            scriptDeclarations = analysis.Declarations;
        }

        var scriptSetupRegions = ScriptRegions.None;
        var scriptSetupBindings = EquatableArray<ScriptBinding>.Empty;
        var scriptSetupDeclarations = ScriptDeclarations.None;
        if (scriptSetup is not null)
        {
            var analysis = ScriptBlockAnalyzer.Analyze(
                input.FilePath,
                scriptSetup,
                diagnostics,
                reservesGeneratedMembers: template is not null);
            scriptSetupRegions = analysis.Regions;
            scriptSetupBindings = analysis.Bindings;
            scriptSetupDeclarations = analysis.Declarations;
        }

        var bindings = MergeBindingsInSourceOrder(
            script,
            scriptBindings,
            scriptSetup,
            scriptSetupBindings);
        var hasScript = script is not null || scriptSetup is not null;
        var bindingMetadata = SingleFileComponentModel.BuildBindingMetadata(hasScript, bindings);

        var moduleClasses = new List<CssModuleClassEntry>();
        var cssVariableBindings = new List<CssVariableBindingEntry>();
        var moduleTemplateNames = new Dictionary<string, string>(StringComparer.Ordinal);
        var (scopeId, extractedStyles) = CompileStyles(
            input, parse, diagnostics, moduleClasses, cssVariableBindings, moduleTemplateNames, bindingMetadata, cancellationToken);

        var model = new SingleFileComponentModel(
            input.Namespace,
            input.ClassName,
            input.FileName,
            input.HintName,
            HasTemplate: template is not null,
            HasScript: hasScript,
            StyleCount: styleCount,
            CustomBlockCount: customBlockCount,
            FilePath: input.FilePath,
            Script: scriptRegions,
            Bindings: bindings,
            RenderBody: null,
            RenderCacheSize: 0,
            ScopeId: scopeId,
            ExtractedStyles: extractedStyles,
            ModuleClasses: moduleClasses.Count == 0
                ? EquatableArray<CssModuleClassEntry>.Empty
                : new EquatableArray<CssModuleClassEntry>(moduleClasses.ToArray()),
            CssVariableBindings: cssVariableBindings.Count == 0
                ? EquatableArray<CssVariableBindingEntry>.Empty
                : new EquatableArray<CssVariableBindingEntry>(cssVariableBindings.ToArray()))
        {
            Declarations = MergeDeclarations(scriptDeclarations, scriptSetupDeclarations),
            ScriptSetup = scriptSetupRegions,
            IsScriptSetupFirst =
                scriptSetup is not null &&
                script is not null &&
                scriptSetup.ContentLocation.Start.Offset < script.ContentLocation.Start.Offset,
            HotReloadMetadata = hotReloadMetadata,
        };

        var cssModules = BuildCssModuleAccessors(moduleClasses, moduleTemplateNames);
        var render = CompileRenderFunction(input, parse, bindingMetadata, cssModules, diagnostics, cancellationToken);
        model = model with { RenderBody = render.Body, RenderCacheSize = render.CacheSize };

        var array = diagnostics.Count == 0
            ? EquatableArray<DiagnosticInfo>.Empty
            : new EquatableArray<DiagnosticInfo>(diagnostics.ToArray());

        return new SingleFileComponentProjectionResult(model, array);
    }

    private static SingleFileComponentHotReloadMetadata? CreateHotReloadMetadata(
        SingleFileComponentProjectionInput input,
        SingleFileComponentTemplateBlock? template,
        SingleFileComponentScriptBlock? script,
        SingleFileComponentScriptBlock? scriptSetup,
        IReadOnlyList<SingleFileComponentStyleBlock> styles)
    {
        if (input.HotReloadComponentIdentifier is not { } componentIdentifier)
        {
            return null;
        }

        return SingleFileComponentHotReloadMetadataFactory.Create(
            componentIdentifier,
            template,
            script,
            scriptSetup,
            styles);
    }

    private static EquatableArray<ScriptBinding> MergeBindingsInSourceOrder(
        SingleFileComponentScriptBlock? script,
        EquatableArray<ScriptBinding> scriptBindings,
        SingleFileComponentScriptBlock? scriptSetup,
        EquatableArray<ScriptBinding> scriptSetupBindings)
    {
        if (scriptBindings.Count == 0)
        {
            return scriptSetupBindings;
        }

        if (scriptSetupBindings.Count == 0)
        {
            return scriptBindings;
        }

        var merged = new List<ScriptBinding>(scriptBindings.Count + scriptSetupBindings.Count);
        if (scriptSetup is not null &&
            (script is null || scriptSetup.ContentLocation.Start.Offset < script.ContentLocation.Start.Offset))
        {
            AppendBindings(merged, scriptSetupBindings);
            AppendBindings(merged, scriptBindings);
        }
        else
        {
            AppendBindings(merged, scriptBindings);
            AppendBindings(merged, scriptSetupBindings);
        }

        return new EquatableArray<ScriptBinding>(merged.ToArray());
    }

    private static void AppendBindings(
        ICollection<ScriptBinding> destination,
        EquatableArray<ScriptBinding> source)
    {
        foreach (var binding in source)
        {
            destination.Add(binding);
        }
    }

    // A tag-based .vue component may carry both a <script> and a <script setup> block, each analyzed on
    // its own; the attribute-declared surfaces ([CMP-26]/[CMP-30]) union here. A name declared in both
    // blocks keeps its first declaration, because two declarations of one name would make the runtime
    // parameter/event alias table throw at mount — the emitted surface must be valid by construction.
    private static ScriptDeclarations MergeDeclarations(
        ScriptDeclarations script,
        ScriptDeclarations scriptSetup)
    {
        if (scriptSetup.IsEmpty && !scriptSetup.DeclaresConstructor)
        {
            return script;
        }

        if (script.IsEmpty && !script.DeclaresConstructor)
        {
            return scriptSetup;
        }

        var parameters = new List<ComponentParameterDeclaration>(
            script.Parameters.Count + scriptSetup.Parameters.Count);
        var parameterNames = new HashSet<string>(StringComparer.Ordinal);
        foreach (var parameter in script.Parameters)
        {
            if (parameterNames.Add(parameter.Name))
            {
                parameters.Add(parameter);
            }
        }

        foreach (var parameter in scriptSetup.Parameters)
        {
            if (parameterNames.Add(parameter.Name))
            {
                parameters.Add(parameter);
            }
        }

        var events = new List<ComponentEventDeclaration>(
            script.Events.Count + scriptSetup.Events.Count);
        var eventNames = new HashSet<string>(StringComparer.Ordinal);
        foreach (var componentEvent in script.Events)
        {
            if (eventNames.Add(componentEvent.Name))
            {
                events.Add(componentEvent);
            }
        }

        foreach (var componentEvent in scriptSetup.Events)
        {
            if (eventNames.Add(componentEvent.Name))
            {
                events.Add(componentEvent);
            }
        }

        return new ScriptDeclarations(
            parameters.Count == 0
                ? EquatableArray<ComponentParameterDeclaration>.Empty
                : new EquatableArray<ComponentParameterDeclaration>(parameters.ToArray()),
            events.Count == 0
                ? EquatableArray<ComponentEventDeclaration>.Empty
                : new EquatableArray<ComponentEventDeclaration>(events.ToArray()),
            script.DeclaresRequiredMember || scriptSetup.DeclaresRequiredMember,
            script.DeclaresConstructor || scriptSetup.DeclaresConstructor);
    }

    private static void AddParseDiagnostics(
        SingleFileComponentProjectionInput input,
        AggregateSyntaxParserResult<SingleFileComponentBlock> parse,
        List<DiagnosticInfo> diagnostics)
    {
        foreach (var diagnostic in parse.Diagnostics)
        {
            diagnostics.Add(SingleFileComponentDiagnostics.Create(
                input.FilePath,
                diagnostic,
                fromTemplate: false,
                blockContentStart: null));
        }

        foreach (var sourceResult in parse.SourceResults)
        {
            var blockContentStart = sourceResult.Node.ContentLocation.Start;
            if (SingleFileComponentParserFactory.IsStyleBlock(sourceResult.Source))
            {
                foreach (var diagnostic in sourceResult.Result.Diagnostics)
                {
                    diagnostics.Add(SingleFileComponentDiagnostics.CreateStyle(input.FilePath, diagnostic, blockContentStart));
                }

                continue;
            }

            var fromTemplate = SingleFileComponentParserFactory.IsTemplateBlock(sourceResult.Source);
            foreach (var diagnostic in sourceResult.Result.Diagnostics)
            {
                diagnostics.Add(SingleFileComponentDiagnostics.Create(input.FilePath, diagnostic, fromTemplate, blockContentStart));
            }
        }
    }

    private static bool ValidateVueScriptLanguage(
        SingleFileComponentProjectionInput input,
        SingleFileComponentScriptBlock script,
        List<DiagnosticInfo> diagnostics)
    {
        if (string.Equals(script.Lang, "csharp", StringComparison.Ordinal))
        {
            return true;
        }

        var languageOption = FindOption(script, "lang");
        var authoredLanguage = script.Lang is null ? "an implicit JavaScript language" : $"'{script.Lang}'";
        diagnostics.Add(SingleFileComponentDiagnostics.CreateRule(
            SingleFileComponentDiagnostics.UnsupportedScriptLanguage,
            $"The .vue script uses {authoredLanguage}. Viu accepts only an explicit lang=\"csharp\" " +
            "attribute and never executes JavaScript.",
            input.FilePath,
            languageOption?.Location ?? OpeningTagLocation(input.Text, script)));
        return false;
    }

    private static SingleFileComponentBlockOption? FindOption(
        SingleFileComponentBlock block,
        string name)
    {
        foreach (var option in block.Options)
        {
            if (string.Equals(option.Name, name, StringComparison.Ordinal))
            {
                return option;
            }
        }

        return null;
    }

    private static SourceLocation OpeningTagLocation(string source, SingleFileComponentBlock block)
    {
        var start = block.Location.Start;
        var end = block.ContentLocation.Start;
        return new SourceLocation(
            start,
            end,
            source.Substring(start.Offset, end.Offset - start.Offset));
    }

    /// <summary>
    /// Compiles the dispatched template parse into the C# render-method body ([V01.01.05.05]):
    /// the registered <see cref="TemplateSyntaxParser"/>'s AST runs through <see cref="Transformer"/>
    /// under <c>PrefixIdentifiers</c> (with <paramref name="bindingMetadata"/> resolving identifier
    /// classifications) and the result is serialized by <see cref="RenderFunctionEmitter"/>. Transform
    /// diagnostics surface on the <c>.viu</c> file exactly like dispatched parse diagnostics.
    /// </summary>
    private static (string? Body, int CacheSize) CompileRenderFunction(
        SingleFileComponentProjectionInput input,
        AggregateSyntaxParserResult<SingleFileComponentBlock> parse,
        BindingMetadata bindingMetadata,
        CssModuleAccessors cssModules,
        List<DiagnosticInfo> diagnostics,
        CancellationToken cancellationToken)
    {
        foreach (var sourceResult in parse.SourceResults)
        {
            if (!SingleFileComponentParserFactory.IsTemplateBlock(sourceResult.Source) ||
                sourceResult.Result is not TemplateSyntaxParserResult templateResult)
            {
                continue;
            }

            cancellationToken.ThrowIfCancellationRequested();

            var blockContentStart = sourceResult.Node.ContentLocation.Start;
            var transformOptions = TransformOptions.CreateDom();
            transformOptions.PrefixIdentifiers = true;
            transformOptions.BindingMetadata = bindingMetadata;
            // CSS Modules accessors ([V01.01.05.04.01]): resolve `$style.<class>` (and named-module) references
            // against the emitted accessor class. The map is complete (every declared class), so an access to an
            // undeclared member is reported on the .viu coordinate.
            transformOptions.CssModules = cssModules;
            // Static caching and stringification ([V01.01.05.07]): fully static subtrees are cached and long
            // static runs collapse to innerHTML string inserts, cutting per-node JS-interop round-trips on
            // WASM. Deterministic and value-equatable, so the incremental-generator cache is preserved.
            transformOptions.HoistStatic = true;
            // CacheHandlers stays off: the cached member-expression wrapper form has no C# spelling yet;
            // handler caching is runtime-binding follow-up work. v-once caching is independent of this
            // switch and fully emitted.
            transformOptions.OnError = error => diagnostics.Add(
                SingleFileComponentDiagnostics.Create(input.FilePath, error, fromTemplate: true, blockContentStart));

            var transformed = Transformer.Transform(templateResult.Root, transformOptions);
            var emitted = RenderFunctionEmitter.Emit(transformed, new RenderFunctionEmitterOptions
            {
                // namespace + class + method-body nesting, or class + method-body without a namespace.
                IndentLevel = string.IsNullOrEmpty(input.Namespace) ? 2 : 3,
            });

            // [V01.01.05.08] Inject #line span directives over the emitted render body so a C# error inside
            // a template expression (an unresolved member under permissive metadata, for example) resolves
            // to the offending .viu template line/column rather than to the generated file — the
            // render-body analogue of the @script merge's #line map. The mapper reuses the same
            // block-to-file composition the dispatched diagnostics use.
            var body = RenderBodySourceMapper.Inject(
                emitted.Code, emitted.SourceMappings, blockContentStart, input.FilePath);

            // The first template block is the component's template (the descriptor carries one).
            return (body, emitted.CacheSlotCount);
        }

        return (null, 0);
    }

    /// <summary>
    /// Compiles the dispatched style parses. The compilation itself lives in the shared Tooling
    /// core ([V01.01.12.12], <see cref="SingleFileComponentStyleCompiler"/>) — the SAME deterministic
    /// method the <c>ViuBundleCss</c> MSBuild task calls over the same <c>.viu</c> inputs, which is what
    /// keeps the emitted <c>ExtractedStyles</c> constant and the physical CSS bundle byte-identical
    /// (docs/UTILITY-CSS-DESIGN.md §2.4). The projection layers the compile-only concerns on top: the
    /// template-facing module-name map ([V01.01.05.04.01]) and the [V01.01.06.06.01] binding-metadata
    /// rewrite of each <c>v-bind()</c> expression, whose diagnostics compose onto exact <c>.viu</c> style
    /// coordinates. The scope id is returned only when a <c>scoped</c> block is declared.
    /// </summary>
    private static (string? ScopeId, string? ExtractedStyles) CompileStyles(
        SingleFileComponentProjectionInput input,
        AggregateSyntaxParserResult<SingleFileComponentBlock> parse,
        List<DiagnosticInfo> diagnostics,
        List<CssModuleClassEntry> moduleClasses,
        List<CssVariableBindingEntry> cssVariableBindings,
        Dictionary<string, string> moduleTemplateNames,
        BindingMetadata bindingMetadata,
        CancellationToken cancellationToken)
    {
        var compilation = SingleFileComponentStyleCompiler.Compile(parse, input.ScopeId, cancellationToken);

        foreach (var moduleClass in compilation.ModuleClasses)
        {
            moduleTemplateNames[moduleClass.Accessor] = ModuleTemplateName(moduleClass.Module);
            moduleClasses.Add(new CssModuleClassEntry(moduleClass.Accessor, moduleClass.Original, moduleClass.Hashed));
        }

        foreach (var variableBinding in compilation.VariableBindings)
        {
            // [V01.01.06.06.01] Route the extracted expression through the template compiler's
            // binding-metadata rewriting (instance-member mode), so `v-bind(count)` unwraps a script
            // IReactiveReference<T> member to `count.Value` automatically, so a CSS binding reads exactly
            // like a template binding.
            // A malformed expression surfaces its diagnostics on the exact .viu style coordinate through
            // the same style-origin envelope the CSS parse diagnostics use.
            var compiled = TemplateExpressionCompiler.CompileInstanceExpression(
                variableBinding.Binding.Expression, bindingMetadata, variableBinding.Binding.Location);
            foreach (var error in compiled.Diagnostics)
            {
                diagnostics.Add(SingleFileComponentDiagnostics.CreateStyle(
                    input.FilePath, error, variableBinding.BlockContentStart));
            }

            cssVariableBindings.Add(new CssVariableBindingEntry(variableBinding.Binding.Name, compiled.Code));
        }

        foreach (var styleDiagnostic in compilation.Diagnostics)
        {
            diagnostics.Add(SingleFileComponentDiagnostics.CreateStyle(
                input.FilePath, styleDiagnostic.Diagnostic, styleDiagnostic.BlockContentStart));
        }

        return (compilation.ScopeId, compilation.ExtractedStyles);
    }

    // The template spelling of a `module` option ([V01.01.05.04.01]): the default (valueless `module`) is
    // `$style`; a named module is referenced by its authored name (`<style module="theme">` -> `theme`).
    private static string ModuleTemplateName(string? moduleName)
        => string.IsNullOrEmpty(moduleName) ? "$style" : moduleName!;

    // Projects the collected CSS module class map into the CssModuleAccessors the template compiler consumes
    // ([V01.01.05.04.01]): entries grouped by accessor class, each carrying its template spelling, its parse
    // identifier (the `$`->`_` form of `$style`, length-preserving so expression offsets are unchanged), and
    // the sanitized member names the emitter writes as consts. ReportsUnknownMembers is enabled because this
    // map is complete — every declared class — so an access to an undeclared member is decidably wrong.
    private static CssModuleAccessors BuildCssModuleAccessors(
        List<CssModuleClassEntry> moduleClasses,
        Dictionary<string, string> moduleTemplateNames)
    {
        if (moduleClasses.Count == 0)
        {
            return CssModuleAccessors.Empty;
        }

        var membersByAccessor = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        var order = new List<string>();
        foreach (var entry in moduleClasses)
        {
            if (!membersByAccessor.TryGetValue(entry.Accessor, out var members))
            {
                members = new List<string>();
                membersByAccessor[entry.Accessor] = members;
                order.Add(entry.Accessor);
            }

            var member = SingleFileComponentSourceEmitter.MemberName(entry.Original);
            if (!members.Contains(member))
            {
                members.Add(member);
            }
        }

        var accessors = new List<CssModuleAccessor>(order.Count);
        foreach (var accessorClass in order)
        {
            var templateName = moduleTemplateNames.TryGetValue(accessorClass, out var name) ? name : accessorClass;
            accessors.Add(new CssModuleAccessor(
                templateName,
                ModuleParseIdentifier(templateName),
                accessorClass,
                membersByAccessor[accessorClass]));
        }

        return new CssModuleAccessors(accessors, reportsUnknownMembers: true);
    }

    // The C#-parseable spelling of a template accessor name: `$style` -> `_style` (`$` is illegal in a C#
    // identifier), every other name unchanged. Length-preserving so expression offsets — and remapped
    // diagnostics — are not shifted.
    private static string ModuleParseIdentifier(string templateName)
        => templateName.Length > 0 && templateName[0] == '$'
            ? "_" + templateName.Substring(1)
            : templateName;
}
