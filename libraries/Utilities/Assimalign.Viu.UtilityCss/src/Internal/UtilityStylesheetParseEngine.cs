using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading;

namespace Assimalign.Viu.UtilityCss;

internal static class UtilityStylesheetParseEngine
{
    private const string UtilityDirectiveName = "utility";
    private const string CustomVariantDirectiveName = "custom-variant";
    private const string VariantDirectiveName = "variant";
    private const string ApplyDirectiveName = "apply";
    private const string ReferenceDirectiveName = "reference";
    private const string PluginDirectiveName = "plugin";
    private const string ConfigurationDirectiveName = "config";

    private static readonly string[] BareValueTypes =
    {
        "number",
        "integer",
        "ratio",
        "percentage",
    };

    private static readonly string[] ArbitraryValueTypes =
    {
        "*",
        "absolute-size",
        "angle",
        "bg-size",
        "color",
        "family-name",
        "generic-name",
        "image",
        "integer",
        "length",
        "line-width",
        "number",
        "percentage",
        "position",
        "ratio",
        "relative-size",
        "url",
        "vector",
    };

    public static UtilityStylesheetParseResult Parse(
        string? css,
        UtilityStylesheetParseOptions options,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (options.ContentOffset < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(options),
                options.ContentOffset,
                "The project stylesheet content offset cannot be negative.");
        }

        if (string.IsNullOrEmpty(css))
        {
            return new UtilityStylesheetParseResult(
                string.Empty,
                UtilityCollection<UtilityDirective>.Empty,
                UtilityCollection<UtilityCssFunction>.Empty,
                UtilityCollection<UtilityDirectiveDiagnostic>.Empty);
        }

        var text = css!;
        var directives = new List<UtilityDirective>();
        var functions = new List<UtilityCssFunction>();
        var diagnostics = new List<UtilityDirectiveDiagnostic>();

        ScanDirectives(
            text,
            options,
            directives,
            diagnostics,
            cancellationToken);
        ScanFunctions(
            text,
            options,
            functions,
            diagnostics,
            cancellationToken);
        ValidateFunctionPlacement(
            directives,
            functions,
            options,
            diagnostics,
            cancellationToken);
        ValidateFunctionalUtilities(
            directives,
            functions,
            options,
            diagnostics,
            cancellationToken);

        directives.Sort(
            static (left, right) => left.SourceSpan.Start.CompareTo(right.SourceSpan.Start));
        functions.Sort(
            static (left, right) => left.SourceSpan.Start.CompareTo(right.SourceSpan.Start));
        diagnostics.Sort(CompareDiagnostics);

        var immutableDirectives = directives.Count == 0
            ? UtilityCollection<UtilityDirective>.Empty
            : new UtilityCollection<UtilityDirective>(directives);
        var immutableFunctions = functions.Count == 0
            ? UtilityCollection<UtilityCssFunction>.Empty
            : new UtilityCollection<UtilityCssFunction>(functions);
        var immutableDiagnostics = diagnostics.Count == 0
            ? UtilityCollection<UtilityDirectiveDiagnostic>.Empty
            : new UtilityCollection<UtilityDirectiveDiagnostic>(diagnostics);

