using System;
using System.Collections.Generic;
using System.Collections.Immutable;

using Microsoft.CodeAnalysis;

using Assimalign.Viu.Syntax;
using Assimalign.Viu.Syntax.SingleFileComponent;
using Assimalign.Viu.Syntax.Templates;
using Assimalign.Viu.Tooling.Css;

namespace Assimalign.Viu.Generators.Syntax;

/// <summary>
/// The incremental source generator for canonical <c>.viu</c> and compatible tag-based <c>.vue</c>
/// single-file components — the composition root of the
/// <c>Assimalign.Viu.Syntax.*</c> cluster ([V01.01.06.02]). It is the C# analog of consuming
/// <c>@vue/compiler-sfc</c> through <c>@vitejs/plugin-vue</c>: MSBuild flows every <c>.viu</c> file in as
/// an <see cref="AdditionalText"/>, this generator parses each one with the composed
/// <see cref="SingleFileComponentParserFactory">block + template parser</see>, and emits a partial
/// class scaffold per component. WASM has no runtime <c>new Function</c> template compilation, so
/// build-time generation is Viu's only compilation path.
/// <para>
/// Every pipeline stage's input and output is a value-equatable record with no <c>AdditionalText</c>,
/// <c>SourceText</c>, <c>Compilation</c>, or <c>ISymbol</c> captured, so editing one <c>.viu</c> file
/// re-parses and re-emits only that file and untouched files stay cached — the contract the incremental
/// tests pin. The scaffold body is intentionally a shell: the render function is [V01.01.05.05]'s output
/// and the merged <c>@script</c> C# is [V01.01.06.03]'s; this generator emits their seams only.
/// </para>
/// <para>
/// For Debug builds, or when explicitly enabled by <c>ViuEmitHotReloadMetadata</c>, the generator also
/// emits [V01.01.06.05]'s path-stable component identity, independent template/script/style hashes, and
/// one consumer-assembly <c>MetadataUpdateHandler</c> that forwards applied deltas to Core. Ordinary
/// Release builds omit that entire development surface.
/// </para>
/// </summary>
[Generator(LanguageNames.CSharp)]
public sealed class SingleFileComponentGenerator : IIncrementalGenerator
{
    private const string ViuExtension = ".viu";
    private const string VueExtension = ".vue";
    private const string RootNamespaceProperty = "build_property.RootNamespace";
    private const string ProjectDirectoryProperty = "build_property.ProjectDir";
    private const string ConfigurationProperty = "build_property.Configuration";
    private const string EmitHotReloadMetadataProperty = "build_property.ViuEmitHotReloadMetadata";

    /// <summary>Pipeline step tracking name for the file-read transform (used by incremental-cache tests).</summary>
    public const string FileTrackingName = "SingleFileComponentFile";

    /// <summary>Pipeline step tracking name for the parse-and-model transform (used by incremental-cache tests).</summary>
    public const string ModelTrackingName = "SingleFileComponentModel";

    // The composed .viu parser is stateless and recoverable, so one shared instance serves every parse. The
    // composition lives in the shared Tooling core ([V01.01.12.12]) so the ViuBundleCss task builds the
    // identical parser and reproduces this generator's ExtractedStyles byte-for-byte.
    private static readonly SingleFileComponentSyntaxParser Parser = SingleFileComponentParserFactory.Create();
    private static readonly VueSingleFileComponentSyntaxParser VueParser = SingleFileComponentParserFactory.CreateVue();

    /// <inheritdoc />
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        // Extract only the two project properties the names depend on into a value-equatable record, so
        // an unrelated config change does not invalidate the cache.
        var projectOptions = context.AnalyzerConfigOptionsProvider.Select(static (provider, _) =>
        {
            provider.GlobalOptions.TryGetValue(RootNamespaceProperty, out var rootNamespace);
            provider.GlobalOptions.TryGetValue(ProjectDirectoryProperty, out var projectDirectory);
            provider.GlobalOptions.TryGetValue(ConfigurationProperty, out var configuration);
            provider.GlobalOptions.TryGetValue(EmitHotReloadMetadataProperty, out var emitHotReloadMetadata);
            return new ProjectOptions(
                rootNamespace,
                projectDirectory,
                ShouldEmitHotReloadMetadata(configuration, emitHotReloadMetadata));
        });

