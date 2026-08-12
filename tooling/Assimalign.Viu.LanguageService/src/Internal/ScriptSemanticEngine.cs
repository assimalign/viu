using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

using Assimalign.Viu.Compiler.Css;
using Assimalign.Viu.Compiler.SingleFileComponent;

namespace Assimalign.Viu.LanguageService;

/// <summary>
/// The SemanticModel-only script completion engine ([V01.01.12.23], #259): a real
/// <see cref="CSharpCompilation"/> built from the host-fed <see cref="LanguageProjectContext"/> —
/// sibling <c>.cs</c> sources plus sibling <c>.viu</c>/<c>.vue</c> components projected through the
/// exact <see cref="SingleFileComponentProjection"/> + <see cref="SingleFileComponentSourceEmitter"/>
/// pipeline the build's source generator runs (names via
/// <see cref="SingleFileComponentNameResolver"/>, scope ids via <see cref="StyleScopeId"/>), so the
/// editor's compilation is identical to the generator's by construction — answered through
/// <see cref="SemanticModel.LookupSymbols(int, INamespaceOrTypeSymbol, string, bool)"/>. No
/// workspace, no MEF, no Features assemblies: the deliberate increment-1 backend, with the
/// CompletionService upgrade recorded as a later seam-compatible swap.
/// </summary>
/// <remarks>
/// The base compilation is cached per project and excluded live-document path. Sibling text is
/// compared directly, while root/options and reference path, length, and write-time fingerprints
/// identify the environment: one changed sibling swaps only its tree via
/// <see cref="CSharpCompilation.ReplaceSyntaxTree"/>, and an environment change rebuilds. Each
/// request forks immutably — the open document's live text is projected, emitted, parsed, and
/// added with <see cref="CSharpCompilation.AddSyntaxTrees"/> — so a keystroke never mutates the
/// stable cache or reprojects its siblings.
/// Thread-safe the way <see cref="ScriptDeclarationReader"/> is: language-service reads compute
/// outside the service lock, so the engine serializes semantic requests under its own gate
/// (bounded work; concurrent requests wait rather than duplicate a cone build), while the
/// per-document resolution cache keeps documentation resolution lock-free. Document close takes the
/// same gate so it evicts compilation and component-contract state atomically. Every failure that is
/// not a cancellation is an engine miss reported as
/// <see langword="null"/> — the caller's fallback is the existing syntax-only path, never an
/// error. Parse options pin <see cref="LanguageVersion.Preview"/> (the repo-wide language surface)
/// with the context's <see cref="LanguageProjectContext.PreprocessorSymbols"/>, so members the
/// build guards with <c>#if</c> compile into the editor compilation exactly as the build sees
/// them.
/// </remarks>
internal sealed class ScriptSemanticEngine
{
    private const string AdoptedComponentBaseMetadataName =
        "Assimalign.Viu.Components.ComponentBase";
    private const string LegacyComponentBaseMetadataName =
        "Assimalign.Viu.Components.ComponentTemplateBase";
    private const string LegacyComponentBaseCompatibilityFilePath =
        "Viu.LanguageService.ComponentBase.Compatibility.g.cs";
    private const string RenderGlueMetadataName =
        SingleFileComponentRenderGlue.GeneratedNamespace + ".RenderGlue";
    private const string RenderGlueFilePath =
        "Viu.LanguageService.RenderGlue.g.cs";
    private const string LegacyComponentBaseCompatibilitySource =
        "#nullable enable\n" +
        "\n" +
        "namespace Assimalign.Viu.Components;\n" +
        "\n" +
        "// Semantic-only bridge for projects whose references have not reached the component-model swap.\n" +
        "internal abstract class ComponentBase : ComponentTemplateBase\n" +
        "{\n" +
        "}\n";

    private static CSharpParseOptions CreateParseOptions(LanguageProjectContext context)
        => new CSharpParseOptions(LanguageVersion.Preview)
            .WithPreprocessorSymbols(context.PreprocessorSymbols);

    private static readonly CSharpCompilationOptions CompilationOptions = new(
        OutputKind.DynamicallyLinkedLibrary,
        allowUnsafe: true,
        nullableContextOptions: NullableContextOptions.Enable);

    // The completion detail rendering: minimal type names, member signatures with parameter names,
    // and property read/write descriptors — "int Count { get; set; }", "Task SaveAsync(int id)".
    private static readonly SymbolDisplayFormat DetailFormat = new(
        globalNamespaceStyle: SymbolDisplayGlobalNamespaceStyle.Omitted,
        typeQualificationStyle: SymbolDisplayTypeQualificationStyle.NameOnly,
        genericsOptions: SymbolDisplayGenericsOptions.IncludeTypeParameters,
        memberOptions: SymbolDisplayMemberOptions.IncludeParameters |
            SymbolDisplayMemberOptions.IncludeType,
        parameterOptions: SymbolDisplayParameterOptions.IncludeType |
            SymbolDisplayParameterOptions.IncludeName |
            SymbolDisplayParameterOptions.IncludeParamsRefOut |
            SymbolDisplayParameterOptions.IncludeDefaultValue,
        propertyStyle: SymbolDisplayPropertyStyle.ShowReadWriteDescriptor,
        localOptions: SymbolDisplayLocalOptions.IncludeType,
        miscellaneousOptions: SymbolDisplayMiscellaneousOptions.EscapeKeywordIdentifiers |
            SymbolDisplayMiscellaneousOptions.UseSpecialTypes);

    // A cancellation-aware gate rather than a monitor lock: a request queued behind a cone
    // build honors $/cancelRequest while waiting instead of only after acquisition.
    private readonly SemaphoreSlim gate = new(1, 1);
    // A project context excludes its one live document, so the live path is part of the base-cone
    // identity. This prevents equal-stamped contexts for two open documents from sharing a base
    // compilation that contains the wrong disk sibling.
    private readonly Dictionary<ProjectCompilationKey, ProjectCompilationState> projectStates =
        new(ProjectCompilationKeyComparer.Instance);
    // The stable state owns referenced/source/sibling contracts. This second cache adds exactly the
    // current live component contract and is replaced per live text snapshot.
    private readonly Dictionary<ProjectCompilationKey, ComponentContractCacheEntry> componentContractCaches =
        new(ProjectCompilationKeyComparer.Instance);
    // Label -> symbol maps from each document's most recent semantic completion, published
    // wholesale and read lock-free so ResolveDocumentation never waits behind a cone build on the
    // engine gate.
    private readonly ConcurrentDictionary<string, IReadOnlyDictionary<string, ResolutionEntry>> resolutionCaches =
        new(StringComparer.OrdinalIgnoreCase);
    private int projectStateBuildCount;
    private int sourceTreeBuildCount;
    private int componentContractBuildCount;
    private int liveDocumentRequestBuildCount;
    private int componentProjectionBuildCount;

    /// <summary>Gets how many times a project's base compilation was built from scratch.</summary>
    internal int ProjectStateBuildCount
    {
        get
        {
            gate.Wait();
            try
            {
                return projectStateBuildCount;
            }
            finally
            {
                gate.Release();
            }
        }
    }

    /// <summary>Gets how many sibling source trees were parsed (initial builds plus per-tree swaps).</summary>
    internal int SourceTreeBuildCount
    {
        get
        {
            gate.Wait();
            try
            {
                return sourceTreeBuildCount;
            }
            finally
            {
                gate.Release();
            }
        }
    }

