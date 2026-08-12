using System;
using System.Collections.Generic;
using System.Text;
using Assimalign.Viu.Compiler.SingleFileComponent;
using Assimalign.Viu.Syntax.Templates;

namespace Assimalign.Viu.LanguageService;

/// <summary>
/// Produces authored template hover for resolved component contracts, native elements, and
/// directives ([V01.01.12.07.16], #337). Component identity and property names reuse the compiler's
/// usage manifest and declaration catalog, so hover follows [SFC-CG-8]/[SFC-USE-5] rather than an
/// editor-only name heuristic.
/// </summary>
internal static class TemplateHoverProvider
{
    /// <summary>Gets template hover at one authored offset.</summary>
    /// <param name="document">The immutable live document.</param>
    /// <param name="projection">The live projection shared with component-contract resolution.</param>
    /// <param name="catalog">The build-equivalent component declaration catalog.</param>
    /// <param name="offset">The zero-based authored document offset.</param>
    /// <returns>The template hover, or <see langword="null"/> when the token has no contract.</returns>
    internal static LanguageHover? GetHover(
        LanguageDocument document,
        SingleFileComponentProjectionResult projection,
        TemplateComponentCatalog catalog,
        int offset)
    {
        var template = document.Syntax.Template;
        if (template is not null &&
            TemplateCompletionProvider.IsInsideComment(
                template.Content,
                offset - template.ContentLocation.Start.Offset))
        {
            return null;
        }

        foreach (var usage in projection.ComponentUsages)
        {
            if (!catalog.TryResolve(usage.Tag, out var declaration))
            {
                continue;
            }

            if (ContainsOffset(usage.Location, offset))
            {
                return CreateComponentHover(usage, declaration);
            }

            foreach (var property in usage.Properties)
            {
                if (!ContainsOffset(property.Location, offset))
                {
                    continue;
                }

                var propertyHover = CreateComponentPropertyHover(
                    usage,
                    property,
                    declaration);
                if (propertyHover is not null)
                {
                    return propertyHover;
                }
            }
        }

        if (!TryGetTokenRange(document.Text, offset, out var tokenStart, out var tokenEnd) ||
            !IsInsideTag(document.Text, tokenStart))
        {
            return null;
        }

        var token = document.Text.Substring(tokenStart, tokenEnd - tokenStart);
        var directive = FindDirectiveDocumentation(token);
        if (directive is not null)
        {
            return CreateCatalogHover(document.Text, tokenStart, tokenEnd, token, directive);
        }

        if (!IsTagName(document.Text, tokenStart))
        {
            return null;
        }

        // [SFC-CG-8]: an uppercase collision remains a component candidate when its declaration
        // is unavailable, so <Button> must never fall through to case-insensitive HTML <button>
        // documentation.
        if (char.IsUpper(token[0]))
        {
            return null;
        }

        var builtIn = FindByLabel(ViuCompletionCatalog.TemplateTags, token);
        if (CompilerDomKnowledge.IsNativeTag(token))
        {
            var family = CompilerDomKnowledge.IsHtmlTag(token)
                ? "HTML"
                : CompilerDomKnowledge.IsSvgTag(token)
                    ? "SVG"
                    : "MathML";
            var documentation = builtIn is null
                ? string.Empty
                : $"\n\n{builtIn.Documentation}";
            return new LanguageHover(
                CreateQuickInfoMarkdown(
                    $"<{token}>",
                    $"A native {family} element. Viu emits it directly through the host's element renderer." +
                        documentation),
                CreateRange(document.Text, tokenStart, tokenEnd));
        }

        return builtIn is null
            ? null
            : CreateCatalogHover(document.Text, tokenStart, tokenEnd, $"<{token}>", builtIn);
    }

    /// <summary>
    /// Renders one hover the way the editor's own Quick Info reads: the declaration in a fenced code
    /// block first, then the prose below it.
    /// </summary>
    /// <remarks>
    /// The alternative — bold text and bullet lists — is a document's structure, not a tooltip's, and
    /// a client renders those markers at heading and list sizes. Fencing the declaration gets the
    /// signature colorized by the C# grammar and keeps every following line at ordinary body size, so
    /// a Viu tooltip sits beside a C# one without announcing itself as something else. Clients that
    /// take plaintext receive the same content with the fences and emphasis stripped.
    /// </remarks>
    private static string CreateQuickInfoMarkdown(string declaration, string description)
        => $"```csharp\n{declaration}\n```\n{description}";

    private static LanguageHover CreateComponentHover(
        ComponentUsage usage,
        TemplateComponentDeclaration declaration)
    {
        var compilerDeclaration = declaration.CompilerDeclaration;
        var signature = new StringBuilder("class ").Append(compilerDeclaration.Name);
        if (compilerDeclaration.IsParameterSurfaceKnown)
        {
            foreach (var parameter in compilerDeclaration.Parameters)
            {
                signature
                    .Append("\n    ")
                    .Append(parameter.TypeText)
                    .Append(' ')
                    .Append(parameter.PropertyName)
                    .Append("   // :")
                    .Append(parameter.Name);
                if (parameter.IsRequired)
                {
                    signature.Append(" (required)");
                }
            }
        }

        foreach (var componentEvent in declaration.Events)
        {
            signature
                .Append("\n    ")
                .Append(componentEvent.Detail)
                .Append("   // @")
                .Append(componentEvent.Name);
        }

        var description = $"Resolves the `<{usage.Tag}>` tag to component `{compilerDeclaration.Name}`.";
        if (!compilerDeclaration.IsParameterSurfaceKnown)
        {
            description +=
                " Parameters are declared imperatively and cannot be enumerated at design time.";
        }

        return new LanguageHover(
            CreateQuickInfoMarkdown(signature.ToString(), description),
            ToLanguageRange(usage.Location));
    }

