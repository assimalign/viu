using System;
using System.Collections.Generic;

using Microsoft.CodeAnalysis;

using Assimalign.Viu.Syntax.SingleFileComponent;
using Assimalign.Viu.Syntax.Templates;
using Assimalign.Viu.Tooling.Css;

namespace Assimalign.Viu.Syntax.Generators;

/// <summary>
/// The incremental source generator for <c>.viu</c> single-file components — the composition root of the
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
/// </summary>
[Generator(LanguageNames.CSharp)]
public sealed class SingleFileComponentGenerator : IIncrementalGenerator
{
    private const string ViuExtension = ".viu";
    private const string RootNamespaceProperty = "build_property.RootNamespace";
    private const string ProjectDirectoryProperty = "build_property.ProjectDir";

    /// <summary>Pipeline step tracking name for the file-read transform (used by incremental-cache tests).</summary>
    public const string FileTrackingName = "SingleFileComponentFile";

    /// <summary>Pipeline step tracking name for the parse-and-model transform (used by incremental-cache tests).</summary>
    public const string ModelTrackingName = "SingleFileComponentModel";

    // The composed .viu parser is stateless and recoverable, so one shared instance serves every parse. The
    // composition lives in the shared Tooling core ([V01.01.12.12]) so the ViuBundleCss task builds the
    // identical parser and reproduces this generator's ExtractedStyles byte-for-byte.
    private static readonly SingleFileComponentSyntaxParser Parser = SingleFileComponentParserFactory.Create();

    /// <inheritdoc />
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        // Extract only the two project properties the names depend on into a value-equatable record, so
        // an unrelated config change does not invalidate the cache.
        var projectOptions = context.AnalyzerConfigOptionsProvider.Select(static (provider, _) =>
        {
            provider.GlobalOptions.TryGetValue(RootNamespaceProperty, out var rootNamespace);
            provider.GlobalOptions.TryGetValue(ProjectDirectoryProperty, out var projectDirectory);
            return new ProjectOptions(rootNamespace, projectDirectory);
        });

        var files = context.AdditionalTextsProvider
            .Where(static text => IsViuFile(text.Path))
            .Combine(projectOptions)
            .Select(static (pair, cancellationToken) => ReadFile(pair.Left, pair.Right, cancellationToken))
            .WithTrackingName(FileTrackingName);

        var results = files
            .Select(static (file, cancellationToken) => BuildResult(file, cancellationToken))
            .WithTrackingName(ModelTrackingName);

