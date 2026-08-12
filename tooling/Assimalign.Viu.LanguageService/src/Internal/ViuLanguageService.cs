using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

using Assimalign.Viu.Syntax;
using Assimalign.Viu.Syntax.SingleFileComponent;
using Assimalign.Viu.Compiler.SingleFileComponent;
using Assimalign.Viu.UtilityCss;

namespace Assimalign.Viu.LanguageService;

internal sealed class ViuLanguageService :
    ILanguageService,
    IUtilityCssLanguageService,
    IScriptSemanticLanguageService
{
    private static readonly UtilityCssRegistry UtilityRegistry = UtilityCssRegistry.BuiltIn;

    private static readonly IReadOnlyDictionary<string, string> HoverDocumentation =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            // Hybrid .viu container ([V01.01.06.10]): <template>/<style> are the canonical tag
            // containers, @script stays an @-block, and the legacy @template/@style containers hover
            // as migration guidance during the transition window.
            ["template"] = "**`<template>`** contains the component's template markup, compiled to a render function at build time.",
            ["@script"] = "**`@script`** contains C# members merged into the generated partial component.",
            ["style"] = "**`<style>`** contains component CSS and can be `scoped` or a CSS `module`.",
            ["@template"] = "**`@template { }`** is the legacy Viu template container. Rewrite the block as `<template>...</template>`; block options become tag attributes.",
            ["@style"] = "**`@style { }`** is the legacy Viu style container. Rewrite the block as `<style>...</style>`; block options become tag attributes such as `scoped`.",
            ["v-if"] = "**`v-if`** conditionally renders an element or component.",
            ["v-for"] = "**`v-for`** repeats an element or component for values in a source.",
            ["v-model"] = "**`v-model`** creates a two-way form value binding.",
            ["@click"] = "**`@click`** registers a click event handler. Task-returning handlers are supported.",
            ["Context"] = "**`Context`** is the generated component's `ComponentContext`.",
            ["Context.Bindings"] = "**`Context.Bindings`** exposes the contract-resolved parent invocation.",
            ["Context.Services"] = "**`Context.Services`** is the application's independent `IServiceProvider`.",
            ["Context.Lifecycle"] = "**`Context.Lifecycle`** registers lifecycle callbacks and exposes component cancellation.",
            ["Context.Scope"] = "**`Context.Scope`** owns this component's reactive effects and watches.",
            ["Context.WatchScheduler"] = "**`Context.WatchScheduler`** schedules this component's watch callbacks.",
            ["Context.Parent"] = "**`Context.Parent`** is the parent component context, or `null` at a root.",
            ["Context.Emit"] = "**`Context.Emit`** emits a declared component event to the parent.",
            ["Context.Expose"] = "**`Context.Expose`** selects the public surface returned through template references.",
            ["Context.Warn"] = "**`Context.Warn`** routes a development warning through the application.",
            ["Context.Watch"] = "**`Context.Watch`** observes a getter within this component's reactive scope.",
            // The root-level lifecycle registrations every component inherits from
            // ComponentTemplateBase ([CMP-32]). Each is a pass-through to the Context.Lifecycle form
            // above, so the two are interchangeable and may be mixed within one component.
            ["OnBeforeMount"] = "**`OnBeforeMount(callback)`** registers a callback that runs before the initial subtree is mounted — the root-level equivalent of `Context.Lifecycle.OnBeforeMount`. Specified by `[CMP-32]`.",
            ["OnMounted"] = "**`OnMounted(callback)`** registers a callback that runs after the initial subtree is mounted — the root-level equivalent of `Context.Lifecycle.OnMounted`. Specified by `[CMP-32]`.",
            ["OnBeforeUpdate"] = "**`OnBeforeUpdate(callback)`** registers a callback that runs before a later subtree is patched — the root-level equivalent of `Context.Lifecycle.OnBeforeUpdate`. Specified by `[CMP-32]`.",
            ["OnUpdated"] = "**`OnUpdated(callback)`** registers a callback that runs after a later subtree is patched — the root-level equivalent of `Context.Lifecycle.OnUpdated`. Specified by `[CMP-32]`.",
            ["OnBeforeUnmount"] = "**`OnBeforeUnmount(callback)`** registers a callback that runs before the component is unmounted — the root-level equivalent of `Context.Lifecycle.OnBeforeUnmount`. Specified by `[CMP-32]`.",
            ["OnUnmounted"] = "**`OnUnmounted(callback)`** registers a callback that runs after the component is unmounted — the root-level equivalent of `Context.Lifecycle.OnUnmounted`. Specified by `[CMP-32]`.",
            ["OnErrorCaptured"] = "**`OnErrorCaptured(callback)`** registers a callback that captures an error from a descendant, returning `false` to stop propagation — the root-level equivalent of `Context.Lifecycle.OnErrorCaptured`. Specified by `[CMP-32]`.",
            ["OnServerPrefetch"] = "**`OnServerPrefetch(callback)`** registers a task server-side rendering awaits before the component is serialized — the root-level equivalent of `Context.Lifecycle.OnServerPrefetch`. Specified by `[CMP-32]`.",
            ["OnActivated"] = "**`OnActivated(callback)`** registers a callback that runs when a cached subtree is reactivated — the root-level equivalent of `Context.Lifecycle.OnActivated`. Specified by `[CMP-32]`.",
            ["OnDeactivated"] = "**`OnDeactivated(callback)`** registers a callback that runs when a cached subtree is deactivated — the root-level equivalent of `Context.Lifecycle.OnDeactivated`. Specified by `[CMP-32]`.",
            ["Reactive"] = "**`Reactive`** is Viu's reactivity facade: references, computeds, effects, watchers, and the tracking escape hatches.",
            ["Reactive.Reference"] = "**`Reactive.Reference(value)`** creates a reactive `Reference<T>` read and written through `.Value`.",
            ["Reactive.Computed"] = "**`Reactive.Computed(getter)`** creates a lazy cached computed value.",
            ["Reactive.Watch"] = "**`Reactive.Watch`** observes a reactive source and returns a disposable `WatchHandle`.",
            ["Reactive.WatchEffect"] = "**`Reactive.WatchEffect`** runs an automatically tracked watcher.",
        };

    private readonly object synchronization = new();
    private readonly LanguageDocumentStore documents = new();
    // Reads compute outside the synchronization lock, so the reader carries its own gate.
    private readonly ScriptDeclarationReader scriptDeclarations = new();
    // The artifact-fed semantic engine ([V01.01.12.23], #259) likewise computes outside the
    // service lock under its own gate; the service only snapshots the per-document context here.
    private readonly ScriptSemanticEngine scriptSemantics = new();
    // Document URIs must compare exactly as they do in the server host's open-document set, or a
    // client that varies casing (a Windows drive letter, most commonly) reports a document as open
    // while the workspace fails to find it and every language feature silently returns nothing.
    private readonly Dictionary<string, UtilityStylesheetLanguageContext> utilityContexts =
        new(StringComparer.OrdinalIgnoreCase);
    // The host-fed project context per document ([V01.01.12.23], #259), snapshotted by script
    // completion and fed to the semantic engine; a document without one keeps the syntax-only
    // answers unchanged.
    private readonly Dictionary<string, LanguageProjectContext> projectContexts =
        new(StringComparer.OrdinalIgnoreCase);
    private int standaloneDocumentProjectionBuildCount;

    /// <summary>Gets all component projections built by this workspace.</summary>
    internal int ComponentProjectionBuildCount
        => scriptSemantics.ComponentProjectionBuildCount +
            Volatile.Read(ref standaloneDocumentProjectionBuildCount);

    /// <summary>Gets how many stable component-contract closures this workspace built.</summary>
    internal int ComponentContractBuildCount => scriptSemantics.ComponentContractBuildCount;

    /// <summary>Gets how many project-scoped semantic states this workspace built.</summary>
    internal int ProjectStateBuildCount => scriptSemantics.ProjectStateBuildCount;

    public void ConfigureProjectContext(
        string documentUri,
        LanguageProjectContext? context)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(documentUri);

        lock (synchronization)
        {
            if (context is null)
            {
                projectContexts.Remove(documentUri);
            }
            else
            {
                projectContexts[documentUri] = context;
            }
        }
    }

    public void ConfigureUtilityStylesheet(
        string documentUri,
        string? stylesheetText)
    {
        ConfigureUtilityStylesheet(
            documentUri,
            stylesheetText,
            documentUri + "#utility-css",
            UtilityStylesheetReferenceGraph.Empty);
    }

    public void ConfigureUtilityStylesheet(
        string documentUri,
        string? stylesheetText,
        string stylesheetIdentity,
        UtilityStylesheetReferenceGraph referenceGraph)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(documentUri);
        ArgumentException.ThrowIfNullOrWhiteSpace(stylesheetIdentity);
        ArgumentNullException.ThrowIfNull(referenceGraph);

        lock (synchronization)
        {
            // The server host reconfigures the stylesheet on every completion and hover request.
            // Rebuilding the context re-parses the theme and recompiles the project stylesheet, so
            // unchanged host inputs must not pay that cost once per keystroke.
            if (utilityContexts.TryGetValue(documentUri, out var existing) &&
                existing.Matches(stylesheetText, stylesheetIdentity, referenceGraph))
            {
                return;
            }
        }

        // The context build (theme parse + project stylesheet compile) runs outside the lock so a
        // reconfigure cannot stall document synchronization or concurrent reads. Two requests
        // configuring the same document concurrently may build identical contexts; the store below
        // is last-wins, and identical inputs produce identical contexts, so the duplicate compute
        // is wasted CPU rather than incorrectness.
        var context = UtilityStylesheetLanguageContext.Create(
            stylesheetText,
            stylesheetIdentity,
            referenceGraph);

        lock (synchronization)
        {
            utilityContexts[documentUri] = context;
        }
    }

    public void OpenDocument(string documentUri, string text, int? version)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(documentUri);
        ArgumentNullException.ThrowIfNull(text);

        lock (synchronization)
        {
            documents.Open(documentUri, text, version);
        }
    }

    public bool ChangeDocument(
        string documentUri,
        int? version,
        IReadOnlyList<LanguageDocumentChange> changes)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(documentUri);
        ArgumentNullException.ThrowIfNull(changes);

        lock (synchronization)
        {
            return documents.Change(documentUri, version, changes);
        }
    }

    public bool CloseDocument(string documentUri)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(documentUri);

        // Eviction serializes with semantic work. Keep it outside the service lock so close cannot
        // invert the service/engine lock order while a feature request holds the engine gate.
        scriptSemantics.CloseDocument(documentUri);
        lock (synchronization)
        {
            utilityContexts.Remove(documentUri);
            projectContexts.Remove(documentUri);
            return documents.Close(documentUri);
        }
    }

    public IReadOnlyList<LanguageDiagnostic> GetDiagnostics(
        string documentUri,
        CancellationToken cancellationToken = default)
        => GetDocumentPublication(
            documentUri,
            includeSemanticClassifications: false,
            cancellationToken).Diagnostics;

    public LanguageDocumentPublication GetDocumentPublication(
        string documentUri,
        bool includeSemanticClassifications,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(documentUri);
        cancellationToken.ThrowIfCancellationRequested();

        LanguageDocument? document;
        LanguageProjectContext? context;
        lock (synchronization)
        {
            document = documents.TryGet(documentUri, out var openDocument)
                ? openDocument
                : null;
            context = projectContexts.TryGetValue(documentUri, out var configured)
                ? configured
                : null;
        }

        if (document is null)
        {
            return new LanguageDocumentPublication(
                ClassificationSnapshot: null,
                Diagnostics: Array.Empty<LanguageDiagnostic>());
        }

        // Keep the incremental parser's recovery order and Vue-compatibility contract intact.
        // Nested compiler diagnostics are appended below instead of replacing this baseline.
        var diagnostics = document.Syntax.Errors
            .Select(ToLanguageDiagnostic)
            .ToList();
        AddVueCompatibilityDiagnostics(document.Syntax, diagnostics);

        var hasCSharpScript =
            IsCSharpScript(document.Syntax.Script) ||
            IsCSharpScript(document.Syntax.ScriptSetup);
        var hasTemplate = document.Syntax.Template is not null;
        ScriptSemanticPublication? semanticPublication = null;
        if (context is not null && (hasCSharpScript || hasTemplate))
        {
            semanticPublication = scriptSemantics.GetPublicationSemantics(
                context,
                GetDocumentFilePath(documentUri),
                document.Text,
                includeSemanticClassifications && hasCSharpScript,
                // A template's expressions are compiled C# whose binding errors belong to the author
                // exactly as the @script block's do, so a component that declares only a template
                // still reports them.
                includeDiagnostics: hasCSharpScript || hasTemplate,
                includeComponentContracts: hasTemplate,
                cancellationToken);
        }

        ComponentDeclarationCatalog? componentCatalog =
            semanticPublication?.ComponentContracts is not { } componentContracts
            ? null
            : new TemplateComponentCatalog(componentContracts).CompilerCatalog;
        var projection = semanticPublication?.Projection ??
            ProjectDocument(document, context, cancellationToken);
        diagnostics.AddRange(
            LanguageDiagnosticProvider.GetProjectionDiagnostics(
                projection,
                componentCatalog,
                cancellationToken));
        if (semanticPublication is not null)
        {
            diagnostics.AddRange(semanticPublication.Diagnostics);
        }

        var classificationSnapshot = includeSemanticClassifications
            ? new LanguageClassificationSnapshot(
                document.Version,
                LanguageTextChecksum.Compute(document.Text),
                semanticPublication?.Classifications ?? Array.Empty<LanguageClassification>())
            : null;
        return new LanguageDocumentPublication(classificationSnapshot, diagnostics);
    }

    public IReadOnlyList<LanguageClassification> GetClassifications(
        string documentUri,
        CancellationToken cancellationToken = default)
        => GetClassificationSnapshot(documentUri, cancellationToken)?.Classifications
            ?? Array.Empty<LanguageClassification>();

    public LanguageClassificationSnapshot? GetClassificationSnapshot(
        string documentUri,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(documentUri);
        cancellationToken.ThrowIfCancellationRequested();

        LanguageDocument? document;
        LanguageProjectContext? context;
        lock (synchronization)
        {
            document = documents.TryGet(documentUri, out var openDocument)
                ? openDocument
                : null;
            context = projectContexts.TryGetValue(documentUri, out var configured)
                ? configured
                : null;
        }

        if (document is null)
        {
            return null;
        }

        IReadOnlyList<LanguageClassification> classifications =
            context is not null &&
            (IsCSharpScript(document.Syntax.Script) ||
             IsCSharpScript(document.Syntax.ScriptSetup))
                ? scriptSemantics.GetClassifications(
                        context,
                        GetDocumentFilePath(documentUri),
                        document.Text,
                        cancellationToken)
                    ?? Array.Empty<LanguageClassification>()
                : Array.Empty<LanguageClassification>();

        return new LanguageClassificationSnapshot(
            document.Version,
            LanguageTextChecksum.Compute(document.Text),
            classifications);
    }

    public IReadOnlyList<LanguageCompletionItem> GetCompletions(
        string documentUri,
        LanguagePosition position,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(documentUri);
        cancellationToken.ThrowIfCancellationRequested();

        var (document, stylesheetContext) = CaptureSnapshot(documentUri);
        if (document is null ||
            !TextCoordinateConverter.TryGetOffset(document.Text, position, out var offset))
        {
            return Array.Empty<LanguageCompletionItem>();
        }

        var block = FindBlock(document.Syntax, offset);
        var template = block as SingleFileComponentTemplateBlock;
        if (template is not null &&
            TemplateCompletionProvider.IsInsideComment(
                template.Content,
                offset - template.ContentLocation.Start.Offset))
        {
            return Array.Empty<LanguageCompletionItem>();
        }

        if (template is not null &&
            UtilityClassCompletionContext.TryCreate(
                template.Content,
                template.ContentLocation.Start.Offset,
                offset,
                out var utilityContext))
        {
            var utilityCompletions = GetUtilityClassCompletions(
                document.Text,
                utilityContext,
                stylesheetContext,
                cancellationToken);
            return StyleClassCompletionProvider.Merge(
                document.Text,
                document.Syntax,
                utilityContext,
                utilityCompletions,
                cancellationToken);
        }

        if (template is not null)
        {
            var contextualCompletions = TemplateCompletionProvider.GetCompletions(
                document,
                template,
                offset,
                scriptDeclarations,
                scriptSemantics,
                CaptureProjectContext(documentUri),
                documentUri,
                GetDocumentFilePath(documentUri),
                cancellationToken);
            if (contextualCompletions is not null)
            {
                return contextualCompletions;
            }
        }

        if (block is SingleFileComponentStyleBlock styleBlock &&
            StyleClassCompletionProvider.IsInsideComment(
                styleBlock.Content,
                offset - styleBlock.ContentLocation.Start.Offset))
        {
            return Array.Empty<LanguageCompletionItem>();
        }

        var linePrefix = TextCoordinateConverter.GetLinePrefix(document.Text, offset);
        if (template is null)
        {
            // Block header options describe a block header, never markup inside a template block.
            // Gating on the template block keeps the recovered `@style ` header working, whose
            // block does contain the cursor.
            var headerCompletions = GetHeaderOptionCompletions(
                linePrefix,
                document.Syntax.Format);
            if (headerCompletions.Count > 0)
            {
                return headerCompletions;
            }
        }

        return block switch
        {
            SingleFileComponentTemplateBlock => GetTemplateCompletions(
                document.Text,
                offset,
                linePrefix),
            SingleFileComponentScriptBlock scriptBlock => GetScriptCompletions(
                documentUri,
                document,
                scriptBlock,
                offset,
                linePrefix,
                cancellationToken),
            SingleFileComponentStyleBlock => ViuCompletionCatalog.StyleProperties,
            _ => GetRootCompletions(document.Syntax),
        };
    }

    public string? ResolveCompletionDocumentation(
        string documentUri,
        string completionLabel,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(documentUri);
        ArgumentNullException.ThrowIfNull(completionLabel);

        if (completionLabel.Length == 0)
        {
            return null;
        }

        cancellationToken.ThrowIfCancellationRequested();

        // Semantic script labels resolve first, from the engine's cached symbols for this
        // document's most recent semantic completion ([V01.01.12.23], #259) — the same deferred-
        // documentation contract the utility path below uses. C# identifiers and dash-separated
        // utility candidates do not overlap in practice, and an unknown or stale label simply
        // falls through to the utility resolution.
        var semanticDocumentation = scriptSemantics.ResolveDocumentation(
            documentUri,
            completionLabel,
            cancellationToken);
        if (semanticDocumentation is not null)
        {
            return semanticDocumentation;
        }

        // Resolution needs only the per-document utility context, never the open document:
        // the label is the complete candidate text, and recomputing against the current
        // stylesheet context is the same one the hover path uses, so a stylesheet edit between
        // completion and resolve can never serve stale documentation.
        var (_, stylesheetContext) = CaptureSnapshot(documentUri);
        var resolution = UtilityRegistry.Resolve(
            completionLabel,
            stylesheetContext.Theme,
            cancellationToken);
        var metadata = resolution.Metadata
            ?? FindProjectRule(
                stylesheetContext.Compile(completionLabel),
                completionLabel);
        return metadata is null ? null : GetUtilityDocumentation(metadata);
    }

    public IReadOnlyList<LanguageDocumentSymbol> GetDocumentSymbols(
        string documentUri,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(documentUri);
        cancellationToken.ThrowIfCancellationRequested();

        var (document, _) = CaptureSnapshot(documentUri);
        if (document is null)
        {
            return Array.Empty<LanguageDocumentSymbol>();
        }

        var blocks = CollectBlocks(document.Syntax);
        var symbols = new List<LanguageDocumentSymbol>(blocks.Count);
        foreach (var block in blocks)
        {
            cancellationToken.ThrowIfCancellationRequested();
            symbols.Add(
                CreateBlockSymbol(
                    document.Text,
                    block,
                    document.Syntax,
                    CreateScriptMemberSymbols(block)));
        }

        return symbols;
    }

    public IReadOnlyList<LanguageFoldingRange> GetFoldingRanges(
        string documentUri,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(documentUri);
        cancellationToken.ThrowIfCancellationRequested();

        var (document, _) = CaptureSnapshot(documentUri);
        if (document is null)
        {
            return Array.Empty<LanguageFoldingRange>();
        }

        var ranges = new List<LanguageFoldingRange>();
        foreach (var block in CollectBlocks(document.Syntax))
        {
            cancellationToken.ThrowIfCancellationRequested();

            // The fold stops one line short of the block end so the closing delimiter ('}' or
            // '</template>') stays visible while the block is collapsed.
            var startLine = ToLanguagePosition(block.Location.Start).Line;
            var endLine = ToLanguagePosition(block.Location.End).Line - 1;
            if (endLine > startLine)
            {
                ranges.Add(new LanguageFoldingRange(startLine, endLine));
            }

            // Element folding ([V01.01.12.07.07]) and script-interior folding ([V01.01.12.07.10]) are
            // additive: the section range above is unchanged and each multi-line construct inside the
            // block contributes a nested range, emitted right after its section so the whole result
            // stays in document order.
            if (ReferenceEquals(block, document.Syntax.Template))
            {
                ranges.AddRange(document.TemplateElementFoldingRanges);
            }
            else if (ReferenceEquals(block, document.Syntax.Script))
            {
                ranges.AddRange(document.ScriptFoldingRanges);
            }
            else if (ReferenceEquals(block, document.Syntax.ScriptSetup))
            {
                ranges.AddRange(document.ScriptSetupFoldingRanges);
            }
        }

        return ranges;
    }

    public IReadOnlyList<LanguageCodeAction> GetCodeActions(
        string documentUri,
        LanguageRange range,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(documentUri);
        cancellationToken.ThrowIfCancellationRequested();

        var (document, _) = CaptureSnapshot(documentUri);
        if (document is null ||
            document.Syntax.Format != LanguageDocumentFormat.Vue)
        {
            return Array.Empty<LanguageCodeAction>();
        }

        // The parse is authoritative: the Vue-compatibility diagnostics are recomputed here
        // rather than trusting whatever diagnostics the client echoed into the request.
        var actions = new List<LanguageCodeAction>();
        AddScriptLanguageCodeAction(document.Text, document.Syntax.Script, range, actions);
        AddScriptLanguageCodeAction(document.Text, document.Syntax.ScriptSetup, range, actions);
        return actions;
    }

    public LanguageHover? GetHover(
        string documentUri,
        LanguagePosition position,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(documentUri);
        cancellationToken.ThrowIfCancellationRequested();

        var (document, stylesheetContext) = CaptureSnapshot(documentUri);
        if (document is null ||
            !TextCoordinateConverter.TryGetOffset(document.Text, position, out var offset))
        {
            return null;
        }

        var block = FindBlock(document.Syntax, offset);
        var template = block as SingleFileComponentTemplateBlock;
        if (template is not null &&
            UtilityClassCompletionContext.TryCreate(
                template.Content,
                template.ContentLocation.Start.Offset,
                offset,
                out var utilityContext))
        {
            // Match completion precedence: a class authored in this component owns its name, while
            // the utility registry remains the emitted-CSS fallback for undeclared class tokens.
            var styleHover = StyleClassHoverProvider.GetHover(
                document,
                utilityContext,
                cancellationToken);
            if (styleHover is not null)
            {
                return styleHover;
            }

            var utilityHover = GetUtilityClassHover(
                document.Text,
                utilityContext,
                stylesheetContext,
                cancellationToken);
            if (utilityHover is not null)
            {
                return utilityHover;
            }
        }

        // A C# expression inside an attribute value or interpolation must not be documented as if
        // it were markup: without this gate `title="v-if"` hovers as the v-if directive.
        if (template is not null &&
            IsExpressionContext(document.Text, template, offset))
        {
            return null;
        }

        var projectContext = CaptureProjectContext(documentUri);
        if (block is SingleFileComponentScriptBlock script &&
            IsCSharpScript(script) &&
            projectContext is not null)
        {
            var scriptHover = scriptSemantics.GetHover(
                projectContext,
                GetDocumentFilePath(documentUri),
                document.Text,
                offset,
                cancellationToken);
            if (scriptHover is not null)
            {
                return scriptHover;
            }
        }

        if (template is not null)
        {
            if (TemplateCompletionProvider.IsInsideComment(
                    template.Content,
                    offset - template.ContentLocation.Start.Offset))
            {
                return null;
            }

            ScriptComponentContractSnapshot? componentContractSnapshot = null;
            if (projectContext is not null)
            {
                componentContractSnapshot = scriptSemantics.GetComponentContractSnapshot(
                    projectContext,
                    GetDocumentFilePath(documentUri),
                    document.Text,
                    cancellationToken);
            }

            var projection = componentContractSnapshot?.Projection ??
                ProjectDocument(document, projectContext, cancellationToken);
            return TemplateHoverProvider.GetHover(
                document,
                projection,
                new TemplateComponentCatalog(componentContractSnapshot?.Declarations),
                offset);
        }

        var tokenRange = GetTokenRange(document.Text, offset);
        if (tokenRange.End <= tokenRange.Start)
        {
            return null;
        }

        var token = document.Text.Substring(
            tokenRange.Start,
            tokenRange.End - tokenRange.Start);
        if (!TryGetHoverDocumentation(token, out var markdown))
        {
            return null;
        }

        return new LanguageHover(
            markdown,
            new LanguageRange(
                TextCoordinateConverter.GetPosition(document.Text, tokenRange.Start),
                TextCoordinateConverter.GetPosition(document.Text, tokenRange.End)));
    }

    private SingleFileComponentProjectionResult ProjectDocument(
        LanguageDocument document,
        LanguageProjectContext? context,
        CancellationToken cancellationToken)
    {
        Interlocked.Increment(ref standaloneDocumentProjectionBuildCount);
        return LanguageDocumentProjection.Project(document, context, cancellationToken);
    }

    /// <summary>
    /// Captures the immutable per-document state a read computes over. The lock is held only for
    /// the two dictionary lookups; the compute runs outside it over the immutable
    /// <see cref="LanguageDocument"/> and <see cref="UtilityStylesheetLanguageContext"/> values, so
    /// document synchronization is never stalled by a long feature computation.
    /// </summary>
    private (LanguageDocument? Document, UtilityStylesheetLanguageContext UtilityContext) CaptureSnapshot(
        string documentUri)
    {
        lock (synchronization)
        {
            var document = documents.TryGet(documentUri, out var openDocument)
                ? openDocument
                : null;
            return (document, GetUtilityContext(documentUri));
        }
    }

    private LanguageProjectContext? CaptureProjectContext(string documentUri)
    {
        lock (synchronization)
        {
            return projectContexts.TryGetValue(documentUri, out var context)
                ? context
                : null;
        }
    }

    private static IReadOnlyList<LanguageCompletionItem> GetRootCompletions(
        LanguageDocumentSyntax syntax)
    {
        var completions = new List<LanguageCompletionItem>();
        var blockHeaders = syntax.Format == LanguageDocumentFormat.Vue
            ? ViuCompletionCatalog.VueBlockHeaders
            : ViuCompletionCatalog.BlockHeaders;
        foreach (var completion in blockHeaders)
        {
            // Both catalogs label the singleton template block "template" ([V01.01.06.10] made the
            // .viu container a tag, so the legacy "@template" label no longer exists).
            if (completion.Label == "template" &&
                syntax.Template is not null)
            {
                continue;
            }

            if ((completion.Label == "@script" || completion.Label == "script") &&
                syntax.Script is not null)
            {
                continue;
            }

            if (completion.Label == "script setup" &&
                syntax.ScriptSetup is not null)
            {
                continue;
            }

            completions.Add(completion);
        }

        return completions;
    }

    private static IReadOnlyList<LanguageCompletionItem> GetHeaderOptionCompletions(
        string linePrefix,
        LanguageDocumentFormat format)
    {
        var trimmed = linePrefix.TrimStart();

        // A completed header is followed by block content, never further options: '>' closes a tag
        // header, '{' opens an @-block. The '{'-gate applies only to @-headers — a tag header never
        // carries one — and the '>'-gate only to tag headers.
        if (trimmed.StartsWith("<", StringComparison.Ordinal))
        {
            if (trimmed.Contains('>', StringComparison.Ordinal))
            {
                return Array.Empty<LanguageCompletionItem>();
            }
        }
        else if (format == LanguageDocumentFormat.Viu &&
                 trimmed.Contains('{', StringComparison.Ordinal))
        {
            return Array.Empty<LanguageCompletionItem>();
        }

        if (format == LanguageDocumentFormat.Vue)
        {
            if (trimmed.StartsWith("<template ", StringComparison.Ordinal))
            {
                return ViuCompletionCatalog.TemplateHeaderOptions;
            }

            if (trimmed.StartsWith("<script ", StringComparison.Ordinal))
            {
                return ViuCompletionCatalog.VueScriptHeaderOptions;
            }

            if (trimmed.StartsWith("<style ", StringComparison.Ordinal))
            {
                return ViuCompletionCatalog.StyleHeaderOptions;
            }

            return Array.Empty<LanguageCompletionItem>();
        }

        // Hybrid .viu ([V01.01.06.10]): template/style options are tag attributes, @script keeps the
        // @-option grammar, and the legacy @template/@style headers keep completing during the
        // migration window (the option catalogs are valid in both grammars).
        if (trimmed.StartsWith("<template ", StringComparison.Ordinal) ||
            trimmed.StartsWith("@template ", StringComparison.Ordinal))
        {
            return ViuCompletionCatalog.TemplateHeaderOptions;
        }

        if (trimmed.StartsWith("@script ", StringComparison.Ordinal))
        {
            return ViuCompletionCatalog.ScriptHeaderOptions;
        }

        if (trimmed.StartsWith("<style ", StringComparison.Ordinal) ||
            trimmed.StartsWith("@style ", StringComparison.Ordinal))
        {
            return ViuCompletionCatalog.StyleHeaderOptions;
        }

        return Array.Empty<LanguageCompletionItem>();
    }

    private static IReadOnlyList<LanguageCompletionItem> GetTemplateCompletions(
        string documentText,
        int offset,
        string linePrefix)
    {
        var trimmed = linePrefix.TrimStart();
        if (trimmed.EndsWith('@') || LastTokenStartsWith(trimmed, "@"))
        {
            return ViuCompletionCatalog.TemplateEvents;
        }

        if (LastTokenStartsWith(trimmed, "v-"))
        {
            return ViuCompletionCatalog.TemplateDirectives;
        }

        if (trimmed.EndsWith(':') || LastTokenStartsWith(trimmed, ":"))
        {
            return ViuCompletionCatalog.TemplateBindings;
        }

        var completions = new List<LanguageCompletionItem>(
            ViuCompletionCatalog.TemplateTags.Count +
            ViuCompletionCatalog.TemplateDirectives.Count +
            ViuCompletionCatalog.TemplateEvents.Count +
            ViuCompletionCatalog.TemplateBindings.Count);
        AppendTagCompletions(documentText, offset, completions);
        completions.AddRange(ViuCompletionCatalog.TemplateDirectives);
        completions.AddRange(ViuCompletionCatalog.TemplateEvents);
        completions.AddRange(ViuCompletionCatalog.TemplateBindings);
        return completions;
    }

    /// <summary>
    /// Appends the element catalog, each item carrying the range its insertion replaces.
    /// </summary>
    /// <remarks>
    /// A tag's insertion opens with <c>&lt;</c>, which the author has almost always typed already —
    /// that keystroke is what asked for the list. An editor left to guess the replaced span takes the
    /// typed <em>word</em>, and <c>&lt;</c> is no part of a word, so committing appends a second one
    /// (<c>&lt;&lt;template&gt;</c>). Naming the range removes the guess: it starts at the <c>&lt;</c>
    /// when one is there and at the partially typed name when it is not.
    /// </remarks>
    private static void AppendTagCompletions(
        string documentText,
        int offset,
        List<LanguageCompletionItem> completions)
    {
        var editRange = GetTagEditRange(documentText, offset);
        foreach (var tag in ViuCompletionCatalog.TemplateTags)
        {
            completions.Add(editRange is null ? tag : tag with { EditRange = editRange });
        }
    }

    private static LanguageRange? GetTagEditRange(string documentText, int offset)
    {
        var start = offset;
        while (start > 0 && IsTagNameCharacter(documentText[start - 1]))
        {
            start--;
        }

        if (start > 0 && documentText[start - 1] == '<')
        {
            start--;
        }

        return start == offset
            ? null
            : new LanguageRange(
                TextCoordinateConverter.GetPosition(documentText, start),
                TextCoordinateConverter.GetPosition(documentText, offset));
    }

    private static bool IsTagNameCharacter(char character)
        => char.IsLetterOrDigit(character) || character is '-' or '_' or '.';

    private static bool IsExpressionContext(
        string documentText,
        SingleFileComponentTemplateBlock template,
        int offset)
    {
        if (UtilityCandidateScanner.IsInsideAttributeValue(
                template.Content,
                offset - template.ContentLocation.Start.Offset))
        {
            return true;
        }

        // Mustache interpolations do not nest, so the nearest unmatched opener decides.
        var prefix = documentText.AsSpan(0, offset);
        return prefix.LastIndexOf("{{".AsSpan()) > prefix.LastIndexOf("}}".AsSpan());
    }

    private static IReadOnlyList<LanguageCompletionItem> GetUtilityClassCompletions(
        string documentText,
        UtilityClassCompletionContext context,
        UtilityStylesheetLanguageContext stylesheetContext,
        CancellationToken cancellationToken)
    {
        var editRange = new LanguageRange(
            TextCoordinateConverter.GetPosition(documentText, context.TokenStart),
            TextCoordinateConverter.GetPosition(documentText, context.TokenEnd));
        var metadataByCandidate =
            new Dictionary<string, UtilityClassMetadata>(
                StringComparer.Ordinal);
        foreach (var metadata in UtilityRegistry.GetCompletions(
                     context.Prefix,
                     stylesheetContext.Theme))
        {
            metadataByCandidate[metadata.CandidateText] = metadata;
        }

        // The token is checked between candidate batches: each batch is bounded work, and a
        // superseded keystroke's request should stop paying for project stylesheet compiles.
        cancellationToken.ThrowIfCancellationRequested();
        AddProjectUtilityCompletions(
            context.Prefix,
            stylesheetContext,
            metadataByCandidate);
        cancellationToken.ThrowIfCancellationRequested();
        AddProjectVariantCompletions(
            context.Prefix,
            stylesheetContext,
            metadataByCandidate);
        cancellationToken.ThrowIfCancellationRequested();

        var colorResolver = UtilityColorValueResolver.Create(stylesheetContext.Theme);
        return metadataByCandidate.Values
            .OrderBy(metadata => metadata.SortOrder)
            .ThenBy(metadata => metadata.CandidateText, StringComparer.Ordinal)
            .Take(LanguageCompletionLimits.MaximumItems)
            .Select(
                metadata => CreateUtilityCompletion(
                    metadata,
                    editRange,
                    colorResolver))
            .ToArray();
    }

    private static void AddProjectUtilityCompletions(
        string typedPrefix,
        UtilityStylesheetLanguageContext stylesheetContext,
        IDictionary<string, UtilityClassMetadata> metadataByCandidate)
    {
        SplitCandidatePrefix(
            typedPrefix,
            out var variantPrefix,
            out var baseFragment);
        var candidates = new List<string>();
        foreach (var definition in
                 stylesheetContext.ProjectCompilation.Utilities)
        {
            var baseCandidate = definition.IsFunctional
                ? definition.Name.Substring(
                    0,
                    definition.Name.Length - "-*".Length)
                : definition.Name;
            if (!baseCandidate.StartsWith(
                    baseFragment,
                    StringComparison.Ordinal))
            {
                continue;
            }

            var candidate = ComposeProjectCandidate(
                variantPrefix,
                stylesheetContext.Theme.Prefix,
                baseCandidate);
            candidates.Add(candidate);
        }

        foreach (var metadata in
                 stylesheetContext.Compile(candidates).Rules)
        {
            metadataByCandidate[metadata.CandidateText] = metadata;
        }
    }

    private static void AddProjectVariantCompletions(
        string typedPrefix,
        UtilityStylesheetLanguageContext stylesheetContext,
        IDictionary<string, UtilityClassMetadata> metadataByCandidate)
    {
        SplitCandidatePrefix(
            typedPrefix,
            out var variantPrefix,
            out var baseFragment);
        if (variantPrefix.Length == 0 ||
            !ContainsProjectVariant(
                variantPrefix,
                stylesheetContext.ProjectCompilation.Variants))
        {
            return;
        }

        var themePrefix = stylesheetContext.Theme.Prefix;
        if (!string.IsNullOrEmpty(themePrefix) &&
            !variantPrefix.StartsWith(
                themePrefix + ":",
                StringComparison.Ordinal))
        {
            return;
        }

        var builtInLookupPrefix =
            string.IsNullOrEmpty(themePrefix)
                ? baseFragment
                : themePrefix + ":" + baseFragment;
        var candidates = new List<string>();
        foreach (var builtIn in UtilityRegistry.GetCompletions(
                     builtInLookupPrefix,
                     stylesheetContext.Theme))
        {
            var baseCandidate = builtIn.CandidateText;
            if (!string.IsNullOrEmpty(themePrefix))
            {
                baseCandidate = baseCandidate.Substring(
                    themePrefix.Length + 1);
            }

            var candidate = variantPrefix + baseCandidate;
            candidates.Add(candidate);
        }

        foreach (var metadata in
                 stylesheetContext.Compile(candidates).Rules)
        {
            metadataByCandidate[metadata.CandidateText] = metadata;
        }
    }

    private static LanguageCompletionItem CreateUtilityCompletion(
        UtilityClassMetadata metadata,
        LanguageRange editRange,
        UtilityColorValueResolver colorResolver)
    {
        var colorValue = colorResolver.Resolve(metadata);
        return new LanguageCompletionItem(
            metadata.CandidateText,
            colorValue is null
                ? LanguageCompletionItemKind.Property
                : LanguageCompletionItemKind.Color,
            "Viu utility class",
            // Utility documentation is deferred to ResolveCompletionDocumentation: interpolating
            // and serializing up to 500 CSS bodies per keystroke dominated the completion payload
            // while the editor renders documentation for one selected item at a time.
            Documentation: string.Empty,
            metadata.CandidateText,
            IsSnippet: false,
            SortText: $"{metadata.SortOrder:D5}:{metadata.CandidateText}",
            EditRange: editRange,
            FilterText: metadata.CandidateText,
            ColorValue: colorValue);
    }

    private static UtilityClassMetadata? FindProjectRule(
        UtilityProjectStylesheetCompilationResult compilation,
        string candidate)
    {
        foreach (var rule in compilation.Rules)
        {
            if (string.Equals(
                    rule.CandidateText,
                    candidate,
                    StringComparison.Ordinal))
            {
                return rule;
            }
        }

        return null;
    }

    private static string ComposeProjectCandidate(
        string variantPrefix,
        string? themePrefix,
        string baseCandidate)
    {
        if (variantPrefix.Length > 0)
        {
            return variantPrefix + baseCandidate;
        }

        return string.IsNullOrEmpty(themePrefix)
            ? baseCandidate
            : themePrefix + ":" + baseCandidate;
    }

    private static bool ContainsProjectVariant(
        string variantPrefix,
        UtilityCollection<UtilityCustomVariantDefinition> definitions)
    {
        var segments = variantPrefix.Split(':');
        foreach (var segment in segments)
        {
            foreach (var definition in definitions)
            {
                if (string.Equals(
                        segment,
                        definition.Name,
                        StringComparison.Ordinal))
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static void SplitCandidatePrefix(
        string typedPrefix,
        out string variantPrefix,
        out string baseFragment)
    {
        var separator = typedPrefix.LastIndexOf(':');
        if (separator < 0)
        {
            variantPrefix = string.Empty;
            baseFragment = typedPrefix;
            return;
        }

        variantPrefix = typedPrefix.Substring(0, separator + 1);
        baseFragment = typedPrefix.Substring(separator + 1);
    }

    private static LanguageHover? GetUtilityClassHover(
        string documentText,
        UtilityClassCompletionContext context,
        UtilityStylesheetLanguageContext stylesheetContext,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(context.TokenText))
        {
            return null;
        }

        var resolution = UtilityRegistry.Resolve(
            context.TokenText,
            stylesheetContext.Theme,
            cancellationToken);
        var metadata = resolution.Metadata;
        if (metadata is null)
        {
            metadata = FindProjectRule(
                stylesheetContext.Compile(context.TokenText),
                context.TokenText);
            if (metadata is null)
            {
                return null;
            }
        }

        return new LanguageHover(
            GetUtilityDocumentation(metadata),
            new LanguageRange(
                TextCoordinateConverter.GetPosition(documentText, context.TokenStart),
                TextCoordinateConverter.GetPosition(documentText, context.TokenEnd)));
    }

    private static string GetUtilityDocumentation(UtilityClassMetadata metadata)
        => $"**`{metadata.CandidateText}`** — {metadata.Description}\n\n```css\n{metadata.Css}\n```";

    // Reads utilityContexts, so every caller must hold the synchronization lock.
    private UtilityStylesheetLanguageContext GetUtilityContext(string documentUri)
        => utilityContexts.TryGetValue(documentUri, out var context)
            ? context
            : UtilityStylesheetLanguageContext.Default;

    private IReadOnlyList<LanguageCompletionItem> GetScriptCompletions(
        string documentUri,
        LanguageDocument document,
        SingleFileComponentScriptBlock script,
        int offset,
        string linePrefix,
        CancellationToken cancellationToken)
    {
        // Every completion source is gated by the position's syntactic context first: the member
        // lookup, the Viu scaffold catalog, and the keyword catalog are all illegal inside a
        // using directive's namespace-or-type name, and offering them there — the component's own
        // private field included — reads as the editor ignoring the C# grammar entirely.
        var contextKind = ScriptCompletionContext.Classify(
            script.Content,
            offset - script.ContentLocation.Start.Offset);
        if (contextKind == ScriptCompletionContextKind.Suppressed)
        {
            return Array.Empty<LanguageCompletionItem>();
        }

        var word = GetTrailingIdentifier(linePrefix);
        var semantic = GetScriptSemanticCompletions(
            documentUri,
            document,
            script,
            offset,
            word,
            contextKind,
            cancellationToken);
        if (semantic is null)
        {
            // ANY engine miss — no project context, an unmapped position, a binder failure —
            // falls through to the existing syntax-only path unchanged: the fallback is the
            // existing code, not a new one. A using directive is the one exception: without a
            // compilation the service knows no namespace, and the honest degraded answer is
            // nothing rather than the member-and-snippet list that is illegal there.
            return contextKind == ScriptCompletionContextKind.UsingDirective
                ? Array.Empty<LanguageCompletionItem>()
                : GetSyntaxOnlyScriptCompletions(linePrefix, document.Syntax);
        }

        // The engine already restricted a using directive's lookup to namespaces and types and
        // ordered namespaces first; appending the scaffold or keyword catalogs would put them back.
        return contextKind == ScriptCompletionContextKind.UsingDirective
            ? semantic.Items
            : MergeScriptSemanticCompletions(semantic, word);
    }

    // The semantic gate ([V01.01.12.23], #259): only an (implicitly or explicitly) C# script
    // block with a host-fed project context reaches the engine; a .vue block in another language
    // already carries VIU1206 and is never treated as C#.
    private ScriptSemanticCompletionResult? GetScriptSemanticCompletions(
        string documentUri,
        LanguageDocument document,
        SingleFileComponentScriptBlock script,
        int offset,
        string word,
        ScriptCompletionContextKind contextKind,
        CancellationToken cancellationToken)
    {
        if (script.Lang is not null &&
            !string.Equals(script.Lang, "csharp", StringComparison.Ordinal))
        {
            return null;
        }

        LanguageProjectContext? context;
        lock (synchronization)
        {
            context = projectContexts.TryGetValue(documentUri, out var configured)
                ? configured
                : null;
        }

        if (context is null)
        {
            return null;
        }

        return scriptSemantics.GetCompletions(
            context,
            documentUri,
            GetDocumentFilePath(documentUri),
            document.Text,
            offset,
            word,
            contextKind,
            cancellationToken);
    }

    // On an engine hit the semantic items REPLACE the declared-member list; the scaffold, root
    // lifecycle, and keyword catalogs still append with the existing first-wins label dedup and
    // SortText bands ("00:" semantic, "01"–"07" scaffold, "08" root lifecycle, "90" keywords).
    // After a member access only the matching Context./Reactive. member catalog appends — keywords
    // never follow a dot, exactly as the syntax-only path never offered them there.
    private static IReadOnlyList<LanguageCompletionItem> MergeScriptSemanticCompletions(
        ScriptSemanticCompletionResult semantic,
        string word)
    {
        var completions = new List<LanguageCompletionItem>(
            semantic.Items.Count +
            ViuCompletionCatalog.ScriptGeneral.Count +
            ViuCompletionCatalog.RootLifecycleRegistrations.Count +
            ViuCompletionCatalog.ScriptKeywords.Count);
        var seenLabels = new HashSet<string>(StringComparer.Ordinal);
        AppendCatalogItems(semantic.Items, completions, seenLabels);
        if (semantic.IsMemberAccess)
        {
            if (string.Equals(
                    semantic.MemberAccessTargetText,
                    "Context",
                    StringComparison.Ordinal))
            {
                AppendCatalogItems(ViuCompletionCatalog.ContextMembers, completions, seenLabels);
            }
            else if (string.Equals(
                         semantic.MemberAccessTargetText,
                         "Reactive",
                         StringComparison.Ordinal))
            {
                AppendCatalogItems(ViuCompletionCatalog.ReactiveMembers, completions, seenLabels);
            }
        }
        else
        {
            AppendCatalogItems(ViuCompletionCatalog.ScriptGeneral, completions, seenLabels);
            AppendCatalogItems(
                ViuCompletionCatalog.RootLifecycleRegistrations,
                completions,
                seenLabels);
            AppendCatalogItems(ViuCompletionCatalog.ScriptKeywords, completions, seenLabels);
        }

        if (word.Length == 0)
        {
            return completions;
        }

        var matches = completions
            .Where(
                completion => completion.Label.StartsWith(
                    word,
                    StringComparison.OrdinalIgnoreCase))
            .ToArray();
        return matches.Length > 0 ? matches : completions;
    }

    private static string GetDocumentFilePath(string documentUri)
        => Uri.TryCreate(documentUri, UriKind.Absolute, out var uri) && uri.IsFile
            ? uri.LocalPath
            : documentUri;

    private static bool IsCSharpScript(SingleFileComponentScriptBlock? script)
        => script is not null &&
           (script.Lang is null ||
            string.Equals(script.Lang, "csharp", StringComparison.Ordinal));

    private IReadOnlyList<LanguageCompletionItem> GetSyntaxOnlyScriptCompletions(
        string linePrefix,
        LanguageDocumentSyntax syntax)
    {
        if (linePrefix.Contains("Context.", StringComparison.Ordinal))
        {
            return ViuCompletionCatalog.ContextMembers;
        }

        if (linePrefix.Contains("Reactive.", StringComparison.Ordinal))
        {
            return ViuCompletionCatalog.ReactiveMembers;
        }

        // Declared members ([V01.01.12.07.04], #261), the Viu scaffold items, and the C# keywords
        // are offered together, then filtered by the partially typed word. Returning the unfiltered
        // scaffold list answered `u` with "mounted callback" and never with `using`, which reads as
        // the editor ignoring what was typed. Ranking comes from SortText: declared "00:" orders
        // before scaffold "01"–"07", which orders before keyword "90". Deduplication is first-wins
        // by label, so an author-declared `Context`/`OnSetup` (already flagged VIU1204 by the
        // generator) shows the author's real declaration rather than a duplicate pair; keywords
        // cannot collide because a member identifier spelled as a keyword carries its `@`.
        var word = GetTrailingIdentifier(linePrefix);
        var completions = new List<LanguageCompletionItem>(
            ViuCompletionCatalog.ScriptGeneral.Count +
            ViuCompletionCatalog.RootLifecycleRegistrations.Count +
            ViuCompletionCatalog.ScriptKeywords.Count);
        var seenLabels = new HashSet<string>(StringComparer.Ordinal);

        // The union of both script blocks: a .vue file's plain and setup scripts merge into one
        // generated partial class, so a completion in either block legitimately sees both.
        AppendDeclaredMembers(syntax.Script, completions, seenLabels);
        AppendDeclaredMembers(syntax.ScriptSetup, completions, seenLabels);
        AppendCatalogItems(ViuCompletionCatalog.ScriptGeneral, completions, seenLabels);
        // The root-level lifecycle registrations ([CMP-32]) are inherited members the semantic
        // engine binds for itself; the static catalog carries them so the degraded, compilation-free
        // answer still knows the idiomatic authoring surface.
        AppendCatalogItems(
            ViuCompletionCatalog.RootLifecycleRegistrations,
            completions,
            seenLabels);
        AppendCatalogItems(ViuCompletionCatalog.ScriptKeywords, completions, seenLabels);

        if (word.Length == 0)
        {
            return completions;
        }

        var matches = completions
            .Where(
                completion => completion.Label.StartsWith(
                    word,
                    StringComparison.OrdinalIgnoreCase))
            .ToArray();
        return matches.Length > 0 ? matches : completions;
    }

    private void AppendDeclaredMembers(
        SingleFileComponentScriptBlock? script,
        List<LanguageCompletionItem> completions,
        HashSet<string> seenLabels)
    {
        if (script is null)
        {
            return;
        }

        foreach (var member in scriptDeclarations.Read(script.Content))
        {
            if (!seenLabels.Add(member.Name))
            {
                continue;
            }

            completions.Add(new LanguageCompletionItem(
                member.Name,
                ToCompletionKind(member.Kind),
                member.Detail,
                member.DocumentationSummary ??
                    $"Declared in this file's @script block as `{member.Detail} {member.Name}`.",
                member.Name,
                IsSnippet: false,
                SortText: "00:" + member.Name));
        }
    }

    // The service-edge materialization of the shared projection's host-neutral member kind
    // ([V01.01.06.11], #258): the library deliberately names no editor enum, so each host maps at
    // its own boundary — the completion analogue of the generator's diagnostic adapter.
    private static LanguageCompletionItemKind ToCompletionKind(ScriptDeclaredMemberKind kind)
        => kind switch
        {
            ScriptDeclaredMemberKind.Property => LanguageCompletionItemKind.Property,
            ScriptDeclaredMemberKind.Method => LanguageCompletionItemKind.Method,
            _ => LanguageCompletionItemKind.Field,
        };

    private static void AppendCatalogItems(
        IReadOnlyList<LanguageCompletionItem> catalog,
        List<LanguageCompletionItem> completions,
        HashSet<string> seenLabels)
    {
        foreach (var item in catalog)
        {
            if (seenLabels.Add(item.Label))
            {
                completions.Add(item);
            }
        }
    }

    private static string GetTrailingIdentifier(string linePrefix)
    {
        var start = linePrefix.Length;
        while (start > 0 &&
               (char.IsLetterOrDigit(linePrefix[start - 1]) || linePrefix[start - 1] == '_'))
        {
            start--;
        }

        return linePrefix.Substring(start);
    }

    private static SingleFileComponentBlock? FindBlock(
        LanguageDocumentSyntax syntax,
        int offset)
    {
        if (ContainsOffset(syntax.Template, offset))
        {
            return syntax.Template;
        }

        if (ContainsOffset(syntax.Script, offset))
        {
            return syntax.Script;
        }

        if (ContainsOffset(syntax.ScriptSetup, offset))
        {
            return syntax.ScriptSetup;
        }

        foreach (var style in syntax.Styles)
        {
            if (ContainsOffset(style, offset))
            {
                return style;
            }
        }

        foreach (var customBlock in syntax.CustomBlocks)
        {
            if (ContainsOffset(customBlock, offset))
            {
                return customBlock;
            }
        }

        return null;
    }

    private static bool ContainsOffset(SingleFileComponentBlock? block, int offset)
        => block is not null &&
           offset >= block.ContentLocation.Start.Offset &&
           offset <= block.ContentLocation.End.Offset;

    private static LanguageDiagnostic ToLanguageDiagnostic(SingleFileComponentError error)
        => new(
            new LanguageRange(
                ToLanguagePosition(error.Location.Start),
                ToLanguagePosition(error.Location.End)),
            error.Severity switch
            {
                DiagnosticSeverity.Warning => LanguageDiagnosticSeverity.Warning,
                DiagnosticSeverity.Information => LanguageDiagnosticSeverity.Information,
                DiagnosticSeverity.Hidden => LanguageDiagnosticSeverity.Hint,
                _ => LanguageDiagnosticSeverity.Error,
            },
            $"VIU{error.RawCode}",
            error.Message,
            "viu");

    private static void AddVueCompatibilityDiagnostics(
        LanguageDocumentSyntax syntax,
        ICollection<LanguageDiagnostic> diagnostics)
    {
        if (syntax.Format != LanguageDocumentFormat.Vue)
        {
            return;
        }

        AddVueScriptLanguageDiagnostic(syntax.Script, diagnostics);
        AddVueScriptLanguageDiagnostic(syntax.ScriptSetup, diagnostics);
    }

    private static void AddVueScriptLanguageDiagnostic(
        SingleFileComponentScriptBlock? script,
        ICollection<LanguageDiagnostic> diagnostics)
    {
        var diagnostic = CreateVueScriptLanguageDiagnostic(script);
        if (diagnostic is not null)
        {
            diagnostics.Add(diagnostic);
        }
    }

    private static LanguageDiagnostic? CreateVueScriptLanguageDiagnostic(
        SingleFileComponentScriptBlock? script)
    {
        if (script is null ||
            string.Equals(script.Lang, "csharp", StringComparison.Ordinal))
        {
            return null;
        }

        var languageOption = FindOption(script, "lang");
        var authoredLanguage = script.Lang is null
            ? "an implicit JavaScript language"
            : $"'{script.Lang}'";
        return new LanguageDiagnostic(
            ToLanguageRange(languageOption?.Location ?? script.Location),
            LanguageDiagnosticSeverity.Error,
            "VIU1206",
            $"The .vue script uses {authoredLanguage}. Viu accepts only an explicit lang=\"csharp\" attribute and never executes JavaScript.",
            "viu");
    }

    private static void AddScriptLanguageCodeAction(
        string documentText,
        SingleFileComponentScriptBlock? script,
        LanguageRange requestRange,
        ICollection<LanguageCodeAction> actions)
    {
        var diagnostic = CreateVueScriptLanguageDiagnostic(script);
        if (diagnostic is null ||
            !RangesIntersect(diagnostic.Range, requestRange))
        {
            return;
        }

        LanguageTextEdit edit;
        var languageOption = FindOption(script!, "lang");
        if (languageOption is not null)
        {
            edit = new LanguageTextEdit(
                ToLanguageRange(languageOption.Location),
                "lang=\"csharp\"");
        }
        else
        {
            // Absent lang: insert before the header's '>'. Parser recovery can leave a malformed
            // header whose content does not start right after '>', in which case no edit is safe.
            var insertOffset = script!.ContentLocation.Start.Offset - 1;
            if (insertOffset < 0 ||
                insertOffset >= documentText.Length ||
                documentText[insertOffset] != '>')
            {
                return;
            }

            var insertPosition = TextCoordinateConverter.GetPosition(documentText, insertOffset);
            edit = new LanguageTextEdit(
                new LanguageRange(insertPosition, insertPosition),
                " lang=\"csharp\"");
        }

        actions.Add(
            new LanguageCodeAction(
                "Use lang=\"csharp\"",
                "quickfix",
                [diagnostic],
                [edit],
                IsPreferred: true));
    }

    private static IReadOnlyList<SingleFileComponentBlock> CollectBlocks(
        LanguageDocumentSyntax syntax)
    {
        var blocks = new List<SingleFileComponentBlock>();
        if (syntax.Template is not null)
        {
            blocks.Add(syntax.Template);
        }

        if (syntax.Script is not null)
        {
            blocks.Add(syntax.Script);
        }

        if (syntax.ScriptSetup is not null)
        {
            blocks.Add(syntax.ScriptSetup);
        }

        foreach (var style in syntax.Styles)
        {
            blocks.Add(style);
        }

        foreach (var customBlock in syntax.CustomBlocks)
        {
            blocks.Add(customBlock);
        }

        blocks.Sort(
            (left, right) =>
                left.Location.Start.Offset.CompareTo(right.Location.Start.Offset));
        return blocks;
    }

    private static LanguageDocumentSymbol CreateBlockSymbol(
        string documentText,
        SingleFileComponentBlock block,
        LanguageDocumentSyntax syntax,
        IReadOnlyList<LanguageDocumentSymbol> children)
    {
        // The hybrid container ([V01.01.06.10]) mixes tag blocks and @-blocks in one document, so
        // the authored container decides the symbol name: the block starts at '<' for a tag and at
        // '@' for an @-block.
        var startOffset = block.Location.Start.Offset;
        var isTagContainer =
            startOffset < documentText.Length &&
            documentText[startOffset] == '<';
        var isSetupScript = ReferenceEquals(block, syntax.ScriptSetup);
        var name = isTagContainer
            ? isSetupScript ? $"<{block.Name} setup>" : $"<{block.Name}>"
            : $"@{block.Name}";

        var start = ToLanguagePosition(block.Location.Start);
        // The selection is the header name token: '@name' for an @-block, 'name' after '<' for a
        // tag. The Language Server Protocol requires the selection to sit inside the full range.
        var selectionStart = isTagContainer
            ? start with { Character = start.Character + 1 }
            : start;
        var selectionLength = isTagContainer
            ? block.Name.Length
            : block.Name.Length + 1;
        var selectionRange = new LanguageRange(
            selectionStart,
            selectionStart with { Character = selectionStart.Character + selectionLength });

        return new LanguageDocumentSymbol(
            name,
            CreateBlockDetail(block, includeSetupOption: !isSetupScript),
            block.Kind == SingleFileComponentBlockKind.Script
                ? LanguageSymbolKind.Class
                : LanguageSymbolKind.Module,
            ToLanguageRange(block.Location),
            selectionRange,
            children);
    }

    private IReadOnlyList<LanguageDocumentSymbol> CreateScriptMemberSymbols(
        SingleFileComponentBlock block)
    {
        // Only a C# script block contributes member children: .viu @script is implicitly C#, and a
        // .vue script participates only with an explicit lang="csharp" (anything else already carries
        // the VIU1206 diagnostic and is never parsed as C#).
        if (block is not SingleFileComponentScriptBlock script ||
            (script.Lang is not null &&
             !string.Equals(script.Lang, "csharp", StringComparison.Ordinal)))
        {
            return Array.Empty<LanguageDocumentSymbol>();
        }

        var members = scriptDeclarations.Read(script.Content);
        if (members.Count == 0)
        {
            return Array.Empty<LanguageDocumentSymbol>();
        }

        var contentStart = script.ContentLocation.Start;
        var symbols = new List<LanguageDocumentSymbol>(members.Count);
        foreach (var member in members)
        {
            symbols.Add(
                new LanguageDocumentSymbol(
                    member.Name,
                    member.Detail,
                    ToSymbolKind(member.Kind),
                    ComposeScriptRange(contentStart, member.MemberLocation),
                    ComposeScriptRange(contentStart, member.IdentifierLocation),
                    Array.Empty<LanguageDocumentSymbol>()));
        }

        return symbols;
    }

    private static LanguageRange ComposeScriptRange(
        Position blockContentStart,
        SourceLocation relative)
        => new(
            ComposeScriptPosition(blockContentStart, relative.Start),
            ComposeScriptPosition(blockContentStart, relative.End));

    private static LanguagePosition ComposeScriptPosition(
        Position blockContentStart,
        Position relative)
    {
        // The same block-to-file arithmetic the emitted #line map uses
        // (SingleFileComponentDiagnostics.ComposeToFilePosition), so outline positions and build
        // positions cannot disagree ([V01.01.06.11]).
        var (line, column) = SingleFileComponentDiagnostics.ComposeToFilePosition(
            blockContentStart,
            relative);
        return new LanguagePosition(line - 1, column - 1);
    }

    private static LanguageSymbolKind ToSymbolKind(ScriptDeclaredMemberKind kind)
        => kind switch
        {
            ScriptDeclaredMemberKind.Field => LanguageSymbolKind.Field,
            ScriptDeclaredMemberKind.Property => LanguageSymbolKind.Property,
            _ => LanguageSymbolKind.Method,
        };

    private static string? CreateBlockDetail(
        SingleFileComponentBlock block,
        bool includeSetupOption)
    {
        var parts = new List<string>(block.Options.Count);
        foreach (var option in block.Options)
        {
            // A setup script folds the 'setup' flag into the symbol name; repeating it in the
            // detail would read as a duplicate option.
            if (!includeSetupOption &&
                option.Value is null &&
                string.Equals(option.Name, "setup", StringComparison.Ordinal))
            {
                continue;
            }

            parts.Add(
                option.Value is null
                    ? option.Name
                    : $"{option.Name}=\"{option.Value}\"");
        }

        return parts.Count == 0 ? null : string.Join(' ', parts);
    }

    private static bool RangesIntersect(LanguageRange left, LanguageRange right)
        => ComparePositions(left.Start, right.End) <= 0 &&
           ComparePositions(right.Start, left.End) <= 0;

    private static int ComparePositions(LanguagePosition left, LanguagePosition right)
        => left.Line != right.Line
            ? left.Line.CompareTo(right.Line)
            : left.Character.CompareTo(right.Character);

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

    private static LanguageRange ToLanguageRange(SourceLocation location)
        => new(
            ToLanguagePosition(location.Start),
            ToLanguagePosition(location.End));

    private static LanguagePosition ToLanguagePosition(Position position)
        => new(
            Math.Max(position.Line - 1, 0),
            Math.Max(position.Column - 1, 0));

    private static bool LastTokenStartsWith(string text, string prefix)
    {
        var lastWhitespace = text.LastIndexOfAny([' ', '\t', '\r', '\n', '<']);
        var tokenStart = lastWhitespace + 1;
        return text.AsSpan(tokenStart).StartsWith(prefix, StringComparison.Ordinal);
    }

    private static (int Start, int End) GetTokenRange(string text, int offset)
    {
        if (offset == text.Length && offset > 0)
        {
            offset--;
        }
        else if (offset < text.Length &&
                 !IsTokenCharacter(text[offset]) &&
                 offset > 0 &&
                 IsTokenCharacter(text[offset - 1]))
        {
            offset--;
        }

        if (offset < 0 || offset >= text.Length || !IsTokenCharacter(text[offset]))
        {
            return (offset, offset);
        }

        var start = offset;
        while (start > 0 && IsTokenCharacter(text[start - 1]))
        {
            start--;
        }

        var end = offset + 1;
        while (end < text.Length && IsTokenCharacter(text[end]))
        {
            end++;
        }

        return (start, end);
    }

    private static bool IsTokenCharacter(char character)
        => char.IsLetterOrDigit(character) ||
           character is '_' or '-' or '.' or '@';

    private static bool TryGetHoverDocumentation(string token, out string markdown)
    {
        var candidate = token;
        while (candidate.Length > 0)
        {
            if (HoverDocumentation.TryGetValue(candidate, out markdown!))
            {
                return true;
            }

            var lastDot = candidate.LastIndexOf('.');
            if (lastDot < 0)
            {
                break;
            }

            candidate = candidate.Substring(0, lastDot);
        }

        markdown = string.Empty;
        return false;
    }
}