        return new UtilityStylesheetParseResult(
            UtilityDirectiveEmitter.Emit(immutableDirectives),
            immutableDirectives,
            immutableFunctions,
            immutableDiagnostics);
    }

    private static void ScanDirectives(
        string text,
        UtilityStylesheetParseOptions options,
        ICollection<UtilityDirective> directives,
        ICollection<UtilityDirectiveDiagnostic> diagnostics,
        CancellationToken cancellationToken)
    {
        var index = 0;
        var nestingDepth = 0;
        while (index < text.Length)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (IsCommentStart(text, index))
            {
                var commentEnd = FindCommentEnd(
                    text,
                    index + 2,
                    cancellationToken);
                if (commentEnd < 0)
                {
                    diagnostics.Add(
                        CreateDiagnostic(
                            options,
                            UtilityDirectiveDiagnosticCode.UnterminatedComment,
                            "The CSS comment is not terminated.",
                            index,
                            text.Length - index));
                    break;
                }

                index = commentEnd;
                continue;
            }

            if (text[index] is '\'' or '"')
            {
                var stringEnd = FindStringEnd(
                    text,
                    index + 1,
                    text[index],
                    cancellationToken);
                if (stringEnd < 0)
                {
                    diagnostics.Add(
                        CreateDiagnostic(
                            options,
                            UtilityDirectiveDiagnosticCode.UnterminatedString,
                            "The CSS string is not terminated.",
                            index,
                            text.Length - index));
                    break;
                }

                index = stringEnd + 1;
                continue;
            }

            switch (text[index])
            {
                case '{':
                    nestingDepth++;
                    index++;
                    continue;
                case '}':
                    nestingDepth = Math.Max(
                        0,
                        nestingDepth - 1);
                    index++;
                    continue;
                case '@':
                    if (TryReadAtRuleName(
                            text,
                            index,
                            out var name,
                            out var nameEnd))
                    {
                        if (TryGetDirectiveKind(
                                name,
                                out var kind))
                        {
                            directives.Add(
                                ParseDirective(
                                    text,
                                    index,
                                    nameEnd,
                                    nestingDepth,
                                    kind,
                                    options,
                                    diagnostics,
                                    cancellationToken));
                        }
                        else if (name is PluginDirectiveName or ConfigurationDirectiveName)
                        {
                            var extentEnd = FindAtRuleExtentEnd(
                                text,
                                nameEnd,
                                cancellationToken);
                            diagnostics.Add(
                                CreateDiagnostic(
                                    options,
                                    UtilityDirectiveDiagnosticCode.UnsupportedExecutableDirective,
                                    name == PluginDirectiveName
                                        ? "The @plugin directive is not supported because Viu Utilities never executes JavaScript plugins."
                                        : "The legacy @config directive is not supported because Viu Utilities never loads JavaScript configuration.",
                                    index,
                                    Math.Max(
                                        1,
                                        extentEnd - index)));
                        }
                        else if (name == "import" &&
                                 TryReadStatementParameters(
                                     text,
                                     nameEnd,
                                     out var importParameters,
                                     out var importEnd,
                                     cancellationToken) &&
                                 IsTailwindSpecifier(importParameters))
                        {
                            diagnostics.Add(
                                CreateDiagnostic(
                                    options,
                                    UtilityDirectiveDiagnosticCode.UnsupportedFrameworkDependency,
                                    "Importing Tailwind CSS is not supported; Viu Utilities is a standalone implementation.",
                                    index,
                                    importEnd - index));
                        }
                    }

                    index++;
                    continue;
                default:
                    index++;
                    continue;
            }
        }
    }

    private static UtilityDirective ParseDirective(
        string text,
        int directiveStart,
        int nameEnd,
        int nestingDepth,
        UtilityDirectiveKind kind,
        UtilityStylesheetParseOptions options,
        ICollection<UtilityDirectiveDiagnostic> diagnostics,
        CancellationToken cancellationToken)
    {
        var header = ReadDirectiveHeader(
            text,
            nameEnd,
            options,
            diagnostics,
            cancellationToken);
        var parameterStart = nameEnd;
        var parameterEnd = header.TerminatorIndex < 0
            ? header.ContentEnd
            : header.TerminatorIndex;
        TrimWhitespace(
            text,
            ref parameterStart,
            ref parameterEnd);
        var parameters = text.Substring(
            parameterStart,
            parameterEnd - parameterStart);
        var hasBlock = header.Terminator == '{';
        var hasSemicolon = header.Terminator == ';';
        var body = default(string);
        var bodyStart = -1;
        var bodyEnd = -1;
        var sourceEnd = header.ContentEnd;
        var isValid = header.IsValid;

        if (hasBlock)
        {
            bodyStart = header.TerminatorIndex + 1;
            var closingBrace = FindMatchingBrace(
                text,
                header.TerminatorIndex,
                cancellationToken);
            if (closingBrace < 0)
            {
                bodyEnd = text.Length;
                sourceEnd = text.Length;
                isValid = false;
                diagnostics.Add(
                    CreateDiagnostic(
                        options,
                        UtilityDirectiveDiagnosticCode.UnterminatedBlock,
                        $"The @{GetDirectiveName(kind)} block is not terminated.",
                        header.TerminatorIndex,
                        text.Length - header.TerminatorIndex));
            }
            else
            {
                bodyEnd = closingBrace;
                sourceEnd = closingBrace + 1;
            }

            body = text.Substring(
                bodyStart,
                bodyEnd - bodyStart);
        }
        else if (hasSemicolon)
        {
            sourceEnd = header.TerminatorIndex + 1;
        }
        else
        {
            isValid = false;
            diagnostics.Add(
                CreateDiagnostic(
                    options,
                    UtilityDirectiveDiagnosticCode.MissingSemicolon,
                    $"The @{GetDirectiveName(kind)} statement is missing its terminating semicolon.",
                    directiveStart,
                    Math.Max(
                        1,
                        header.ContentEnd - directiveStart)));
        }

        string? identifier = null;
        ValidateDirective(
            text,
            kind,
            parameters,
            parameterStart,
            parameterEnd,
            body,
            hasBlock,
            nestingDepth,
            options,
            diagnostics,
            ref identifier,
            ref isValid,
            cancellationToken);

        return new UtilityDirective(
            kind,
            parameters,
            identifier,
            body,
            nestingDepth,
            CreateSourceSpan(
                options,
                directiveStart,
                Math.Max(
                    1,
                    sourceEnd - directiveStart)),
            CreateSourceSpan(
                options,
                parameterStart,
                parameterEnd - parameterStart),
            hasBlock
                ? CreateSourceSpan(
                    options,
                    bodyStart,
                    bodyEnd - bodyStart)
                : null,
            isValid);
    }

    private static void ValidateDirective(
        string text,
        UtilityDirectiveKind kind,
        string parameters,
        int parameterStart,
        int parameterEnd,
        string? body,
        bool hasBlock,
        int nestingDepth,
        UtilityStylesheetParseOptions options,
        ICollection<UtilityDirectiveDiagnostic> diagnostics,
        ref string? identifier,
        ref bool isValid,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (parameters.Length == 0)
        {
            isValid = false;
            diagnostics.Add(
                CreateDiagnostic(
                    options,
                    UtilityDirectiveDiagnosticCode.MissingParameters,
                    $"The @{GetDirectiveName(kind)} directive requires parameters.",
                    parameterStart,
                    Math.Max(
                        1,
                        parameterEnd - parameterStart)));
        }

        switch (kind)
        {
            case UtilityDirectiveKind.Utility:
                identifier = DecodeCssIdentifier(parameters);
                ValidateBlockExpectation(
                    kind,
                    hasBlock,
                    true,
                    parameterStart,
                    parameterEnd,
                    options,
                    diagnostics,
                    ref isValid);
                ValidateTopLevelPlacement(
                    kind,
                    nestingDepth,
                    parameterStart,
                    parameterEnd,
                    options,
                    diagnostics,
                    ref isValid);
                if (parameters.Length != 0 &&
                    !IsValidUtilityName(identifier))
                {
                    isValid = false;
                    diagnostics.Add(
                        CreateDiagnostic(
                            options,
                            UtilityDirectiveDiagnosticCode.InvalidUtilityName,
                        "A custom utility name must use the v4.3.3 static or final '-*' functional name grammar.",
                            parameterStart,
                            parameterEnd - parameterStart));
                }

                break;

            case UtilityDirectiveKind.CustomVariant:
                ParseCustomVariantIdentifier(
                    parameters,
                    out identifier,
                    out var selector);
                ValidateTopLevelPlacement(
                    kind,
                    nestingDepth,
                    parameterStart,
                    parameterEnd,
                    options,
                    diagnostics,
                    ref isValid);
                if (identifier is null ||
                    !IsValidVariantName(identifier))
                {
                    isValid = false;
                    diagnostics.Add(
                        CreateDiagnostic(
                            options,
                            UtilityDirectiveDiagnosticCode.InvalidVariantName,
                            "A custom variant name must start with a lowercase ASCII letter and contain lowercase letters, digits, or hyphens only.",
                            parameterStart,
                            Math.Max(
                                1,
                                identifier?.Length ?? parameters.Length)));
                }

                if (hasBlock)
                {
                    if (!ContainsAtRule(
                            body ?? string.Empty,
                            "slot",
                            cancellationToken))
                    {
                        isValid = false;
                        diagnostics.Add(
                            CreateDiagnostic(
                                options,
                                UtilityDirectiveDiagnosticCode.MissingVariantSlot,
                                "A block-form @custom-variant directive must contain an @slot placeholder.",
                                parameterStart,
                                Math.Max(
                                    1,
                                    parameterEnd - parameterStart)));
                    }
                }
                else if (string.IsNullOrWhiteSpace(selector) ||
                         !IsParenthesized(selector))
                {
                    isValid = false;
                    diagnostics.Add(
                        CreateDiagnostic(
                            options,
                            UtilityDirectiveDiagnosticCode.MissingBlock,
                            "A statement-form @custom-variant directive requires a parenthesized selector, otherwise use a block containing @slot.",
                            parameterStart,
                            Math.Max(
                                1,
                                parameterEnd - parameterStart)));
                }

                break;

            case UtilityDirectiveKind.Variant:
                ValidateBlockExpectation(
                    kind,
                    hasBlock,
                    true,
                    parameterStart,
                    parameterEnd,
                    options,
                    diagnostics,
                    ref isValid);
                break;

            case UtilityDirectiveKind.Apply:
                ValidateBlockExpectation(
                    kind,
                    hasBlock,
                    false,
                    parameterStart,
                    parameterEnd,
                    options,
                    diagnostics,
                    ref isValid);
                break;

            case UtilityDirectiveKind.Reference:
                ValidateBlockExpectation(
                    kind,
                    hasBlock,
                    false,
                    parameterStart,
                    parameterEnd,
                    options,
                    diagnostics,
                    ref isValid);
                ValidateTopLevelPlacement(
                    kind,
                    nestingDepth,
                    parameterStart,
                    parameterEnd,
                    options,
                    diagnostics,
                    ref isValid);
                if (!TryDecodeQuotedSpecifier(
                        parameters,
                        out identifier))
                {
                    isValid = false;
                    diagnostics.Add(
                        CreateDiagnostic(
                            options,
                            UtilityDirectiveDiagnosticCode.InvalidReference,
                            "The @reference directive requires exactly one quoted stylesheet specifier.",
                            parameterStart,
                            Math.Max(
                                1,
                                parameterEnd - parameterStart)));
                }
                else if (IsTailwindSpecifier(identifier))
                {
                    isValid = false;
                    diagnostics.Add(
                        CreateDiagnostic(
                            options,
                            UtilityDirectiveDiagnosticCode.UnsupportedFrameworkDependency,
                            "Referencing Tailwind CSS is not supported; reference a Viu project stylesheet instead.",
                            parameterStart,
                            parameterEnd - parameterStart));
                }

                break;

            default:
                throw new ArgumentOutOfRangeException(
                    nameof(kind),
                    kind,
                    "Unknown utility directive kind.");
        }
    }

    private static void ScanFunctions(
        string text,
        UtilityStylesheetParseOptions options,
        ICollection<UtilityCssFunction> functions,
        ICollection<UtilityDirectiveDiagnostic> diagnostics,
        CancellationToken cancellationToken)
    {
        var index = 0;
        while (index < text.Length)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (IsCommentStart(text, index))
            {
                var commentEnd = FindCommentEnd(
                    text,
                    index + 2,
                    cancellationToken);
                index = commentEnd < 0
                    ? text.Length
                    : commentEnd;
                continue;
            }

            if (text[index] is '\'' or '"')
            {
                var stringEnd = FindStringEnd(
                    text,
                    index + 1,
                    text[index],
                    cancellationToken);
                index = stringEnd < 0
                    ? text.Length
                    : stringEnd + 1;
                continue;
            }

            if (TryReadFunctionKind(
                    text,
                    index,
                    out var kind,
                    out var openParenthesis))
            {
                functions.Add(
                    ParseFunction(
                        text,
                        index,
                        openParenthesis,
                        kind,
                        options,
                        diagnostics,
                        cancellationToken));
            }

            index++;
        }
    }

    private static UtilityCssFunction ParseFunction(
        string text,
        int functionStart,
        int openParenthesis,
        UtilityCssFunctionKind kind,
        UtilityStylesheetParseOptions options,
        ICollection<UtilityDirectiveDiagnostic> diagnostics,
        CancellationToken cancellationToken)
    {
        var closeParenthesis = FindMatchingParenthesis(
            text,
            openParenthesis,
            cancellationToken,
            out var unbalancedStart);
        var isValid = closeParenthesis >= 0 && unbalancedStart < 0;
        var functionEnd = closeParenthesis < 0
            ? text.Length
            : closeParenthesis + 1;
        if (closeParenthesis < 0)
        {
            diagnostics.Add(
                CreateDiagnostic(
                    options,
                    UtilityDirectiveDiagnosticCode.UnterminatedFunction,
                    $"The {GetFunctionName(kind)} function call is not terminated.",
                    functionStart,
                    text.Length - functionStart));
        }

        if (unbalancedStart >= 0)
        {
            diagnostics.Add(
                CreateDiagnostic(
                    options,
                    UtilityDirectiveDiagnosticCode.UnbalancedDelimiter,
                    $"The {GetFunctionName(kind)} function contains an unbalanced delimiter.",
                    unbalancedStart,
                    1));
        }

        var argumentsEnd = closeParenthesis < 0
            ? text.Length
            : closeParenthesis;
        var arguments = ParseFunctionArguments(
            text,
            openParenthesis + 1,
            argumentsEnd,
            kind,
            options,
            diagnostics,
            ref isValid,
            cancellationToken);

        return new UtilityCssFunction(
            kind,
            text.Substring(
                functionStart,
                functionEnd - functionStart),
            arguments,
            CreateSourceSpan(
                options,
                functionStart,
                functionEnd - functionStart),
            isValid);
    }

    private static UtilityCollection<UtilityCssFunctionArgument> ParseFunctionArguments(
        string text,
        int start,
        int end,
        UtilityCssFunctionKind kind,
        UtilityStylesheetParseOptions options,
        ICollection<UtilityDirectiveDiagnostic> diagnostics,
        ref bool isValid,
        CancellationToken cancellationToken)
    {
        var segments = SplitFunctionArguments(
            text,
            start,
            end,
            cancellationToken);
        var arguments = new List<UtilityCssFunctionArgument>();
        foreach (var segment in segments)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var argumentStart = segment.Start;
            var argumentEnd = segment.End;
            TrimWhitespace(
                text,
                ref argumentStart,
                ref argumentEnd);
            var argumentText = text.Substring(
                argumentStart,
                argumentEnd - argumentStart);
            var argumentKind = ClassifyFunctionArgument(
                kind,
                argumentText);
            var argumentValid = IsValidFunctionArgument(
                kind,
                argumentText,
                argumentKind);
            if (!argumentValid)
            {
                isValid = false;
                diagnostics.Add(
                    CreateDiagnostic(
                        options,
                        UtilityDirectiveDiagnosticCode.InvalidFunctionArguments,
                        $"The argument '{argumentText}' is not supported by {GetFunctionName(kind)}.",
                        argumentStart,
                        Math.Max(
                            1,
                            argumentEnd - argumentStart)));
            }

            arguments.Add(
                new UtilityCssFunctionArgument(
                    argumentText,
                    argumentKind,
                    CreateSourceSpan(
                        options,
                        argumentStart,
                        argumentEnd - argumentStart)));
        }

        if (!ValidateFunctionArgumentSet(
                text,
                start,
                end,
                kind,
                arguments,
                options,
                diagnostics))
        {
            isValid = false;
        }

        return arguments.Count == 0
            ? UtilityCollection<UtilityCssFunctionArgument>.Empty
            : new UtilityCollection<UtilityCssFunctionArgument>(arguments);
    }

    private static bool ValidateFunctionArgumentSet(
        string text,
        int start,
        int end,
        UtilityCssFunctionKind kind,
        IReadOnlyCollection<UtilityCssFunctionArgument> arguments,
        UtilityStylesheetParseOptions options,
        ICollection<UtilityDirectiveDiagnostic> diagnostics)
    {
        var validCount = kind switch
        {
            UtilityCssFunctionKind.Value => arguments.Count > 0,
            UtilityCssFunctionKind.Modifier => arguments.Count > 0,
            UtilityCssFunctionKind.Default => arguments.Count == 1,
            UtilityCssFunctionKind.Spacing => arguments.Count == 1,
            UtilityCssFunctionKind.Alpha => arguments.Count == 1,
            _ => false,
        };
        var validAlpha = kind != UtilityCssFunctionKind.Alpha ||
                         HasTopLevelAlphaSeparator(
                             text,
                             start,
                             end);
        if (validCount && validAlpha)
        {
            return true;
        }

        var message = kind switch
        {
            UtilityCssFunctionKind.Value => "--value() requires at least one documented resolution mode.",
            UtilityCssFunctionKind.Modifier => "--modifier() requires at least one documented resolution mode.",
            UtilityCssFunctionKind.Default => "--default() requires exactly one fallback expression.",
            UtilityCssFunctionKind.Spacing => "--spacing() requires exactly one balanced CSS expression.",
            UtilityCssFunctionKind.Alpha => "--alpha() requires one color/opacity expression separated by a top-level slash.",
            _ => "The CSS function arguments are invalid.",
        };
        diagnostics.Add(
            CreateDiagnostic(
                options,
                UtilityDirectiveDiagnosticCode.InvalidFunctionArguments,
                message,
                start,
                Math.Max(
                    1,
                    end - start)));
        return false;
    }

    private static void ValidateFunctionPlacement(
        IReadOnlyList<UtilityDirective> directives,
        IList<UtilityCssFunction> functions,
        UtilityStylesheetParseOptions options,
        ICollection<UtilityDirectiveDiagnostic> diagnostics,
        CancellationToken cancellationToken)
    {
        for (var index = 0; index < functions.Count; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var function = functions[index];
            var validPlacement = function.Kind switch
            {
                UtilityCssFunctionKind.Value => IsInsideUtilityDirective(
                    function,
                    directives),
                UtilityCssFunctionKind.Modifier => IsInsideUtilityDirective(
                    function,
                    directives),
                UtilityCssFunctionKind.Default => IsInsideValueOrModifierFunction(
                    function,
                    functions),
                UtilityCssFunctionKind.Spacing => true,
                UtilityCssFunctionKind.Alpha => true,
                _ => false,
            };
            if (validPlacement)
            {
                continue;
            }

            diagnostics.Add(
                CreateDiagnostic(
                    options,
                    UtilityDirectiveDiagnosticCode.InvalidFunctionPlacement,
                    function.Kind == UtilityCssFunctionKind.Default
                        ? "--default() is supported only as an argument of --value() or --modifier()."
                        : $"{GetFunctionName(function.Kind)} is supported only inside an @utility declaration block.",
                    function.SourceSpan.Start - options.ContentOffset,
                    function.SourceSpan.Length));
            functions[index] = function with
            {
                IsValid = false,
            };
        }
    }

    private static void ValidateFunctionalUtilities(
        IList<UtilityDirective> directives,
        IReadOnlyList<UtilityCssFunction> functions,
        UtilityStylesheetParseOptions options,
        ICollection<UtilityDirectiveDiagnostic> diagnostics,
        CancellationToken cancellationToken)
    {
        for (var index = 0; index < directives.Count; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var directive = directives[index];
            if (directive.Kind != UtilityDirectiveKind.Utility ||
                directive.Identifier is null ||
                !directive.Identifier.EndsWith(
                    "-*",
                    StringComparison.Ordinal) ||
                directive.BodySourceSpan is null)
            {
                continue;
            }

            var hasValue = functions.Any(
                function =>
                    function.Kind == UtilityCssFunctionKind.Value &&
                    Contains(
                        directive.BodySourceSpan,
                        function.SourceSpan));
            if (hasValue)
            {
                continue;
            }

            diagnostics.Add(
                CreateDiagnostic(
                    options,
                    UtilityDirectiveDiagnosticCode.MissingFunctionalValue,
                    "A functional @utility definition must contain at least one --value() call.",
                    directive.ParametersSourceSpan.Start - options.ContentOffset,
                    Math.Max(
                        1,
                        directive.ParametersSourceSpan.Length)));
            directives[index] = directive with
            {
                IsValid = false,
            };
        }
    }

    private static DirectiveHeader ReadDirectiveHeader(
        string text,
        int start,
        UtilityStylesheetParseOptions options,
        ICollection<UtilityDirectiveDiagnostic> diagnostics,
        CancellationToken cancellationToken)
    {
        var closingDelimiters = new List<char>();
        var index = start;
        var isValid = true;
        while (index < text.Length)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (IsCommentStart(text, index))
            {
                var commentEnd = FindCommentEnd(
                    text,
                    index + 2,
                    cancellationToken);
                if (commentEnd < 0)
                {
                    return new DirectiveHeader(
                        -1,
                        '\0',
                        text.Length,
                        false);
                }

                index = commentEnd;
                continue;
            }

            var character = text[index];
            if (character is '\'' or '"')
            {
                var stringEnd = FindStringEnd(
                    text,
                    index + 1,
                    character,
                    cancellationToken);
                if (stringEnd < 0)
                {
                    return new DirectiveHeader(
                        -1,
                        '\0',
                        text.Length,
                        false);
                }

                index = stringEnd + 1;
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
                if (closingDelimiters.Count == 0 ||
                    closingDelimiters[closingDelimiters.Count - 1] != character)
                {
                    isValid = false;
                    diagnostics.Add(
                        CreateDiagnostic(
                            options,
                            UtilityDirectiveDiagnosticCode.UnbalancedDelimiter,
                            "The directive parameters contain an unmatched closing delimiter.",
                            index,
                            1));
                    if (closingDelimiters.Count != 0)
                    {
                        closingDelimiters.RemoveAt(closingDelimiters.Count - 1);
                    }
                }
                else
                {
                    closingDelimiters.RemoveAt(closingDelimiters.Count - 1);
                }

                index++;
                continue;
            }

            if (closingDelimiters.Count == 0)
            {
                if (character is ';' or '{')
                {
                    return new DirectiveHeader(
                        index,
                        character,
                        index,
                        isValid);
                }

                if (character == '}')
                {
                    break;
                }
            }

            index++;
        }

        if (closingDelimiters.Count != 0)
        {
            isValid = false;
            diagnostics.Add(
                CreateDiagnostic(
                    options,
                    UtilityDirectiveDiagnosticCode.UnbalancedDelimiter,
                    "The directive parameters contain an unterminated delimiter.",
                    Math.Max(
                        start,
                        index - 1),
                    1));
        }

        return new DirectiveHeader(
            -1,
            '\0',
            index,
            isValid);
    }

    private static List<TextRange> SplitFunctionArguments(
        string text,
        int start,
        int end,
        CancellationToken cancellationToken)
    {
        var result = new List<TextRange>();
        var closingDelimiters = new List<char>();
        var segmentStart = start;
        var index = start;
        while (index < end)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (IsCommentStart(text, index))
            {
                var commentEnd = FindCommentEnd(
                    text,
                    index + 2,
                    cancellationToken);
                index = commentEnd < 0 || commentEnd > end
                    ? end
                    : commentEnd;
                continue;
            }

            var character = text[index];
            if (character is '\'' or '"')
            {
                var stringEnd = FindStringEnd(
                    text,
                    index + 1,
                    character,
                    cancellationToken);
                index = stringEnd < 0 || stringEnd >= end
                    ? end
                    : stringEnd + 1;
                continue;
            }

            if (character is '(' or '[' or '{')
            {
                closingDelimiters.Add(
                    character switch
                    {
                        '(' => ')',
                        '[' => ']',
                        _ => '}',
                    });
                index++;
                continue;
            }

            if (character is ')' or ']' or '}')
            {
                if (closingDelimiters.Count != 0 &&
                    closingDelimiters[closingDelimiters.Count - 1] == character)
                {
                    closingDelimiters.RemoveAt(closingDelimiters.Count - 1);
                }

                index++;
                continue;
            }

            if (character == ',' &&
                closingDelimiters.Count == 0)
            {
                result.Add(
                    new TextRange(
                        segmentStart,
                        index));
                segmentStart = index + 1;
            }

            index++;
        }

        if (segmentStart < end ||
            result.Count != 0)
        {
            result.Add(
                new TextRange(
                    segmentStart,
                    end));
        }

        return result;
    }

    private static UtilityCssFunctionArgumentKind ClassifyFunctionArgument(
        UtilityCssFunctionKind functionKind,
        string argument)
    {
        if (functionKind is UtilityCssFunctionKind.Spacing or UtilityCssFunctionKind.Alpha or
            UtilityCssFunctionKind.Default)
        {
            return UtilityCssFunctionArgumentKind.Expression;
        }

        if (argument.StartsWith(
                "--default(",
                StringComparison.Ordinal) &&
            argument.EndsWith(
                ")",
                StringComparison.Ordinal))
        {
            return UtilityCssFunctionArgumentKind.Default;
        }

        if (IsThemePattern(argument))
        {
            return UtilityCssFunctionArgumentKind.Theme;
        }

        if (Array.IndexOf(
                BareValueTypes,
                argument) >= 0)
        {
            return UtilityCssFunctionArgumentKind.Bare;
        }

        if (IsQuoted(argument))
        {
            return UtilityCssFunctionArgumentKind.Literal;
        }

        if (argument.Length >= 3 &&
            argument[0] == '[' &&
            argument[argument.Length - 1] == ']')
        {
            return UtilityCssFunctionArgumentKind.Arbitrary;
        }

        return UtilityCssFunctionArgumentKind.Expression;
    }

    private static bool IsValidFunctionArgument(
        UtilityCssFunctionKind functionKind,
        string argument,
        UtilityCssFunctionArgumentKind argumentKind)
    {
        if (argument.Length == 0)
        {
            return false;
        }

        if (functionKind is UtilityCssFunctionKind.Spacing or UtilityCssFunctionKind.Alpha or
            UtilityCssFunctionKind.Default)
        {
            return true;
        }

        return argumentKind switch
        {
            UtilityCssFunctionArgumentKind.Theme => true,
            UtilityCssFunctionArgumentKind.Bare => true,
            UtilityCssFunctionArgumentKind.Literal => true,
            UtilityCssFunctionArgumentKind.Arbitrary => Array.IndexOf(
                ArbitraryValueTypes,
                argument.Substring(
                    1,
                    argument.Length - 2)) >= 0,
            UtilityCssFunctionArgumentKind.Default => true,
            _ => false,
        };
    }

    private static bool IsInsideUtilityDirective(
        UtilityCssFunction function,
        IReadOnlyList<UtilityDirective> directives) =>
        directives.Any(
            directive =>
                directive.Kind == UtilityDirectiveKind.Utility &&
                directive.BodySourceSpan is not null &&
                Contains(
                    directive.BodySourceSpan,
                    function.SourceSpan));

    private static bool IsInsideValueOrModifierFunction(
        UtilityCssFunction function,
        IEnumerable<UtilityCssFunction> functions) =>
        functions.Any(
            parent =>
                parent.Kind is UtilityCssFunctionKind.Value or UtilityCssFunctionKind.Modifier &&
                parent.SourceSpan != function.SourceSpan &&
                Contains(
                    parent.SourceSpan,
                    function.SourceSpan));

    private static bool Contains(
        UtilityStylesheetSourceSpan container,
        UtilityStylesheetSourceSpan value) =>
        value.Start >= container.Start &&
        value.End <= container.End;

    private static bool HasTopLevelAlphaSeparator(
        string text,
        int start,
        int end)
    {
        var closingDelimiters = new List<char>();
        var quote = '\0';
        var isEscaped = false;
        for (var index = start; index < end; index++)
        {
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

            if (character is '(' or '[')
            {
                closingDelimiters.Add(
                    character == '('
                        ? ')'
                        : ']');
                continue;
            }

            if (character is ')' or ']')
            {
                if (closingDelimiters.Count != 0 &&
                    closingDelimiters[closingDelimiters.Count - 1] == character)
                {
                    closingDelimiters.RemoveAt(closingDelimiters.Count - 1);
                }

                continue;
            }

            if (character != '/' ||
                closingDelimiters.Count != 0 ||
                IsCommentStart(text, index))
            {
                continue;
            }

            var left = index - 1;
            while (left >= start &&
                   char.IsWhiteSpace(text[left]))
            {
                left--;
            }

            var right = index + 1;
            while (right < end &&
                   char.IsWhiteSpace(text[right]))
            {
                right++;
            }

            return left >= start &&
                   right < end;
        }

        return false;
    }

    private static int FindMatchingBrace(
        string text,
        int openBrace,
        CancellationToken cancellationToken)
    {
        var depth = 1;
        var index = openBrace + 1;
        while (index < text.Length)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (IsCommentStart(text, index))
            {
                var commentEnd = FindCommentEnd(
                    text,
                    index + 2,
                    cancellationToken);
                if (commentEnd < 0)
                {
                    return -1;
                }

                index = commentEnd;
                continue;
            }

            if (text[index] is '\'' or '"')
            {
                var stringEnd = FindStringEnd(
                    text,
                    index + 1,
                    text[index],
                    cancellationToken);
                if (stringEnd < 0)
                {
                    return -1;
                }

                index = stringEnd + 1;
                continue;
            }

            if (text[index] == '{')
            {
                depth++;
            }
            else if (text[index] == '}')
            {
                depth--;
                if (depth == 0)
                {
                    return index;
                }
            }

            index++;
        }

        return -1;
    }

    private static int FindMatchingParenthesis(
        string text,
        int openParenthesis,
        CancellationToken cancellationToken,
        out int unbalancedStart)
    {
        var closingDelimiters = new List<char>
        {
            ')',
        };
        unbalancedStart = -1;
        var index = openParenthesis + 1;
        while (index < text.Length)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (IsCommentStart(text, index))
            {
                var commentEnd = FindCommentEnd(
                    text,
                    index + 2,
                    cancellationToken);
                if (commentEnd < 0)
                {
                    return -1;
                }

                index = commentEnd;
                continue;
            }

            var character = text[index];
            if (character is '\'' or '"')
            {
                var stringEnd = FindStringEnd(
                    text,
                    index + 1,
                    character,
                    cancellationToken);
                if (stringEnd < 0)
                {
                    return -1;
                }

                index = stringEnd + 1;
                continue;
            }

            if (character is '(' or '[' or '{')
            {
                closingDelimiters.Add(
                    character switch
                    {
                        '(' => ')',
                        '[' => ']',
                        _ => '}',
                    });
                index++;
                continue;
            }

            if (character is ')' or ']' or '}')
            {
                if (closingDelimiters.Count == 0 ||
                    closingDelimiters[closingDelimiters.Count - 1] != character)
                {
                    if (unbalancedStart < 0)
                    {
                        unbalancedStart = index;
                    }

                    while (closingDelimiters.Count > 1 &&
                           closingDelimiters[closingDelimiters.Count - 1] != character)
                    {
                        closingDelimiters.RemoveAt(closingDelimiters.Count - 1);
                    }
                    if (closingDelimiters.Count != 0 &&
                        closingDelimiters[closingDelimiters.Count - 1] == character)
                    {
                        closingDelimiters.RemoveAt(closingDelimiters.Count - 1);
                        if (closingDelimiters.Count == 0)
                        {
                            return index;
                        }
                    }

                    index++;
                    continue;
                }

                closingDelimiters.RemoveAt(closingDelimiters.Count - 1);
                if (closingDelimiters.Count == 0)
                {
                    return index;
                }
            }

            index++;
        }

        return -1;
    }

    private static bool ContainsAtRule(
        string text,
        string expectedName,
        CancellationToken cancellationToken)
    {
        var index = 0;
        while (index < text.Length)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (IsCommentStart(text, index))
            {
                var commentEnd = FindCommentEnd(
                    text,
                    index + 2,
                    cancellationToken);
                index = commentEnd < 0
                    ? text.Length
                    : commentEnd;
                continue;
            }

            if (text[index] is '\'' or '"')
            {
                var stringEnd = FindStringEnd(
                    text,
                    index + 1,
                    text[index],
                    cancellationToken);
                index = stringEnd < 0
                    ? text.Length
                    : stringEnd + 1;
                continue;
            }

            if (text[index] == '@' &&
                TryReadAtRuleName(
                    text,
                    index,
                    out var name,
                    out _) &&
                name == expectedName)
            {
                return true;
            }

            index++;
        }

        return false;
    }

    private static int FindAtRuleExtentEnd(
        string text,
        int nameEnd,
        CancellationToken cancellationToken)
    {
        var header = ReadDirectiveHeader(
            text,
            nameEnd,
            UtilityStylesheetParseOptions.Default,
            new List<UtilityDirectiveDiagnostic>(),
            cancellationToken);
        if (header.Terminator == ';')
        {
            return header.TerminatorIndex + 1;
        }

        if (header.Terminator == '{')
        {
            var closingBrace = FindMatchingBrace(
                text,
                header.TerminatorIndex,
                cancellationToken);
            return closingBrace < 0
                ? text.Length
                : closingBrace + 1;
        }

        return header.ContentEnd;
    }

    private static bool TryReadStatementParameters(
        string text,
        int nameEnd,
        out string parameters,
        out int statementEnd,
        CancellationToken cancellationToken)
    {
        var header = ReadDirectiveHeader(
            text,
            nameEnd,
            UtilityStylesheetParseOptions.Default,
            new List<UtilityDirectiveDiagnostic>(),
            cancellationToken);
        if (header.Terminator != ';')
        {
            parameters = string.Empty;
            statementEnd = header.ContentEnd;
            return false;
        }

        var start = nameEnd;
        var end = header.TerminatorIndex;
        TrimWhitespace(
            text,
            ref start,
            ref end);
        parameters = text.Substring(
            start,
            end - start);
        statementEnd = header.TerminatorIndex + 1;
        return true;
    }

    private static bool TryReadFunctionKind(
        string text,
        int start,
        out UtilityCssFunctionKind kind,
        out int openParenthesis)
    {
        kind = default;
        openParenthesis = -1;
        if (start > 0 &&
            IsCssIdentifierCharacter(text[start - 1]))
        {
            return false;
        }

        return TryMatchFunction(
                   text,
                   start,
                   "--modifier",
                   UtilityCssFunctionKind.Modifier,
                   out kind,
                   out openParenthesis) ||
               TryMatchFunction(
                   text,
                   start,
                   "--spacing",
                   UtilityCssFunctionKind.Spacing,
                   out kind,
                   out openParenthesis) ||
               TryMatchFunction(
                   text,
                   start,
                   "--default",
                   UtilityCssFunctionKind.Default,
                   out kind,
                   out openParenthesis) ||
               TryMatchFunction(
                   text,
                   start,
                   "--value",
                   UtilityCssFunctionKind.Value,
                   out kind,
                   out openParenthesis) ||
               TryMatchFunction(
                   text,
                   start,
                   "--alpha",
                   UtilityCssFunctionKind.Alpha,
                   out kind,
                   out openParenthesis);
    }

    private static bool TryMatchFunction(
        string text,
        int start,
        string name,
        UtilityCssFunctionKind expectedKind,
        out UtilityCssFunctionKind kind,
        out int openParenthesis)
    {
        kind = default;
        openParenthesis = -1;
        if (start + name.Length >= text.Length ||
            string.CompareOrdinal(
                text,
                start,
                name,
                0,
                name.Length) != 0 ||
            text[start + name.Length] != '(')
        {
            return false;
        }

        kind = expectedKind;
        openParenthesis = start + name.Length;
        return true;
    }

    private static bool TryReadAtRuleName(
        string text,
        int start,
        out string name,
        out int nameEnd)
    {
        var index = start + 1;
        while (index < text.Length &&
               (IsAsciiLetter(text[index]) ||
                text[index] == '-'))
        {
            index++;
        }

        if (index == start + 1)
        {
            name = string.Empty;
            nameEnd = index;
            return false;
        }

        name = text.Substring(
            start + 1,
            index - start - 1);
        nameEnd = index;
        return true;
    }

    private static bool TryGetDirectiveKind(
        string name,
        out UtilityDirectiveKind kind)
    {
        switch (name)
        {
            case UtilityDirectiveName:
                kind = UtilityDirectiveKind.Utility;
                return true;
            case CustomVariantDirectiveName:
                kind = UtilityDirectiveKind.CustomVariant;
                return true;
            case VariantDirectiveName:
                kind = UtilityDirectiveKind.Variant;
                return true;
            case ApplyDirectiveName:
                kind = UtilityDirectiveKind.Apply;
                return true;
            case ReferenceDirectiveName:
                kind = UtilityDirectiveKind.Reference;
                return true;
            default:
                kind = default;
                return false;
        }
    }

    private static void ValidateBlockExpectation(
        UtilityDirectiveKind kind,
        bool hasBlock,
        bool expectsBlock,
        int parameterStart,
        int parameterEnd,
        UtilityStylesheetParseOptions options,
        ICollection<UtilityDirectiveDiagnostic> diagnostics,
        ref bool isValid)
    {
        if (hasBlock == expectsBlock)
        {
            return;
        }

        isValid = false;
        diagnostics.Add(
            CreateDiagnostic(
                options,
                expectsBlock
                    ? UtilityDirectiveDiagnosticCode.MissingBlock
                    : UtilityDirectiveDiagnosticCode.UnexpectedBlock,
                expectsBlock
                    ? $"The @{GetDirectiveName(kind)} directive requires a block."
                    : $"The @{GetDirectiveName(kind)} directive uses statement syntax and cannot contain a block.",
                parameterStart,
                Math.Max(
                    1,
                    parameterEnd - parameterStart)));
    }

    private static void ValidateTopLevelPlacement(
        UtilityDirectiveKind kind,
        int nestingDepth,
        int parameterStart,
        int parameterEnd,
        UtilityStylesheetParseOptions options,
        ICollection<UtilityDirectiveDiagnostic> diagnostics,
        ref bool isValid)
    {
        if (nestingDepth == 0)
        {
            return;
        }

        isValid = false;
        diagnostics.Add(
            CreateDiagnostic(
                options,
                UtilityDirectiveDiagnosticCode.InvalidPlacement,
                $"The @{GetDirectiveName(kind)} directive must be declared at the stylesheet root.",
                parameterStart,
                Math.Max(
                    1,
                    parameterEnd - parameterStart)));
    }

    private static bool IsValidUtilityName(string? name)
    {
        if (name is null ||
            name.Length == 0)
        {
            return false;
        }

        return name.EndsWith(
                "-*",
                StringComparison.Ordinal)
            ? IsValidFunctionalUtilityName(
                name.Substring(
                    0,
                    name.Length - 2))
            : IsValidStaticUtilityName(name);
    }

    private static bool IsValidFunctionalUtilityName(string name)
    {
        var rootEnd = FindUtilityRootEnd(name);
        return rootEnd == name.Length;
    }

    private static bool IsValidStaticUtilityName(string name)
    {
        var rootEnd = FindUtilityRootEnd(name);
        if (rootEnd <= 0)
        {
            return false;
        }

        if (rootEnd == name.Length)
        {
            return name[name.Length - 1] != '-';
        }

        var seenSlash = false;
        for (var index = rootEnd; index < name.Length; index++)
        {
            var character = name[index];
            switch (character)
            {
                case '%':
                    if (index != name.Length - 1 ||
                        !IsAsciiDigit(
                            index == 0
                                ? '\0'
                                : name[index - 1]))
                    {
                        return false;
                    }

                    break;

                case '/':
                    if (index == name.Length - 1 ||
                        seenSlash)
                    {
                        return false;
                    }

                    seenSlash = true;
                    break;

                case '.':
                    if (index == 0 ||
                        index == name.Length - 1 ||
                        !IsAsciiDigit(name[index - 1]) ||
                        !IsAsciiDigit(name[index + 1]))
                    {
                        return false;
                    }

                    break;

                case '_':
                case '-':
                    break;

                default:
                    if (!IsAsciiLetter(character) &&
                        !IsAsciiDigit(character))
                    {
                        return false;
                    }

                    break;
            }
        }

        return true;
    }

    private static int FindUtilityRootEnd(string name)
    {
        var index = name.Length != 0 &&
                    name[0] == '-'
            ? 1
            : 0;
        if (index >= name.Length ||
            !IsLowerAsciiLetter(name[index]))
        {
            return -1;
        }

        index++;
        while (index < name.Length)
        {
            var character = name[index];
            if (!IsAsciiLetter(character) &&
                !IsAsciiDigit(character) &&
                character is not ('_' or '-'))
            {
                break;
            }

            index++;
        }

        return index;
    }

    private static bool IsValidVariantName(string name)
    {
        if (name.Length == 0 ||
            !IsLowerAsciiLetter(name[0]))
        {
            return false;
        }

        for (var index = 1; index < name.Length; index++)
        {
            var character = name[index];
            if (!IsLowerAsciiLetter(character) &&
                !char.IsDigit(character) &&
                character != '-')
            {
                return false;
            }
        }

        return true;
    }

    private static void ParseCustomVariantIdentifier(
        string parameters,
        out string? identifier,
        out string selector)
    {
        var index = 0;
        while (index < parameters.Length &&
               !char.IsWhiteSpace(parameters[index]))
        {
            index++;
        }

        identifier = index == 0
            ? null
            : parameters.Substring(
                0,
                index);
        selector = index >= parameters.Length
            ? string.Empty
            : parameters.Substring(index).Trim();
    }

    private static bool IsParenthesized(string value) =>
        value.Length >= 2 &&
        value[0] == '(' &&
        value[value.Length - 1] == ')';

    private static bool TryDecodeQuotedSpecifier(
        string parameters,
        out string? specifier)
    {
        specifier = null;
        if (parameters.Length < 2 ||
            parameters[0] is not ('\'' or '"'))
        {
            return false;
        }

        var quote = parameters[0];
        var builder = new StringBuilder();
        var isEscaped = false;
        for (var index = 1; index < parameters.Length; index++)
        {
            var character = parameters[index];
            if (isEscaped)
            {
                builder.Append(character);
                isEscaped = false;
                continue;
            }

            if (character == '\\')
            {
                isEscaped = true;
                continue;
            }

            if (character != quote)
            {
                builder.Append(character);
                continue;
            }

            for (var tail = index + 1; tail < parameters.Length; tail++)
            {
                if (!char.IsWhiteSpace(parameters[tail]))
                {
                    return false;
                }
            }

            specifier = builder.ToString();
            return specifier.Length != 0;
        }

        return false;
    }

    private static bool IsTailwindSpecifier(string? value) =>
        value is not null &&
        (IsTailwindSpecifierCore(value) ||
         (TryDecodeQuotedSpecifier(
              value,
              out var decoded) &&
          IsTailwindSpecifierCore(decoded ?? string.Empty)));

    private static bool IsTailwindSpecifierCore(string value) =>
        value == "tailwindcss" ||
        value.StartsWith(
            "tailwindcss/",
            StringComparison.Ordinal) ||
        value.StartsWith(
            "@tailwindcss/",
            StringComparison.Ordinal);

    private static bool IsThemePattern(string value)
    {
        var normalized = NormalizeThemePattern(value);
        if (normalized.Length < 3 ||
            !normalized.StartsWith(
                "--",
                StringComparison.Ordinal))
        {
            return false;
        }

        for (var index = 2; index < normalized.Length; index++)
        {
            var character = normalized[index];
            if (character == '*' &&
                (index == 0 ||
                 normalized[index - 1] != '-'))
            {
                return false;
            }

            if (!IsAsciiLetter(character) &&
                !IsAsciiDigit(character) &&
                character is not ('-' or '_' or '*'))
            {
                return false;
            }
        }

        return normalized.IndexOf(
                   '*') < 0 ||
               normalized.Contains(
                   "-*");
    }

    private static string NormalizeThemePattern(string value)
    {
        var normalized = value
            .Replace(
                "\\*",
                "*")
            .Trim();
        var builder = new StringBuilder(normalized.Length + 2);
        var sawWhitespace = false;
        foreach (var character in normalized)
        {
            if (char.IsWhiteSpace(character))
            {
                sawWhitespace = true;
                continue;
            }

            if (sawWhitespace &&
                builder.Length != 0 &&
                character == '-' &&
                builder[builder.Length - 1] != '*')
            {
                builder.Append("-*");
            }

            sawWhitespace = false;
            builder.Append(character);
        }

        if (builder.ToString().IndexOf(
                "-*",
                StringComparison.Ordinal) < 0)
        {
            builder.Append("-*");
        }

        return builder.ToString();
    }

    private static string DecodeCssIdentifier(string value)
    {
        if (value.IndexOf('\\') < 0)
        {
            return value;
        }

        var builder = new StringBuilder(value.Length);
        for (var index = 0; index < value.Length; index++)
        {
            if (value[index] != '\\' ||
                index + 1 >= value.Length)
            {
                builder.Append(value[index]);
                continue;
            }

            var escapeStart = index + 1;
            var escapeEnd = escapeStart;
            while (escapeEnd < value.Length &&
                   escapeEnd - escapeStart < 6 &&
                   IsHexDigit(value[escapeEnd]))
            {
                escapeEnd++;
            }

            if (escapeEnd != escapeStart)
            {
                var codePoint = int.Parse(
                    value.Substring(
                        escapeStart,
                        escapeEnd - escapeStart),
                    NumberStyles.AllowHexSpecifier,
                    CultureInfo.InvariantCulture);
                builder.Append(
                    codePoint == 0 ||
                    codePoint > 0x10FFFF
                        ? "\uFFFD"
                        : char.ConvertFromUtf32(codePoint));
                if (escapeEnd < value.Length &&
                    char.IsWhiteSpace(value[escapeEnd]))
                {
                    escapeEnd++;
                }

                index = escapeEnd - 1;
                continue;
            }

            builder.Append(value[escapeStart]);
            index = escapeStart;
        }

        return builder.ToString();
    }

    private static bool IsHexDigit(char value) =>
        IsAsciiDigit(value) ||
        value is >= 'a' and <= 'f' or >= 'A' and <= 'F';

    private static bool IsAsciiDigit(char value) =>
        value is >= '0' and <= '9';

    private static bool IsQuoted(string value) =>
        value.Length >= 2 &&
        value[0] is '\'' or '"' &&
        value[value.Length - 1] == value[0];

    private static bool IsCssIdentifierCharacter(char value) =>
        IsAsciiLetter(value) ||
        char.IsDigit(value) ||
        value is '_' or '-';

    private static bool IsAsciiLetter(char value) =>
        IsLowerAsciiLetter(value) ||
        value is >= 'A' and <= 'Z';

    private static bool IsLowerAsciiLetter(char value) =>
        value is >= 'a' and <= 'z';

    private static bool IsCommentStart(
        string text,
        int index) =>
        index + 1 < text.Length &&
        text[index] == '/' &&
        text[index + 1] == '*';

    private static int FindCommentEnd(
        string text,
        int start,
        CancellationToken cancellationToken)
    {
        for (var index = start; index + 1 < text.Length; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (text[index] == '*' &&
                text[index + 1] == '/')
            {
                return index + 2;
            }
        }

        return -1;
    }

    private static int FindStringEnd(
        string text,
        int start,
        char quote,
        CancellationToken cancellationToken)
    {
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

            if (character == quote)
            {
                return index;
            }
        }

        return -1;
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

    private static string GetDirectiveName(UtilityDirectiveKind kind) =>
        kind switch
        {
            UtilityDirectiveKind.Utility => UtilityDirectiveName,
            UtilityDirectiveKind.CustomVariant => CustomVariantDirectiveName,
            UtilityDirectiveKind.Variant => VariantDirectiveName,
            UtilityDirectiveKind.Apply => ApplyDirectiveName,
            UtilityDirectiveKind.Reference => ReferenceDirectiveName,
            _ => throw new ArgumentOutOfRangeException(
                nameof(kind),
                kind,
                "Unknown utility directive kind."),
        };

    private static string GetFunctionName(UtilityCssFunctionKind kind) =>
        kind switch
        {
            UtilityCssFunctionKind.Value => "--value()",
            UtilityCssFunctionKind.Modifier => "--modifier()",
            UtilityCssFunctionKind.Default => "--default()",
            UtilityCssFunctionKind.Spacing => "--spacing()",
            UtilityCssFunctionKind.Alpha => "--alpha()",
            _ => throw new ArgumentOutOfRangeException(
                nameof(kind),
                kind,
                "Unknown utility CSS function kind."),
        };

    private static UtilityDirectiveDiagnostic CreateDiagnostic(
        UtilityStylesheetParseOptions options,
        UtilityDirectiveDiagnosticCode code,
        string message,
        int start,
        int length) =>
        new(
            code,
            UtilityDirectiveDiagnosticSeverity.Error,
            message,
            CreateSourceSpan(
                options,
                start,
                length));

    private static UtilityStylesheetSourceSpan CreateSourceSpan(
        UtilityStylesheetParseOptions options,
        int start,
        int length) =>
        new(
            options.SourceIdentity,
            checked(options.ContentOffset + start),
            Math.Max(
                0,
                length));

    private static int CompareDiagnostics(
        UtilityDirectiveDiagnostic left,
        UtilityDirectiveDiagnostic right)
    {
        var start = left.SourceSpan.Start.CompareTo(right.SourceSpan.Start);
        return start != 0
            ? start
            : left.Code.CompareTo(right.Code);
    }

    private readonly struct DirectiveHeader
    {
        public DirectiveHeader(
            int terminatorIndex,
            char terminator,
            int contentEnd,
            bool isValid)
        {
            TerminatorIndex = terminatorIndex;
            Terminator = terminator;
            ContentEnd = contentEnd;
            IsValid = isValid;
        }

        public int TerminatorIndex { get; }

        public char Terminator { get; }

        public int ContentEnd { get; }

        public bool IsValid { get; }
    }

    private readonly struct TextRange
    {
        public TextRange(
            int start,
            int end)
        {
            Start = start;
            End = end;
        }

        public int Start { get; }

        public int End { get; }
    }
}
