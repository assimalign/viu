using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

using Assimalign.Viu.Compiler.SingleFileComponent;
using Assimalign.Viu.Syntax.SingleFileComponent;
using Assimalign.Viu.Syntax.Templates;

namespace Assimalign.Viu.LanguageService;

/// <summary>
/// Provides expression- and contract-aware completion inside a template block. The provider keeps
/// authored tag casing intact so its native/component decision is identical to [SFC-CG-8], then
/// resolves component contracts through the same [SFC-USE-5] catalog as the compiler.
/// [V01.01.12.07.12]
/// </summary>
internal static class TemplateCompletionProvider
{
    private static readonly IReadOnlyList<LanguageCompletionItem> HandlerSnippets =
    [
        new LanguageCompletionItem(
            "$event lambda",
            LanguageCompletionItemKind.Snippet,
            "Template event lambda",
            "Handles the event with an inline expression whose `$event` parameter is the dispatched event value.",
            "\\$event => $1",
            IsSnippet: true,
            SortText: "10:event-lambda"),
        new LanguageCompletionItem(
            "async $event lambda",
            LanguageCompletionItemKind.Snippet,
            "Asynchronous template event lambda",
            "Handles the event with an asynchronous inline expression whose `$event` parameter is the dispatched event value.",
            "async \\$event => $1",
            IsSnippet: true,
            SortText: "11:async-event-lambda"),
    ];

    /// <summary>Returns whether the cursor is inside an HTML comment in template content.</summary>
    internal static bool IsInsideComment(string content, int relativeOffset)
    {
        if (relativeOffset < 0 || relativeOffset > content.Length)
        {
            return false;
        }

        AnalyzeMarkupPrefix(
            content,
            relativeOffset,
            out _,
            out _,
            out var isComment);
        return isComment;
    }

    /// <summary>
    /// Returns contextual template completions, an empty list when the cursor is in a suppressed or
    /// expression-only context, or <see langword="null"/> when the structural template catalog should
    /// answer the request.
    /// </summary>
    internal static IReadOnlyList<LanguageCompletionItem>? GetCompletions(
        LanguageDocument document,
        SingleFileComponentTemplateBlock template,
        int documentOffset,
        ScriptDeclarationReader declarationReader,
        ScriptSemanticEngine semanticEngine,
        LanguageProjectContext? projectContext,
        string documentFilePath,
        CancellationToken cancellationToken)
    {
        var relativeOffset = documentOffset - template.ContentLocation.Start.Offset;
        if (relativeOffset < 0 || relativeOffset > template.Content.Length)
        {
            return null;
        }

        AnalyzeMarkupPrefix(
            template.Content,
            relativeOffset,
            out var currentTagStart,
            out var isInterpolation,
            out var isComment);
        if (isComment)
        {
            return Array.Empty<LanguageCompletionItem>();
        }

        if (currentTagStart >= 0 &&
            TryReadTagContext(
                template.Content,
                currentTagStart,
                relativeOffset,
                out var tagContext))
        {
            if (tagContext.IsAttributeValue)
            {
                return IsEventAttribute(tagContext.AttributeName)
                    ? GetHandlerCompletions(
                        document.Syntax,
                        tagContext.AttributeValuePrefix,
                        declarationReader)
                    : Array.Empty<LanguageCompletionItem>();
            }

            if (IsEventAttributePrefix(tagContext.AttributeNamePrefix))
            {
                return GetEventCompletions(
                    tagContext,
                    semanticEngine,
                    projectContext,
                    documentFilePath,
                    document.Text,
                    cancellationToken);
            }

            if (IsBindingAttributePrefix(tagContext.AttributeNamePrefix))
            {
                return GetBindingCompletions(
                    tagContext,
                    semanticEngine,
                    projectContext,
                    documentFilePath,
                    document.Text,
                    cancellationToken);
            }

            if (tagContext.AttributeNamePrefix.StartsWith("v-", StringComparison.Ordinal))
            {
                return FilterByPrefix(
                    ViuCompletionCatalog.TemplateDirectives,
                    tagContext.AttributeNamePrefix);
            }

            return null;
        }

        return isInterpolation
            ? GetMemberCompletions(
                document.Syntax,
                GetTrailingIdentifier(template.Content, relativeOffset),
                declarationReader,
                methodsOnly: false)
            : null;
    }

    private static IReadOnlyList<LanguageCompletionItem> GetHandlerCompletions(
        LanguageDocumentSyntax syntax,
        string valuePrefix,
        ScriptDeclarationReader declarationReader)
    {
        var completions = GetMemberCompletions(
                syntax,
                GetTrailingIdentifier(valuePrefix, valuePrefix.Length),
                declarationReader,
                methodsOnly: true)
            .ToList();
        completions.AddRange(HandlerSnippets);
        return completions;
    }

