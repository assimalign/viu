using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;

namespace Assimalign.Viu.Tooling.UtilityCss;

internal static class UtilityThemeParseEngine
{
    public static UtilityThemeParseResult Parse(
        string? css,
        UtilityThemeParseOptions options,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ValidateOptions(options);

        var baseTheme = ApplyImportedThemeOptions(
            options.BaseTheme ?? UtilityTheme.Default,
            options.ImportedThemeOptions);
        var prefix = options.Prefix ?? baseTheme.Prefix;
        if (string.IsNullOrEmpty(css))
        {
            var isImportant =
                options.IsImportant ||
                baseTheme.IsImportant;
            var configuredTheme = string.Equals(
                    prefix,
                    baseTheme.Prefix,
                    StringComparison.Ordinal) &&
                isImportant == baseTheme.IsImportant
                ? baseTheme
                : new UtilityTheme(
                    baseTheme.Properties,
                    prefix,
                    baseTheme.Keyframes,
                    isImportant);
            return new UtilityThemeParseResult(
                configuredTheme,
                string.Empty,
                UtilityCollection<UtilityThemeDeclaration>.Empty,
                UtilityCollection<UtilityThemeDiagnostic>.Empty);
        }

        var text = css!;
        var propertiesByName = new Dictionary<string, UtilityThemeProperty>(
            StringComparer.Ordinal);
        foreach (var property in baseTheme.Properties)
        {
            propertiesByName[property.Name] = property;
        }

        var effectiveAuthoredDeclarations =
            new Dictionary<string, UtilityThemeDeclaration>(
                StringComparer.Ordinal);
        var declarations = new List<UtilityThemeDeclaration>();
        var diagnostics = new List<UtilityThemeDiagnostic>();
        var index = 0;

        while (index < text.Length)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (IsCommentStart(text, index))
            {
                index = SkipComment(
                    text,
                    index,
                    cancellationToken);
                continue;
            }

            if (MatchesThemeRule(text, index))
            {
                ParseThemeRule(
                    text,
                    ref index,
                    options,
                    propertiesByName,
                    effectiveAuthoredDeclarations,
                    declarations,
                    diagnostics,
                    ref prefix,
                    cancellationToken);
                continue;
            }

            if (text[index] == '{')
            {
                var blockEnd = FindMatchingBrace(
                    text,
                    index,
                    cancellationToken);
                index = blockEnd < 0
                    ? text.Length
                    : blockEnd + 1;
                continue;
            }

            if (text[index] is '\'' or '"')
            {
                index = FindClosingQuote(
                    text,
                    index + 1,
                    text.Length,
                    text[index],
                    cancellationToken);
                if (index < text.Length)
                {
                    index++;
                }

                continue;
            }

            index++;
        }

        var theme = new UtilityTheme(
            propertiesByName.Values,
            prefix,
            baseTheme.Keyframes,
            options.IsImportant || baseTheme.IsImportant);
        var renderedCss = RenderCss(
            theme,
            effectiveAuthoredDeclarations.Values,
            cancellationToken);

