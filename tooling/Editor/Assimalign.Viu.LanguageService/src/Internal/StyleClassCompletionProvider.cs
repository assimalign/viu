using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;

using Assimalign.Viu.Syntax;
using Assimalign.Viu.Syntax.Css;

namespace Assimalign.Viu.LanguageService;

/// <summary>
/// Extracts authored class selectors from the component's style blocks. [V01.01.12.07.13]
/// </summary>
internal static class StyleClassCompletionProvider
{
    /// <summary>
    /// Returns matching component-local selectors followed by build-contributed catalog entries.
    /// Component selectors own colliding names, and the language-service completion limit is
    /// applied once to the merged result.
    /// </summary>
    internal static LanguageCompletionList Merge(
        string documentText,
        LanguageDocumentSyntax syntax,
        TemplateClassValueContext context,
        ClassCatalogSet catalogs,
        CancellationToken cancellationToken)
    {
        var editRange = new LanguageRange(
            TextCoordinateConverter.GetPosition(documentText, context.TokenStart),
            TextCoordinateConverter.GetPosition(documentText, context.TokenEnd));
        var completions = new List<LanguageCompletionItem>();
        var seenLabels = new HashSet<string>(StringComparer.Ordinal);
        var componentClassNames = ExtractClassNames(syntax, cancellationToken);
        var isIncomplete = catalogs.IsTruncated;

        foreach (var className in componentClassNames)
        {
            if (!className.StartsWith(context.Prefix, StringComparison.Ordinal) ||
                !seenLabels.Add(className))
            {
                continue;
            }

            completions.Add(new LanguageCompletionItem(
                className,
                LanguageCompletionItemKind.Property,
                "Component style class",
                $"**`.{className}`** is declared in this component's style block.",
                className,
                IsSnippet: false,
                SortText: "00000:component-style:" + className,
                EditRange: editRange,
                FilterText: className));
            if (completions.Count == LanguageCompletionLimits.MaximumItems)
            {
                return new LanguageCompletionList(completions, IsIncomplete: true);
            }
        }

        foreach (var entry in catalogs.Entries)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!entry.ClassName.StartsWith(context.Prefix, StringComparison.Ordinal) ||
                !seenLabels.Add(entry.ClassName))
            {
                continue;
            }

            var catalogSortText = entry.SortText ??
                entry.Order.ToString("D8", CultureInfo.InvariantCulture);
            completions.Add(new LanguageCompletionItem(
                entry.ClassName,
                entry.ColorValue is null
                    ? LanguageCompletionItemKind.Property
                    : LanguageCompletionItemKind.Color,
                "Build-contributed class",
                Documentation: string.Empty,
                entry.ClassName,
                IsSnippet: false,
                SortText: "10000:class-catalog:" + catalogSortText + ":" + entry.ClassName,
                EditRange: editRange,
                FilterText: entry.ClassName,
                ColorValue: entry.ColorValue));
            if (completions.Count == LanguageCompletionLimits.MaximumItems)
            {
                return new LanguageCompletionList(completions, IsIncomplete: true);
            }
        }

        return new LanguageCompletionList(completions, isIncomplete);
    }

    /// <summary>Returns whether the cursor is within a CSS block comment in a style block.</summary>
    internal static bool IsInsideComment(string content, int relativeOffset)
    {
        if (relativeOffset < 0 || relativeOffset > content.Length)
        {
            return false;
        }

        var isComment = false;
        var quote = '\0';
        var escaped = false;
        for (var index = 0; index < relativeOffset; index++)
        {
            var character = content[index];
            if (isComment)
            {
                if (character == '*' &&
                    index + 1 < relativeOffset &&
                    content[index + 1] == '/')
                {
                    isComment = false;
                    index++;
                }

                continue;
            }

            if (quote != '\0')
            {
                if (escaped)
                {
                    escaped = false;
                }
                else if (character == '\\')
                {
                    escaped = true;
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
            }
            else if (character == '/' &&
                     index + 1 < relativeOffset &&
                     content[index + 1] == '*')
            {
                isComment = true;
                index++;
            }
        }

        return isComment;
    }

    private static IReadOnlyList<string> ExtractClassNames(
        LanguageDocumentSyntax syntax,
        CancellationToken cancellationToken)
    {
        var classNames = new HashSet<string>(StringComparer.Ordinal);
        foreach (var style in syntax.Styles)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (style.Lang is not null &&
                !string.Equals(style.Lang, "css", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var parse = new CssSyntaxParser().ParseSyntax(style.Content, cancellationToken);
            foreach (var stylesheet in parse.Nodes.OfType<CssStylesheetNode>())
            {
                AppendRuleClassNames(stylesheet.Rules, classNames, cancellationToken);
            }
        }

        return classNames.OrderBy(name => name, StringComparer.Ordinal).ToArray();
    }

    private static void AppendRuleClassNames(
        SyntaxList<CssSyntaxNode> rules,
        HashSet<string> classNames,
        CancellationToken cancellationToken)
    {
        foreach (var rule in rules)
        {
            cancellationToken.ThrowIfCancellationRequested();
            switch (rule)
            {
                case CssQualifiedRuleNode qualified:
                    AppendSelectorClassNames(qualified.Selectors, classNames);
                    break;
                case CssAtRuleNode atRule when !string.Equals(
                    atRule.Name,
                    "keyframes",
                    StringComparison.OrdinalIgnoreCase):
                    AppendRuleClassNames(atRule.Body, classNames, cancellationToken);
                    break;
            }
        }
    }

    private static void AppendSelectorClassNames(
        CssSelectorListNode selectorList,
        HashSet<string> classNames)
    {
        foreach (var selector in selectorList.Selectors)
        {
            foreach (var part in selector.Parts)
            {
                if (part is CssSimpleSelectorNode
                    {
                        Selector: CssSimpleSelectorKind.Class,
                        Text.Length: > 1,
                    } simple)
                {
                    classNames.Add(simple.Text.Substring(1));
                }
                else if (part is CssPseudoSelectorNode { Argument: { } argument })
                {
                    AppendSelectorClassNames(argument, classNames);
                }
            }
        }
    }
}