    private static LanguageHover? CreateComponentPropertyHover(
        ComponentUsage usage,
        ComponentUsageProperty property,
        TemplateComponentDeclaration declaration)
    {
        if (property.Kind == ComponentUsagePropertyKind.Listener)
        {
            var canonicalName = ComponentDeclarationNames.Canonicalize(property.Name);
            foreach (var componentEvent in declaration.Events)
            {
                if (!string.Equals(
                        ComponentDeclarationNames.Canonicalize(componentEvent.Name),
                        canonicalName,
                        StringComparison.Ordinal))
                {
                    continue;
                }

                return new LanguageHover(
                    CreateQuickInfoMarkdown(
                        componentEvent.Detail,
                        $"`@{property.AuthoredName}` — event on component `<{usage.Tag}>`."),
                    ToLanguageRange(property.Location));
            }

            return null;
        }

        if (property.Kind is not (ComponentUsagePropertyKind.Static or ComponentUsagePropertyKind.Bound) ||
            !declaration.CompilerDeclaration.IsParameterSurfaceKnown)
        {
            return null;
        }

        var propertyName = ComponentDeclarationNames.Canonicalize(property.Name);
        foreach (var parameter in declaration.CompilerDeclaration.Parameters)
        {
            if (!string.Equals(
                    ComponentDeclarationNames.Canonicalize(parameter.Name),
                    propertyName,
                    StringComparison.Ordinal))
            {
                continue;
            }

            var requirement = parameter.IsRequired ? " Required." : string.Empty;
            return new LanguageHover(
                CreateQuickInfoMarkdown(
                    $"{parameter.TypeText} {parameter.PropertyName}",
                    $"`{property.AuthoredName}` — parameter on component `<{usage.Tag}>`.{requirement}"),
                ToLanguageRange(property.Location));
        }

        return null;
    }

    private static LanguageCompletionItem? FindDirectiveDocumentation(string token)
    {
        var baseToken = token;
        var modifier = baseToken.IndexOf('.');
        if (modifier >= 0)
        {
            baseToken = baseToken.Substring(0, modifier);
        }

        if (baseToken.StartsWith("v-bind", StringComparison.Ordinal) ||
            baseToken.StartsWith(':'))
        {
            return FindByLabel(ViuCompletionCatalog.TemplateBindings, baseToken) ??
                FindByLabel(ViuCompletionCatalog.TemplateDirectives, "v-bind");
        }

        if (baseToken.StartsWith("v-on", StringComparison.Ordinal) ||
            baseToken.StartsWith('@'))
        {
            return FindByLabel(ViuCompletionCatalog.TemplateEvents, baseToken) ??
                FindByLabel(ViuCompletionCatalog.TemplateDirectives, "v-on");
        }

        if (baseToken.StartsWith("v-slot", StringComparison.Ordinal) ||
            baseToken.StartsWith('#'))
        {
            return FindByLabel(ViuCompletionCatalog.TemplateDirectives, "v-slot");
        }

        var colon = baseToken.IndexOf(':');
        if (colon >= 0)
        {
            baseToken = baseToken.Substring(0, colon);
        }

        return baseToken.StartsWith("v-", StringComparison.Ordinal)
            ? FindByLabel(ViuCompletionCatalog.TemplateDirectives, baseToken)
            : null;
    }

    private static LanguageCompletionItem? FindByLabel(
        IReadOnlyList<LanguageCompletionItem> items,
        string label)
    {
        foreach (var item in items)
        {
            if (string.Equals(item.Label, label, StringComparison.Ordinal))
            {
                return item;
            }
        }

        return null;
    }

    private static LanguageHover CreateCatalogHover(
        string documentText,
        int tokenStart,
        int tokenEnd,
        string displayToken,
        LanguageCompletionItem item)
        => new(
            CreateQuickInfoMarkdown(displayToken, $"{item.Detail}. {item.Documentation}"),
            CreateRange(documentText, tokenStart, tokenEnd));

    private static bool ContainsOffset(LocationInfo location, int offset)
        => offset >= location.StartOffset && offset <= location.EndOffset;

    private static LanguageRange ToLanguageRange(LocationInfo location)
        => new(
            new LanguagePosition(location.StartLine, location.StartCharacter),
            new LanguagePosition(location.EndLine, location.EndCharacter));

    private static LanguageRange CreateRange(string text, int start, int end)
        => new(
            TextCoordinateConverter.GetPosition(text, start),
            TextCoordinateConverter.GetPosition(text, end));

    private static bool TryGetTokenRange(
        string text,
        int offset,
        out int start,
        out int end)
    {
        start = offset;
        end = offset;
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
            return false;
        }

        start = offset;
        while (start > 0 && IsTokenCharacter(text[start - 1]))
        {
            start--;
        }

        end = offset + 1;
        while (end < text.Length && IsTokenCharacter(text[end]))
        {
            end++;
        }

        return true;
    }

    private static bool IsTokenCharacter(char character)
        => char.IsLetterOrDigit(character) ||
           character is '_' or '-' or '.' or ':' or '@' or '#';

    private static bool IsInsideTag(string text, int offset)
    {
        var open = text.LastIndexOf('<', Math.Max(offset - 1, 0));
        var close = text.LastIndexOf('>', Math.Max(offset - 1, 0));
        return open > close;
    }

    private static bool IsTagName(string text, int tokenStart)
        => tokenStart > 0 &&
           (text[tokenStart - 1] == '<' ||
            (text[tokenStart - 1] == '/' && tokenStart > 1 && text[tokenStart - 2] == '<'));
}