        var canonicalFileKeys = context.AdditionalTextsProvider
            .Where(static text => IsViuFile(text.Path))
            .Select(static (text, _) => ComponentBasePath(text.Path))
            .Collect();

        var files = context.AdditionalTextsProvider
            .Where(static text => IsSingleFileComponentFile(text.Path))
            .Combine(projectOptions)
            .Combine(canonicalFileKeys)
            .Select(static (pair, cancellationToken) => ReadFile(
                pair.Left.Left,
                pair.Left.Right,
                pair.Right,
                cancellationToken))
            .WithTrackingName(FileTrackingName);

        var results = files
            .Where(static file => !file.HasCanonicalPeer)
            .Select(static (file, cancellationToken) => BuildResult(file, cancellationToken))
            .WithTrackingName(ModelTrackingName);

        var collisions = files
            .Where(static file => file.HasCanonicalPeer)
            .Select(static (file, _) => SingleFileComponentDiagnostics.CreateFileRule(
                SingleFileComponentDiagnostics.ConflictingComponentFormats,
                "The compatibility .vue source is shadowed by a same-directory, same-base .viu source. " +
                "The canonical .viu component takes precedence.",
                file.FilePath));

        context.RegisterSourceOutput(results, static (production, result) => Execute(production, result));
        context.RegisterSourceOutput(collisions, static (production, diagnostic) =>
            production.ReportDiagnostic(diagnostic.ToDiagnostic()));
        context.RegisterSourceOutput(
            projectOptions,
            static (production, options) =>
            {
                if (options.EmitHotReloadMetadata)
                {
                    production.AddSource(
                        SingleFileComponentHotReloadHandlerEmitter.HintName,
                        SingleFileComponentHotReloadHandlerEmitter.Emit());
                }
            });
    }

    private static bool IsSingleFileComponentFile(string path)
        => IsViuFile(path) || IsVueFile(path);

    private static bool IsViuFile(string path)
        => path.EndsWith(ViuExtension, StringComparison.OrdinalIgnoreCase);

    private static bool IsVueFile(string path)
        => path.EndsWith(VueExtension, StringComparison.OrdinalIgnoreCase);

    private static SingleFileComponentFile ReadFile(
        AdditionalText additionalText,
        ProjectOptions options,
        ImmutableArray<string> canonicalFileKeys,
        System.Threading.CancellationToken cancellationToken)
    {
        var text = additionalText.GetText(cancellationToken);
        var content = text?.ToString() ?? string.Empty;
        var names = SingleFileComponentNameResolver.Resolve(additionalText.Path, options.ProjectDirectory, options.RootNamespace);
        var scopeId = StyleScopeId.Resolve(additionalText.Path, options.ProjectDirectory);
        var format = IsVueFile(additionalText.Path)
            ? SingleFileComponentFormat.Vue
            : SingleFileComponentFormat.Viu;
        return new SingleFileComponentFile(
            format,
            additionalText.Path,
            LeafFileName(additionalText.Path),
            content,
            names.Namespace,
            names.ClassName,
            names.HintName,
            scopeId,
            options.EmitHotReloadMetadata
                ? SingleFileComponentHotReloadMetadataFactory.ResolveComponentIdentifier(
                    additionalText.Path,
                    options.ProjectDirectory)
                : null,
            HasCanonicalPeer(format, additionalText.Path, canonicalFileKeys));
    }

    private static bool ShouldEmitHotReloadMetadata(
        string? configuration,
        string? configuredValue)
    {
        if (!string.IsNullOrEmpty(configuredValue))
        {
            return string.Equals(configuredValue, "true", StringComparison.OrdinalIgnoreCase);
        }

        return string.Equals(configuration, "Debug", StringComparison.OrdinalIgnoreCase);
    }

    private static bool HasCanonicalPeer(
        SingleFileComponentFormat format,
        string filePath,
        ImmutableArray<string> canonicalFileKeys)
    {
        if (format != SingleFileComponentFormat.Vue)
        {
            return false;
        }

        var key = ComponentBasePath(filePath);
        foreach (var canonicalKey in canonicalFileKeys)
        {
            if (string.Equals(
                    key,
                    canonicalKey,
                    SingleFileComponentPathComparison.Comparison))
            {
                return true;
            }
        }

        return false;
    }

    private static string ComponentBasePath(string path)
    {
        var normalized = path.Replace('\\', '/');
        return normalized.Length >= ViuExtension.Length
            ? normalized.Substring(0, normalized.Length - ViuExtension.Length)
            : normalized;
    }

    private static SingleFileComponentGeneratorResult BuildResult(
        SingleFileComponentFile file,
        System.Threading.CancellationToken cancellationToken)
    {
        if (file.Format == SingleFileComponentFormat.Vue)
        {
            var parse = VueParser.ParseComponent(file.Text, cancellationToken);
            var diagnostics = new List<DiagnosticInfo>();
            AddParseDiagnostics(file, parse, diagnostics);

            SingleFileComponentScriptBlock? script = null;
            if (parse.Descriptor.Script is { } ordinaryScript
                && ValidateVueScriptLanguage(file, ordinaryScript, diagnostics))
            {
                script = ordinaryScript;
            }

            SingleFileComponentScriptBlock? scriptSetup = null;
            if (parse.Descriptor.ScriptSetup is { } authoredScriptSetup
                && ValidateVueScriptLanguage(file, authoredScriptSetup, diagnostics))
            {
                scriptSetup = authoredScriptSetup;
            }

            return BuildResult(
                file,
                parse,
                parse.Descriptor.Template,
                script,
                scriptSetup,
                parse.Descriptor.Styles.Count,
                parse.Descriptor.CustomBlocks.Count,
                diagnostics,
                CreateHotReloadMetadata(
                    file,
                    parse.Descriptor.Template,
                    parse.Descriptor.Script,
                    parse.Descriptor.ScriptSetup,
                    parse.Descriptor.Styles),
                cancellationToken);
        }

        var viuParse = Parser.ParseComponent(file.Text, cancellationToken);
        var viuDiagnostics = new List<DiagnosticInfo>();
        AddParseDiagnostics(file, viuParse, viuDiagnostics);
        return BuildResult(
            file,
            viuParse,
            viuParse.Descriptor.Template,
            viuParse.Descriptor.Script,
            scriptSetup: null,
            viuParse.Descriptor.Styles.Count,
            viuParse.Descriptor.CustomBlocks.Count,
            viuDiagnostics,
            CreateHotReloadMetadata(
                file,
                viuParse.Descriptor.Template,
                viuParse.Descriptor.Script,
                scriptSetup: null,
                viuParse.Descriptor.Styles),
            cancellationToken);
    }

    private static SingleFileComponentGeneratorResult BuildResult(
        SingleFileComponentFile file,
        AggregateSyntaxParserResult<SingleFileComponentBlock> parse,
        SingleFileComponentTemplateBlock? template,
        SingleFileComponentScriptBlock? script,
        SingleFileComponentScriptBlock? scriptSetup,
        int styleCount,
        int customBlockCount,
        List<DiagnosticInfo> diagnostics,
        SingleFileComponentHotReloadMetadata? hotReloadMetadata,
        System.Threading.CancellationToken cancellationToken)
    {
        var scriptRegions = ScriptRegions.None;
        var scriptBindings = EquatableArray<ScriptBinding>.Empty;
        if (script is not null)
        {
            var analysis = ScriptBlockAnalyzer.Analyze(
                file.FilePath,
                script,
                diagnostics,
                reservesGeneratedMembers: template is not null);
            scriptRegions = analysis.Regions;
            scriptBindings = analysis.Bindings;
        }

        var scriptSetupRegions = ScriptRegions.None;
        var scriptSetupBindings = EquatableArray<ScriptBinding>.Empty;
        if (scriptSetup is not null)
        {
            var analysis = ScriptBlockAnalyzer.Analyze(
                file.FilePath,
                scriptSetup,
                diagnostics,
                reservesGeneratedMembers: template is not null);
            scriptSetupRegions = analysis.Regions;
            scriptSetupBindings = analysis.Bindings;
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
            file, parse, diagnostics, moduleClasses, cssVariableBindings, moduleTemplateNames, bindingMetadata, cancellationToken);

        var model = new SingleFileComponentModel(
            file.Namespace,
            file.ClassName,
            file.FileName,
            file.HintName,
            HasTemplate: template is not null,
            HasScript: hasScript,
            StyleCount: styleCount,
            CustomBlockCount: customBlockCount,
            FilePath: file.FilePath,
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
            ScriptSetup = scriptSetupRegions,
            IsScriptSetupFirst =
                scriptSetup is not null &&
                script is not null &&
                scriptSetup.ContentLocation.Start.Offset < script.ContentLocation.Start.Offset,
            HotReloadMetadata = hotReloadMetadata,
        };

        var cssModules = BuildCssModuleAccessors(moduleClasses, moduleTemplateNames);
        var render = CompileRenderFunction(file, parse, bindingMetadata, cssModules, diagnostics, cancellationToken);
        model = model with { RenderBody = render.Body, RenderCacheSize = render.CacheSize };

        var array = diagnostics.Count == 0
            ? EquatableArray<DiagnosticInfo>.Empty
            : new EquatableArray<DiagnosticInfo>(diagnostics.ToArray());

        return new SingleFileComponentGeneratorResult(model, array);
    }

    private static SingleFileComponentHotReloadMetadata? CreateHotReloadMetadata(
        SingleFileComponentFile file,
        SingleFileComponentTemplateBlock? template,
        SingleFileComponentScriptBlock? script,
        SingleFileComponentScriptBlock? scriptSetup,
        IReadOnlyList<SingleFileComponentStyleBlock> styles)
    {
        if (file.HotReloadComponentIdentifier is not { } componentIdentifier)
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

    private static void AddParseDiagnostics(
        SingleFileComponentFile file,
        AggregateSyntaxParserResult<SingleFileComponentBlock> parse,
        List<DiagnosticInfo> diagnostics)
    {
        foreach (var diagnostic in parse.Diagnostics)
        {
            diagnostics.Add(SingleFileComponentDiagnostics.Create(
                file.FilePath,
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
                    diagnostics.Add(SingleFileComponentDiagnostics.CreateStyle(file.FilePath, diagnostic, blockContentStart));
                }

                continue;
            }

            var fromTemplate = SingleFileComponentParserFactory.IsTemplateBlock(sourceResult.Source);
            foreach (var diagnostic in sourceResult.Result.Diagnostics)
            {
                diagnostics.Add(SingleFileComponentDiagnostics.Create(file.FilePath, diagnostic, fromTemplate, blockContentStart));
            }
        }
    }

    private static bool ValidateVueScriptLanguage(
        SingleFileComponentFile file,
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
            file.FilePath,
            languageOption?.Location ?? OpeningTagLocation(file.Text, script)));
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
        SingleFileComponentFile file,
        AggregateSyntaxParserResult<SingleFileComponentBlock> parse,
        BindingMetadata bindingMetadata,
        CssModuleAccessors cssModules,
        List<DiagnosticInfo> diagnostics,
        System.Threading.CancellationToken cancellationToken)
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
            // CacheHandlers stays off: the upstream cached member-expression wrapper `(...args) => ...`
            // has no C# spelling yet; handler caching is runtime-binding follow-up work. v-once caching
            // is independent of this switch and fully emitted.
            transformOptions.OnError = error => diagnostics.Add(
                SingleFileComponentDiagnostics.Create(file.FilePath, error, fromTemplate: true, blockContentStart));

            var transformed = Transformer.Transform(templateResult.Root, transformOptions);
            var emitted = RenderFunctionEmitter.Emit(transformed, new RenderFunctionEmitterOptions
            {
                // namespace + class + method-body nesting, or class + method-body without a namespace.
                IndentLevel = string.IsNullOrEmpty(file.Namespace) ? 2 : 3,
            });

            // [V01.01.05.08] Inject #line span directives over the emitted render body so a C# error inside
            // a template expression (an unresolved member under permissive metadata, for example) resolves
            // to the offending .viu template line/column rather than to this generated file — the
            // render-body analogue of the @script merge's #line map. The mapper reuses the same
            // block-to-file composition the dispatched diagnostics use.
            var body = RenderBodySourceMapper.Inject(
                emitted.Code, emitted.SourceMappings, blockContentStart, file.FilePath);

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
    /// (docs/UTILITY-CSS-DESIGN.md §2.4). The generator layers the compile-only concerns on top: the
    /// template-facing module-name map ([V01.01.05.04.01]) and the [V01.01.06.06.01] binding-metadata
    /// rewrite of each <c>v-bind()</c> expression, whose diagnostics compose onto exact <c>.viu</c> style
    /// coordinates. The scope id is returned only when a <c>scoped</c> block is declared.
    /// </summary>
    private static (string? ScopeId, string? ExtractedStyles) CompileStyles(
        SingleFileComponentFile file,
        AggregateSyntaxParserResult<SingleFileComponentBlock> parse,
        List<DiagnosticInfo> diagnostics,
        List<CssModuleClassEntry> moduleClasses,
        List<CssVariableBindingEntry> cssVariableBindings,
        Dictionary<string, string> moduleTemplateNames,
        BindingMetadata bindingMetadata,
        System.Threading.CancellationToken cancellationToken)
    {
        var compilation = SingleFileComponentStyleCompiler.Compile(parse, file.ScopeId, cancellationToken);

        foreach (var moduleClass in compilation.ModuleClasses)
        {
            moduleTemplateNames[moduleClass.Accessor] = ModuleTemplateName(moduleClass.Module);
            moduleClasses.Add(new CssModuleClassEntry(moduleClass.Accessor, moduleClass.Original, moduleClass.Hashed));
        }

        foreach (var variableBinding in compilation.VariableBindings)
        {
            // [V01.01.06.06.01] Route the extracted expression through the template compiler's
            // binding-metadata rewriting (instance-member mode), so `v-bind(count)` unwraps a script
            // IReactiveReference<T> member to `count.Value` automatically — matching upstream cssVars
            // ergonomics.
            // A malformed expression surfaces its diagnostics on the exact .viu style coordinate through
            // the same style-origin envelope the CSS parse diagnostics use.
            var compiled = TemplateExpressionCompiler.CompileInstanceExpression(
                variableBinding.Binding.Expression, bindingMetadata, variableBinding.Binding.Location);
            foreach (var error in compiled.Diagnostics)
            {
                diagnostics.Add(SingleFileComponentDiagnostics.CreateStyle(
                    file.FilePath, error, variableBinding.BlockContentStart));
            }

            cssVariableBindings.Add(new CssVariableBindingEntry(variableBinding.Binding.Name, compiled.Code));
        }

        foreach (var styleDiagnostic in compilation.Diagnostics)
        {
            diagnostics.Add(SingleFileComponentDiagnostics.CreateStyle(
                file.FilePath, styleDiagnostic.Diagnostic, styleDiagnostic.BlockContentStart));
        }

        return (compilation.ScopeId, compilation.ExtractedStyles);
    }

    // The template spelling of a `module` option ([V01.01.05.04.01]): the default (valueless `module`) is
    // Vue's `$style`; a named module is referenced by its authored name (`<style module="theme">` -> `theme`),
    // exactly as Vue's render context exposes it.
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

    private static void Execute(SourceProductionContext context, SingleFileComponentGeneratorResult result)
    {
        foreach (var diagnostic in result.Diagnostics)
        {
            context.ReportDiagnostic(diagnostic.ToDiagnostic());
        }

        context.AddSource(result.Model.HintName, SingleFileComponentSourceEmitter.Emit(result.Model));
    }

    private static string LeafFileName(string path)
    {
        var separator = path.LastIndexOfAny(PathSeparators);
        return separator >= 0 ? path.Substring(separator + 1) : path;
    }

    private static readonly char[] PathSeparators = { '/', '\\' };
}
