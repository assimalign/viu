using System;
using System.Collections.Generic;

using Microsoft.CodeAnalysis;

using Assimalign.Vue.Syntax.SingleFileComponent;
using Assimalign.Vue.Syntax.Templates;
using Assimalign.Vue.Tooling.Css;

namespace Assimalign.Vue.Syntax.Generators;

/// <summary>
/// The incremental source generator for <c>.viu</c> single-file components — the composition root of the
/// <c>Assimalign.Vue.Syntax.*</c> cluster ([V01.01.06.02]). It is the C# analog of consuming
/// <c>@vue/compiler-sfc</c> through <c>@vitejs/plugin-vue</c>: MSBuild flows every <c>.viu</c> file in as
/// an <see cref="AdditionalText"/>, this generator parses each one with the composed
/// <see cref="SingleFileComponentParserFactory">block + template parser</see>, and emits a partial
/// class scaffold per component. WASM has no runtime <c>new Function</c> template compilation, so
/// build-time generation is Vuecs's only compilation path.
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
    // composition lives in the shared Tooling core ([V01.01.12.12]) so the VuecsBundleCss task builds the
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

        // [V01.01.06.04]/[V01.01.06.06] @style compilation: scoped blocks are rewritten with the component's
        // scope id, `module` blocks have their class names locally hashed, `v-bind()` usages become
        // component-scoped custom properties, and non-scoped/non-module/non-v-bind blocks pass through
        // unmodified. The scope id, extracted CSS, module class map, and v-bind bindings surface in the model.
        //
        // The compilation itself lives in the shared Tooling core ([V01.01.12.12]) — the SAME deterministic
        // method the VuecsBundleCss MSBuild task calls over the same .viu inputs. Running one implementation
        // in both hosts is what makes the emitted ExtractedStyles constant and the physical CSS bundle
        // byte-identical (see docs/UTILITY-CSS-DESIGN.md §2.4); the generator maps the core result into its
        // value-equatable model entries and routes any v-bind diagnostics onto the .viu coordinates exactly
        // as before.
        var compilation = SingleFileComponentStyleCompiler.Compile(parse, file.ScopeId, cancellationToken);
        var scopeId = compilation.ScopeId;
        var extractedStyles = compilation.ExtractedStyles;

        var moduleClasses = new List<CssModuleClassEntry>(compilation.ModuleClasses.Count);
        foreach (var moduleClass in compilation.ModuleClasses)
        {
            moduleClasses.Add(new CssModuleClassEntry(moduleClass.Accessor, moduleClass.Original, moduleClass.Hashed));
        }

        var cssVariableBindings = new List<CssVariableBindingEntry>(compilation.VariableBindings.Count);
        foreach (var binding in compilation.VariableBindings)
        {
            cssVariableBindings.Add(new CssVariableBindingEntry(binding.Name, binding.Expression));
        }

        foreach (var styleDiagnostic in compilation.Diagnostics)
        {
            diagnostics.Add(SingleFileComponentDiagnostics.CreateStyle(
                file.FilePath, styleDiagnostic.Diagnostic, styleDiagnostic.BlockContentStart));
        }

        // [V01.01.06.03]/[V01.01.06.03.01] @script integration: when the component declares a script,
        // split it into a hoisted using region and a class-body member region, validate both, and extract
        // binding metadata (routing any Roslyn parse diagnostics onto the .viu file). The regions carry
        // their own #line anchors so the emitter maps each back to the .viu source.
        var scriptRegions = ScriptRegions.None;
        var bindings = EquatableArray<ScriptBinding>.Empty;
        if (descriptor.Script is { } script)
        {
            var analysis = ScriptBlockAnalyzer.Analyze(file.FilePath, script, diagnostics);
            scriptRegions = analysis.Regions;
            bindings = analysis.Bindings;
        }

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
        // template compiler's ref-unwrapping decisions, so a Reference<T> member reads as `.Value` in
        // the emitted render body instead of falling back to `_ctx.`.
        var render = CompileRenderFunction(file, parse, model.ToBindingMetadata(), diagnostics, cancellationToken);
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
