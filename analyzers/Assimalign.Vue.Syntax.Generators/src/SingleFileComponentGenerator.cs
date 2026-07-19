using System;
using System.Collections.Generic;
using System.Text;

using Microsoft.CodeAnalysis;

using Assimalign.Vue.Syntax.Css;
using Assimalign.Vue.Syntax.SingleFileComponent;
using Assimalign.Vue.Syntax.Templates;

namespace Assimalign.Vue.Syntax.Generators;

/// <summary>
/// The incremental source generator for <c>.viu</c> single-file components — the composition root of the
/// <c>Assimalign.Vue.Syntax.*</c> cluster ([V01.01.06.02]). It is the C# analog of consuming
/// <c>@vue/compiler-sfc</c> through <c>@vitejs/plugin-vue</c>: MSBuild flows every <c>.viu</c> file in as
/// an <see cref="AdditionalText"/>, this generator parses each one with the composed
/// <see cref="SingleFileComponentParserComposition">block + template parser</see>, and emits a partial
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

    // The scope-id prefix ([V01.01.06.04] StyleScopeId), stripped to recover the short salt the
    // module/v-bind hashes use ([V01.01.06.06]).
    private const string ScopeIdPrefix = "data-v-";

    /// <summary>Pipeline step tracking name for the file-read transform (used by incremental-cache tests).</summary>
    public const string FileTrackingName = "SingleFileComponentFile";

    /// <summary>Pipeline step tracking name for the parse-and-model transform (used by incremental-cache tests).</summary>
    public const string ModelTrackingName = "SingleFileComponentModel";

    // The composed .viu parser is stateless and recoverable, so one shared instance serves every parse.
    private static readonly SingleFileComponentSyntaxParser Parser = SingleFileComponentParserComposition.Create();

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
            if (SingleFileComponentParserComposition.IsStyleBlock(sourceResult.Source))
            {
                foreach (var diagnostic in sourceResult.Result.Diagnostics)
                {
                    diagnostics.Add(SingleFileComponentDiagnostics.CreateStyle(file.FilePath, diagnostic, blockContentStart));
                }

                continue;
            }

            var fromTemplate = SingleFileComponentParserComposition.IsTemplateBlock(sourceResult.Source);
            foreach (var diagnostic in sourceResult.Result.Diagnostics)
            {
                diagnostics.Add(SingleFileComponentDiagnostics.Create(file.FilePath, diagnostic, fromTemplate, blockContentStart));
            }
        }

        // [V01.01.06.04]/[V01.01.06.06] @style compilation: scoped blocks are rewritten with the component's
        // scope id, `module` blocks have their class names locally hashed, `v-bind()` usages become
        // component-scoped custom properties, and non-scoped/non-module/non-v-bind blocks pass through
        // unmodified. The scope id, extracted CSS, module class map, and v-bind bindings surface in the model.
        var moduleClasses = new List<CssModuleClassEntry>();
        var cssVariableBindings = new List<CssVariableBindingEntry>();
        var (scopeId, extractedStyles) = CompileStyles(file, parse, diagnostics, moduleClasses, cssVariableBindings, cancellationToken);

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
            if (!SingleFileComponentParserComposition.IsTemplateBlock(sourceResult.Source) ||
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

    /// <summary>
    /// Compiles the dispatched <c>@style</c> parses ([V01.01.06.04]/[V01.01.06.06]): each block is run
    /// through the CSS-Modules class rename (<c>module</c> blocks, <see cref="CssModuleRewriter"/>), the
    /// <c>v-bind()</c> custom-property rewrite (<see cref="CssBindingRewriter"/>), and then serialized —
    /// scoped blocks via <see cref="CssScopedRewriter"/> with the component's stable scope id, rewritten
    /// non-scoped blocks via <see cref="CssStylesheetWriter"/>, and untouched non-scoped blocks verbatim.
    /// The module class map and <c>v-bind()</c> bindings accumulate into <paramref name="moduleClasses"/> /
    /// <paramref name="cssVariableBindings"/> for the metadata seams, and malformed <c>v-bind()</c>
    /// diagnostics compose onto the <c>.viu</c> style coordinates through the same style-origin envelope the
    /// CSS parse diagnostics use. The scope id is returned only when a <c>scoped</c> block is declared.
    /// </summary>
    private static (string? ScopeId, string? ExtractedStyles) CompileStyles(
        SingleFileComponentFile file,
        SingleFileComponentSyntaxParserResult parse,
        List<DiagnosticInfo> diagnostics,
        List<CssModuleClassEntry> moduleClasses,
        List<CssVariableBindingEntry> cssVariableBindings,
        System.Threading.CancellationToken cancellationToken)
    {
        string? scopeId = null;
        StringBuilder? styles = null;

        // The module/v-bind hashes are salted by the component's short scope id (the path hash without the
        // `data-v-` prefix), which the file always carries — so a `module`/`v-bind` block is component-scoped
        // even when it is not `scoped` ([V01.01.06.06]).
        var localHashSalt = ShortScopeId(file.ScopeId);

        foreach (var sourceResult in parse.SourceResults)
        {
            if (!SingleFileComponentParserComposition.IsStyleBlock(sourceResult.Source) ||
                sourceResult.Node is not SingleFileComponentStyleBlock styleBlock)
            {
                continue;
            }

            cancellationToken.ThrowIfCancellationRequested();

            string css;
            if (sourceResult.Result.Nodes.Count > 0 && sourceResult.Result.Nodes[0] is CssStylesheetNode parsed)
            {
                var stylesheet = parsed;
                var rewritten = false;

                // `module`: rename local class selectors and record original -> hashed for the accessor.
                if (styleBlock.IsModule)
                {
                    var moduleResult = CssModuleRewriter.Rewrite(stylesheet, localHashSalt);
                    stylesheet = moduleResult.Stylesheet;
                    rewritten = rewritten || moduleResult.Classes.Count > 0;
                    var accessor = ModuleAccessorName(styleBlock.ModuleName);
                    foreach (var pair in moduleResult.Classes)
                    {
                        moduleClasses.Add(new CssModuleClassEntry(accessor, pair.Key, pair.Value));
                    }
                }

                // `v-bind()`: rewrite each usage to a custom property and record the (hash, expression)
                // binding. The content guard skips the rewrite for the common no-binding block.
                if (styleBlock.Content.IndexOf("v-bind", StringComparison.Ordinal) >= 0)
                {
                    var bindingResult = CssBindingRewriter.Rewrite(stylesheet, localHashSalt);
                    stylesheet = bindingResult.Stylesheet;
                    rewritten = rewritten || bindingResult.Bindings.Count > 0;
                    foreach (var binding in bindingResult.Bindings)
                    {
                        cssVariableBindings.Add(new CssVariableBindingEntry(binding.Name, binding.Expression));
                    }

                    var blockContentStart = sourceResult.Node.ContentLocation.Start;
                    foreach (var diagnostic in bindingResult.Diagnostics)
                    {
                        diagnostics.Add(SingleFileComponentDiagnostics.CreateStyle(file.FilePath, diagnostic, blockContentStart));
                    }
                }

                if (styleBlock.Scoped)
                {
                    // A scoped block is serialized with the component's stable scope id (module/v-bind
                    // rewrites already updated the tree the scoped serializer reads).
                    scopeId ??= file.ScopeId;
                    css = CssScopedRewriter.Rewrite(stylesheet, scopeId);
                }
                else if (rewritten)
                {
                    // A rewritten non-scoped block is serialized canonically (its class names / values
                    // changed, so the raw content no longer matches).
                    css = CssStylesheetWriter.Write(stylesheet);
                }
                else
                {
                    // An untouched non-scoped block passes through verbatim (issue acceptance criterion).
                    css = styleBlock.Content;
                }
            }
            else
            {
                css = styleBlock.Content;
            }

            styles ??= new StringBuilder();
            styles.Append(css);
            if (css.Length > 0 && css[css.Length - 1] != '\n')
            {
                styles.Append('\n');
            }
        }

        return (scopeId, styles?.ToString());
    }

    // The component's short scope id — the `data-v-` scope id with its prefix stripped — used to salt the
    // module/v-bind hashes so they are component-scoped and deterministic ([V01.01.06.06]).
    private static string ShortScopeId(string scopeId)
        => scopeId.StartsWith(ScopeIdPrefix, StringComparison.Ordinal)
            ? scopeId.Substring(ScopeIdPrefix.Length)
            : scopeId;

    // The generated accessor class name for a `module` option: default (valueless `module`) maps to
    // `Style` — the C# analogue of Vue's `$style`, which has no legal C# spelling — and `module="name"`
    // maps to the pascal-cased name.
    private static string ModuleAccessorName(string? moduleName)
        => string.IsNullOrEmpty(moduleName) ? "Style" : PascalCase(moduleName!);

    // Pascal-cases an authored identifier for use as a C# type/member name: word boundaries at '-'/'_'/' '
    // start a new capitalized word, a leading digit is prefixed with '_', and non-identifier characters are
    // dropped. Deterministic so the emitted accessor is stable.
    private static string PascalCase(string value)
    {
        var builder = new StringBuilder(value.Length);
        var capitalizeNext = true;
        foreach (var character in value)
        {
            if (character == '-' || character == '_' || character == ' ')
            {
                capitalizeNext = true;
                continue;
            }

            if (!char.IsLetterOrDigit(character))
            {
                continue;
            }

            if (builder.Length == 0 && char.IsDigit(character))
            {
                builder.Append('_');
            }

            builder.Append(capitalizeNext ? char.ToUpperInvariant(character) : character);
            capitalizeNext = false;
        }

        return builder.Length == 0 ? "Style" : builder.ToString();
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
