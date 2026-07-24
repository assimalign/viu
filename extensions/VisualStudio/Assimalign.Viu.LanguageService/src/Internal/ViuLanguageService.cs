using System;
using System.Collections.Generic;
using System.Linq;

using Assimalign.Viu.Syntax;
using Assimalign.Viu.Syntax.SingleFileComponent;

namespace Assimalign.Viu.LanguageService;

internal sealed class ViuLanguageService : IViuLanguageService
{
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

            return document.ParseResult.Errors
                .Select(ToLanguageDiagnostic)
                .ToArray();
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

            var linePrefix = TextCoordinateConverter.GetLinePrefix(document.Text, offset);
            var headerCompletions = GetHeaderOptionCompletions(linePrefix);
            if (headerCompletions.Count > 0)
            {
                return headerCompletions;
            }

            var block = FindBlock(document.ParseResult.Descriptor, offset);
            return block?.Kind switch
            {
                SingleFileComponentBlockKind.Template => GetTemplateCompletions(linePrefix),
                SingleFileComponentBlockKind.Script => GetScriptCompletions(linePrefix),
                SingleFileComponentBlockKind.Style => ViuCompletionCatalog.StyleProperties,
                _ => GetRootCompletions(document.ParseResult.Descriptor),
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
        SingleFileComponentDescriptor descriptor)
    {
        var completions = new List<LanguageCompletionItem>();
        foreach (var completion in ViuCompletionCatalog.BlockHeaders)
        {
            if (completion.Label == "@template" && descriptor.Template is not null)
            {
                continue;
            }

            if (completion.Label == "@script" && descriptor.Script is not null)
            {
                continue;
            }

            completions.Add(completion);
        }

        return completions;
    }

    private static IReadOnlyList<LanguageCompletionItem> GetHeaderOptionCompletions(string linePrefix)
    {
        var trimmed = linePrefix.TrimStart();
        if (trimmed.Contains('{', StringComparison.Ordinal))
        {
            return Array.Empty<LanguageCompletionItem>();
        }

        if (trimmed.StartsWith("@template ", StringComparison.Ordinal))
        {
            return ViuCompletionCatalog.TemplateHeaderOptions;
        }

        if (trimmed.StartsWith("@script ", StringComparison.Ordinal))
        {
            return ViuCompletionCatalog.ScriptHeaderOptions;
        }

        if (trimmed.StartsWith("@style ", StringComparison.Ordinal))
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
        SingleFileComponentDescriptor descriptor,
        int offset)
    {
        if (ContainsOffset(descriptor.Template, offset))
        {
            return descriptor.Template;
        }

        if (ContainsOffset(descriptor.Script, offset))
        {
            return descriptor.Script;
        }

        foreach (var style in descriptor.Styles)
        {
            if (ContainsOffset(style, offset))
            {
                return style;
            }
        }

        foreach (var customBlock in descriptor.CustomBlocks)
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