        context.RegisterSourceOutput(results, static (production, result) => Execute(production, result));
    }

    private static bool IsViuFile(string path)
        => path.EndsWith(ViuExtension, StringComparison.OrdinalIgnoreCase);

    private static SingleFileComponentFile ReadFile(
        AdditionalText additionalText,
        ProjectOptions options,
        System.Threading.CancellationToken cancellationToken)
    {
        var text = additionalText.GetText(cancellationToken);
        var content = text?.ToString() ?? string.Empty;
        var names = SingleFileComponentNameResolver.Resolve(additionalText.Path, options.ProjectDirectory, options.RootNamespace);
        var scopeId = StyleScopeId.Resolve(additionalText.Path, options.ProjectDirectory);
        return new SingleFileComponentFile(
            additionalText.Path,
            LeafFileName(additionalText.Path),
            content,
            names.Namespace,
            names.ClassName,
            names.HintName,
            scopeId);
    }

    private static SingleFileComponentGeneratorResult BuildResult(
        SingleFileComponentFile file,
        System.Threading.CancellationToken cancellationToken)
    {
        var parse = Parser.ParseComponent(file.Text, cancellationToken);
        var descriptor = parse.Descriptor;

        var diagnostics = new List<DiagnosticInfo>();

        // Container diagnostics: the .viu block parser reports file-relative positions.
        foreach (var diagnostic in parse.Diagnostics)
        {
            diagnostics.Add(SingleFileComponentDiagnostics.Create(file.FilePath, diagnostic, fromTemplate: false, blockContentStart: null));
        }

        // Dispatched-block diagnostics: each registered parser reports positions relative to the block's
        // content, so compose them with the block's content-start position to reach file coordinates. The
        // origin (template vs style) selects the diagnostic envelope so a CSS error lands under the style
        // descriptors ([V01.01.06.04]).
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

        // [V01.01.06.03]/[V01.01.06.03.01] @script integration: when the component declares a script,
        // split it into a hoisted using region and a class-body member region, validate both, and extract
        // binding metadata (routing any Roslyn parse diagnostics onto the .viu file). The regions carry
        // their own #line anchors so the emitter maps each back to the .viu source. This runs before @style
        // compilation because the v-bind() CSS rewriting ([V01.01.06.06.01]) needs the same binding metadata.
        var scriptRegions = ScriptRegions.None;
        var bindings = EquatableArray<ScriptBinding>.Empty;
        if (descriptor.Script is { } script)
        {
            var analysis = ScriptBlockAnalyzer.Analyze(
                file.FilePath,
                script,
                diagnostics,
                reservesGeneratedMembers: descriptor.Template is not null);
            scriptRegions = analysis.Regions;
            bindings = analysis.Bindings;
        }

        var bindingMetadata = SingleFileComponentModel.BuildBindingMetadata(descriptor.Script is not null, bindings);

        // [V01.01.06.04]/[V01.01.06.06] @style compilation: scoped blocks are rewritten with the component's
        // scope id, `module` blocks have their class names locally hashed, `v-bind()` usages become
        // component-scoped custom properties (with their expressions routed through the same binding-metadata
        // rewriting the render path uses, [V01.01.06.06.01]), and non-scoped/non-module/non-v-bind blocks pass
        // through unmodified. The scope id, extracted CSS, module class map, and v-bind bindings surface in the model.
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
            HasTemplate: descriptor.Template is not null,
            HasScript: descriptor.Script is not null,
            StyleCount: descriptor.Styles.Count,
            CustomBlockCount: descriptor.CustomBlocks.Count,
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
                : new EquatableArray<CssVariableBindingEntry>(cssVariableBindings.ToArray()));

        // The [V01.01.06.03] -> [V01.01.05.05] hand-off: the script's classified bindings drive the
        // template compiler's ref-unwrapping decisions, so an IReactiveReference<T> member reads as
        // `.Value` in
        // the emitted render body instead of falling back to `_ctx.`. The CSS module accessors
        // ([V01.01.05.04.01] -> [V01.01.06.06]) let a `$style.<class>` template reference resolve to the
        // emitted accessor class instead of a phantom `_ctx` member.
        var cssModules = BuildCssModuleAccessors(moduleClasses, moduleTemplateNames);
        var render = CompileRenderFunction(file, parse, bindingMetadata, cssModules, diagnostics, cancellationToken);
        model = model with { RenderBody = render.Body, RenderCacheSize = render.CacheSize };

        var array = diagnostics.Count == 0
            ? EquatableArray<DiagnosticInfo>.Empty
            : new EquatableArray<DiagnosticInfo>(diagnostics.ToArray());

        return new SingleFileComponentGeneratorResult(model, array);
    }

    /// <summary>
    /// Compiles the dispatched <c>@template</c> parse into the C# render-method body ([V01.01.05.05]):
    /// the registered <see cref="TemplateSyntaxParser"/>'s AST runs through <see cref="Transformer"/>
    /// under <c>PrefixIdentifiers</c> (with <paramref name="bindingMetadata"/> resolving identifier
    /// classifications) and the result is serialized by <see cref="RenderFunctionEmitter"/>. Transform
    /// diagnostics surface on the <c>.viu</c> file exactly like dispatched parse diagnostics.
    /// </summary>
    private static (string? Body, int CacheSize) CompileRenderFunction(
        SingleFileComponentFile file,
        SingleFileComponentSyntaxParserResult parse,
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

            // The first @template block is the component's template (the descriptor carries one).
            return (body, emitted.CacheSlotCount);
        }

        return (null, 0);
    }

    /// <summary>
    /// Compiles the dispatched <c>@style</c> parses. The compilation itself lives in the shared Tooling
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
        SingleFileComponentSyntaxParserResult parse,
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