    /// <summary>
    /// Computes the semantic completions for one script-block position, or <see langword="null"/>
    /// on any engine miss (an unmapped position, an unreadable reference, a binder failure) — the
    /// caller then serves its syntax-only answer unchanged.
    /// </summary>
    /// <param name="context">The host-fed project context.</param>
    /// <param name="documentUri">The editor document URI (the resolution-cache key).</param>
    /// <param name="documentFilePath">The document's file path (the projection and <c>#line</c> anchor).</param>
    /// <param name="documentText">The document's live editor text.</param>
    /// <param name="documentOffset">The zero-based cursor offset within <paramref name="documentText"/>.</param>
    /// <param name="typedPrefix">The partially typed identifier at the cursor, or empty.</param>
    /// <param name="contextKind">
    /// The position's syntactic context. <see cref="ScriptCompletionContextKind.UsingDirective"/>
    /// restricts the lookup to namespaces and types and orders namespaces first, because nothing
    /// else is legal in a using directive's namespace-or-type name.
    /// </param>
    /// <param name="cancellationToken">The token cancelling the computation.</param>
    /// <returns>The semantic completion result, or <see langword="null"/> on a miss.</returns>
    internal ScriptSemanticCompletionResult? GetCompletions(
        LanguageProjectContext context,
        string documentUri,
        string documentFilePath,
        string documentText,
        int documentOffset,
        string typedPrefix,
        ScriptCompletionContextKind contextKind,
        CancellationToken cancellationToken)
    {
        try
        {
            gate.Wait(cancellationToken);
            try
            {
                cancellationToken.ThrowIfCancellationRequested();
                return GetCompletionsCore(
                    context,
                    documentUri,
                    documentFilePath,
                    documentText,
                    documentOffset,
                    typedPrefix,
                    contextKind,
                    cancellationToken);
            }
            finally
            {
                gate.Release();
            }
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch
        {
            // ANY non-cancellation failure is an engine miss: the syntax-only fallback is the
            // existing code path, and a semantic hiccup must never surface as a request failure.
            return null;
        }
    }

    /// <summary>
    /// Computes the semantic completions for one position inside a template expression, or
    /// <see langword="null"/> on any miss — an offset the render source map does not cover, a
    /// position whose receiver does not bind, or a position that is not a member access.
    /// </summary>
    /// <param name="context">The host-fed project context.</param>
    /// <param name="documentUri">The editor document URI (the resolution-cache key).</param>
    /// <param name="documentFilePath">The document's file path (the projection and <c>#line</c> anchor).</param>
    /// <param name="documentText">The document's live editor text.</param>
    /// <param name="documentOffset">The zero-based cursor offset within <paramref name="documentText"/>.</param>
    /// <param name="typedPrefix">The partially typed member name at the cursor, or empty.</param>
    /// <param name="cancellationToken">The token cancelling the computation.</param>
    /// <returns>The semantic completion result, or <see langword="null"/> on a miss.</returns>
    /// <remarks>
    /// A template expression is compiled into the render body rather than merged verbatim, so its
    /// only image in the compilation is the one the render source map's span directives name. Binding
    /// there is what lets a <c>v-for</c> alias complete at all: the alias is a local the compiler
    /// declares over the loop source, so nothing outside that body knows its type. [V01.01.12.07.12]
    /// </remarks>
    internal ScriptSemanticCompletionResult? GetTemplateExpressionCompletions(
        LanguageProjectContext context,
        string documentUri,
        string documentFilePath,
        string documentText,
        int documentOffset,
        string typedPrefix,
        CancellationToken cancellationToken)
    {
        try
        {
            gate.Wait(cancellationToken);
            try
            {
                cancellationToken.ThrowIfCancellationRequested();
                return GetTemplateExpressionCompletionsCore(
                    context,
                    documentUri,
                    documentFilePath,
                    documentText,
                    documentOffset,
                    typedPrefix,
                    cancellationToken);
            }
            finally
            {
                gate.Release();
            }
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch
        {
            // Template completion is additive over the syntax-only answer, exactly as the script
            // path's is: any projection or binding failure degrades rather than failing the request.
            return null;
        }
    }

    /// <summary>Gets how many stable project/reference/sibling contract closures were materialized.</summary>
    internal int ComponentContractBuildCount
    {
        get
        {
            gate.Wait();
            try
            {
                return componentContractBuildCount;
            }
            finally
            {
                gate.Release();
            }
        }
    }

    /// <summary>Gets how many immutable live-document compilation forks were created.</summary>
    internal int LiveDocumentRequestBuildCount
    {
        get
        {
            gate.Wait();
            try
            {
                return liveDocumentRequestBuildCount;
            }
            finally
            {
                gate.Release();
            }
        }
    }

    /// <summary>Gets how many single-file-component projections were materialized.</summary>
    internal int ComponentProjectionBuildCount
    {
        get
        {
            gate.Wait();
            try
            {
                return componentProjectionBuildCount;
            }
            finally
            {
                gate.Release();
            }
        }
    }

    /// <summary>
    /// Classifies authored identifiers in the live document's projected C# tree, or returns
    /// <see langword="null"/> when project semantics cannot be constructed. [V01.01.12.07.11]
    /// </summary>
    internal IReadOnlyList<LanguageClassification>? GetClassifications(
        LanguageProjectContext context,
        string documentFilePath,
        string documentText,
        CancellationToken cancellationToken)
    {
        try
        {
            gate.Wait(cancellationToken);
            try
            {
                cancellationToken.ThrowIfCancellationRequested();
                return GetClassificationsCore(
                    context,
                    documentFilePath,
                    documentText,
                    cancellationToken);
            }
            finally
            {
                gate.Release();
            }
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch
        {
            // Semantic classification is additive. Any reference, projection, or binding failure
            // degrades to the language service's empty semantic layer; lexical color remains.
            return null;
        }
    }

    /// <summary>
    /// Reads every source, projected-component, and referenced-assembly component contract visible
    /// to the live document's semantic compilation. [V01.01.12.07.12]
    /// </summary>
    internal IReadOnlyList<TemplateComponentDeclaration>? GetComponentContracts(
        LanguageProjectContext context,
        string documentFilePath,
        string documentText,
        CancellationToken cancellationToken)
        => GetComponentContractSnapshot(
            context,
            documentFilePath,
            documentText,
            cancellationToken)?.Declarations;

    /// <summary>
    /// Reads the component contracts and live projection shared by template hover, completion, and
    /// publication, or returns <see langword="null"/> when semantic construction fails.
    /// </summary>
    internal ScriptComponentContractSnapshot? GetComponentContractSnapshot(
        LanguageProjectContext context,
        string documentFilePath,
        string documentText,
        CancellationToken cancellationToken)
    {
        try
        {
            gate.Wait(cancellationToken);
            try
            {
                cancellationToken.ThrowIfCancellationRequested();
                var state = GetOrCreateProjectState(
                    context,
                    documentFilePath,
                    cancellationToken);
                if (TryGetCachedComponentContracts(
                        state,
                        documentText,
                        out var cached))
                {
                    return cached;
                }

                var request = CreateLiveDocumentRequest(
                    state,
                    context,
                    documentFilePath,
                    documentText,
                    cancellationToken);
                return GetOrCreateComponentContracts(
                    request,
                    documentText,
                    cancellationToken);
            }
            finally
            {
                gate.Release();
            }
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Computes the semantic layers for one publication round from a single immutable live-document
    /// fork. Optional layers stay off when the client or document does not require them.
    /// [V01.01.12.07.11] [V01.01.12.07.15]
    /// </summary>
    internal ScriptSemanticPublication? GetPublicationSemantics(
        LanguageProjectContext context,
        string documentFilePath,
        string documentText,
        bool includeClassifications,
        bool includeDiagnostics,
        bool includeComponentContracts,
        CancellationToken cancellationToken)
    {
        try
        {
            gate.Wait(cancellationToken);
            try
            {
                cancellationToken.ThrowIfCancellationRequested();
                var state = GetOrCreateProjectState(
                    context,
                    documentFilePath,
                    cancellationToken);
                var request = CreateLiveDocumentRequest(
                    state,
                    context,
                    documentFilePath,
                    documentText,
                    cancellationToken);
                var classifications = includeClassifications
                    ? GetClassificationsCore(request, documentText, cancellationToken)
                    : Array.Empty<LanguageClassification>();
                var diagnostics = includeDiagnostics
                    ? GetDiagnosticsCore(request, documentText, cancellationToken)
                    : Array.Empty<LanguageDiagnostic>();
                var componentContracts = includeComponentContracts
                    ? GetOrCreateComponentContracts(
                        request,
                        documentText,
                        cancellationToken)
                    : null;
                return new ScriptSemanticPublication(
                    classifications,
                    diagnostics,
                    componentContracts?.Declarations,
                    request.Projection);
            }
            finally
            {
                gate.Release();
            }
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch
        {
            // Every semantic publication layer is additive. A failed fork leaves the parser and
            // projection diagnostics available and lexical classification intact.
            return null;
        }
    }

    /// <summary>
    /// Gets compiler diagnostics whose generated spans map back onto authored code in the live
    /// document — the merged <c>@script</c> block and the template expressions the render body
    /// compiles. Generated component scaffold spans are suppressed. [V01.01.12.07.15]
    /// </summary>
    internal IReadOnlyList<LanguageDiagnostic>? GetDiagnostics(
        LanguageProjectContext context,
        string documentFilePath,
        string documentText,
        CancellationToken cancellationToken)
    {
        try
        {
            gate.Wait(cancellationToken);
            try
            {
                cancellationToken.ThrowIfCancellationRequested();
                return GetDiagnosticsCore(
                    context,
                    documentFilePath,
                    documentText,
                    cancellationToken);
            }
            finally
            {
                gate.Release();
            }
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch
        {
            // Script diagnostics are an additive semantic layer. A reference, projection, or
            // binding failure leaves the parser/template/style diagnostics available unchanged.
            return null;
        }
    }

    /// <summary>
    /// Resolves the Roslyn symbol at one authored C# position and returns its signature and source
    /// or reference-assembly documentation, mapped back to the authored token. [V01.01.12.07.16]
    /// </summary>
    internal LanguageHover? GetHover(
        LanguageProjectContext context,
        string documentFilePath,
        string documentText,
        int documentOffset,
        CancellationToken cancellationToken)
    {
        try
        {
            gate.Wait(cancellationToken);
            try
            {
                cancellationToken.ThrowIfCancellationRequested();
                return GetHoverCore(
                    context,
                    documentFilePath,
                    documentText,
                    documentOffset,
                    cancellationToken);
            }
            finally
            {
                gate.Release();
            }
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch
        {
            // Hover is additive. A semantic miss lets the caller retain its syntax-only hover path.
            return null;
        }
    }

    /// <summary>
    /// Resolves deferred documentation for a label from the document's most recent semantic
    /// completion: the symbol's signature plus its XML documentation summary (source-declared
    /// <c>///</c> or the reference assembly's sibling <c>.xml</c> provider).
    /// </summary>
    /// <param name="documentUri">The editor document URI.</param>
    /// <param name="completionLabel">The completion item label being resolved.</param>
    /// <param name="cancellationToken">The token cancelling the resolution.</param>
    /// <returns>The Markdown documentation, or <see langword="null"/> when the label is unknown.</returns>
    internal string? ResolveDocumentation(
        string documentUri,
        string completionLabel,
        CancellationToken cancellationToken)
    {
        if (!resolutionCaches.TryGetValue(documentUri, out var entries) ||
            !entries.TryGetValue(completionLabel, out var entry))
        {
            return null;
        }

        cancellationToken.ThrowIfCancellationRequested();
        return FormatSymbolDocumentation(
            entry.Detail,
            entry.Symbol,
            cancellationToken);
    }

    private static string FormatSymbolDocumentation(
        string detail,
        ISymbol symbol,
        CancellationToken cancellationToken)
    {
        string? summary = null;
        try
        {
            var documentationXml = symbol.GetDocumentationCommentXml(
                preferredCulture: CultureInfo.InvariantCulture,
                expandIncludes: false,
                cancellationToken: cancellationToken);
            summary = ExtractDocumentationSummary(documentationXml);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch
        {
            // A malformed documentation payload degrades to the signature alone.
        }

        return summary is null
            ? $"```csharp\n{detail}\n```"
            : $"```csharp\n{detail}\n```\n\n{summary}";
    }

    /// <summary>Clears the semantic and component-contract state a closed document leaves behind.</summary>
    /// <param name="documentUri">The editor document URI.</param>
    internal void CloseDocument(string documentUri)
    {
        resolutionCaches.TryRemove(documentUri, out _);
        var documentFilePath = GetDocumentFilePath(documentUri);
        gate.Wait();
        try
        {
            var keys = projectStates.Keys
                .Where(key => string.Equals(
                    key.LiveDocumentFilePath,
                    documentFilePath,
                    StringComparison.OrdinalIgnoreCase))
                .ToArray();
            foreach (var key in keys)
            {
                projectStates.Remove(key);
                componentContractCaches.Remove(key);
            }
        }
        finally
        {
            gate.Release();
        }
    }

    private ScriptSemanticCompletionResult? GetCompletionsCore(
        LanguageProjectContext context,
        string documentUri,
        string documentFilePath,
        string documentText,
        int documentOffset,
        string typedPrefix,
        ScriptCompletionContextKind contextKind,
        CancellationToken cancellationToken)
    {
        var request = CreateLiveDocumentRequest(
            context,
            documentFilePath,
            documentText,
            cancellationToken);
        return request.Mapper.TryMapFileOffsetToGenerated(documentOffset, out var generatedPosition)
            ? CompleteAtGeneratedPosition(
                request,
                documentUri,
                generatedPosition,
                typedPrefix,
                contextKind,
                requireMemberAccess: false,
                cancellationToken)
            : null;
    }

    private ScriptSemanticCompletionResult? GetTemplateExpressionCompletionsCore(
        LanguageProjectContext context,
        string documentUri,
        string documentFilePath,
        string documentText,
        int documentOffset,
        string typedPrefix,
        CancellationToken cancellationToken)
    {
        var request = CreateLiveDocumentRequest(
            context,
            documentFilePath,
            documentText,
            cancellationToken);
        return request.Mapper.TryMapTemplateExpressionOffsetToGenerated(
                documentOffset,
                out var generatedPosition)
            ? CompleteAtGeneratedPosition(
                request,
                documentUri,
                generatedPosition,
                typedPrefix,
                ScriptCompletionContextKind.Expression,
                requireMemberAccess: true,
                cancellationToken)
            : null;
    }

    private ScriptSemanticCompletionResult? CompleteAtGeneratedPosition(
        LiveDocumentRequest request,
        string documentUri,
        int generatedPosition,
        string typedPrefix,
        ScriptCompletionContextKind contextKind,
        bool requireMemberAccess,
        CancellationToken cancellationToken)
    {
        if (!TryFindMemberAccessTarget(request.Root, generatedPosition, out var target))
        {
            return null;
        }

        // A template expression's generated home is the render body, whose locals and static-method
        // scope are the code generator's rather than the author's: a bare-identifier lookup there
        // would answer the template with the loop temporaries the compiler happened to emit. A bound
        // receiver has no such problem — its members are the same members either scope would name —
        // so the template path takes the member lookup and leaves every other position to its caller.
        if (requireMemberAccess && target is null)
        {
            return null;
        }

        var symbols = LookupCompletionSymbols(
            request.SemanticModel,
            generatedPosition,
            target,
            cancellationToken);
        if (symbols is null)
        {
            return null;
        }

        // The syntactic gate, applied to the bound symbols rather than to the finished items: the
        // item list is capped at LanguageCompletionLimits.MaximumItems, so filtering afterwards
        // would let a class-body lookup's members crowd out the namespaces the position asked for.
        if (contextKind == ScriptCompletionContextKind.UsingDirective)
        {
            symbols = symbols
                .Where(symbol => symbol.Kind is SymbolKind.Namespace or SymbolKind.NamedType)
                .ToArray();
        }

        var resolutionEntries = new Dictionary<string, ResolutionEntry>(StringComparer.Ordinal);
        var items = CreateCompletionItems(
            symbols,
            request.SemanticModel,
            generatedPosition,
            typedPrefix,
            contextKind == ScriptCompletionContextKind.UsingDirective,
            resolutionEntries);
        if (items.Count == 0)
        {
            // An empty lookup (an unresolvable container being the common cause) is a miss: the
            // syntax-only path — including the Context./Reactive. catalogs — answers instead.
            return null;
        }

        resolutionCaches[documentUri] = resolutionEntries;
        return new ScriptSemanticCompletionResult(
            items,
            target is not null,
            target?.ToString());
    }

    private IReadOnlyList<LanguageClassification> GetClassificationsCore(
        LanguageProjectContext context,
        string documentFilePath,
        string documentText,
        CancellationToken cancellationToken)
    {
        var request = CreateLiveDocumentRequest(
            context,
            documentFilePath,
            documentText,
            cancellationToken);
        return GetClassificationsCore(request, documentText, cancellationToken);
    }

    private static IReadOnlyList<LanguageClassification> GetClassificationsCore(
        LiveDocumentRequest request,
        string documentText,
        CancellationToken cancellationToken)
    {
        var generated = ScriptSemanticClassifier.Classify(
            request.SemanticModel,
            request.Root,
            cancellationToken);
        var authored = new List<LanguageClassification>(generated.Count);
        foreach (var classification in generated)
        {
            if (!request.Mapper.TryMapGeneratedSpanToFile(
                    classification.Span.Start,
                    classification.Span.Length,
                    out var fileStart,
                    out var fileLength))
            {
                continue;
            }

            authored.Add(
                new LanguageClassification(
                    new LanguageRange(
                        TextCoordinateConverter.GetPosition(documentText, fileStart),
                        TextCoordinateConverter.GetPosition(documentText, fileStart + fileLength)),
                    classification.ClassificationTypeName));
        }

        return authored;
    }

    private IReadOnlyList<LanguageDiagnostic> GetDiagnosticsCore(
        LanguageProjectContext context,
        string documentFilePath,
        string documentText,
        CancellationToken cancellationToken)
    {
        var request = CreateLiveDocumentRequest(
            context,
            documentFilePath,
            documentText,
            cancellationToken);
        return GetDiagnosticsCore(request, documentText, cancellationToken);
    }

    private static IReadOnlyList<LanguageDiagnostic> GetDiagnosticsCore(
        LiveDocumentRequest request,
        string documentText,
        CancellationToken cancellationToken)
    {
        var generated = request.SemanticModel.GetDiagnostics(
            cancellationToken: cancellationToken);
        var authored = new List<LanguageDiagnostic>(generated.Length);
        foreach (var diagnostic in generated)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!diagnostic.Location.IsInSource ||
                !ReferenceEquals(diagnostic.Location.SourceTree, request.Tree))
            {
                continue;
            }

            if (!TryMapDiagnosticRange(request, diagnostic, documentText, out var range))
            {
                continue;
            }

            authored.Add(
                new LanguageDiagnostic(
                    range,
                    diagnostic.Severity switch
                    {
                        Microsoft.CodeAnalysis.DiagnosticSeverity.Error =>
                            LanguageDiagnosticSeverity.Error,
                        Microsoft.CodeAnalysis.DiagnosticSeverity.Warning =>
                            LanguageDiagnosticSeverity.Warning,
                        Microsoft.CodeAnalysis.DiagnosticSeverity.Info =>
                            LanguageDiagnosticSeverity.Information,
                        _ => LanguageDiagnosticSeverity.Hint,
                    },
                    diagnostic.Id,
                    diagnostic.GetMessage(CultureInfo.InvariantCulture),
                    "csharp"));
        }

        return authored;
    }

    /// <summary>
    /// Places one compiler diagnostic on the authored source, or reports that it belongs to generated
    /// code no author wrote. [V01.01.12.07.15]
    /// </summary>
    /// <remarks>
    /// Two mappings, in order. The <c>@script</c> block is merged verbatim under simple-form
    /// <c>#line</c> regions, so its spans map affinely and are verified character-for-character. A
    /// template expression has no verbatim image — it is compiled into the render body — but the
    /// emitted body carries span-form <c>#line</c> directives ([V01.01.05.08]) that Roslyn itself
    /// resolves onto the template, so the position is taken from Roslyn and only the decision to trust
    /// it belongs here: <see cref="GeneratedScriptDocumentMapper.IsRenderExpressionDiagnostic"/> admits
    /// it only when it lands inside the expression its directive names, keeping scaffolding that shares
    /// the generated line out of the template. Anything else stays suppressed rather than misplaced.
    /// </remarks>
    private static bool TryMapDiagnosticRange(
        LiveDocumentRequest request,
        Diagnostic diagnostic,
        string documentText,
        out LanguageRange range)
    {
        var generatedSpan = diagnostic.Location.SourceSpan;
        if (request.Mapper.TryMapGeneratedSpanToFile(
                generatedSpan.Start,
                generatedSpan.Length,
                out var fileStart,
                out var fileLength))
        {
            range = new LanguageRange(
                TextCoordinateConverter.GetPosition(documentText, fileStart),
                TextCoordinateConverter.GetPosition(documentText, fileStart + fileLength));
            return true;
        }

        range = default;
        var mapped = diagnostic.Location.GetMappedLineSpan();
        if (!mapped.IsValid || !mapped.HasMappedPath)
        {
            return false;
        }

        var unmapped = diagnostic.Location.GetLineSpan();
        if (!request.Mapper.IsRenderExpressionDiagnostic(
                unmapped.StartLinePosition.Line,
                unmapped.StartLinePosition.Character,
                mapped.StartLinePosition.Line,
                mapped.StartLinePosition.Character,
                mapped.EndLinePosition.Line,
                mapped.EndLinePosition.Character))
        {
            return false;
        }

        range = new LanguageRange(
            new LanguagePosition(
                mapped.StartLinePosition.Line,
                mapped.StartLinePosition.Character),
            new LanguagePosition(
                mapped.EndLinePosition.Line,
                mapped.EndLinePosition.Character));
        return true;
    }

    private LanguageHover? GetHoverCore(
        LanguageProjectContext context,
        string documentFilePath,
        string documentText,
        int documentOffset,
        CancellationToken cancellationToken)
    {
        var request = CreateLiveDocumentRequest(
            context,
            documentFilePath,
            documentText,
            cancellationToken);
        if (!request.Mapper.TryMapFileOffsetToGenerated(
                documentOffset,
                out var generatedPosition) ||
            generatedPosition >= request.Root.FullSpan.End)
        {
            return null;
        }

        var token = request.Root.FindToken(generatedPosition);
        if (!token.Span.Contains(generatedPosition) || token.Span.Length == 0)
        {
            return null;
        }

        var symbol = ResolveHoverSymbol(
            request.SemanticModel,
            token,
            cancellationToken);
        if (symbol is null)
        {
            return null;
        }

        if (!request.Mapper.TryMapGeneratedSpanToFile(
                token.Span.Start,
                token.Span.Length,
                out var fileStart,
                out var fileLength))
        {
            return null;
        }

        if (symbol is IAliasSymbol alias)
        {
            symbol = alias.Target;
        }

        var detail = symbol.ToMinimalDisplayString(
            request.SemanticModel,
            generatedPosition,
            DetailFormat);
        return new LanguageHover(
            FormatSymbolDocumentation(detail, symbol, cancellationToken),
            new LanguageRange(
                TextCoordinateConverter.GetPosition(documentText, fileStart),
                TextCoordinateConverter.GetPosition(documentText, fileStart + fileLength)));
    }

    private static ISymbol? ResolveHoverSymbol(
        SemanticModel semanticModel,
        SyntaxToken token,
        CancellationToken cancellationToken)
    {
        var declaredSymbol = token.Parent switch
        {
            BaseTypeDeclarationSyntax declaration when declaration.Identifier == token =>
                semanticModel.GetDeclaredSymbol(declaration, cancellationToken),
            DelegateDeclarationSyntax declaration when declaration.Identifier == token =>
                semanticModel.GetDeclaredSymbol(declaration, cancellationToken),
            MethodDeclarationSyntax declaration when declaration.Identifier == token =>
                semanticModel.GetDeclaredSymbol(declaration, cancellationToken),
            ConstructorDeclarationSyntax declaration when declaration.Identifier == token =>
                semanticModel.GetDeclaredSymbol(declaration, cancellationToken),
            DestructorDeclarationSyntax declaration when declaration.Identifier == token =>
                semanticModel.GetDeclaredSymbol(declaration, cancellationToken),
            PropertyDeclarationSyntax declaration when declaration.Identifier == token =>
                semanticModel.GetDeclaredSymbol(declaration, cancellationToken),
            EventDeclarationSyntax declaration when declaration.Identifier == token =>
                semanticModel.GetDeclaredSymbol(declaration, cancellationToken),
            VariableDeclaratorSyntax declaration when declaration.Identifier == token =>
                semanticModel.GetDeclaredSymbol(declaration, cancellationToken),
            ParameterSyntax declaration when declaration.Identifier == token =>
                semanticModel.GetDeclaredSymbol(declaration, cancellationToken),
            TypeParameterSyntax declaration when declaration.Identifier == token =>
                semanticModel.GetDeclaredSymbol(declaration, cancellationToken),
            EnumMemberDeclarationSyntax declaration when declaration.Identifier == token =>
                semanticModel.GetDeclaredSymbol(declaration, cancellationToken),
            LocalFunctionStatementSyntax declaration when declaration.Identifier == token =>
                semanticModel.GetDeclaredSymbol(declaration, cancellationToken),
            _ => null,
        };
        if (declaredSymbol is not null)
        {
            return declaredSymbol;
        }

        for (var node = token.Parent; node is not null; node = node.Parent)
        {
            if (node is PredefinedTypeSyntax)
            {
                return semanticModel.GetTypeInfo(node, cancellationToken).Type;
            }

            if (node is not SimpleNameSyntax &&
                node is not MemberAccessExpressionSyntax &&
                node is not QualifiedNameSyntax &&
                node is not AliasQualifiedNameSyntax &&
                node is not MemberBindingExpressionSyntax &&
                node is not ObjectCreationExpressionSyntax)
            {
                if (node is StatementSyntax or MemberDeclarationSyntax)
                {
                    break;
                }

                continue;
            }

            var symbolInformation = semanticModel.GetSymbolInfo(node, cancellationToken);
            var symbol = symbolInformation.Symbol ??
                (symbolInformation.CandidateSymbols.Length > 0
                    ? symbolInformation.CandidateSymbols[0]
                    : null);
            if (symbol is not null)
            {
                return symbol;
            }

            if (node is TypeSyntax or ExpressionSyntax)
            {
                var type = semanticModel.GetTypeInfo(node, cancellationToken).Type;
                if (type is not null && type.TypeKind != TypeKind.Error)
                {
                    return type;
                }
            }
        }

        return null;
    }

    /// <summary>
    /// Creates the immutable per-request projection/compilation used by semantic completion,
    /// classification, diagnostics, hover, and component-contract reads. The cached base
    /// compilation is never mutated.
    /// </summary>
    private LiveDocumentRequest CreateLiveDocumentRequest(
        LanguageProjectContext context,
        string documentFilePath,
        string documentText,
        CancellationToken cancellationToken)
        => CreateLiveDocumentRequest(
            GetOrCreateProjectState(context, documentFilePath, cancellationToken),
            context,
            documentFilePath,
            documentText,
            cancellationToken);

    private LiveDocumentRequest CreateLiveDocumentRequest(
        ProjectCompilationState state,
        LanguageProjectContext context,
        string documentFilePath,
        string documentText,
        CancellationToken cancellationToken)
    {
        liveDocumentRequestBuildCount++;
        componentProjectionBuildCount++;
        var projection = ProjectComponent(
            documentFilePath,
            documentText,
            context,
            cancellationToken);
        var generatedText = SingleFileComponentSourceEmitter.Emit(projection.Model);
        var mapper = GeneratedScriptDocumentMapper.Create(
            documentText,
            generatedText,
            documentFilePath);
        var tree = CSharpSyntaxTree.ParseText(
            generatedText,
            CreateParseOptions(context),
            documentFilePath,
            cancellationToken: cancellationToken);
        var compilation = state.BaseCompilation.AddSyntaxTrees(tree);
        return new LiveDocumentRequest(
            state,
            compilation,
            tree,
            compilation.GetSemanticModel(tree),
            tree.GetRoot(cancellationToken),
            mapper,
            projection);
    }

    // Classifies the completion position from the parsed generated tree: returns false for a dot
    // context the engine cannot serve (a conditional-access binding, a stray dot), true with a
    // null target for an identifier position, and true with the accessed expression for expr. —
    // covering both the expression form (member access inside a body) and the type-position form
    // (an incomplete member declaration typed at class level parses as a qualified name).
    private static bool TryFindMemberAccessTarget(
        SyntaxNode root,
        int position,
        out ExpressionSyntax? target)
    {
        var token = root.FindToken(Math.Max(position - 1, 0));
        if (token.IsKind(SyntaxKind.DotToken))
        {
            target = token.Parent switch
            {
                MemberAccessExpressionSyntax memberAccess when memberAccess.OperatorToken == token
                    => memberAccess.Expression,
                QualifiedNameSyntax qualifiedName when qualifiedName.DotToken == token
                    => qualifiedName.Left,
                _ => null,
            };
            return target is not null;
        }

        if (token.Parent is SimpleNameSyntax name)
        {
            if (name.Parent is MemberAccessExpressionSyntax memberAccess &&
                memberAccess.Name == name)
            {
                target = memberAccess.Expression;
                return true;
            }

            if (name.Parent is QualifiedNameSyntax qualifiedName &&
                qualifiedName.Right == name)
            {
                target = qualifiedName.Left;
                return true;
            }

            if (name.Parent is MemberBindingExpressionSyntax)
            {
                target = null;
                return false;
            }
        }

        target = null;
        return true;
    }

    // Section 2(c) mechanics: an identifier position lists everything in scope; a member access
    // binds the target (GetSymbolInfo/GetTypeInfo) and lists the container's members —
    // LookupSymbols honors accessibility from the position's enclosing binder in every branch.
    private static IReadOnlyList<ISymbol>? LookupCompletionSymbols(
        SemanticModel semanticModel,
        int position,
        ExpressionSyntax? target,
        CancellationToken cancellationToken)
    {
        if (target is null)
        {
            return FilterCompletionSymbols(
                semanticModel.LookupSymbols(position),
                semanticModel);
        }

        var symbolInfo = semanticModel.GetSymbolInfo(target, cancellationToken);
        var targetSymbol = symbolInfo.Symbol ??
            (symbolInfo.CandidateSymbols.Length > 0 ? symbolInfo.CandidateSymbols[0] : null);
        if (targetSymbol is null && target is SimpleNameSyntax simpleName)
        {
            // A class-level `receiver.` parses as an incomplete member's qualified TYPE name, and
            // the type-only binder reports neither a symbol nor a candidate for a value receiver
            // (unlike the method-body expression form, which reports a NotATypeOrNamespace
            // candidate). Re-resolving the receiver by name through the position's full lookup
            // keeps `Count.` at member-declaration level listing its type's instance members —
            // the packed-consumer probe A shape.
            targetSymbol = semanticModel
                .LookupSymbols(position, container: null, simpleName.Identifier.ValueText)
                .FirstOrDefault(
                    symbol => symbol.Kind is SymbolKind.Field or SymbolKind.Property
                        or SymbolKind.Local or SymbolKind.Parameter);
        }

        if (targetSymbol is IAliasSymbol alias)
        {
            targetSymbol = alias.Target;
        }

        if (targetSymbol is INamespaceSymbol namespaceSymbol)
        {
            return FilterCompletionSymbols(
                semanticModel.LookupSymbols(position, namespaceSymbol)
                    .Where(symbol => symbol.Kind is SymbolKind.NamedType or SymbolKind.Namespace),
                semanticModel);
        }

        if (targetSymbol is ITypeSymbol typeReference)
        {
            // The target names a type (Reactive., String.): static members and nested types.
            return FilterCompletionSymbols(
                semanticModel.LookupStaticMembers(position, typeReference)
                    .Where(symbol => symbol.Kind is SymbolKind.Field or SymbolKind.Property
                        or SymbolKind.Method or SymbolKind.Event or SymbolKind.NamedType),
                semanticModel);
        }

        var expressionType = semanticModel.GetTypeInfo(target, cancellationToken).Type;
        if (expressionType is null || expressionType.TypeKind == TypeKind.Error)
        {
            // A class-body `Context.` parses as a qualified name whose left side binds only as a
            // NotATypeOrNamespace candidate; the candidate's declared type is still the container.
            expressionType = targetSymbol switch
            {
                IPropertySymbol property => property.Type,
                IFieldSymbol field => field.Type,
                ILocalSymbol local => local.Type,
                IParameterSymbol parameter => parameter.Type,
                _ => expressionType,
            };
        }

        if (expressionType is null || expressionType.TypeKind == TypeKind.Error)
        {
            return null;
        }

        return FilterCompletionSymbols(
            semanticModel
                .LookupSymbols(position, expressionType, includeReducedExtensionMethods: true)
                .Where(symbol => symbol.Kind is SymbolKind.Field or SymbolKind.Property
                    or SymbolKind.Method or SymbolKind.Event)
                .Where(symbol => !symbol.IsStatic ||
                    symbol is IMethodSymbol { MethodKind: MethodKind.ReducedExtension }),
            semanticModel);
    }

    // The referenced Microsoft.CodeAnalysis.CSharp 5.3.0 surface exposes no public
    // ISymbol.IsEditorBrowsable API. LookupSymbols therefore needs this metadata-level filter.
    // Declarations in the current generated tree without a #line mapping are compiler scaffold,
    // while authored script declarations retain their mapped source location ([SFC-CG-1]).
    private static IReadOnlyList<ISymbol> FilterCompletionSymbols(
        IEnumerable<ISymbol> symbols,
        SemanticModel semanticModel)
    {
        var editorBrowsableAttribute = semanticModel.Compilation.GetTypeByMetadataName(
            "System.ComponentModel.EditorBrowsableAttribute");
        if (editorBrowsableAttribute is null)
        {
            return symbols
                .Where(symbol => !IsCurrentGeneratedScaffoldSymbol(
                    symbol,
                    semanticModel.SyntaxTree))
                .ToArray();
        }

        return symbols
            .Where(symbol => !IsCurrentGeneratedScaffoldSymbol(
                symbol,
                semanticModel.SyntaxTree))
            .Where(symbol => !IsEditorBrowsableNever(symbol, editorBrowsableAttribute))
            .ToArray();
    }

    private static bool IsCurrentGeneratedScaffoldSymbol(
        ISymbol symbol,
        SyntaxTree currentTree)
    {
        // Namespace symbols aggregate every declaration across source and referenced metadata. A
        // generated component under `Assimalign.*` therefore gives the referenced `Assimalign`
        // namespace one unmapped scaffold location; treating that aggregate as scaffold removes the
        // entire namespace from `using ` completion. Only declaration/member symbols can themselves
        // be generated scaffold. [V01.01.12.07.14]
        if (symbol is INamespaceSymbol)
        {
            return false;
        }

        foreach (Location location in symbol.Locations)
        {
            if (location.IsInSource
                && ReferenceEquals(location.SourceTree, currentTree)
                && !location.GetMappedLineSpan().HasMappedPath)
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsEditorBrowsableNever(
        ISymbol symbol,
        INamedTypeSymbol editorBrowsableAttribute)
    {
        foreach (var attribute in symbol.GetAttributes())
        {
            if (!SymbolEqualityComparer.Default.Equals(
                    attribute.AttributeClass,
                    editorBrowsableAttribute) ||
                attribute.ConstructorArguments.Length != 1)
            {
                continue;
            }

            return attribute.ConstructorArguments[0].Value is int state &&
                state == (int)EditorBrowsableState.Never;
        }

        return false;
    }

    private List<LanguageCompletionItem> CreateCompletionItems(
        IReadOnlyList<ISymbol> symbols,
        SemanticModel semanticModel,
        int position,
        string typedPrefix,
        bool orderNamespacesFirst,
        Dictionary<string, ResolutionEntry> resolutionEntries)
    {
        var items = new List<LanguageCompletionItem>();
        var seenNames = new HashSet<string>(StringComparer.Ordinal);
        var ordered = symbols
            .Where(symbol => symbol.CanBeReferencedByName && symbol.Name.Length > 0)
            .Where(symbol => typedPrefix.Length == 0 ||
                symbol.Name.StartsWith(typedPrefix, StringComparison.OrdinalIgnoreCase))
            // A using directive names a namespace far more often than a type (`using static` and
            // the alias form are the exceptions), so its two-band order puts every namespace above
            // every type; every other context keeps the single "00:" semantic band that sorts
            // above the appended scaffold and keyword catalogs.
            .OrderBy(symbol => orderNamespacesFirst && symbol.Kind != SymbolKind.Namespace ? 1 : 0)
            .ThenBy(symbol => symbol.Name, StringComparer.Ordinal)
            .ThenBy(symbol => (int)symbol.Kind);
        foreach (var symbol in ordered)
        {
            // First-wins per name: overloads collapse to one item, matching the service's own
            // label deduplication.
            if (!seenNames.Add(symbol.Name))
            {
                continue;
            }

            var detail = symbol.ToMinimalDisplayString(semanticModel, position, DetailFormat);
            var sortBand = orderNamespacesFirst && symbol.Kind != SymbolKind.Namespace
                ? "01:"
                : "00:";
            items.Add(new LanguageCompletionItem(
                symbol.Name,
                ToCompletionKind(symbol),
                detail,
                // Deferred to ResolveDocumentation: serializing XML-documentation lookups for
                // hundreds of items per keystroke is the exact cost the resolve path avoids.
                Documentation: string.Empty,
                symbol.Name,
                IsSnippet: false,
                SortText: sortBand + symbol.Name));
            resolutionEntries[symbol.Name] = new ResolutionEntry(detail, symbol);
            if (items.Count == LanguageCompletionLimits.MaximumItems)
            {
                break;
            }
        }

        return items;
    }

    // A namespace maps to Module, the protocol value editors render with the namespace glyph
    // ({} in Visual Studio). Without the case it fell to Text and namespaces came back wearing the
    // plain-text glyph, which reads as anything but a namespace.
    private static LanguageCompletionItemKind ToCompletionKind(ISymbol symbol)
        => symbol switch
        {
            INamespaceSymbol => LanguageCompletionItemKind.Module,
            IPropertySymbol => LanguageCompletionItemKind.Property,
            IMethodSymbol => LanguageCompletionItemKind.Method,
            IFieldSymbol => LanguageCompletionItemKind.Field,
            ILocalSymbol or IParameterSymbol => LanguageCompletionItemKind.Field,
            INamedTypeSymbol or ITypeParameterSymbol => LanguageCompletionItemKind.Class,
            _ => LanguageCompletionItemKind.Text,
        };

    // Reads projectStates, so every caller must hold the synchronization gate.
    private ProjectCompilationState GetOrCreateProjectState(
        LanguageProjectContext context,
        string liveDocumentFilePath,
        CancellationToken cancellationToken)
    {
        var key = new ProjectCompilationKey(
            context.ProjectFilePath,
            liveDocumentFilePath);
        var environmentSignature = ComputeEnvironmentSignature(context);
        if (projectStates.TryGetValue(key, out var state))
        {
            if (string.Equals(
                    state.EnvironmentSignature,
                    environmentSignature,
                    StringComparison.Ordinal) &&
                HasSameDocumentSet(state, context))
            {
                RefreshChangedTrees(state, context, cancellationToken);
                return state;
            }
        }

        state = BuildProjectState(key, context, environmentSignature, cancellationToken);
        projectStates[key] = state;
        componentContractCaches.Remove(key);
        return state;
    }

    private bool HasSameDocumentSet(
        ProjectCompilationState state,
        LanguageProjectContext context)
    {
        if (state.Trees.Count != context.SourceDocuments.Count)
        {
            return false;
        }

        foreach (var document in context.SourceDocuments)
        {
            if (!state.Trees.TryGetValue(document.FilePath, out var existing) ||
                existing.IsComponent != document.IsComponent)
            {
                return false;
            }
        }

        return true;
    }

    // The per-tree invalidation path: only documents whose text actually changed are re-parsed
    // (components re-projected first), and each swaps into the base compilation with
    // ReplaceSyntaxTree so every unchanged tree's binding state survives.
    private void RefreshChangedTrees(
        ProjectCompilationState state,
        LanguageProjectContext context,
        CancellationToken cancellationToken)
    {
        var changed = false;
        foreach (var document in context.SourceDocuments)
        {
            var existing = state.Trees[document.FilePath];
            if (string.Equals(existing.Text, document.Text, StringComparison.Ordinal))
            {
                continue;
            }

            cancellationToken.ThrowIfCancellationRequested();
            if (!changed)
            {
                // Invalidate before the first mutable tree swap. A cancellation after one sibling
                // changes can then leave only an incomplete base cone, never a stale closure over it.
                InvalidateComponentContracts(state);
                changed = true;
            }

            var sourceTree = CreateSourceTreeState(document, context, cancellationToken);
            state.BaseCompilation = state.BaseCompilation.ReplaceSyntaxTree(
                existing.Tree,
                sourceTree.Tree);
            state.Trees[document.FilePath] = sourceTree;
        }
    }

    private ProjectCompilationState BuildProjectState(
        ProjectCompilationKey key,
        LanguageProjectContext context,
        string environmentSignature,
        CancellationToken cancellationToken)
    {
        projectStateBuildCount++;
        var references = CreateReferences(context.ReferenceAssemblyPaths);
        var trees = new Dictionary<string, SourceTreeState>(
            context.SourceDocuments.Count,
            StringComparer.OrdinalIgnoreCase);
        foreach (var document in context.SourceDocuments)
        {
            cancellationToken.ThrowIfCancellationRequested();
            trees[document.FilePath] = CreateSourceTreeState(
                document,
                context,
                cancellationToken);
        }

        var compilation = CSharpCompilation.Create(
            Path.GetFileNameWithoutExtension(context.ProjectFilePath),
            trees.Values.Select(tree => tree.Tree),
            references,
            CompilationOptions);
        compilation = AddLegacyComponentBaseCompatibilityIfRequired(
            compilation,
            context,
            cancellationToken);
        compilation = AddRenderGlueIfRequired(compilation, context, cancellationToken);
        return new ProjectCompilationState(
            key,
            environmentSignature,
            trees,
            compilation);
    }

    private static CSharpCompilation AddLegacyComponentBaseCompatibilityIfRequired(
        CSharpCompilation compilation,
        LanguageProjectContext context,
        CancellationToken cancellationToken)
    {
        if (compilation.GetTypeByMetadataName(AdoptedComponentBaseMetadataName) is not null
            || compilation.GetTypeByMetadataName(LegacyComponentBaseMetadataName) is null)
        {
            return compilation;
        }

        SyntaxTree compatibilityTree = CSharpSyntaxTree.ParseText(
            LegacyComponentBaseCompatibilitySource,
            CreateParseOptions(context),
            LegacyComponentBaseCompatibilityFilePath,
            cancellationToken: cancellationToken);
        return compilation.AddSyntaxTrees(compatibilityTree);
    }

    /// <summary>
    /// Adds the tier-three render glue the build's source generator emits once per consumer assembly.
    /// </summary>
    /// <remarks>
    /// Every compiled template reaches the runtime through this glue — a <c>v-for</c> source is
    /// unwrapped by it, an event handler is wrapped by it — and no generator runs over the editor's
    /// compilation to produce it. Without it those calls bind to an error type and carry the whole
    /// surrounding expression with them, which is what left a <c>v-for</c> alias untypeable and its
    /// members uncompletable. The text is the generator's own
    /// (<see cref="SingleFileComponentRenderGlue.Source"/>), so the two compilations cannot drift.
    /// A project whose own sources already declare the glue — a consumer that checked generator output
    /// in, or a second component projection in the same cone — keeps its declaration and skips this.
    /// </remarks>
    private static CSharpCompilation AddRenderGlueIfRequired(
        CSharpCompilation compilation,
        LanguageProjectContext context,
        CancellationToken cancellationToken)
    {
        if (compilation.GetTypeByMetadataName(RenderGlueMetadataName) is not null)
        {
            return compilation;
        }

        SyntaxTree glueTree = CSharpSyntaxTree.ParseText(
            SingleFileComponentRenderGlue.Source,
            CreateParseOptions(context),
            RenderGlueFilePath,
            cancellationToken: cancellationToken);
        return compilation.AddSyntaxTrees(glueTree);
    }

    private static IReadOnlyList<MetadataReference> CreateReferences(
        IReadOnlyList<string> referenceAssemblyPaths)
    {
        var references = new List<MetadataReference>(referenceAssemblyPaths.Count);
        var seenPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var path in referenceAssemblyPaths)
        {
            if (!seenPaths.Add(path))
            {
                continue;
            }

            // The sibling .xml beside a reference assembly (ref packs ship them) feeds symbol
            // documentation through the resolve path at nearly zero cost; runtime-directory
            // assemblies without one simply resolve no metadata documentation.
            var documentationPath = Path.ChangeExtension(path, ".xml");
            var documentation = File.Exists(documentationPath)
                ? new ScriptSemanticDocumentationProvider(documentationPath)
                : null;
            references.Add(MetadataReference.CreateFromFile(path, documentation: documentation));
        }

        return references;
    }

    private SourceTreeState CreateSourceTreeState(
        LanguageProjectSourceDocument document,
        LanguageProjectContext context,
        CancellationToken cancellationToken)
    {
        sourceTreeBuildCount++;
        SingleFileComponentProjectionResult? projection = null;
        string text;
        if (document.IsComponent)
        {
            componentProjectionBuildCount++;
            projection = ProjectComponent(
                document.FilePath,
                document.Text,
                context,
                cancellationToken);
            text = SingleFileComponentSourceEmitter.Emit(projection.Value.Model);
        }
        else
        {
            text = document.Text;
        }

        var tree = CSharpSyntaxTree.ParseText(
            text,
            CreateParseOptions(context),
            document.FilePath,
            cancellationToken: cancellationToken);
        return new SourceTreeState(
            document.Text,
            document.IsComponent,
            tree,
            projection);
    }

    // The generator-identical projection: the same facade, emitter, name resolver, and scope-id
    // derivation the Assimalign.Viu.Generators.Syntax source generator composes, fed the same
    // ProjectDirectory/RootNamespace the build publishes — identical generated text by
    // construction. Hot-reload metadata stays off (the deterministic Release shape) and the
    // canonical-peer filter is a multi-file generator concern the projection never reads.
    private static SingleFileComponentProjectionResult ProjectComponent(
        string filePath,
        string text,
        LanguageProjectContext context,
        CancellationToken cancellationToken)
    {
        var name = SingleFileComponentNameResolver.Resolve(
            filePath,
            context.ProjectDirectory,
            context.RootNamespace);
        var input = new SingleFileComponentProjectionInput(
            GetComponentFormat(filePath),
            filePath,
            GetLeafFileName(filePath),
            text,
            name.Namespace,
            name.ClassName,
            name.HintName,
            StyleScopeId.Resolve(filePath, context.ProjectDirectory),
            HotReloadComponentIdentifier: null,
            HasCanonicalPeer: false);
        return SingleFileComponentProjection.Project(input, cancellationToken);
    }

    private bool TryGetCachedComponentContracts(
        ProjectCompilationState state,
        string documentText,
        out ScriptComponentContractSnapshot snapshot)
    {
        if (componentContractCaches.TryGetValue(state.Key, out var cached) &&
            cached.ProjectContractVersion == state.ComponentContractVersion &&
            string.Equals(cached.DocumentText, documentText, StringComparison.Ordinal))
        {
            snapshot = cached.Snapshot;
            return true;
        }

        snapshot = null!;
        return false;
    }

    private ScriptComponentContractSnapshot GetOrCreateComponentContracts(
        LiveDocumentRequest request,
        string documentText,
        CancellationToken cancellationToken)
    {
        if (TryGetCachedComponentContracts(
                request.ProjectState,
                documentText,
                out var cached))
        {
            return cached;
        }

        var stableDeclarations = GetOrCreateStableComponentContracts(
            request.ProjectState,
            cancellationToken);
        var declarations = new List<TemplateComponentDeclaration>(stableDeclarations.Count + 1);
        declarations.AddRange(stableDeclarations);
        AppendProjectedComponentContract(
            declarations,
            request.Projection.Model);
        declarations.Sort(static (left, right) => string.CompareOrdinal(
            left.CompilerDeclaration.Name,
            right.CompilerDeclaration.Name));
        IReadOnlyList<TemplateComponentDeclaration> result = declarations.ToArray();
        var snapshot = new ScriptComponentContractSnapshot(result, request.Projection);
        componentContractCaches[request.ProjectState.Key] =
            new ComponentContractCacheEntry(
                request.ProjectState.ComponentContractVersion,
                documentText,
                snapshot);
        return snapshot;
    }

    private IReadOnlyList<TemplateComponentDeclaration> GetOrCreateStableComponentContracts(
        ProjectCompilationState state,
        CancellationToken cancellationToken)
    {
        if (state.StableComponentContracts is { } cached)
        {
            return cached;
        }

        componentContractBuildCount++;
        var declarations = new List<TemplateComponentDeclaration>(
            ScriptComponentContractReader.Read(
                state.BaseCompilation,
                cancellationToken));
        foreach (var sourceTree in state.Trees.Values)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (sourceTree.Projection is { } projection)
            {
                AppendProjectedComponentContract(declarations, projection.Model);
            }
        }

        declarations.Sort(static (left, right) => string.CompareOrdinal(
            left.CompilerDeclaration.Name,
            right.CompilerDeclaration.Name));
        IReadOnlyList<TemplateComponentDeclaration> result = declarations.ToArray();
        state.StableComponentContracts = result;
        return result;
    }

    private static void AppendProjectedComponentContract(
        List<TemplateComponentDeclaration> declarations,
        SingleFileComponentModel model)
    {
        var events = new List<TemplateComponentEvent>(model.Declarations.Events.Count);
        foreach (var componentEvent in model.Declarations.Events)
        {
            var modifierPrefix = string.IsNullOrEmpty(componentEvent.Modifiers)
                ? string.Empty
                : componentEvent.Modifiers + " ";
            events.Add(
                new TemplateComponentEvent(
                    componentEvent.Name,
                    modifierPrefix +
                        "void " +
                        componentEvent.MethodName +
                        componentEvent.ParameterList));
        }

        declarations.Add(
            new TemplateComponentDeclaration(
                new ComponentDeclarationEntry(
                    model.ClassName,
                    model.Declarations.Parameters)
                {
                    IsParameterSurfaceKnown =
                        !model.Declarations.DeclaresImperativeParameters,
                },
                events));
    }

    private static SingleFileComponentFormat GetComponentFormat(string filePath)
        => filePath.EndsWith(".vue", StringComparison.OrdinalIgnoreCase)
            ? SingleFileComponentFormat.Vue
            : SingleFileComponentFormat.Viu;

    private static string GetLeafFileName(string filePath)
    {
        var lastSeparator = filePath.LastIndexOfAny(['/', '\\']);
        return lastSeparator >= 0 ? filePath.Substring(lastSeparator + 1) : filePath;
    }

    private static string GetDocumentFilePath(string documentUri)
        => Uri.TryCreate(documentUri, UriKind.Absolute, out var uri) && uri.IsFile
            ? uri.LocalPath
            : documentUri;

    private void InvalidateComponentContracts(ProjectCompilationState state)
    {
        state.ComponentContractVersion++;
        state.StableComponentContracts = null;
        componentContractCaches.Remove(state.Key);
    }

    private static string ComputeEnvironmentSignature(LanguageProjectContext context)
    {
        var builder = new StringBuilder();
        builder.Append(context.RootNamespace).Append('\n').Append(context.ProjectDirectory);
        foreach (var symbol in context.PreprocessorSymbols)
        {
            builder.Append('\n').Append("symbol|").Append(symbol);
        }

        foreach (var path in context.ReferenceAssemblyPaths)
        {
            long length = 0;
            long lastWriteTicks = 0;
            try
            {
                var information = new FileInfo(path);
                length = information.Exists ? information.Length : 0;
                lastWriteTicks = information.Exists
                    ? information.LastWriteTimeUtc.Ticks
                    : 0;
            }
            catch (Exception exception) when (
                exception is IOException or UnauthorizedAccessException)
            {
                // An unreadable reference already degrades semantic construction to a miss. Its
                // zero fingerprint still changes when the host later supplies a readable file.
            }

            builder
                .Append('\n')
                .Append("reference|")
                .Append(path)
                .Append('|')
                .Append(length)
                .Append('|')
                .Append(lastWriteTicks);
        }

        return builder.ToString();
    }

    // A lightweight <summary> text extraction (no XML document model): inline tags are dropped and
    // whitespace runs collapse so a multi-line summary reads as one documentation line.
    private static string? ExtractDocumentationSummary(string? documentationXml)
    {
        if (string.IsNullOrEmpty(documentationXml))
        {
            return null;
        }

        var start = documentationXml.IndexOf("<summary>", StringComparison.Ordinal);
        if (start < 0)
        {
            return null;
        }

        start += "<summary>".Length;
        var end = documentationXml.IndexOf("</summary>", start, StringComparison.Ordinal);
        if (end < 0)
        {
            return null;
        }

        var builder = new StringBuilder(end - start);
        var pendingSpace = false;
        var index = start;
        while (index < end)
        {
            var character = documentationXml[index];
            if (character == '<')
            {
                var close = documentationXml.IndexOf('>', index + 1);
                if (close < 0 || close >= end)
                {
                    break;
                }

                index = close + 1;
                continue;
            }

            if (char.IsWhiteSpace(character))
            {
                pendingSpace = builder.Length > 0;
                index++;
                continue;
            }

            if (pendingSpace)
            {
                builder.Append(' ');
                pendingSpace = false;
            }

            builder.Append(character);
            index++;
        }

        var summary = builder.ToString();
        return summary.Length == 0 ? null : summary;
    }

    /// <summary>One cached project's base-compilation state.</summary>
    private sealed class ProjectCompilationState
    {
        internal ProjectCompilationState(
            ProjectCompilationKey key,
            string environmentSignature,
            Dictionary<string, SourceTreeState> trees,
            CSharpCompilation baseCompilation)
        {
            Key = key;
            EnvironmentSignature = environmentSignature;
            Trees = trees;
            BaseCompilation = baseCompilation;
        }

        internal ProjectCompilationKey Key { get; }

        internal string EnvironmentSignature { get; }

        internal Dictionary<string, SourceTreeState> Trees { get; }

        internal CSharpCompilation BaseCompilation { get; set; }

        internal int ComponentContractVersion { get; set; }

        internal IReadOnlyList<TemplateComponentDeclaration>? StableComponentContracts { get; set; }
    }

    /// <summary>One sibling document's cached parse.</summary>
    private sealed record SourceTreeState(
        string Text,
        bool IsComponent,
        SyntaxTree Tree,
        SingleFileComponentProjectionResult? Projection);

    /// <summary>One live document snapshot's stable-plus-current component-contract closure.</summary>
    private sealed record ComponentContractCacheEntry(
        int ProjectContractVersion,
        string DocumentText,
        ScriptComponentContractSnapshot Snapshot);

    /// <summary>One immutable live-document semantic fork over a cached project compilation.</summary>
    private sealed record LiveDocumentRequest(
        ProjectCompilationState ProjectState,
        CSharpCompilation Compilation,
        SyntaxTree Tree,
        SemanticModel SemanticModel,
        SyntaxNode Root,
        GeneratedScriptDocumentMapper Mapper,
        SingleFileComponentProjectionResult Projection);

    /// <summary>Identifies one project cone after excluding its current live document.</summary>
    private readonly record struct ProjectCompilationKey(
        string ProjectFilePath,
        string LiveDocumentFilePath);

    /// <summary>Compares project and live-document paths using the host's Windows path semantics.</summary>
    private sealed class ProjectCompilationKeyComparer : IEqualityComparer<ProjectCompilationKey>
    {
        internal static ProjectCompilationKeyComparer Instance { get; } = new();

        public bool Equals(ProjectCompilationKey left, ProjectCompilationKey right)
            => string.Equals(
                    left.ProjectFilePath,
                    right.ProjectFilePath,
                    StringComparison.OrdinalIgnoreCase) &&
                string.Equals(
                    left.LiveDocumentFilePath,
                    right.LiveDocumentFilePath,
                    StringComparison.OrdinalIgnoreCase);

        public int GetHashCode(ProjectCompilationKey value)
            => HashCode.Combine(
                StringComparer.OrdinalIgnoreCase.GetHashCode(value.ProjectFilePath),
                StringComparer.OrdinalIgnoreCase.GetHashCode(value.LiveDocumentFilePath));
    }

    /// <summary>One resolvable label from a document's most recent semantic completion.</summary>
    private sealed record ResolutionEntry(string Detail, ISymbol Symbol);
}
