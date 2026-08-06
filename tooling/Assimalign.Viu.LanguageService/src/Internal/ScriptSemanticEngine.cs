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
/// The base compilation is cached per project and revalidated by the context's
/// <see cref="LanguageProjectContext.CacheStamp"/>: an equal stamp reuses the cache untouched, a
/// changed single file swaps only its tree via
/// <see cref="CSharpCompilation.ReplaceSyntaxTree"/>, and anything else rebuilds. Each request
/// forks immutably — the open document's live text is projected, emitted, parsed, and added with
/// <see cref="CSharpCompilation.AddSyntaxTrees"/> — so a keystroke never mutates the cache.
/// Thread-safe the way <see cref="ScriptDeclarationReader"/> is: language-service reads compute
/// outside the service lock, so the engine serializes semantic requests under its own gate
/// (bounded work; concurrent requests wait rather than duplicate a cone build), while the
/// per-document resolution cache is lock-free so documentation resolution and document close never
/// wait behind a compile. Every failure that is not a cancellation is an engine miss reported as
/// <see langword="null"/> — the caller's fallback is the existing syntax-only path, never an
/// error. Parse options pin <see cref="LanguageVersion.Preview"/> (the repo-wide language surface)
/// with the context's <see cref="LanguageProjectContext.PreprocessorSymbols"/>, so members the
/// build guards with <c>#if</c> compile into the editor compilation exactly as the build sees
/// them.
/// </remarks>
internal sealed class ScriptSemanticEngine
{
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
    // Keyed like every host path key: ordinal-ignore-case, matching Windows path semantics.
    private readonly Dictionary<string, ProjectCompilationState> projectStates =
        new(StringComparer.OrdinalIgnoreCase);
    // Label -> symbol maps from each document's most recent semantic completion, published
    // wholesale and read lock-free so ResolveDocumentation and CloseDocument never wait behind a
    // cone build on the engine gate.
    private readonly ConcurrentDictionary<string, IReadOnlyDictionary<string, ResolutionEntry>> resolutionCaches =
        new(StringComparer.OrdinalIgnoreCase);
    private int projectStateBuildCount;
    private int sourceTreeBuildCount;

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
        string? summary = null;
        try
        {
            var documentationXml = entry.Symbol.GetDocumentationCommentXml(
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
            ? $"```csharp\n{entry.Detail}\n```"
            : $"```csharp\n{entry.Detail}\n```\n\n{summary}";
    }

    /// <summary>Clears the per-document state a closed document leaves behind.</summary>
    /// <param name="documentUri">The editor document URI.</param>
    internal void CloseDocument(string documentUri)
        => resolutionCaches.TryRemove(documentUri, out _);

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
        var state = GetOrCreateProjectState(context, cancellationToken);

        // Fork-per-request, here and only here: the live text is projected, emitted, parsed, and
        // added to an immutable fork — the cached base compilation is never mutated per keystroke.
        var generatedText = EmitComponent(
            documentFilePath,
            documentText,
            context,
            cancellationToken);
        var mapper = GeneratedScriptDocumentMapper.Create(
            documentText,
            generatedText,
            documentFilePath);
        if (!mapper.TryMapFileOffsetToGenerated(documentOffset, out var generatedPosition))
        {
            return null;
        }

        var currentTree = CSharpSyntaxTree.ParseText(
            generatedText,
            CreateParseOptions(context),
            documentFilePath,
            cancellationToken: cancellationToken);
        var requestCompilation = state.BaseCompilation.AddSyntaxTrees(currentTree);
        var semanticModel = requestCompilation.GetSemanticModel(currentTree);
        var root = currentTree.GetRoot(cancellationToken);
        if (!TryFindMemberAccessTarget(root, generatedPosition, out var target))
        {
            return null;
        }

        var symbols = LookupCompletionSymbols(
            semanticModel,
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
            semanticModel,
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
            return FilterEditorBrowsableSymbols(
                semanticModel.LookupSymbols(position),
                semanticModel.Compilation);
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
            return FilterEditorBrowsableSymbols(
                semanticModel.LookupSymbols(position, namespaceSymbol)
                    .Where(symbol => symbol.Kind is SymbolKind.NamedType or SymbolKind.Namespace),
                semanticModel.Compilation);
        }

        if (targetSymbol is ITypeSymbol typeReference)
        {
            // The target names a type (Reactive., String.): static members and nested types.
            return FilterEditorBrowsableSymbols(
                semanticModel.LookupStaticMembers(position, typeReference)
                    .Where(symbol => symbol.Kind is SymbolKind.Field or SymbolKind.Property
                        or SymbolKind.Method or SymbolKind.Event or SymbolKind.NamedType),
                semanticModel.Compilation);
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

        return FilterEditorBrowsableSymbols(
            semanticModel
                .LookupSymbols(position, expressionType, includeReducedExtensionMethods: true)
                .Where(symbol => symbol.Kind is SymbolKind.Field or SymbolKind.Property
                    or SymbolKind.Method or SymbolKind.Event)
                .Where(symbol => !symbol.IsStatic ||
                    symbol is IMethodSymbol { MethodKind: MethodKind.ReducedExtension }),
            semanticModel.Compilation);
    }

    // The referenced Microsoft.CodeAnalysis.CSharp 5.3.0 surface exposes no public
    // ISymbol.IsEditorBrowsable API. LookupSymbols therefore needs this metadata-level filter so
    // the compiler-only seam stays out of .viu completion ([V01.01.14.02]).
    private static IReadOnlyList<ISymbol> FilterEditorBrowsableSymbols(
        IEnumerable<ISymbol> symbols,
        Compilation compilation)
    {
        var editorBrowsableAttribute = compilation.GetTypeByMetadataName(
            "System.ComponentModel.EditorBrowsableAttribute");
        if (editorBrowsableAttribute is null)
        {
            return symbols.ToArray();
        }

        return symbols
            .Where(symbol => !IsEditorBrowsableNever(symbol, editorBrowsableAttribute))
            .ToArray();
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
        CancellationToken cancellationToken)
    {
        var environmentSignature = ComputeEnvironmentSignature(context);
        if (projectStates.TryGetValue(context.ProjectFilePath, out var state))
        {
            if (string.Equals(state.CacheStamp, context.CacheStamp, StringComparison.Ordinal))
            {
                // Equal stamps make contexts interchangeable (the LanguageProjectContext
                // contract), so every derived state is reusable untouched.
                return state;
            }

            if (string.Equals(
                    state.EnvironmentSignature,
                    environmentSignature,
                    StringComparison.Ordinal) &&
                HasSameDocumentSet(state, context))
            {
                RefreshChangedTrees(state, context, cancellationToken);
                state.CacheStamp = context.CacheStamp;
                return state;
            }
        }

        state = BuildProjectState(context, environmentSignature, cancellationToken);
        projectStates[context.ProjectFilePath] = state;
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
        foreach (var document in context.SourceDocuments)
        {
            var existing = state.Trees[document.FilePath];
            if (string.Equals(existing.Text, document.Text, StringComparison.Ordinal))
            {
                continue;
            }

            cancellationToken.ThrowIfCancellationRequested();
            var tree = CreateSourceTree(document, context, cancellationToken);
            state.BaseCompilation = state.BaseCompilation.ReplaceSyntaxTree(existing.Tree, tree);
            state.Trees[document.FilePath] = new SourceTreeState(
                document.Text,
                document.IsComponent,
                tree);
        }
    }

    private ProjectCompilationState BuildProjectState(
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
            trees[document.FilePath] = new SourceTreeState(
                document.Text,
                document.IsComponent,
                CreateSourceTree(document, context, cancellationToken));
        }

        var compilation = CSharpCompilation.Create(
            Path.GetFileNameWithoutExtension(context.ProjectFilePath),
            trees.Values.Select(tree => tree.Tree),
            references,
            CompilationOptions);
        return new ProjectCompilationState(
            context.CacheStamp,
            environmentSignature,
            trees,
            compilation);
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

    private SyntaxTree CreateSourceTree(
        LanguageProjectSourceDocument document,
        LanguageProjectContext context,
        CancellationToken cancellationToken)
    {
        sourceTreeBuildCount++;
        var text = document.IsComponent
            ? EmitComponent(document.FilePath, document.Text, context, cancellationToken)
            : document.Text;
        return CSharpSyntaxTree.ParseText(
            text,
            CreateParseOptions(context),
            document.FilePath,
            cancellationToken: cancellationToken);
    }

    // The generator-identical projection: the same facade, emitter, name resolver, and scope-id
    // derivation the Assimalign.Viu.Generators.Syntax source generator composes, fed the same
    // ProjectDirectory/RootNamespace the build publishes — identical generated text by
    // construction. Hot-reload metadata stays off (the deterministic Release shape) and the
    // canonical-peer filter is a multi-file generator concern the projection never reads.
    private static string EmitComponent(
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
        var projection = SingleFileComponentProjection.Project(input, cancellationToken);
        return SingleFileComponentSourceEmitter.Emit(projection.Model);
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

    private static string ComputeEnvironmentSignature(LanguageProjectContext context)
    {
        var builder = new StringBuilder();
        builder.Append(context.RootNamespace).Append('\n').Append(context.ProjectDirectory);
        foreach (var path in context.ReferenceAssemblyPaths)
        {
            builder.Append('\n').Append(path);
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
            string cacheStamp,
            string environmentSignature,
            Dictionary<string, SourceTreeState> trees,
            CSharpCompilation baseCompilation)
        {
            CacheStamp = cacheStamp;
            EnvironmentSignature = environmentSignature;
            Trees = trees;
            BaseCompilation = baseCompilation;
        }

        internal string CacheStamp { get; set; }

        internal string EnvironmentSignature { get; }

        internal Dictionary<string, SourceTreeState> Trees { get; }

        internal CSharpCompilation BaseCompilation { get; set; }
    }

    /// <summary>One sibling document's cached parse.</summary>
    private sealed record SourceTreeState(string Text, bool IsComponent, SyntaxTree Tree);

    /// <summary>One resolvable label from a document's most recent semantic completion.</summary>
    private sealed record ResolutionEntry(string Detail, ISymbol Symbol);
}
