using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Threading;

namespace Assimalign.Viu.UtilityCss;

internal static class UtilitySourceParseEngine
{
    private const string UtilitiesSpecifier = "viu-utilities";

    internal static UtilitySourceParseResult Parse(
        string? css,
        UtilitySourceParseOptions options,
        CancellationToken cancellationToken)
    {
        if (options.ContentOffset < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(options),
                "The source content offset cannot be negative.");
        }

        if (options.MaximumExpansionCount <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(options),
                "The source expansion limit must be positive.");
        }

        if (options.VariantRegistry is null)
        {
            throw new ArgumentNullException(
                nameof(options),
                "The source variant registry cannot be null.");
        }

        var text = css ?? string.Empty;
        var diagnostics = new List<UtilitySourceDiagnostic>();
        var parsedDirectives = new List<ParsedSourceDirective>();
        var state = new ImportState();
        var blockDepth = 0;
        var index = 0;

        while (index < text.Length)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (TrySkipCommentOrString(text, ref index))
            {
                continue;
            }

            var character = text[index];
            if (character == '{')
            {
                blockDepth++;
                index++;
                continue;
            }

            if (character == '}')
            {
                if (blockDepth > 0)
                {
                    blockDepth--;
                }

                index++;
                continue;
            }

            if (character != '@')
            {
                index++;
                continue;
            }

            var statementStart = index;
            var nameStart = index + 1;
            var nameEnd = nameStart;
            while (nameEnd < text.Length && IsNameCharacter(text[nameEnd]))
            {
                nameEnd++;
            }

            if (nameEnd == nameStart)
            {
                index++;
                continue;
            }

            var name = text.Substring(nameStart, nameEnd - nameStart);
            if (name != "import" && name != "source")
            {
                index = nameEnd;
                continue;
            }

            if (!TryFindStatementEnd(
                    text,
                    nameEnd,
                    out var statementEnd))
            {
                diagnostics.Add(
                    CreateDiagnostic(
                        options,
                        UtilitySourceDiagnosticCode.UnterminatedStatement,
                        $"The @{name} statement is not terminated with a semicolon.",
                        statementStart,
                        text.Length - statementStart));
                break;
            }

            var parametersStart = nameEnd;
            while (parametersStart < statementEnd &&
                   char.IsWhiteSpace(text[parametersStart]))
            {
                parametersStart++;
            }

            var parametersEnd = statementEnd;
            while (parametersEnd > parametersStart &&
                   char.IsWhiteSpace(text[parametersEnd - 1]))
            {
                parametersEnd--;
            }

            var parameters = text.Substring(
                parametersStart,
                parametersEnd - parametersStart);
            if (name == "import")
            {
                ParseImport(
                    parameters,
                    statementStart,
                    statementEnd + 1,
                    options,
                    state,
                    diagnostics);
            }
            else if (blockDepth != 0)
            {
                diagnostics.Add(
                    CreateDiagnostic(
                        options,
                        UtilitySourceDiagnosticCode.InvalidPlacement,
                        "The @source directive must be authored at the stylesheet top level.",
                        statementStart,
                        statementEnd + 1 - statementStart));
            }
            else
            {
                ParseSource(
                    parameters,
                    statementStart,
                    statementEnd + 1,
                    options,
                    parsedDirectives,
                    diagnostics);
            }

            index = statementEnd + 1;
        }

        var directives = FinalizeDirectives(
            parsedDirectives,
            options.Prefix ?? state.Prefix,
            options,
            diagnostics,
            cancellationToken);
        var includedPaths = new List<string>();
        var excludedPaths = new List<string>();
        var includedCandidates = new List<string>();
        var excludedCandidates = new List<string>();
        var includedPathSet = new HashSet<string>(StringComparer.Ordinal);
        var excludedPathSet = new HashSet<string>(StringComparer.Ordinal);
        var includedCandidateSet = new HashSet<string>(StringComparer.Ordinal);
        var excludedCandidateSet = new HashSet<string>(StringComparer.Ordinal);
        foreach (var directive in directives)
        {
            switch (directive.Kind)
            {
                case UtilitySourceDirectiveKind.IncludePath:
                    AddDistinct(
                        directive.Value,
                        includedPaths,
                        includedPathSet);
                    break;
                case UtilitySourceDirectiveKind.ExcludePath:
                    AddDistinct(
                        directive.Value,
                        excludedPaths,
                        excludedPathSet);
                    break;
                case UtilitySourceDirectiveKind.IncludeInline:
                    AddDistinct(
                        directive.Candidates,
                        includedCandidates,
                        includedCandidateSet);
                    break;
                case UtilitySourceDirectiveKind.ExcludeInline:
                    AddDistinct(
                        directive.Candidates,
                        excludedCandidates,
                        excludedCandidateSet);
                    break;
                default:
                    throw new InvalidOperationException(
                        "Unknown utility source directive kind.");
            }
        }

        diagnostics.Sort(
            (left, right) =>
            {
                var byStart = left.SourceSpan.Start.CompareTo(
                    right.SourceSpan.Start);
                return byStart != 0
                    ? byStart
                    : left.Code.CompareTo(right.Code);
            });

        return new UtilitySourceParseResult(
            new UtilitySourceConfiguration(
                state.HasUtilitiesImport,
                state.IsAutomaticDetectionEnabled,
                state.BasePath,
                state.Prefix,
                state.IsImportant,
                state.ImportedThemeOptions,
                new UtilityCollection<UtilitySourceDirective>(directives),
                new UtilityCollection<string>(includedPaths),
                new UtilityCollection<string>(excludedPaths),
                new UtilityCollection<string>(includedCandidates),
                new UtilityCollection<string>(excludedCandidates)),
            new UtilityCollection<UtilitySourceDiagnostic>(diagnostics));
    }

    private static void ParseImport(
        string parameters,
        int statementStart,
        int statementEnd,
        UtilitySourceParseOptions options,
        ImportState state,
        ICollection<UtilitySourceDiagnostic> diagnostics)
    {
        var index = 0;
        SkipWhitespace(parameters, ref index);
        if (!TryReadQuoted(
                parameters,
                ref index,
                out var specifier))
        {
            return;
        }

        if (IsTailwindSpecifier(specifier))
        {
            diagnostics.Add(
                CreateDiagnostic(
                    options,
                    UtilitySourceDiagnosticCode.TailwindDependencyNotAllowed,
                    "Importing Tailwind CSS is not supported; use the standalone \"viu-utilities\" compiler sentinel.",
                    statementStart,
                    statementEnd - statementStart));
            return;
        }

        if (specifier != UtilitiesSpecifier)
        {
            return;
        }

        if (state.HasUtilitiesImport)
        {
            diagnostics.Add(
                CreateDiagnostic(
                    options,
                    UtilitySourceDiagnosticCode.DuplicateUtilitiesImport,
                    "The \"viu-utilities\" compiler sentinel may be imported only once.",
                    statementStart,
                    statementEnd - statementStart));
            return;
        }

        state.HasUtilitiesImport = true;
        var seenSource = false;
        var seenPrefix = false;
        var seenTheme = false;
        var seenImportant = false;
        while (index < parameters.Length)
        {
            SkipWhitespace(parameters, ref index);
            if (index >= parameters.Length)
            {
                break;
            }

            var nameStart = index;
            while (index < parameters.Length &&
                   IsNameCharacter(parameters[index]))
            {
                index++;
            }

            if (index == nameStart)
            {
                AddInvalidImportModifier(
                    statementStart,
                    statementEnd,
                    options,
                    diagnostics);
                return;
            }

            var name = parameters.Substring(
                nameStart,
                index - nameStart);
            if (name == "important")
            {
                if (seenImportant)
                {
                    AddInvalidImportModifier(
                        statementStart,
                        statementEnd,
                        options,
                        diagnostics);
                    return;
                }

                seenImportant = true;
                state.IsImportant = true;
                continue;
            }

            SkipWhitespace(parameters, ref index);
            if (index >= parameters.Length ||
                parameters[index] != '(' ||
                !TryReadFunctionArgument(
                    parameters,
                    ref index,
                    out var argument))
            {
                AddInvalidImportModifier(
                    statementStart,
                    statementEnd,
                    options,
                    diagnostics);
                return;
            }

            if (name == "source" && !seenSource)
            {
                seenSource = true;
                ParseImportSource(
                    argument,
                    statementStart,
                    statementEnd,
                    options,
                    state,
                    diagnostics);
                continue;
            }

            if (name == "prefix" && !seenPrefix)
            {
                seenPrefix = true;
                var prefix = argument.Trim();
                if (!IsValidPrefix(prefix))
                {
                    diagnostics.Add(
                        CreateDiagnostic(
                            options,
                            UtilitySourceDiagnosticCode.InvalidPrefix,
                            "A utility prefix must contain lowercase ASCII letters only.",
                            statementStart,
                            statementEnd - statementStart));
                }
                else
                {
                    state.Prefix = prefix;
                }

                continue;
            }

            if (name == "theme" && !seenTheme)
            {
                seenTheme = true;
                var mode = argument.Trim();
                if (mode == "static")
                {
                    state.ImportedThemeOptions |= UtilityThemeOptions.Static;
                }
                else if (mode == "inline")
                {
                    state.ImportedThemeOptions |= UtilityThemeOptions.Inline;
                }
                else
                {
                    AddInvalidImportModifier(
                        statementStart,
                        statementEnd,
                        options,
                        diagnostics);
                }

                continue;
            }

            AddInvalidImportModifier(
                statementStart,
                statementEnd,
                options,
                diagnostics);
            return;
        }
    }

    private static void ParseImportSource(
        string argument,
        int statementStart,
        int statementEnd,
        UtilitySourceParseOptions options,
        ImportState state,
        ICollection<UtilitySourceDiagnostic> diagnostics)
    {
        var trimmed = argument.Trim();
        if (trimmed == "none")
        {
            state.IsAutomaticDetectionEnabled = false;
            state.BasePath = null;
            return;
        }

        var argumentIndex = 0;
        if (TryReadQuoted(
                trimmed,
                ref argumentIndex,
                out var basePath))
        {
            SkipWhitespace(trimmed, ref argumentIndex);
            if (argumentIndex == trimmed.Length)
            {
                state.BasePath = basePath;
                return;
            }
        }

        diagnostics.Add(
            CreateDiagnostic(
                options,
                UtilitySourceDiagnosticCode.InvalidImportModifier,
                "The source import modifier requires source(none) or one quoted base path.",
                statementStart,
                statementEnd - statementStart));
    }

    private static void ParseSource(
        string parameters,
        int statementStart,
        int statementEnd,
        UtilitySourceParseOptions options,
        ICollection<ParsedSourceDirective> directives,
        ICollection<UtilitySourceDiagnostic> diagnostics)
    {
        var index = 0;
        SkipWhitespace(parameters, ref index);
        var isExcluded = TryReadWord(
            parameters,
            ref index,
            "not");
        SkipWhitespace(parameters, ref index);

        if (TryReadQuoted(
                parameters,
                ref index,
                out var path))
        {
            SkipWhitespace(parameters, ref index);
            if (index == parameters.Length && path.Length > 0)
            {
                directives.Add(
                    new ParsedSourceDirective(
                        isExcluded
                            ? UtilitySourceDirectiveKind.ExcludePath
                            : UtilitySourceDirectiveKind.IncludePath,
                        path,
                        CreateSpan(
                            options,
                            statementStart,
                            statementEnd - statementStart)));
                return;
            }
        }
        else if (TryReadWord(
                     parameters,
                     ref index,
                     "inline"))
        {
            SkipWhitespace(parameters, ref index);
            if (index < parameters.Length &&
                parameters[index] == '(' &&
                TryReadFunctionArgument(
                    parameters,
                    ref index,
                    out var argument))
            {
                var argumentIndex = 0;
                SkipWhitespace(argument, ref argumentIndex);
                if (TryReadQuoted(
                        argument,
                        ref argumentIndex,
                        out var expression))
                {
                    SkipWhitespace(argument, ref argumentIndex);
                    SkipWhitespace(parameters, ref index);
                    if (argumentIndex == argument.Length &&
                        index == parameters.Length &&
                        expression.Length > 0)
                    {
                        directives.Add(
                            new ParsedSourceDirective(
                                isExcluded
                                    ? UtilitySourceDirectiveKind.ExcludeInline
                                    : UtilitySourceDirectiveKind.IncludeInline,
                                expression,
                                CreateSpan(
                                    options,
                                    statementStart,
                                    statementEnd - statementStart)));
                        return;
                    }
                }
            }
        }

        diagnostics.Add(
            CreateDiagnostic(
                options,
                UtilitySourceDiagnosticCode.InvalidSourceDirective,
                "Use @source \"path\", @source not \"path\", @source inline(\"expression\"), or @source not inline(\"expression\").",
                statementStart,
                statementEnd - statementStart));
    }

    private static List<UtilitySourceDirective> FinalizeDirectives(
        IEnumerable<ParsedSourceDirective> parsedDirectives,
        string? prefix,
        UtilitySourceParseOptions options,
        ICollection<UtilitySourceDiagnostic> diagnostics,
        CancellationToken cancellationToken)
    {
        var result = new List<UtilitySourceDirective>();
        foreach (var parsedDirective in parsedDirectives)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (parsedDirective.Kind == UtilitySourceDirectiveKind.IncludePath ||
                parsedDirective.Kind == UtilitySourceDirectiveKind.ExcludePath)
            {
                result.Add(
                    new UtilitySourceDirective(
                        parsedDirective.Kind,
                        parsedDirective.Value,
                        UtilityCollection<string>.Empty,
                        parsedDirective.SourceSpan));
                continue;
            }

            if (!TryExpand(
                    parsedDirective.Value,
                    options.MaximumExpansionCount,
                    cancellationToken,
                    out var expanded,
                    out var expansionError))
            {
                diagnostics.Add(
                    new UtilitySourceDiagnostic(
                        expansionError == ExpansionError.Limit
                            ? UtilitySourceDiagnosticCode.ExpansionLimitExceeded
                            : UtilitySourceDiagnosticCode.InvalidInlineExpression,
                        UtilitySourceDiagnosticSeverity.Error,
                        expansionError == ExpansionError.Limit
                            ? $"The inline source expression exceeds the {options.MaximumExpansionCount.ToString(CultureInfo.InvariantCulture)}-value expansion limit."
                            : "The inline source expression contains an unmatched brace or invalid numeric range.",
                        parsedDirective.SourceSpan));
                continue;
            }

            var candidates = new List<string>();
            var candidateSet = new HashSet<string>(StringComparer.Ordinal);
            foreach (var value in expanded)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var scan = UtilityCandidateScanner.Scan(
                    value,
                    new UtilityCandidateScanOptions
                    {
                        Prefix = prefix,
                        VariantRegistry = options.VariantRegistry,
                    },
                    cancellationToken);
                foreach (var detection in scan.Candidates)
                {
                    AddDistinct(
                        detection.Candidate.RawText,
                        candidates,
                        candidateSet);
                }
            }

            result.Add(
                new UtilitySourceDirective(
                    parsedDirective.Kind,
                    parsedDirective.Value,
                    new UtilityCollection<string>(candidates),
                    parsedDirective.SourceSpan));
        }

        return result;
    }

    private static bool TryExpand(
        string expression,
        int maximumCount,
        CancellationToken cancellationToken,
        out List<string> values,
        out ExpansionError error)
    {
        values = new List<string>();
        error = ExpansionError.None;
        return TryExpandCore(
            expression,
            maximumCount,
            cancellationToken,
            values,
            ref error);
    }

    private static bool TryExpandCore(
        string expression,
        int maximumCount,
        CancellationToken cancellationToken,
        ICollection<string> values,
        ref ExpansionError error)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var openingBrace = FindOpeningBrace(expression);
        if (openingBrace < 0)
        {
            if (ContainsUnescapedBrace(expression))
            {
                error = ExpansionError.Syntax;
                return false;
            }

            if (values.Count >= maximumCount)
            {
                error = ExpansionError.Limit;
                return false;
            }

            values.Add(UnescapeBraces(expression));
            return true;
        }

        var closingBrace = FindMatchingBrace(
            expression,
            openingBrace);
        if (closingBrace < 0)
        {
            error = ExpansionError.Syntax;
            return false;
        }

        var prefix = expression.Substring(0, openingBrace);
        var content = expression.Substring(
            openingBrace + 1,
            closingBrace - openingBrace - 1);
        var suffix = expression.Substring(closingBrace + 1);
        if (!TryGetExpansionAlternatives(
                content,
                out var alternatives))
        {
            error = ExpansionError.Syntax;
            return false;
        }

        foreach (var alternative in alternatives)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!TryExpandCore(
                    prefix + alternative + suffix,
                    maximumCount,
                    cancellationToken,
                    values,
                    ref error))
            {
                return false;
            }
        }

        return true;
    }

    private static bool TryGetExpansionAlternatives(
        string content,
        out List<string> alternatives)
    {
        alternatives = SplitTopLevel(content, ',');
        if (alternatives.Count > 1)
        {
            return true;
        }

        return TryExpandNumericRange(
            content,
            out alternatives);
    }

    private static bool TryExpandNumericRange(
        string content,
        out List<string> alternatives)
    {
        alternatives = new List<string>();
        var parts = SplitRange(content);
        if (parts.Count is < 2 or > 3 ||
            !int.TryParse(
                parts[0],
                NumberStyles.AllowLeadingSign,
                CultureInfo.InvariantCulture,
                out var start) ||
            !int.TryParse(
                parts[1],
                NumberStyles.AllowLeadingSign,
                CultureInfo.InvariantCulture,
                out var end))
        {
            return false;
        }

        var step = 1;
        if (parts.Count == 3 &&
            (!int.TryParse(
                 parts[2],
                 NumberStyles.AllowLeadingSign,
                 CultureInfo.InvariantCulture,
                 out step) ||
             step == 0))
        {
            return false;
        }

        step = Math.Abs(step);
        if (start > end)
        {
            step = -step;
        }

        var width = GetNumericWidth(
            parts[0],
            parts[1]);
        var current = start;
        while ((step > 0 && current <= end) ||
               (step < 0 && current >= end))
        {
            alternatives.Add(
                FormatRangeValue(
                    current,
                    width));
            if (current == end)
            {
                break;
            }

            var next = (long)current + step;
            if ((step > 0 && next > end) ||
                (step < 0 && next < end))
            {
                break;
            }

            if (next > int.MaxValue || next < int.MinValue)
            {
                return false;
            }

            current = (int)next;
        }

        return alternatives.Count > 0;
    }

    private static List<string> SplitRange(string content)
    {
        var result = new List<string>();
        var start = 0;
        var index = 0;
        while (index + 1 < content.Length)
        {
            if (content[index] == '.' &&
                content[index + 1] == '.')
            {
                result.Add(
                    content.Substring(
                        start,
                        index - start));
                index += 2;
                start = index;
                continue;
            }

            index++;
        }

        result.Add(content.Substring(start));
        return result;
    }

    private static List<string> SplitTopLevel(
        string value,
        char separator)
    {
        var result = new List<string>();
        var depth = 0;
        var start = 0;
        var isEscaped = false;
        for (var index = 0; index < value.Length; index++)
        {
            var character = value[index];
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

            if (character == '{')
            {
                depth++;
            }
            else if (character == '}')
            {
                depth--;
                if (depth < 0)
                {
                    return new List<string>();
                }
            }
            else if (character == separator && depth == 0)
            {
                result.Add(
                    value.Substring(
                        start,
                        index - start));
                start = index + 1;
            }
        }

        if (depth != 0)
        {
            return new List<string>();
        }

        result.Add(value.Substring(start));
        return result;
    }

    private static int FindOpeningBrace(string value)
    {
        var isEscaped = false;
        for (var index = 0; index < value.Length; index++)
        {
            if (isEscaped)
            {
                isEscaped = false;
                continue;
            }

            if (value[index] == '\\')
            {
                isEscaped = true;
            }
            else if (value[index] == '{')
            {
                return index;
            }
        }

        return -1;
    }

    private static int FindMatchingBrace(
        string value,
        int openingBrace)
    {
        var depth = 0;
        var isEscaped = false;
        for (var index = openingBrace; index < value.Length; index++)
        {
            if (isEscaped)
            {
                isEscaped = false;
                continue;
            }

            if (value[index] == '\\')
            {
                isEscaped = true;
            }
            else if (value[index] == '{')
            {
                depth++;
            }
            else if (value[index] == '}')
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

    private static bool ContainsUnescapedBrace(string value)
    {
        var isEscaped = false;
        foreach (var character in value)
        {
            if (isEscaped)
            {
                isEscaped = false;
                continue;
            }

            if (character == '\\')
            {
                isEscaped = true;
            }
            else if (character is '{' or '}')
            {
                return true;
            }
        }

        return false;
    }

    private static string UnescapeBraces(string value) =>
        value.Replace("\\{", "{").Replace("\\}", "}");

    private static int GetNumericWidth(
        string start,
        string end)
    {
        var startDigits = start.TrimStart('+', '-');
        var endDigits = end.TrimStart('+', '-');
        if ((startDigits.Length > 1 && startDigits[0] == '0') ||
            (endDigits.Length > 1 && endDigits[0] == '0'))
        {
            return Math.Max(
                startDigits.Length,
                endDigits.Length);
        }

        return 0;
    }

    private static string FormatRangeValue(
        int value,
        int width)
    {
        if (width == 0)
        {
            return value.ToString(CultureInfo.InvariantCulture);
        }

        var magnitude = Math.Abs((long)value).ToString(
            "D" + width.ToString(CultureInfo.InvariantCulture),
            CultureInfo.InvariantCulture);
        return value < 0
            ? "-" + magnitude
            : magnitude;
    }

    private static bool TryFindStatementEnd(
        string text,
        int start,
        out int statementEnd)
    {
        var parenthesisDepth = 0;
        var bracketDepth = 0;
        var index = start;
        while (index < text.Length)
        {
            if (TrySkipCommentOrString(text, ref index))
            {
                continue;
            }

            var character = text[index];
            if (character == '(')
            {
                parenthesisDepth++;
            }
            else if (character == ')' && parenthesisDepth > 0)
            {
                parenthesisDepth--;
            }
            else if (character == '[')
            {
                bracketDepth++;
            }
            else if (character == ']' && bracketDepth > 0)
            {
                bracketDepth--;
            }
            else if (character == ';' &&
                     parenthesisDepth == 0 &&
                     bracketDepth == 0)
            {
                statementEnd = index;
                return true;
            }
            else if (character == '{' &&
                     parenthesisDepth == 0 &&
                     bracketDepth == 0)
            {
                statementEnd = -1;
                return false;
            }

            index++;
        }

        statementEnd = -1;
        return false;
    }

    private static bool TrySkipCommentOrString(
        string text,
        ref int index)
    {
        if (index + 1 < text.Length &&
            text[index] == '/' &&
            text[index + 1] == '*')
        {
            index += 2;
            while (index + 1 < text.Length &&
                   !(text[index] == '*' && text[index + 1] == '/'))
            {
                index++;
            }

            index = Math.Min(
                text.Length,
                index + 2);
            return true;
        }

        if (text[index] != '"' && text[index] != '\'')
        {
            return false;
        }

        var quote = text[index++];
        while (index < text.Length)
        {
            if (text[index] == '\\' &&
                index + 1 < text.Length)
            {
                index += 2;
                continue;
            }

            if (text[index++] == quote)
            {
                break;
            }
        }

        return true;
    }

    private static bool TryReadQuoted(
        string value,
        ref int index,
        out string decoded)
    {
        decoded = string.Empty;
        if (index >= value.Length ||
            (value[index] != '"' && value[index] != '\''))
        {
            return false;
        }

        var quote = value[index++];
        var result = new StringBuilder();
        while (index < value.Length)
        {
            var character = value[index++];
            if (character == quote)
            {
                decoded = result.ToString();
                return true;
            }

            if (character == '\\' && index < value.Length)
            {
                result.Append(value[index++]);
            }
            else
            {
                result.Append(character);
            }
        }

        return false;
    }

    private static bool TryReadFunctionArgument(
        string value,
        ref int index,
        out string argument)
    {
        argument = string.Empty;
        if (index >= value.Length ||
            value[index] != '(')
        {
            return false;
        }

        var contentStart = ++index;
        var depth = 1;
        while (index < value.Length)
        {
            if (value[index] == '"' || value[index] == '\'')
            {
                var quotedIndex = index;
                if (!TryReadQuoted(
                        value,
                        ref quotedIndex,
                        out _))
                {
                    return false;
                }

                index = quotedIndex;
                continue;
            }

            if (value[index] == '(')
            {
                depth++;
            }
            else if (value[index] == ')')
            {
                depth--;
                if (depth == 0)
                {
                    argument = value.Substring(
                        contentStart,
                        index - contentStart);
                    index++;
                    return true;
                }
            }

            index++;
        }

        return false;
    }

    private static bool TryReadWord(
        string value,
        ref int index,
        string expected)
    {
        if (index + expected.Length > value.Length ||
            !string.Equals(
                value.Substring(
                    index,
                    expected.Length),
                expected,
                StringComparison.Ordinal) ||
            (index + expected.Length < value.Length &&
             IsNameCharacter(value[index + expected.Length])))
        {
            return false;
        }

        index += expected.Length;
        return true;
    }

    private static void SkipWhitespace(
        string value,
        ref int index)
    {
        while (index < value.Length &&
               char.IsWhiteSpace(value[index]))
        {
            index++;
        }
    }

    private static bool IsNameCharacter(char character) =>
        (character >= 'a' && character <= 'z') ||
        (character >= 'A' && character <= 'Z') ||
        (character >= '0' && character <= '9') ||
        character is '-' or '_';

    private static bool IsValidPrefix(string value)
    {
        if (value.Length == 0)
        {
            return false;
        }

        foreach (var character in value)
        {
            if (character < 'a' || character > 'z')
            {
                return false;
            }
        }

        return true;
    }

    private static bool IsTailwindSpecifier(string value) =>
        value == "tailwindcss" ||
        value.StartsWith(
            "tailwindcss/",
            StringComparison.Ordinal) ||
        value.StartsWith(
            "@tailwindcss/",
            StringComparison.Ordinal);

    private static void AddInvalidImportModifier(
        int statementStart,
        int statementEnd,
        UtilitySourceParseOptions options,
        ICollection<UtilitySourceDiagnostic> diagnostics) =>
        diagnostics.Add(
            CreateDiagnostic(
                options,
                UtilitySourceDiagnosticCode.InvalidImportModifier,
                "The \"viu-utilities\" import accepts source(none), source(\"path\"), prefix(lowercase), theme(static), theme(inline), and important once each.",
                statementStart,
                statementEnd - statementStart));

    private static UtilitySourceDiagnostic CreateDiagnostic(
        UtilitySourceParseOptions options,
        UtilitySourceDiagnosticCode code,
        string message,
        int start,
        int length) =>
        new(
            code,
            UtilitySourceDiagnosticSeverity.Error,
            message,
            CreateSpan(
                options,
                start,
                length));

    private static UtilityStylesheetSourceSpan CreateSpan(
        UtilitySourceParseOptions options,
        int start,
        int length) =>
        new(
            options.SourceIdentity,
            options.ContentOffset + start,
            length);

    private static void AddDistinct(
        string value,
        ICollection<string> values,
        ISet<string> seen)
    {
        if (seen.Add(value))
        {
            values.Add(value);
        }
    }

    private static void AddDistinct(
        IEnumerable<string> source,
        ICollection<string> values,
        ISet<string> seen)
    {
        foreach (var value in source)
        {
            AddDistinct(
                value,
                values,
                seen);
        }
    }

    private sealed class ImportState
    {
        public bool HasUtilitiesImport { get; set; }

        public bool IsAutomaticDetectionEnabled { get; set; } = true;

        public string? BasePath { get; set; }

        public string? Prefix { get; set; }

        public bool IsImportant { get; set; }

        public UtilityThemeOptions ImportedThemeOptions { get; set; }
    }

    private sealed record ParsedSourceDirective(
        UtilitySourceDirectiveKind Kind,
        string Value,
        UtilityStylesheetSourceSpan SourceSpan);

    private enum ExpansionError
    {
        None,
        Syntax,
        Limit,
    }
}
