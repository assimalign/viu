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
        string documentUri,
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

        // A node position — the caret is starting a tag name, or sitting in content where a tag could
        // start. Only something that can BE a node belongs here.
        if (IsTagNamePosition(template.Content, relativeOffset))
        {
            return GetElementCompletions(
                document,
                template,
                relativeOffset,
                semanticEngine,
                projectContext,
                documentFilePath,
                cancellationToken);
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
                return GetAttributeValueCompletions(
                    document,
                    tagContext,
                    documentOffset,
                    declarationReader,
                    semanticEngine,
                    projectContext,
                    documentUri,
                    documentFilePath,
                    cancellationToken);
            }

            return GetAttributeNameCompletions(
                document,
                tagContext,
                documentOffset,
                semanticEngine,
                projectContext,
                documentFilePath,
                cancellationToken);
        }

        if (currentTagStart >= 0)
        {
            // Somewhere inside a tag this provider does not serve — a closing tag, say. Markup names
            // are not valid there and neither is anything else, so answer with nothing.
            return Array.Empty<LanguageCompletionItem>();
        }

        if (!isInterpolation)
        {
            return GetElementCompletions(
                document,
                template,
                relativeOffset,
                semanticEngine,
                projectContext,
                documentFilePath,
                cancellationToken);
        }

        return GetExpressionCompletions(
            document,
            template.Content,
            relativeOffset,
            documentOffset,
            semanticEngine,
            projectContext,
            documentUri,
            documentFilePath,
            cancellationToken) ??
            GetMemberCompletions(
                document.Syntax,
                GetTrailingIdentifier(template.Content, relativeOffset),
                declarationReader,
                methodsOnly: false);
    }

    /// <summary>
    /// Returns everything that can start a node here: the native elements, the components this
    /// compilation can resolve, and Viu's own framework tags.
    /// </summary>
    /// <remarks>
    /// A directive, a binding, and an event handler are all <em>attributes</em> — they can only
    /// appear inside a tag that already has a name, so offering them where a node begins suggests
    /// markup that cannot parse. Components come from the same [SFC-USE-5] catalog the compiler
    /// resolves usages against, so the list names exactly what <c>&lt;Name&gt;</c> would bind to, and
    /// a component the author just wrote appears without the catalog being told about it.
    /// </remarks>
    private static IReadOnlyList<LanguageCompletionItem> GetElementCompletions(
        LanguageDocument document,
        SingleFileComponentTemplateBlock template,
        int relativeOffset,
        ScriptSemanticEngine semanticEngine,
        LanguageProjectContext? projectContext,
        string documentFilePath,
        CancellationToken cancellationToken)
    {
        var editRange = GetTagEditRange(
            document.Text,
            template.ContentLocation.Start.Offset + relativeOffset);
        var completions = new List<LanguageCompletionItem>();
        var seenNames = new HashSet<string>(StringComparer.Ordinal);
        foreach (var component in ResolveComponents(
                     semanticEngine,
                     projectContext,
                     documentFilePath,
                     document.Text,
                     cancellationToken))
        {
            if (seenNames.Add(component))
            {
                completions.Add(CreateElementCompletion(
                    component,
                    LanguageCompletionItemKind.Class,
                    "Component",
                    $"Renders the `{component}` component.",
                    "10:" + component,
                    editRange));
            }
        }

        foreach (var frameworkTag in ViuCompletionCatalog.TemplateTags)
        {
            if (seenNames.Add(frameworkTag.Label))
            {
                completions.Add(
                    frameworkTag with { EditRange = editRange });
            }
        }

        foreach (var tag in CompilerDomKnowledge.EnumerateKnownHtmlTags())
        {
            if (seenNames.Add(tag))
            {
                completions.Add(CreateElementCompletion(
                    tag,
                    LanguageCompletionItemKind.Property,
                    "HTML element",
                    $"Renders the native `<{tag}>` element.",
                    "30:" + tag,
                    editRange));
            }
        }

        return completions;
    }

    private static LanguageCompletionItem CreateElementCompletion(
        string name,
        LanguageCompletionItemKind kind,
        string detail,
        string documentation,
        string sortText,
        LanguageRange editRange)
        => new(
            name,
            kind,
            detail,
            documentation,
            "<" + name,
            IsSnippet: false,
            sortText,
            editRange);

    private static IEnumerable<string> ResolveComponents(
        ScriptSemanticEngine semanticEngine,
        LanguageProjectContext? projectContext,
        string documentFilePath,
        string documentText,
        CancellationToken cancellationToken)
    {
        if (projectContext is null)
        {
            return Array.Empty<string>();
        }

        var contracts = semanticEngine.GetComponentContracts(
            projectContext,
            documentFilePath,
            documentText,
            cancellationToken);
        if (contracts is null)
        {
            return Array.Empty<string>();
        }

        var names = new List<string>(contracts.Count);
        foreach (var contract in contracts)
        {
            names.Add(contract.CompilerDeclaration.Name);
        }

        names.Sort(StringComparer.Ordinal);
        return names;
    }

    /// <summary>
    /// Returns whether the caret is spelling a tag name — inside <c>&lt;name</c>, with the name
    /// possibly still empty.
    /// </summary>
    /// <remarks>
    /// The <c>&lt;</c> is the whole signal. A closing tag reaches this with a <c>/</c> between the
    /// bracket and the name, which fails the test deliberately: the auto-close already writes those.
    /// </remarks>
    private static bool IsTagNamePosition(string content, int offset)
    {
        var start = offset;
        while (start > 0 && IsTagNameCharacter(content[start - 1]))
        {
            start--;
        }

        return start > 0 && content[start - 1] == '<';
    }

    /// <summary>
    /// Returns the range an element completion replaces, which reaches back over the <c>&lt;</c> the
    /// author typed to ask for the list.
    /// </summary>
    private static LanguageRange GetTagEditRange(string documentText, int offset)
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

        return new LanguageRange(
            TextCoordinateConverter.GetPosition(documentText, start),
            TextCoordinateConverter.GetPosition(documentText, offset));
    }

    private static bool IsTagNameCharacter(char character)
        => char.IsLetterOrDigit(character) || character is '-' or '_' or '.';

    /// <summary>
    /// Returns everything nameable in a tag's attribute area: what the tag itself accepts, plus the
    /// directives every tag accepts.
    /// </summary>
    /// <remarks>
    /// One vocabulary answers every spelling, because the typed prefix is what narrows it — <c>:</c>
    /// leaves the bindings, <c>@</c> the handlers, a bare letter the plain attributes. A parameter
    /// therefore appears in both forms it can legally take: <c>title="…"</c> assigns a static value
    /// and <c>:title="…"</c> binds an expression, and offering only the bound form hid half the
    /// surface from anyone not already typing a colon. The long <c>v-bind:</c>/<c>v-on:</c> spellings
    /// are the same items rewritten, so neither form can name something the other cannot.
    /// </remarks>
    private static IReadOnlyList<LanguageCompletionItem> GetAttributeNameCompletions(
        LanguageDocument document,
        TemplateTagContext tagContext,
        int documentOffset,
        ScriptSemanticEngine semanticEngine,
        LanguageProjectContext? projectContext,
        string documentFilePath,
        CancellationToken cancellationToken)
    {
        var prefix = tagContext.AttributeNamePrefix;
        var candidates = new List<LanguageCompletionItem>();
        if (TryResolveComponent(
                tagContext.TagName,
                semanticEngine,
                projectContext,
                documentFilePath,
                document.Text,
                cancellationToken,
                out var component))
        {
            AppendComponentAttributes(component, tagContext.TagName, candidates);
        }
        else if (!char.IsUpper(tagContext.TagName[0]))
        {
            // [SFC-CG-8]: an uppercase tag stays a component candidate even when its declaration is
            // missing, so <Button> must never fall through to the native <button> vocabulary.
            AppendNativeAttributes(tagContext.TagName, candidates);
        }

        candidates.AddRange(ViuCompletionCatalog.TemplateDirectives);

        var editRange = GetAttributeEditRange(document.Text, documentOffset);
        var completions = new List<LanguageCompletionItem>(candidates.Count);
        foreach (var candidate in RewriteToTypedForm(candidates, prefix))
        {
            if (!candidate.Label.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            completions.Add(candidate with { EditRange = editRange });
        }

        return completions;
    }

    private static void AppendComponentAttributes(
        TemplateComponentDeclaration component,
        string tagName,
        List<LanguageCompletionItem> candidates)
    {
        foreach (var parameter in component.CompilerDeclaration.Parameters)
        {
            var detail = parameter.TypeText + " component parameter";
            var documentation = parameter.IsRequired
                ? $"Assigns the required `{parameter.Name}` parameter on `{tagName}`."
                : $"Assigns the `{parameter.Name}` parameter on `{tagName}`.";
            candidates.Add(new LanguageCompletionItem(
                parameter.Name,
                LanguageCompletionItemKind.Property,
                detail,
                documentation,
                parameter.Name + "=\"$1\"",
                IsSnippet: true,
                "05:" + parameter.Name));
            candidates.Add(new LanguageCompletionItem(
                ":" + parameter.Name,
                LanguageCompletionItemKind.Property,
                detail,
                parameter.IsRequired
                    ? $"Binds the required `{parameter.Name}` parameter on `{tagName}`."
                    : $"Binds the `{parameter.Name}` parameter on `{tagName}`.",
                ":" + parameter.Name + "=\"$1\"",
                IsSnippet: true,
                "06:" + parameter.Name));
        }

        foreach (var componentEvent in component.Events)
        {
            candidates.Add(new LanguageCompletionItem(
                "@" + componentEvent.Name,
                LanguageCompletionItemKind.Property,
                componentEvent.Detail,
                $"Registers the component's `{componentEvent.Name}` event.",
                "@" + componentEvent.Name + "=\"$1\"",
                IsSnippet: true,
                "07:" + componentEvent.Name));
        }
    }

    private static void AppendNativeAttributes(
        string tagName,
        List<LanguageCompletionItem> candidates)
    {
        IEnumerable<string> attributes;
        if (CompilerDomKnowledge.IsHtmlTag(tagName))
        {
            attributes = CompilerDomKnowledge.EnumerateKnownHtmlAttributes();
        }
        else if (CompilerDomKnowledge.IsSvgTag(tagName))
        {
            attributes = CompilerDomKnowledge.EnumerateKnownSvgAttributes();
        }
        else if (CompilerDomKnowledge.IsMathMlTag(tagName))
        {
            attributes = CompilerDomKnowledge.EnumerateKnownMathMlAttributes();
        }
        else
        {
            return;
        }

        foreach (var attribute in attributes)
        {
            candidates.Add(new LanguageCompletionItem(
                attribute,
                LanguageCompletionItemKind.Property,
                "Native element attribute",
                $"Sets the `{attribute}` attribute on the native `{tagName}` element.",
                attribute + "=\"$1\"",
                IsSnippet: true,
                "10:" + attribute));
            candidates.Add(new LanguageCompletionItem(
                ":" + attribute,
                LanguageCompletionItemKind.Property,
                "Native element attribute",
                $"Binds the `{attribute}` attribute on the native `{tagName}` element.",
                ":" + attribute + "=\"$1\"",
                IsSnippet: true,
                "11:" + attribute));
        }

        candidates.AddRange(ViuCompletionCatalog.TemplateEvents);
    }

    /// <summary>
    /// Rewrites the shorthand items into the long <c>v-bind:</c>/<c>v-on:</c> spelling when that is
    /// the one being typed, so both spellings name the same surface.
    /// </summary>
    private static IEnumerable<LanguageCompletionItem> RewriteToTypedForm(
        IReadOnlyList<LanguageCompletionItem> candidates,
        string prefix)
    {
        var shorthand = prefix.StartsWith("v-bind:", StringComparison.Ordinal)
            ? ':'
            : prefix.StartsWith("v-on:", StringComparison.Ordinal)
                ? '@'
                : '\0';
        if (shorthand == '\0')
        {
            return candidates;
        }

        var longForm = shorthand == ':' ? "v-bind:" : "v-on:";
        var rewritten = new List<LanguageCompletionItem>(candidates.Count);
        foreach (var candidate in candidates)
        {
            if (candidate.Label.Length > 0 && candidate.Label[0] == shorthand)
            {
                rewritten.Add(candidate with
                {
                    Label = longForm + candidate.Label.Substring(1),
                    InsertText = longForm + candidate.InsertText.Substring(1),
                });
            }
        }

        return rewritten;
    }

    /// <summary>
    /// Returns the range an attribute completion replaces: the whole attribute name being typed, its
    /// leading <c>:</c>, <c>@</c>, or <c>#</c> included.
    /// </summary>
    /// <remarks>
    /// Those characters are punctuation, so an editor inferring the replaced span from the typed word
    /// leaves them in place and the committed name lands beside them — <c>::title</c>, <c>@@click</c>.
    /// Naming the range is what makes the shorthand a part of the name rather than something typed
    /// before it.
    /// </remarks>
    private static LanguageRange GetAttributeEditRange(string documentText, int offset)
    {
        var start = offset;
        while (start > 0 && IsAttributeNameCharacter(documentText[start - 1]))
        {
            start--;
        }

        return new LanguageRange(
            TextCoordinateConverter.GetPosition(documentText, start),
            TextCoordinateConverter.GetPosition(documentText, offset));
    }

    private static bool IsAttributeNameCharacter(char character)
        => char.IsLetterOrDigit(character) || character is '-' or '_' or '.' or ':' or '@' or '#';

    // An attribute value is either an expression the component binds or plain markup text. Only the
    // directive and binding forms are expressions; a static class="..." is markup, and its utility
    // candidates were already offered before this provider ran.
    private static IReadOnlyList<LanguageCompletionItem> GetAttributeValueCompletions(
        LanguageDocument document,
        TemplateTagContext tagContext,
        int documentOffset,
        ScriptDeclarationReader declarationReader,
        ScriptSemanticEngine semanticEngine,
        LanguageProjectContext? projectContext,
        string documentUri,
        string documentFilePath,
        CancellationToken cancellationToken)
    {
        var valuePrefix = tagContext.AttributeValuePrefix;
        if (!IsExpressionAttributeName(tagContext.AttributeName))
        {
            return Array.Empty<LanguageCompletionItem>();
        }

        // A v-for value is "alias in source": only the source is an expression. The aliases are
        // declarations, and completing a name the author is in the middle of introducing is noise.
        if (IsForDirectiveName(tagContext.AttributeName) &&
            !IsAfterForSourceKeyword(valuePrefix))
        {
            return Array.Empty<LanguageCompletionItem>();
        }

        var semantic = GetExpressionCompletions(
            document,
            valuePrefix,
            valuePrefix.Length,
            documentOffset,
            semanticEngine,
            projectContext,
            documentUri,
            documentFilePath,
            cancellationToken);
        if (semantic is not null)
        {
            // An event value's handler slot also accepts an inline lambda, which no symbol lookup can
            // offer. The snippets join the bound members at the root of the expression, and only there:
            // after a dot the author is naming a member, and a lambda cannot follow one.
            return IsEventAttribute(tagContext.AttributeName) &&
                   !IsMemberAccess(valuePrefix, valuePrefix.Length)
                ? [.. semantic, .. HandlerSnippets]
                : semantic;
        }

        // The degraded answers, which know no types: the file's own declared members for a v-for
        // source, the handler surface for an event value, and nothing for every other expression —
        // the same silence [V01.01.12.07.12] chose rather than guessing at a bound value.
        if (IsForDirectiveName(tagContext.AttributeName))
        {
            return GetMemberCompletions(
                document.Syntax,
                GetTrailingIdentifier(valuePrefix, valuePrefix.Length),
                declarationReader,
                methodsOnly: false);
        }

        return IsEventAttribute(tagContext.AttributeName)
            ? GetHandlerCompletions(document.Syntax, valuePrefix, declarationReader)
            : Array.Empty<LanguageCompletionItem>();
    }

    /// <summary>
    /// Answers a position inside a template expression by binding it where the compiler put it, or
    /// returns <see langword="null"/> when it cannot be bound and the caller's degraded answer applies.
    /// </summary>
    /// <remarks>
    /// A member position — after the dot of <c>receiver.</c> — resolves through the render source map
    /// to the receiver's real members, which is the only way an alias such as <c>v-for</c>'s
    /// <c>capability</c> can complete at all: nothing outside the compiled loop knows its type. When
    /// that lookup misses, a member position still answers with an empty list rather than the caller's
    /// root-level member list: the names of the component are never the names of a receiver.
    /// </remarks>
    private static IReadOnlyList<LanguageCompletionItem>? GetExpressionCompletions(
        LanguageDocument document,
        string expressionText,
        int expressionOffset,
        int documentOffset,
        ScriptSemanticEngine semanticEngine,
        LanguageProjectContext? projectContext,
        string documentUri,
        string documentFilePath,
        CancellationToken cancellationToken)
    {
        if (projectContext is not null)
        {
            var semantic = semanticEngine.GetTemplateExpressionCompletions(
                projectContext,
                documentUri,
                documentFilePath,
                document.Text,
                documentOffset,
                GetTrailingIdentifier(expressionText, expressionOffset),
                cancellationToken);
            if (semantic is not null)
            {
                return semantic.Items;
            }
        }

        return IsMemberAccess(expressionText, expressionOffset)
            ? Array.Empty<LanguageCompletionItem>()
            : null;
    }

    /// <summary>
    /// Returns whether the position sits in the member position of a template expression — after the
    /// dot of <c>receiver.</c>, with or without a partially typed member name.
    /// </summary>
    private static bool IsMemberAccess(string text, int offset)
    {
        var start = offset;
        while (start > 0 &&
               (char.IsLetterOrDigit(text[start - 1]) || text[start - 1] == '_'))
        {
            start--;
        }

        return start > 0 && text[start - 1] == '.';
    }

    /// <summary>
    /// Returns whether an attribute's value is a C# expression the component binds, rather than the
    /// markup text of a plain attribute or the prop declarations of a slot.
    /// </summary>
    private static bool IsExpressionAttributeName(string attributeName)
        => (attributeName.StartsWith(':') ||
            attributeName.StartsWith('@') ||
            attributeName.StartsWith("v-", StringComparison.Ordinal)) &&
           !string.Equals(attributeName, "v-slot", StringComparison.Ordinal) &&
           !attributeName.StartsWith("v-slot:", StringComparison.Ordinal);

    private static bool IsForDirectiveName(string attributeName)
        => string.Equals(attributeName, "v-for", StringComparison.Ordinal);

    /// <summary>
    /// Returns whether a <c>v-for</c> value prefix has passed its <c>in</c> keyword, which is where
    /// the alias declarations end and the iterated source expression begins.
    /// </summary>
    private static bool IsAfterForSourceKeyword(string valuePrefix)
    {
        var depth = 0;
        for (var index = 0; index < valuePrefix.Length; index++)
        {
            var character = valuePrefix[index];
            if (character == '(')
            {
                depth++;
            }
            else if (character == ')')
            {
                depth--;
            }
            else if (depth == 0 &&
                     character == 'i' &&
                     index + 1 < valuePrefix.Length &&
                     valuePrefix[index + 1] == 'n' &&
                     !IsIdentifierCharacter(index - 1, valuePrefix) &&
                     !IsIdentifierCharacter(index + 2, valuePrefix))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsIdentifierCharacter(int index, string text)
        => index >= 0 &&
           index < text.Length &&
           (char.IsLetterOrDigit(text[index]) || text[index] == '_');

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

    private static bool IsEventAttribute(string attributeName)
        => attributeName.StartsWith('@') ||
           attributeName.StartsWith("v-on:", StringComparison.Ordinal);

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

    internal static void AnalyzeMarkupPrefix(
        string content,
        int offset,
        out int currentTagStart,
        out bool isInterpolation,
        out bool isComment,
        bool recognizeInterpolations = true,
        bool recognizeEscapedQuotes = false)
    {
        currentTagStart = -1;
        isInterpolation = false;
        isComment = false;
        var quote = '\0';
        var isEscaped = false;
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
                    if (recognizeEscapedQuotes && isEscaped)
                    {
                        isEscaped = false;
                    }
                    else if (recognizeEscapedQuotes && character == '\\')
                    {
                        isEscaped = true;
                    }
                    else if (character == quote)
                    {
                        quote = '\0';
                    }

                    continue;
                }

                if (character is '\'' or '"')
                {
                    quote = character;
                    isEscaped = false;
                }
                else if (character == '>')
                {
                    currentTagStart = -1;
                }

                continue;
            }

            if (recognizeInterpolations && isInterpolation)
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
            else if (recognizeInterpolations && StartsWith(content, index, "{{"))
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

    internal static bool TryReadTagContext(
        string content,
        int tagStart,
        int offset,
        out TemplateTagContext context,
        bool recognizeEscapedQuotes = false)
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
            var valueSearchStart = cursor;
            while (cursor < offset && char.IsWhiteSpace(content[cursor]))
            {
                cursor++;
            }

            if (cursor >= offset)
            {
                context = CreateAttributeValueContext(
                    content,
                    tagName,
                    attributeName,
                    string.Empty,
                    valueSearchStart);
                return true;
            }

            var quote = content[cursor] is '\'' or '"' ? content[cursor++] : '\0';
            var valueStart = cursor;
            if (quote != '\0')
            {
                var isEscaped = false;
                while (cursor < offset)
                {
                    if (recognizeEscapedQuotes && isEscaped)
                    {
                        isEscaped = false;
                    }
                    else if (recognizeEscapedQuotes && content[cursor] == '\\')
                    {
                        isEscaped = true;
                    }
                    else if (content[cursor] == quote)
                    {
                        break;
                    }

                    cursor++;
                }

                if (cursor == offset)
                {
                    context = CreateAttributeValueContext(
                        content,
                        tagName,
                        attributeName,
                        content.Substring(valueStart, cursor - valueStart),
                        valueSearchStart);
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
                    context = CreateAttributeValueContext(
                        content,
                        tagName,
                        attributeName,
                        content.Substring(valueStart, cursor - valueStart),
                        valueSearchStart);
                    return true;
                }
            }
        }

        context = new TemplateTagContext(tagName, string.Empty, string.Empty, false);
        return true;
    }

    private static TemplateTagContext CreateAttributeValueContext(
        string content,
        string tagName,
        string attributeName,
        string attributeValuePrefix,
        int valueSearchStart)
    {
        var cursor = valueSearchStart;
        while (cursor < content.Length && char.IsWhiteSpace(content[cursor]))
        {
            cursor++;
        }

        if (cursor >= content.Length)
        {
            return new TemplateTagContext(
                tagName,
                attributeName,
                attributeValuePrefix,
                true);
        }

        var quote = content[cursor] is '\'' or '"' ? content[cursor++] : '\0';
        var valueStart = cursor;
        var isEscaped = false;
        while (cursor < content.Length)
        {
            var character = content[cursor];
            if (quote == '\0' &&
                (char.IsWhiteSpace(character) || character == '>'))
            {
                break;
            }

            if (isEscaped)
            {
                isEscaped = false;
            }
            else if (character == '\\')
            {
                isEscaped = true;
            }
            else if (quote != '\0' && character == quote)
            {
                break;
            }

            cursor++;
        }

        return new TemplateTagContext(
            tagName,
            attributeName,
            attributeValuePrefix,
            true,
            valueStart,
            cursor);
    }

    private static bool StartsWith(string text, int offset, string value)
        => offset + value.Length <= text.Length &&
           text.AsSpan(offset, value.Length).SequenceEqual(value.AsSpan());

    private static bool IsNameCharacter(char character)
        => char.IsLetterOrDigit(character) || character is '-' or '_' or ':' or '.';

    internal sealed record TemplateTagContext(
        string TagName,
        string AttributeNamePrefix,
        string AttributeValuePrefix,
        bool IsAttributeValue,
        int AttributeValueStart = -1,
        int AttributeValueEnd = -1)
    {
        internal string AttributeName => AttributeNamePrefix;
    }
}
