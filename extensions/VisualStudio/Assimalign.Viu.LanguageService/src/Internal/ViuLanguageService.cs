using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

using Assimalign.Viu.Syntax;
using Assimalign.Viu.Syntax.SingleFileComponent;
using Assimalign.Viu.Tooling.UtilityCss;

namespace Assimalign.Viu.LanguageService;

internal sealed class ViuLanguageService : IViuLanguageService, IUtilityCssLanguageService
{
    private static readonly UtilityCssRegistry UtilityRegistry = UtilityCssRegistry.BuiltIn;

    private static readonly IReadOnlyDictionary<string, string> HoverDocumentation =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["@template"] = "**`@template`** contains Vue-compatible template markup for the component.",
            ["@script"] = "**`@script`** contains C# members merged into the generated partial component.",
            ["@style"] = "**`@style`** contains component CSS and can be `scoped` or a CSS `module`.",
            ["v-if"] = "**`v-if`** conditionally renders an element or component.",
            ["v-for"] = "**`v-for`** repeats an element or component for values in a source.",
            ["v-model"] = "**`v-model`** creates a two-way form value binding.",
            ["@click"] = "**`@click`** registers a click event handler. Task-returning handlers are supported.",
            ["Context"] = "**`Context`** is the generated component's `IComponentContext`.",
            ["Context.Arguments"] = "**`Context.Arguments`** exposes arguments supplied by the parent render.",
            ["Context.Attributes"] = "**`Context.Attributes`** exposes undeclared fallthrough attributes.",
            ["Context.Components"] = "**`Context.Components`** is the application-selected `IComponentFactory`.",
            ["Context.Services"] = "**`Context.Services`** is the application's independent `IServiceProvider`.",
            ["Context.Lifecycle"] = "**`Context.Lifecycle`** registers lifecycle callbacks and exposes component cancellation.",
            ["Context.Slots"] = "**`Context.Slots`** exposes the component's current named slots.",
            ["Context.Emit"] = "**`Context.Emit`** emits a declared component event to the parent.",
            ["Context.Expose"] = "**`Context.Expose`** selects the public surface returned through template references.",
            ["Reactive"] = "**`Reactive`** is Viu's Vue-compatible reactivity facade.",
            ["Reactive.Reference"] = "**`Reactive.Reference(value)`** creates a reactive `Reference<T>` read and written through `.Value`.",
            ["Reactive.Computed"] = "**`Reactive.Computed(getter)`** creates a lazy cached computed value.",
            ["Reactive.Watch"] = "**`Reactive.Watch`** observes a reactive source and returns a disposable `WatchHandle`.",
            ["Reactive.WatchEffect"] = "**`Reactive.WatchEffect`** runs an automatically tracked watcher.",
        };

    private readonly object synchronization = new();
    private readonly LanguageDocumentStore documents = new();
    // Document URIs must compare exactly as they do in the server host's open-document set, or a
    // client that varies casing (a Windows drive letter, most commonly) reports a document as open
    // while the workspace fails to find it and every language feature silently returns nothing.
    private readonly Dictionary<string, UtilityStylesheetLanguageContext> utilityContexts =
        new(StringComparer.OrdinalIgnoreCase);

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

            utilityContexts[documentUri] =
                UtilityStylesheetLanguageContext.Create(
                    stylesheetText,
                    stylesheetIdentity,
                    referenceGraph);
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

        lock (synchronization)
        {
            utilityContexts.Remove(documentUri);
            return documents.Close(documentUri);
        }
    }

    public IReadOnlyList<LanguageDiagnostic> GetDiagnostics(string documentUri)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(documentUri);

        lock (synchronization)
        {
            if (!documents.TryGet(documentUri, out var document))
            {
                return Array.Empty<LanguageDiagnostic>();
            }

            var diagnostics = document.Syntax.Errors
                .Select(ToLanguageDiagnostic)
                .ToList();
            AddVueCompatibilityDiagnostics(document.Syntax, diagnostics);
            return diagnostics;
        }
    }

    public IReadOnlyList<LanguageCompletionItem> GetCompletions(
        string documentUri,
        LanguagePosition position)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(documentUri);

        lock (synchronization)
        {
            if (!documents.TryGet(documentUri, out var document) ||
                !TextCoordinateConverter.TryGetOffset(document.Text, position, out var offset))
            {
                return Array.Empty<LanguageCompletionItem>();
            }

            var block = FindBlock(document.Syntax, offset);
            if (block is SingleFileComponentTemplateBlock template &&
                UtilityClassCompletionContext.TryCreate(
                    template.Content,
                    template.ContentLocation.Start.Offset,
                    offset,
                    out var utilityContext))
            {
                return GetUtilityClassCompletions(
                    document.Text,
                    utilityContext,
                    GetUtilityContext(documentUri));
            }

            var linePrefix = TextCoordinateConverter.GetLinePrefix(document.Text, offset);
            var headerCompletions = GetHeaderOptionCompletions(
                linePrefix,
                document.Syntax.Format);
            if (headerCompletions.Count > 0)
            {
                return headerCompletions;
            }

            return block?.Kind switch
            {
                SingleFileComponentBlockKind.Template => GetTemplateCompletions(linePrefix),
                SingleFileComponentBlockKind.Script => GetScriptCompletions(linePrefix),
                SingleFileComponentBlockKind.Style => ViuCompletionCatalog.StyleProperties,
                _ => GetRootCompletions(document.Syntax),
            };
        }
    }

    public LanguageHover? GetHover(string documentUri, LanguagePosition position)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(documentUri);

        lock (synchronization)
        {
            if (!documents.TryGet(documentUri, out var document) ||
                !TextCoordinateConverter.TryGetOffset(document.Text, position, out var offset))
            {
                return null;
            }

            var block = FindBlock(document.Syntax, offset);
            if (block is SingleFileComponentTemplateBlock template &&
                UtilityClassCompletionContext.TryCreate(
                    template.Content,
                    template.ContentLocation.Start.Offset,
                    offset,
                    out var utilityContext))
            {
                var utilityHover = GetUtilityClassHover(
                    document.Text,
                    utilityContext,
                    GetUtilityContext(documentUri));
                if (utilityHover is not null)
                {
                    return utilityHover;
                }
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
            if ((completion.Label == "@template" || completion.Label == "template") &&
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
        if (format == LanguageDocumentFormat.Viu &&
            trimmed.Contains('{', StringComparison.Ordinal))
        {
            return Array.Empty<LanguageCompletionItem>();
        }

        if (trimmed.StartsWith(
                format == LanguageDocumentFormat.Vue ? "<template " : "@template ",
                StringComparison.Ordinal))
        {
            return ViuCompletionCatalog.TemplateHeaderOptions;
        }

        if (trimmed.StartsWith(
                format == LanguageDocumentFormat.Vue ? "<script " : "@script ",
                StringComparison.Ordinal))
        {
            return format == LanguageDocumentFormat.Vue
                ? ViuCompletionCatalog.VueScriptHeaderOptions
                : ViuCompletionCatalog.ScriptHeaderOptions;
        }

        if (trimmed.StartsWith(
                format == LanguageDocumentFormat.Vue ? "<style " : "@style ",
                StringComparison.Ordinal))
        {
            return ViuCompletionCatalog.StyleHeaderOptions;
        }

        return Array.Empty<LanguageCompletionItem>();
    }

    private static IReadOnlyList<LanguageCompletionItem> GetTemplateCompletions(string linePrefix)
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
        completions.AddRange(ViuCompletionCatalog.TemplateTags);
        completions.AddRange(ViuCompletionCatalog.TemplateDirectives);
        completions.AddRange(ViuCompletionCatalog.TemplateEvents);
        completions.AddRange(ViuCompletionCatalog.TemplateBindings);
        return completions;
    }

    private static IReadOnlyList<LanguageCompletionItem> GetUtilityClassCompletions(
        string documentText,
        UtilityClassCompletionContext context,
        UtilityStylesheetLanguageContext stylesheetContext)
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

        AddProjectUtilityCompletions(
            context.Prefix,
            stylesheetContext,
            metadataByCandidate);
        AddProjectVariantCompletions(
            context.Prefix,
            stylesheetContext,
            metadataByCandidate);

        return metadataByCandidate.Values
            .OrderBy(metadata => metadata.SortOrder)
            .ThenBy(metadata => metadata.CandidateText, StringComparer.Ordinal)
            .Take(LanguageCompletionLimits.MaximumItems)
            .Select(
                metadata => CreateUtilityCompletion(
                    metadata,
                    editRange))
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
        LanguageRange editRange) =>
        new(
            metadata.CandidateText,
            LanguageCompletionItemKind.Property,
            "Viu utility class",
            GetUtilityDocumentation(metadata),
            metadata.CandidateText,
            IsSnippet: false,
            SortText: $"{metadata.SortOrder:D5}:{metadata.CandidateText}",
            EditRange: editRange,
            FilterText: metadata.CandidateText);

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
        UtilityStylesheetLanguageContext stylesheetContext)
    {
        if (string.IsNullOrEmpty(context.TokenText))
        {
            return null;
        }

        var resolution = UtilityRegistry.Resolve(
            context.TokenText,
            stylesheetContext.Theme,
            CancellationToken.None);
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

    private UtilityStylesheetLanguageContext GetUtilityContext(string documentUri)
        => utilityContexts.TryGetValue(documentUri, out var context)
            ? context
            : UtilityStylesheetLanguageContext.Default;

    private static IReadOnlyList<LanguageCompletionItem> GetScriptCompletions(string linePrefix)
    {
        if (linePrefix.Contains("Context.", StringComparison.Ordinal))
        {
            return ViuCompletionCatalog.ContextMembers;
        }

        if (linePrefix.Contains("Reactive.", StringComparison.Ordinal))
        {
            return ViuCompletionCatalog.ReactiveMembers;
        }

        return ViuCompletionCatalog.ScriptGeneral;
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
        if (script is null ||
            string.Equals(script.Lang, "csharp", StringComparison.Ordinal))
        {
            return;
        }

        var languageOption = FindOption(script, "lang");
        var authoredLanguage = script.Lang is null
            ? "an implicit JavaScript language"
            : $"'{script.Lang}'";
        diagnostics.Add(
            new LanguageDiagnostic(
                ToLanguageRange(languageOption?.Location ?? script.Location),
                LanguageDiagnosticSeverity.Error,
                "VIU1206",
                $"The .vue script uses {authoredLanguage}. Viu accepts only an explicit lang=\"csharp\" attribute and never executes JavaScript.",
                "viu"));
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