        return new UtilityThemeParseResult(
            theme,
            renderedCss,
            declarations.Count == 0
                ? UtilityCollection<UtilityThemeDeclaration>.Empty
                : new UtilityCollection<UtilityThemeDeclaration>(declarations),
            diagnostics.Count == 0
                ? UtilityCollection<UtilityThemeDiagnostic>.Empty
                : new UtilityCollection<UtilityThemeDiagnostic>(diagnostics));
    }

    private static void ParseThemeRule(
        string text,
        ref int index,
        UtilityThemeParseOptions parseOptions,
        IDictionary<string, UtilityThemeProperty> propertiesByName,
        IDictionary<string, UtilityThemeDeclaration> effectiveAuthoredDeclarations,
        ICollection<UtilityThemeDeclaration> declarations,
        ICollection<UtilityThemeDiagnostic> diagnostics,
        ref string? prefix,
        CancellationToken cancellationToken)
    {
        var ruleStart = index;
        var parameterStart = index + "@theme".Length;
        var openBrace = FindThemeOpenBrace(
            text,
            parameterStart,
            cancellationToken,
            out var statementEnd);
        if (openBrace < 0)
        {
            var end = statementEnd < 0
                ? text.Length
                : statementEnd + 1;
            diagnostics.Add(
                CreateDiagnostic(
                    parseOptions,
                    UtilityThemeDiagnosticCode.MissingBlock,
                    "An @theme rule must contain a declaration block.",
                    ruleStart,
                    end - ruleStart));
            index = end;
            return;
        }

        var themeOptions = ParseThemeOptions(
            text,
            parameterStart,
            openBrace,
            parseOptions,
            diagnostics,
            ref prefix,
            cancellationToken);
        var closeBrace = FindMatchingBrace(
            text,
            openBrace,
            cancellationToken);
        var contentEnd = closeBrace < 0
            ? text.Length
            : closeBrace;
        if (closeBrace < 0)
        {
            diagnostics.Add(
                CreateDiagnostic(
                    parseOptions,
                    UtilityThemeDiagnosticCode.UnterminatedBlock,
                    "The @theme declaration block is not terminated.",
                    openBrace,
                    text.Length - openBrace));
        }

        ParseDeclarations(
            text,
            openBrace + 1,
            contentEnd,
            themeOptions,
            parseOptions,
            propertiesByName,
            effectiveAuthoredDeclarations,
            declarations,
            diagnostics,
            cancellationToken);

        index = closeBrace < 0
            ? text.Length
            : closeBrace + 1;
    }

    private static UtilityThemeOptions ParseThemeOptions(
        string text,
        int start,
        int end,
        UtilityThemeParseOptions parseOptions,
        ICollection<UtilityThemeDiagnostic> diagnostics,
        ref string? prefix,
        CancellationToken cancellationToken)
    {
        var result = UtilityThemeOptions.None;
        var index = start;

        while (index < end)
        {
            cancellationToken.ThrowIfCancellationRequested();
            SkipWhitespaceAndComments(
                text,
                ref index,
                end,
                cancellationToken);
            if (index >= end)
            {
                break;
            }

            var optionStart = index;
            var parenthesisDepth = 0;
            while (index < end)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var character = text[index];
                if (character == '(')
                {
                    parenthesisDepth++;
                    index++;
                    continue;
                }

                if (character == ')' && parenthesisDepth != 0)
                {
                    parenthesisDepth--;
                    index++;
                    continue;
                }

                if (parenthesisDepth == 0 &&
                    (char.IsWhiteSpace(character) ||
                     IsCommentStart(text, index)))
                {
                    break;
                }

                index++;
            }

            var option = text.Substring(
                optionStart,
                index - optionStart);
            switch (option)
            {
                case "inline":
                    result |= UtilityThemeOptions.Inline;
                    break;
                case "static":
                    result |= UtilityThemeOptions.Static;
                    break;
                case "reference":
                    result |= UtilityThemeOptions.Reference;
                    break;
                case "default":
                    result |= UtilityThemeOptions.Default;
                    break;
                default:
                    if (option.StartsWith(
                            "prefix(",
                            StringComparison.Ordinal) &&
                        option.EndsWith(
                            ")",
                            StringComparison.Ordinal))
                    {
                        var candidatePrefix = option.Substring(
                            "prefix(".Length,
                            option.Length - "prefix(".Length - 1);
                        if (!IsValidPrefix(candidatePrefix))
                        {
                            diagnostics.Add(
                                CreateDiagnostic(
                                    parseOptions,
                                    UtilityThemeDiagnosticCode.InvalidPrefix,
                                    "A utility prefix must contain lowercase ASCII letters only.",
                                    optionStart,
                                    option.Length));
                        }
                        else if (prefix is not null &&
                                 !string.Equals(
                                     prefix,
                                     candidatePrefix,
                                     StringComparison.Ordinal))
                        {
                            diagnostics.Add(
                                CreateDiagnostic(
                                    parseOptions,
                                    UtilityThemeDiagnosticCode.ConflictingPrefix,
                                    $"The utility prefix '{candidatePrefix}' conflicts with the existing '{prefix}' prefix.",
                                    optionStart,
                                    option.Length));
                        }
                        else
                        {
                            prefix = candidatePrefix;
                        }
                    }
                    else
                    {
                        diagnostics.Add(
                            CreateDiagnostic(
                                parseOptions,
                                UtilityThemeDiagnosticCode.InvalidOption,
                                $"The @theme option '{option}' is not supported.",
                                optionStart,
                                option.Length));
                    }

                    break;
            }
        }

        return result;
    }

    private static void ParseDeclarations(
        string text,
        int start,
        int end,
        UtilityThemeOptions themeOptions,
        UtilityThemeParseOptions parseOptions,
        IDictionary<string, UtilityThemeProperty> propertiesByName,
        IDictionary<string, UtilityThemeDeclaration> effectiveAuthoredDeclarations,
        ICollection<UtilityThemeDeclaration> declarations,
        ICollection<UtilityThemeDiagnostic> diagnostics,
        CancellationToken cancellationToken)
    {
        var index = start;
        while (index < end)
        {
            cancellationToken.ThrowIfCancellationRequested();
            SkipWhitespaceAndComments(
                text,
                ref index,
                end,
                cancellationToken);
            if (index >= end)
            {
                break;
            }

            var declarationStart = index;
            var colon = -1;
            var statementEnd = end;
            var hasSemicolon = false;
            var closingDelimiters = new List<char>();
            var quote = '\0';
            var isEscaped = false;
            var nestedBlockStart = -1;

            while (index < end)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var character = text[index];
                if (isEscaped)
                {
                    isEscaped = false;
                    index++;
                    continue;
                }

                if (character == '\\')
                {
                    isEscaped = true;
                    index++;
                    continue;
                }

                if (quote != '\0')
                {
                    if (character == quote)
                    {
                        quote = '\0';
                    }

                    index++;
                    continue;
                }

                if (character is '\'' or '"')
                {
                    quote = character;
                    index++;
                    continue;
                }

                if (IsCommentStart(text, index))
                {
                    index = SkipComment(
                        text,
                        index,
                        cancellationToken);
                    continue;
                }

                if (character is '(' or '[')
                {
                    closingDelimiters.Add(
                        character == '('
                            ? ')'
                            : ']');
                    index++;
                    continue;
                }

                if (character is ')' or ']')
                {
                    if (closingDelimiters.Count != 0 &&
                        closingDelimiters[closingDelimiters.Count - 1] == character)
                    {
                        closingDelimiters.RemoveAt(closingDelimiters.Count - 1);
                    }

                    index++;
                    continue;
                }

                if (closingDelimiters.Count == 0)
                {
                    if (character == ':' && colon < 0)
                    {
                        colon = index;
                        index++;
                        continue;
                    }

                    if (character == ';')
                    {
                        statementEnd = index;
                        hasSemicolon = true;
                        break;
                    }

                    if (character == '{')
                    {
                        nestedBlockStart = index;
                        break;
                    }
                }

                index++;
            }

            if (nestedBlockStart >= 0)
            {
                var nestedBlockEnd = FindMatchingBrace(
                    text,
                    nestedBlockStart,
                    cancellationToken);
                var invalidEnd = nestedBlockEnd < 0 || nestedBlockEnd >= end
                    ? end
                    : nestedBlockEnd + 1;
                diagnostics.Add(
                    CreateDiagnostic(
                        parseOptions,
                        UtilityThemeDiagnosticCode.InvalidDeclaration,
                        "@theme blocks may contain custom-property declarations only in this slice.",
                        declarationStart,
                        invalidEnd - declarationStart));
                index = invalidEnd;
                continue;
            }

            if (colon < 0)
            {
                var invalidLength = statementEnd - declarationStart;
                if (hasSemicolon)
                {
                    invalidLength++;
                }

                diagnostics.Add(
                    CreateDiagnostic(
                        parseOptions,
                        UtilityThemeDiagnosticCode.InvalidDeclaration,
                        "A theme declaration must contain a custom-property name and value.",
                        declarationStart,
                        invalidLength));
                index = hasSemicolon
                    ? statementEnd + 1
                    : end;
                continue;
            }

            var nameStart = declarationStart;
            var nameEnd = colon;
            TrimWhitespace(
                text,
                ref nameStart,
                ref nameEnd);
            var valueStart = colon + 1;
            var valueEnd = statementEnd;
            TrimWhitespace(
                text,
                ref valueStart,
                ref valueEnd);

            var completeEnd = hasSemicolon
                ? statementEnd + 1
                : statementEnd;
            if (!TryNormalizeCustomPropertyName(
                    text,
                    nameStart,
                    nameEnd,
                    cancellationToken,
                    out var propertyName))
            {
                diagnostics.Add(
                    CreateDiagnostic(
                        parseOptions,
                        UtilityThemeDiagnosticCode.InvalidCustomProperty,
                        "A theme declaration name must be a valid custom property beginning with '--'.",
                        nameStart,
                        Math.Max(0, nameEnd - nameStart)));
            }
            else if (valueStart == valueEnd)
            {
                diagnostics.Add(
                    CreateDiagnostic(
                        parseOptions,
                        UtilityThemeDiagnosticCode.InvalidDeclaration,
                        "A theme custom property must have a value.",
                        declarationStart,
                        completeEnd - declarationStart));
            }
            else
            {
                var value = text.Substring(
                    valueStart,
                    valueEnd - valueStart);
                ApplyDeclaration(
                    propertyName!,
                    value,
                    themeOptions,
                    parseOptions,
                    declarationStart,
                    completeEnd - declarationStart,
                    propertiesByName,
                    effectiveAuthoredDeclarations,
                    declarations,
                    diagnostics,
                    cancellationToken);
            }

            index = hasSemicolon
                ? statementEnd + 1
                : end;
        }
    }

    private static void ApplyDeclaration(
        string propertyName,
        string value,
        UtilityThemeOptions themeOptions,
        UtilityThemeParseOptions parseOptions,
        int start,
        int length,
        IDictionary<string, UtilityThemeProperty> propertiesByName,
        IDictionary<string, UtilityThemeDeclaration> effectiveAuthoredDeclarations,
        ICollection<UtilityThemeDeclaration> declarations,
        ICollection<UtilityThemeDiagnostic> diagnostics,
        CancellationToken cancellationToken)
    {
        var isWildcard = IsWildcard(propertyName);
        var isInitial = string.Equals(
            value,
            "initial",
            StringComparison.Ordinal);
        if (isWildcard && !isInitial)
        {
            diagnostics.Add(
                CreateDiagnostic(
                    parseOptions,
                    UtilityThemeDiagnosticCode.InvalidNamespaceReset,
                    "A wildcard theme namespace must use the 'initial' reset value.",
                    start,
                    length));
            return;
        }

        var declaration = new UtilityThemeDeclaration(
            propertyName,
            value,
            themeOptions,
            isInitial,
            CreateSourceSpan(
                parseOptions,
                start,
                length));
        declarations.Add(declaration);

        if (isWildcard)
        {
            ClearNamespace(
                propertyName,
                propertiesByName,
                effectiveAuthoredDeclarations,
                cancellationToken);
            return;
        }

        if (isInitial)
        {
            propertiesByName.Remove(propertyName);
            effectiveAuthoredDeclarations.Remove(propertyName);
            return;
        }

        if ((themeOptions & UtilityThemeOptions.Default) != 0 &&
            propertiesByName.TryGetValue(
                propertyName,
                out var existing) &&
            (existing.Options & UtilityThemeOptions.Default) == 0)
        {
            return;
        }

        propertiesByName[propertyName] = new UtilityThemeProperty(
            propertyName,
            value,
            themeOptions);
        effectiveAuthoredDeclarations[propertyName] = declaration;
    }

    private static void ClearNamespace(
        string wildcardName,
        IDictionary<string, UtilityThemeProperty> propertiesByName,
        IDictionary<string, UtilityThemeDeclaration> effectiveAuthoredDeclarations,
        CancellationToken cancellationToken)
    {
        if (wildcardName == "--*")
        {
            propertiesByName.Clear();
            effectiveAuthoredDeclarations.Clear();
            return;
        }

        var namespacePrefix = wildcardName.Substring(
            0,
            wildcardName.Length - 1);
        var keys = propertiesByName.Keys.ToArray();
        foreach (var key in keys)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!key.StartsWith(
                    namespacePrefix,
                    StringComparison.Ordinal) ||
                IsIgnoredOverlappingNamespace(
                    wildcardName,
                    key))
            {
                continue;
            }

            propertiesByName.Remove(key);
            effectiveAuthoredDeclarations.Remove(key);
        }
    }

    private static bool IsIgnoredOverlappingNamespace(
        string wildcardName,
        string propertyName)
    {
        if (wildcardName == "--font-*")
        {
            return propertyName.StartsWith(
                "--font-weight-",
                StringComparison.Ordinal) ||
                propertyName.StartsWith(
                    "--font-size-",
                    StringComparison.Ordinal);
        }

        if (wildcardName == "--text-*")
        {
            return propertyName.StartsWith(
                "--text-shadow-",
                StringComparison.Ordinal);
        }

        return false;
    }

    private static string RenderCss(
        UtilityTheme theme,
        IEnumerable<UtilityThemeDeclaration> declarations,
        CancellationToken cancellationToken)
    {
        var emitted = declarations
            .Where(
                declaration =>
                    !declaration.IsReset &&
                    (declaration.Options & UtilityThemeOptions.Reference) == 0)
            .OrderBy(
                declaration => declaration.Name,
                StringComparer.Ordinal)
            .ToArray();
        if (emitted.Length == 0)
        {
            return string.Empty;
        }

        var result = new StringBuilder();
        result.AppendLine("@layer theme {");
        result.AppendLine("  :root, :host {");
        foreach (var declaration in emitted)
        {
            cancellationToken.ThrowIfCancellationRequested();
            result.Append("    ");
            result.Append(
                theme.FormatCustomPropertyName(
                    declaration.Name));
            result.Append(": ");
            result.Append(
                NormalizeLineEndings(
                    declaration.Value));
            result.AppendLine(";");
        }

        result.AppendLine("  }");
        result.AppendLine("}");
        return result.ToString();
    }

    private static string NormalizeLineEndings(string value) =>
        value.Replace(
                "\r\n",
                "\n")
            .Replace(
                '\r',
                '\n');

    private static int FindThemeOpenBrace(
        string text,
        int start,
        CancellationToken cancellationToken,
        out int statementEnd)
    {
        statementEnd = -1;
        var parenthesisDepth = 0;
        var quote = '\0';
        var isEscaped = false;

        for (var index = start; index < text.Length; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var character = text[index];
            if (isEscaped)
            {
                isEscaped = false;
                continue;
            }

            if (character == '\\')
            {
                isEscaped = true;
                continue;
            }

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
                continue;
            }

            if (IsCommentStart(text, index))
            {
                index = SkipComment(
                    text,
                    index,
                    cancellationToken) - 1;
                continue;
            }

            if (character == '(')
            {
                parenthesisDepth++;
                continue;
            }

            if (character == ')' && parenthesisDepth != 0)
            {
                parenthesisDepth--;
                continue;
            }

            if (parenthesisDepth == 0)
            {
                if (character == '{')
                {
                    return index;
                }

                if (character == ';')
                {
                    statementEnd = index;
                    return -1;
                }
            }
        }

        return -1;
    }

    private static int FindMatchingBrace(
        string text,
        int openBrace,
        CancellationToken cancellationToken)
    {
        var depth = 1;
        var quote = '\0';
        var isEscaped = false;

        for (var index = openBrace + 1;
             index < text.Length;
             index++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var character = text[index];
            if (isEscaped)
            {
                isEscaped = false;
                continue;
            }

            if (character == '\\')
            {
                isEscaped = true;
                continue;
            }

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
                continue;
            }

            if (IsCommentStart(text, index))
            {
                index = SkipComment(
                    text,
                    index,
                    cancellationToken) - 1;
                continue;
            }

            if (character == '{')
            {
                depth++;
            }
            else if (character == '}')
            {
                depth--;
                if (depth == 0)
                {
                    return index;
                }
            }
        }

        return -1;
    }

    private static int FindClosingQuote(
        string text,
        int start,
        int end,
        char quote,
        CancellationToken cancellationToken)
    {
        var isEscaped = false;
        for (var index = start; index < end; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var character = text[index];
            if (isEscaped)
            {
                isEscaped = false;
                continue;
            }

            if (character == '\\')
            {
                isEscaped = true;
                continue;
            }

            if (character == quote)
            {
                return index;
            }
        }

        return end;
    }

    private static void SkipWhitespaceAndComments(
        string text,
        ref int index,
        int end,
        CancellationToken cancellationToken)
    {
        while (index < end)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (char.IsWhiteSpace(text[index]))
            {
                index++;
                continue;
            }

            if (IsCommentStart(text, index))
            {
                index = SkipComment(
                    text,
                    index,
                    cancellationToken);
                continue;
            }

            break;
        }
    }

    private static int SkipComment(
        string text,
        int start,
        CancellationToken cancellationToken)
    {
        var index = start + 2;
        while (index < text.Length)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (index + 1 < text.Length &&
                text[index] == '*' &&
                text[index + 1] == '/')
            {
                return index + 2;
            }

            index++;
        }

        return text.Length;
    }

    private static void TrimWhitespace(
        string text,
        ref int start,
        ref int end)
    {
        while (start < end &&
               char.IsWhiteSpace(text[start]))
        {
            start++;
        }

        while (end > start &&
               char.IsWhiteSpace(text[end - 1]))
        {
            end--;
        }
    }

    private static bool TryNormalizeCustomPropertyName(
        string text,
        int start,
        int end,
        CancellationToken cancellationToken,
        out string? propertyName)
    {
        propertyName = null;
        if (end - start <= 2 ||
            text[start] != '-' ||
            text[start + 1] != '-')
        {
            return false;
        }

        var result = new StringBuilder(end - start);
        result.Append("--");
        for (var index = start + 2; index < end; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var character = text[index];
            if (character == '\\')
            {
                if (index + 1 >= end)
                {
                    return false;
                }

                result.Append(text[index + 1]);
                index++;
                continue;
            }

            if (!IsCustomPropertyCharacter(character))
            {
                return false;
            }

            result.Append(character);
        }

        var normalized = result.ToString();
        var wildcardIndex = normalized.IndexOf('*');
        if (wildcardIndex >= 0 &&
            (wildcardIndex != normalized.Length - 1 ||
             (normalized != "--*" &&
              normalized[wildcardIndex - 1] != '-')))
        {
            return false;
        }

        propertyName = normalized;
        return true;
    }

    private static bool IsCustomPropertyCharacter(char character) =>
        (character >= 'a' && character <= 'z') ||
        (character >= 'A' && character <= 'Z') ||
        (character >= '0' && character <= '9') ||
        character is '-' or '_' or '.' or '%' or '/' or '*';

    private static bool IsWildcard(string propertyName) =>
        propertyName == "--*" ||
        propertyName.EndsWith(
            "-*",
            StringComparison.Ordinal);

    private static bool IsValidPrefix(string prefix)
    {
        if (prefix.Length == 0)
        {
            return false;
        }

        foreach (var character in prefix)
        {
            if (character < 'a' ||
                character > 'z')
            {
                return false;
            }
        }

        return true;
    }

    private static bool MatchesThemeRule(
        string text,
        int index)
    {
        const string name = "@theme";
        if (index + name.Length > text.Length ||
            !string.Equals(
                text.Substring(index, name.Length),
                name,
                StringComparison.Ordinal))
        {
            return false;
        }

        var end = index + name.Length;
        return end == text.Length ||
               char.IsWhiteSpace(text[end]) ||
               text[end] is '{' or ';';
    }

    private static bool IsCommentStart(
        string text,
        int index) =>
        index + 1 < text.Length &&
        text[index] == '/' &&
        text[index + 1] == '*';

    private static UtilityThemeDiagnostic CreateDiagnostic(
        UtilityThemeParseOptions options,
        UtilityThemeDiagnosticCode code,
        string message,
        int start,
        int length) =>
        new(
            code,
            UtilityThemeDiagnosticSeverity.Error,
            message,
            CreateSourceSpan(
                options,
                start,
                length));

    private static UtilityThemeSourceSpan CreateSourceSpan(
        UtilityThemeParseOptions options,
        int start,
        int length) =>
        new(
            options.SourceIdentity,
            checked(options.ContentOffset + start),
            Math.Max(0, length));

    private static void ValidateOptions(UtilityThemeParseOptions options)
    {
        if (options.ContentOffset < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(options),
                "The theme content offset cannot be negative.");
        }

        if (options.Prefix is not null &&
            !IsValidPrefix(options.Prefix))
        {
            throw new ArgumentException(
                "The configured utility prefix must contain lowercase ASCII letters only.",
                nameof(options));
        }

        const UtilityThemeOptions supportedImportOptions =
            UtilityThemeOptions.Inline |
            UtilityThemeOptions.Static;
        if ((options.ImportedThemeOptions & ~supportedImportOptions) != 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(options),
                "Import-level theme options may contain only Inline and Static.");
        }
    }

    private static UtilityTheme ApplyImportedThemeOptions(
        UtilityTheme baseTheme,
        UtilityThemeOptions importedThemeOptions)
    {
        if (importedThemeOptions == UtilityThemeOptions.None)
        {
            return baseTheme;
        }

        var properties = new List<UtilityThemeProperty>(
            baseTheme.Properties.Count);
        foreach (var property in baseTheme.Properties)
        {
            properties.Add(
                property with
                {
                    Options = property.Options | importedThemeOptions,
                });
        }

        return new UtilityTheme(
            properties,
            baseTheme.Prefix,
            baseTheme.Keyframes,
            baseTheme.IsImportant);
    }
}