    private static IReadOnlyList<LanguageCompletionItem> GetEventCompletions(
        TemplateTagContext tagContext,
        ScriptSemanticEngine semanticEngine,
        LanguageProjectContext? projectContext,
        string documentFilePath,
        string documentText,
        CancellationToken cancellationToken)
    {
        if (TryResolveComponent(
                tagContext.TagName,
                semanticEngine,
                projectContext,
                documentFilePath,
                documentText,
                cancellationToken,
                out var component))
        {
            var usesLongForm = tagContext.AttributeNamePrefix.StartsWith(
                "v-on:",
                StringComparison.Ordinal);
            var prefix = usesLongForm ? "v-on:" : "@";
            return component.Events
                .Select(
                    templateEvent => new LanguageCompletionItem(
                        prefix + templateEvent.Name,
                        LanguageCompletionItemKind.Snippet,
                        templateEvent.Detail,
                        $"Registers the component's `{templateEvent.Name}` event.",
                        prefix + templateEvent.Name + "=\"$1\"",
                        IsSnippet: true,
                        SortText: "00:" + templateEvent.Name))
                .Where(
                    item => item.Label.StartsWith(
                        tagContext.AttributeNamePrefix,
                        StringComparison.OrdinalIgnoreCase))
                .ToArray();
        }

        // [SFC-CG-8]: PascalCase collisions and unresolved lowercase static tags remain component
        // candidates. The native event vocabulary is valid only for a confirmed native tag.
        if (char.IsUpper(tagContext.TagName[0]) ||
            !CompilerDomKnowledge.IsNativeTag(tagContext.TagName))
        {
            return Array.Empty<LanguageCompletionItem>();
        }

        if (tagContext.AttributeNamePrefix.StartsWith("v-on:", StringComparison.Ordinal))
        {
            var typedEvent = tagContext.AttributeNamePrefix.Substring("v-on:".Length);
            return ViuCompletionCatalog.TemplateEvents
                .Where(
                    item => item.Label.AsSpan(1).StartsWith(
                        typedEvent,
                        StringComparison.OrdinalIgnoreCase))
                .Select(
                    item => item with
                    {
                        Label = "v-on:" + item.Label.Substring(1),
                        InsertText = "v-on:" + item.InsertText.Substring(1),
                    })
                .ToArray();
        }

        return FilterByPrefix(
            ViuCompletionCatalog.TemplateEvents,
            tagContext.AttributeNamePrefix);
    }

    private static IReadOnlyList<LanguageCompletionItem> GetBindingCompletions(
        TemplateTagContext tagContext,
        ScriptSemanticEngine semanticEngine,
        LanguageProjectContext? projectContext,
        string documentFilePath,
        string documentText,
        CancellationToken cancellationToken)
    {
        var usesLongForm = tagContext.AttributeNamePrefix.StartsWith(
            "v-bind:",
            StringComparison.Ordinal);
        var bindingPrefix = usesLongForm ? "v-bind:" : ":";
        if (TryResolveComponent(
                tagContext.TagName,
                semanticEngine,
                projectContext,
                documentFilePath,
                documentText,
                cancellationToken,
                out var component))
        {
            return component.CompilerDeclaration.Parameters
                .Select(
                    parameter => new LanguageCompletionItem(
                        bindingPrefix + parameter.Name,
                        LanguageCompletionItemKind.Property,
                        parameter.TypeText + " component parameter",
                        parameter.IsRequired
                            ? $"Binds the required `{parameter.Name}` parameter on `{tagContext.TagName}`."
                            : $"Binds the `{parameter.Name}` parameter on `{tagContext.TagName}`.",
                        bindingPrefix + parameter.Name + "=\"$1\"",
                        IsSnippet: true,
                        SortText: "00:" + parameter.Name))
                .Where(
                    item => item.Label.StartsWith(
                        tagContext.AttributeNamePrefix,
                        StringComparison.OrdinalIgnoreCase))
                .ToArray();
        }

        // [SFC-CG-8]: an uppercase collision remains a component candidate even when its declaration
        // is currently missing, so never fall through from <Button> to HTML <button> attributes.
        if (char.IsUpper(tagContext.TagName[0]))
        {
            return Array.Empty<LanguageCompletionItem>();
        }

        IEnumerable<string> attributes;
        if (CompilerDomKnowledge.IsHtmlTag(tagContext.TagName))
        {
            attributes = CompilerDomKnowledge.EnumerateKnownHtmlAttributes();
        }
        else if (CompilerDomKnowledge.IsSvgTag(tagContext.TagName))
        {
            attributes = CompilerDomKnowledge.EnumerateKnownSvgAttributes();
        }
        else if (CompilerDomKnowledge.IsMathMlTag(tagContext.TagName))
        {
            attributes = CompilerDomKnowledge.EnumerateKnownMathMlAttributes();
        }
        else
        {
            return Array.Empty<LanguageCompletionItem>();
        }

        return attributes
            .Select(
                attribute => new LanguageCompletionItem(
                    bindingPrefix + attribute,
                    LanguageCompletionItemKind.Property,
                    "Native element attribute",
                    $"Binds the `{attribute}` attribute on the native `{tagContext.TagName}` element.",
                    bindingPrefix + attribute + "=\"$1\"",
                    IsSnippet: true,
                    SortText: "10:" + attribute))
            .Where(
                item => item.Label.StartsWith(
                    tagContext.AttributeNamePrefix,
                    StringComparison.OrdinalIgnoreCase))
            .OrderBy(item => item.Label, StringComparer.Ordinal)
            .ToArray();
    }

    private static bool TryResolveComponent(
        string tagName,
        ScriptSemanticEngine semanticEngine,
        LanguageProjectContext? projectContext,
        string documentFilePath,
        string documentText,
        CancellationToken cancellationToken,
        out TemplateComponentDeclaration declaration)
    {
        declaration = null!;
        if (projectContext is null ||
            (!char.IsUpper(tagName[0]) && CompilerDomKnowledge.IsNativeTag(tagName)))
        {
            return false;
        }

        var contracts = semanticEngine.GetComponentContracts(
            projectContext,
            documentFilePath,
            documentText,
            cancellationToken);
        return contracts is not null &&
            new TemplateComponentCatalog(contracts).TryResolve(tagName, out declaration);
    }

    private static IReadOnlyList<LanguageCompletionItem> GetMemberCompletions(
        LanguageDocumentSyntax syntax,
        string typedPrefix,
        ScriptDeclarationReader declarationReader,
        bool methodsOnly)
    {
        var completions = new List<LanguageCompletionItem>();
        var seenNames = new HashSet<string>(StringComparer.Ordinal);
        AppendMembers(syntax.Script, declarationReader, methodsOnly, completions, seenNames);
        AppendMembers(syntax.ScriptSetup, declarationReader, methodsOnly, completions, seenNames);
        if (typedPrefix.Length == 0)
        {
            return completions;
        }

        var filtered = completions
            .Where(
                item => item.Label.StartsWith(
                    typedPrefix,
                    StringComparison.OrdinalIgnoreCase))
            .ToArray();
        return filtered.Length == 0 ? completions : filtered;
    }

    private static void AppendMembers(
        SingleFileComponentScriptBlock? script,
        ScriptDeclarationReader declarationReader,
        bool methodsOnly,
        List<LanguageCompletionItem> completions,
        HashSet<string> seenNames)
    {
        if (script is null)
        {
            return;
        }

        foreach (var member in declarationReader.Read(script.Content))
        {
            if ((methodsOnly && member.Kind != ScriptDeclaredMemberKind.Method) ||
                !seenNames.Add(member.Name))
            {
                continue;
            }

            completions.Add(new LanguageCompletionItem(
                member.Name,
                member.Kind switch
                {
                    ScriptDeclaredMemberKind.Property => LanguageCompletionItemKind.Property,
                    ScriptDeclaredMemberKind.Method => LanguageCompletionItemKind.Method,
                    _ => LanguageCompletionItemKind.Field,
                },
                member.Detail,
                member.DocumentationSummary ??
                    $"Declared in this file's @script block as `{member.Detail} {member.Name}`.",
                member.Name,
                IsSnippet: false,
                SortText: "00:" + member.Name));
        }
    }

    private static IReadOnlyList<LanguageCompletionItem> FilterByPrefix(
        IReadOnlyList<LanguageCompletionItem> candidates,
        string prefix)
        => candidates
            .Where(
                item => item.Label.StartsWith(
                    prefix,
                    StringComparison.OrdinalIgnoreCase))
            .ToArray();

    private static bool IsEventAttribute(string attributeName)
        => attributeName.StartsWith('@') ||
           attributeName.StartsWith("v-on:", StringComparison.Ordinal);

    private static bool IsEventAttributePrefix(string prefix)
        => prefix.StartsWith('@') ||
           prefix.StartsWith("v-on:", StringComparison.Ordinal);

    private static bool IsBindingAttributePrefix(string prefix)
        => prefix.StartsWith(':') ||
           prefix.StartsWith("v-bind:", StringComparison.Ordinal);

    private static string GetTrailingIdentifier(string text, int offset)
    {
        var start = offset;
        while (start > 0 &&
               (char.IsLetterOrDigit(text[start - 1]) || text[start - 1] == '_'))
        {
            start--;
        }

        return text.Substring(start, offset - start);
    }

    private static void AnalyzeMarkupPrefix(
        string content,
        int offset,
        out int currentTagStart,
        out bool isInterpolation,
        out bool isComment)
    {
        currentTagStart = -1;
        isInterpolation = false;
        isComment = false;
        var quote = '\0';
        for (var index = 0; index < offset; index++)
        {
            if (isComment)
            {
                if (StartsWith(content, index, "-->"))
                {
                    isComment = false;
                    index += 2;
                }

                continue;
            }

            if (currentTagStart >= 0)
            {
                var character = content[index];
                if (quote != '\0')
                {
                    if (character == quote)
                    {
                        quote = '\0';
                    }

                    continue;
                }

                if (character is '\'' or '"')
                {
                    quote = character;
                }
                else if (character == '>')
                {
                    currentTagStart = -1;
                }

                continue;
            }

            if (isInterpolation)
            {
                if (StartsWith(content, index, "}}"))
                {
                    isInterpolation = false;
                    index++;
                }

                continue;
            }

            if (StartsWith(content, index, "<!--"))
            {
                isComment = true;
                index += 3;
            }
            else if (StartsWith(content, index, "{{"))
            {
                isInterpolation = true;
                index++;
            }
            else if (content[index] == '<')
            {
                currentTagStart = index;
            }
        }
    }

    private static bool TryReadTagContext(
        string content,
        int tagStart,
        int offset,
        out TemplateTagContext context)
    {
        context = null!;
        var cursor = tagStart + 1;
        if (cursor >= offset || content[cursor] is '/' or '!' or '?')
        {
            return false;
        }

        while (cursor < offset && char.IsWhiteSpace(content[cursor]))
        {
            cursor++;
        }

        var tagNameStart = cursor;
        while (cursor < offset && IsNameCharacter(content[cursor]))
        {
            cursor++;
        }

        if (tagNameStart == cursor)
        {
            return false;
        }

        var tagName = content.Substring(tagNameStart, cursor - tagNameStart);
        if (cursor == offset && !char.IsWhiteSpace(content[offset - 1]))
        {
            context = new TemplateTagContext(tagName, string.Empty, string.Empty, false);
            return true;
        }

        while (cursor < offset)
        {
            while (cursor < offset && char.IsWhiteSpace(content[cursor]))
            {
                cursor++;
            }

            if (cursor >= offset)
            {
                context = new TemplateTagContext(tagName, string.Empty, string.Empty, false);
                return true;
            }

            if (content[cursor] == '/')
            {
                cursor++;
                continue;
            }

            var attributeStart = cursor;
            while (cursor < offset &&
                   !char.IsWhiteSpace(content[cursor]) &&
                   content[cursor] is not '=' and not '/' and not '>')
            {
                cursor++;
            }

            var attributeName = content.Substring(attributeStart, cursor - attributeStart);
            if (cursor == offset)
            {
                context = new TemplateTagContext(tagName, attributeName, string.Empty, false);
                return true;
            }

            while (cursor < offset && char.IsWhiteSpace(content[cursor]))
            {
                cursor++;
            }

            if (cursor >= offset || content[cursor] != '=')
            {
                continue;
            }

            cursor++;
            while (cursor < offset && char.IsWhiteSpace(content[cursor]))
            {
                cursor++;
            }

            if (cursor >= offset)
            {
                context = new TemplateTagContext(tagName, attributeName, string.Empty, true);
                return true;
            }

            var quote = content[cursor] is '\'' or '"' ? content[cursor++] : '\0';
            var valueStart = cursor;
            if (quote != '\0')
            {
                while (cursor < offset && content[cursor] != quote)
                {
                    cursor++;
                }

                if (cursor == offset)
                {
                    context = new TemplateTagContext(
                        tagName,
                        attributeName,
                        content.Substring(valueStart, cursor - valueStart),
                        true);
                    return true;
                }

                cursor++;
            }
            else
            {
                while (cursor < offset && !char.IsWhiteSpace(content[cursor]))
                {
                    cursor++;
                }

                if (cursor == offset)
                {
                    context = new TemplateTagContext(
                        tagName,
                        attributeName,
                        content.Substring(valueStart, cursor - valueStart),
                        true);
                    return true;
                }
            }
        }

        context = new TemplateTagContext(tagName, string.Empty, string.Empty, false);
        return true;
    }

    private static bool StartsWith(string text, int offset, string value)
        => offset + value.Length <= text.Length &&
           text.AsSpan(offset, value.Length).SequenceEqual(value.AsSpan());

    private static bool IsNameCharacter(char character)
        => char.IsLetterOrDigit(character) || character is '-' or '_' or ':' or '.';

    private sealed record TemplateTagContext(
        string TagName,
        string AttributeNamePrefix,
        string AttributeValuePrefix,
        bool IsAttributeValue)
    {
        internal string AttributeName => AttributeNamePrefix;
    }
}
