using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading;

namespace Assimalign.Viu.Tooling.UtilityCss;

internal static class UtilityProjectStylesheetCompileEngine
{
    private const string UnresolvedFunctionMarker = "\uE000VIU_UNRESOLVED_UTILITY_VALUE\uE001";
    private const string NonRatioValueMarker = "\uE002VIU_NON_RATIO_VALUE\uE003";

    public static UtilityProjectStylesheetCompilationResult Compile(
        string css,
        IEnumerable<string> candidateTexts,
        UtilityProjectStylesheetCompilationOptions options,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var context = new CompilationContext(
            options,
            cancellationToken);
        var root = context.VisitRoot(css);
        var authoredCss = context.RewriteDocument(root);
        var candidateRules = context.CompileCandidates(candidateTexts);

        return new UtilityProjectStylesheetCompilationResult(
            NormalizeCss(authoredCss),
            RenderUtilityLayer(candidateRules),
            ToCollection(candidateRules),
            ToCollection(context.Utilities),
            ToCollection(context.Variants),
            ToCollection(
                context.DirectiveDiagnostics
                    .OrderBy(diagnostic => diagnostic.SourceSpan.SourceIdentity, StringComparer.Ordinal)
                    .ThenBy(diagnostic => diagnostic.SourceSpan.Start)
                    .ThenBy(diagnostic => diagnostic.Code)),
            ToCollection(
                context.Diagnostics
                    .OrderBy(diagnostic => diagnostic.SourceSpan.SourceIdentity, StringComparer.Ordinal)
                    .ThenBy(diagnostic => diagnostic.SourceSpan.Start)
                    .ThenBy(diagnostic => diagnostic.Code)));
    }

    private static UtilityCollection<T> ToCollection<T>(IEnumerable<T> values)
    {
        var array = values.ToArray();
        return array.Length == 0
            ? UtilityCollection<T>.Empty
            : new UtilityCollection<T>(array);
    }

    private static string RenderUtilityLayer(
        IReadOnlyCollection<UtilityClassMetadata> rules)
    {
        if (rules.Count == 0)
        {
            return string.Empty;
        }

        var builder = new StringBuilder();
        builder.AppendLine("@layer utilities {");
        foreach (var rule in rules)
        {
            foreach (var line in NormalizeLineEndings(rule.Css).Split('\n'))
            {
                builder.Append("  ");
                builder.AppendLine(line);
            }
        }

        builder.Append('}');
        return builder.ToString();
    }

    private static string NormalizeCss(string css) =>
        NormalizeLineEndings(css).Trim();

    private static string NormalizeLineEndings(string value) =>
        value
            .Replace(
                "\r\n",
                "\n")
            .Replace(
                '\r',
                '\n');

    private sealed class CompilationContext
    {
        private readonly CancellationToken cancellationToken;
        private readonly List<string> applyingCustomVariantNames;
        private readonly HashSet<string> visitedIdentities;
        private readonly UtilityProjectStylesheetCompilationOptions options;
        private readonly List<string> visitingIdentities;
        private int customVariantFailureCount;
        private Dictionary<string, UtilityCustomVariantDefinition>? variantsByName;
        private UtilityVariantRegistry? variantRegistry;

        public CompilationContext(
            UtilityProjectStylesheetCompilationOptions options,
            CancellationToken cancellationToken)
        {
            this.options = options;
            this.cancellationToken = cancellationToken;
            applyingCustomVariantNames = new List<string>();
            visitedIdentities = new HashSet<string>(StringComparer.Ordinal);
            visitingIdentities = new List<string>();
        }

        public List<UtilityCustomUtilityDefinition> Utilities { get; } = new();

        public List<UtilityCustomVariantDefinition> Variants { get; } = new();

        public List<UtilityDirectiveDiagnostic> DirectiveDiagnostics { get; } = new();

        public List<UtilityProjectStylesheetDiagnostic> Diagnostics { get; } = new();

        public StylesheetDocument VisitRoot(string css)
        {
            var identity = options.SourceIdentity ?? string.Empty;
            var document = ParseDocument(
                identity,
                css,
                false);
            visitingIdentities.Add(identity);
            visitedIdentities.Add(identity);
            VisitDefinitions(document);
            visitingIdentities.RemoveAt(visitingIdentities.Count - 1);
            return document;
        }

        public string RewriteDocument(StylesheetDocument document) =>
            StripConfigurationDirectives(
                RewriteFragment(
                    document.Css,
                    document.SourceIdentity,
                    0,
                    null,
                    new List<string>(),
                    true));

        public UtilityClassMetadata[] CompileCandidates(
            IEnumerable<string> candidateTexts)
        {
            var rulesByCandidate = new Dictionary<string, UtilityClassMetadata>(
                StringComparer.Ordinal);
            foreach (var candidateText in candidateTexts)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (string.IsNullOrWhiteSpace(candidateText))
                {
                    continue;
                }

                var parseResult = UtilityCandidateParser.Parse(
                    candidateText,
                    options.Theme.Prefix,
                    GetVariantRegistry(),
                    cancellationToken);
                if (!parseResult.IsSuccess ||
                    parseResult.Candidate is null)
                {
                    continue;
                }

                var candidate = parseResult.Candidate;
                var definitions = FindMatchingUtilities(candidate).ToArray();
                if (definitions.Length == 0)
                {
                    if (HasCustomVariant(candidate) &&
                        TryCompileBuiltInWithProjectVariants(
                            candidate,
                            out var builtInRule))
                    {
                        rulesByCandidate[candidate.CanonicalText] = builtInRule;
                    }

                    continue;
                }

                var bodies = new List<string>();
                var firstOrder = int.MaxValue;
                foreach (var definition in definitions)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    firstOrder = Math.Min(
                        firstOrder,
                        Utilities.IndexOf(definition));
                    var resolved = ResolveCustomUtilityBody(
                        definition,
                        candidate,
                        new List<string>());
                    if (!string.IsNullOrWhiteSpace(resolved))
                    {
                        bodies.Add(resolved);
                    }
                }

                if (bodies.Count == 0)
                {
                    if (definitions.Any(definition => definition.IsFunctional))
                    {
                        Diagnostics.Add(
                            CreateDiagnostic(
                                UtilityProjectStylesheetDiagnosticCode.UnresolvedFunctionalValue,
                                $"The project utility candidate '{candidateText}' did not satisfy any declaration in its functional custom definition.",
                                definitions[0].SourceSpan));
                    }

                    continue;
                }

                var body = string.Join(
                    "\n",
                    bodies);
                if (candidate.IsImportant ||
                    options.Theme.IsImportant)
                {
                    body = AddImportantMarkers(body);
                }

                if (!TryApplyCandidateVariants(
                        candidate,
                        body,
                        definitions[0].SourceSpan,
                        out var variantBody))
                {
                    continue;
                }

                var css = "." +
                          UtilityCssText.EscapeClassName(candidate.RawText) +
                          " {\n" +
                          Indent(variantBody, 1) +
                          "\n}";
                rulesByCandidate[candidate.CanonicalText] =
                    new UtilityClassMetadata(
                        candidate.CanonicalText,
                        $"Project-defined utility '{definitions[0].Name}'.",
                        css,
                        2000 + Math.Max(
                            0,
                            firstOrder));
            }

            return rulesByCandidate.Values
                .OrderBy(rule => rule.SortOrder)
                .ThenBy(rule => rule.CandidateText, StringComparer.Ordinal)
                .ToArray();
        }

        private bool TryCompileBuiltInWithProjectVariants(
            UtilityCandidate candidate,
            out UtilityClassMetadata rule)
        {
            rule = null!;
            var baseCandidateText = CreateBaseCandidateText(candidate);
            var resolution = options.Registry.Resolve(
                baseCandidateText,
                options.Theme,
                cancellationToken);
            if (!resolution.IsSuccess ||
                resolution.Metadata is null ||
                !TryExtractRuleBody(
                    resolution.Metadata,
                    out var body))
            {
                Diagnostics.Add(
                    CreateDiagnostic(
                        UtilityProjectStylesheetDiagnosticCode.UnsupportedVariant,
                        $"The project-variant candidate '{candidate.RawText}' does not have a resolvable built-in utility body.",
                        GetVariantsByName()
                            .Where(pair => candidate.Variants.Any(
                                variant => variant.Root == pair.Key))
                            .Select(pair => pair.Value.SourceSpan)
                            .First()));
                return false;
            }

            if (!TryApplyCandidateVariants(
                    candidate,
                    body,
                    GetVariantsByName()
                        .Where(pair => candidate.Variants.Any(
                            variant => variant.Root == pair.Key))
                        .Select(pair => pair.Value.SourceSpan)
                        .First(),
                    out var variantBody))
            {
                return false;
            }

            rule = new UtilityClassMetadata(
                candidate.CanonicalText,
                resolution.Metadata.Description,
                "." +
                UtilityCssText.EscapeClassName(candidate.RawText) +
                " {\n" +
                Indent(variantBody, 1) +
                "\n}",
                resolution.Metadata.SortOrder);
            return true;
        }

        private bool HasCustomVariant(UtilityCandidate candidate)
        {
            var customVariants = GetVariantsByName();
            return candidate.Variants.Any(
                variant => customVariants.ContainsKey(variant.Root));
        }

        private string CreateBaseCandidateText(UtilityCandidate candidate)
        {
            var builder = new StringBuilder();
            if (!string.IsNullOrEmpty(options.Theme.Prefix))
            {
                builder.Append(options.Theme.Prefix);
                builder.Append(':');
            }

            if (candidate.IsNegative)
            {
                builder.Append('-');
            }

            builder.Append(candidate.UtilityText);
            if (candidate.IsImportant)
            {
                builder.Append('!');
            }

            return builder.ToString();
        }

        private StylesheetDocument ParseDocument(
            string identity,
            string css,
            bool isReferenced)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var parseResult = UtilityStylesheetParser.Parse(
                css,
                new UtilityStylesheetParseOptions
                {
                    SourceIdentity = identity,
                },
                cancellationToken);
            DirectiveDiagnostics.AddRange(parseResult.Diagnostics);
            return new StylesheetDocument(
                identity,
                css,
                parseResult,
                isReferenced);
        }

        private void VisitDefinitions(StylesheetDocument document)
        {
            foreach (var directive in document.ParseResult.Directives)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (!directive.IsValid)
                {
                    continue;
                }

                switch (directive.Kind)
                {
                    case UtilityDirectiveKind.Reference:
                        VisitReference(
                            document,
                            directive);
                        break;

                    case UtilityDirectiveKind.Utility:
                        if (directive.Identifier is not null &&
                            directive.Body is not null)
                        {
                            Utilities.Add(
                                new UtilityCustomUtilityDefinition(
                                    directive.Identifier,
                                    directive.Body,
                                    directive.SourceSpan,
                                    directive.BodySourceSpan ??
                                    directive.SourceSpan,
                                    document.IsReferenced));
                        }

                        break;

                    case UtilityDirectiveKind.CustomVariant:
                        if (directive.Identifier is not null)
                        {
                            ParseCustomVariant(
                                directive,
                                document.IsReferenced,
                                out var selector,
                                out var body);
                            Variants.Add(
                                new UtilityCustomVariantDefinition(
                                    directive.Identifier,
                                    selector,
                                    body,
                                    directive.SourceSpan,
                                    document.IsReferenced));
                        }

                        break;
                }
            }
        }

        private void VisitReference(
            StylesheetDocument document,
            UtilityDirective directive)
        {
            var specifier = directive.Identifier;
            if (specifier is null ||
                !options.ReferenceGraph.TryResolve(
                    document.SourceIdentity,
                    specifier,
                    out var reference) ||
                reference is null)
            {
                Diagnostics.Add(
                    CreateDiagnostic(
                        UtilityProjectStylesheetDiagnosticCode.UnresolvedReference,
                        $"The stylesheet reference '{specifier ?? directive.Parameters}' could not be resolved by the host-supplied graph.",
                        directive.ParametersSourceSpan));
                return;
            }

            if (visitingIdentities.Contains(
                    reference.ResolvedSourceIdentity,
                    StringComparer.Ordinal))
            {
                Diagnostics.Add(
                    CreateDiagnostic(
                        UtilityProjectStylesheetDiagnosticCode.CyclicReference,
                        $"The stylesheet reference '{specifier}' creates a cycle through '{reference.ResolvedSourceIdentity}'.",
                        directive.ParametersSourceSpan));
                return;
            }

            if (!visitedIdentities.Add(reference.ResolvedSourceIdentity))
            {
                return;
            }

            var referencedDocument = ParseDocument(
                reference.ResolvedSourceIdentity,
                reference.Css ?? string.Empty,
                true);
            visitingIdentities.Add(reference.ResolvedSourceIdentity);
            VisitDefinitions(referencedDocument);
            visitingIdentities.RemoveAt(visitingIdentities.Count - 1);
        }

        private string RewriteFragment(
            string css,
            string sourceIdentity,
            int sourceOffset,
            FunctionalCandidate? functionalCandidate,
            IList<string> applyStack,
            bool collectParserDiagnostics)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var parseResult = UtilityStylesheetParser.Parse(
                css,
                new UtilityStylesheetParseOptions
                {
                    ContentOffset = sourceOffset,
                    SourceIdentity = sourceIdentity,
                },
                cancellationToken);
            if (collectParserDiagnostics)
            {
                // Root and graph documents have already contributed their parser diagnostics.
                // This path exists for independently compiled fragments only.
            }

            var builder = new StringBuilder(css.Length);
            var cursor = 0;
            foreach (var directive in parseResult.Directives)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var relativeStart = directive.SourceSpan.Start - sourceOffset;
                if (relativeStart < cursor ||
                    relativeStart < 0 ||
                    relativeStart > css.Length)
                {
                    continue;
                }

                builder.Append(
                    ResolveFunctions(
                        css.Substring(
                            cursor,
                            relativeStart - cursor),
                        sourceIdentity,
                        sourceOffset + cursor,
                        functionalCandidate));

                if (!directive.IsValid)
                {
                    builder.Append(
                        css,
                        relativeStart,
                        directive.SourceSpan.Length);
                    cursor = relativeStart + directive.SourceSpan.Length;
                    continue;
                }

                switch (directive.Kind)
                {
                    case UtilityDirectiveKind.Utility:
                    case UtilityDirectiveKind.CustomVariant:
                    case UtilityDirectiveKind.Reference:
                        break;

                    case UtilityDirectiveKind.Apply:
                        builder.Append(
                            ExpandApply(
                                directive,
                                applyStack));
                        break;

                    case UtilityDirectiveKind.Variant:
                        builder.Append(
                            ExpandVariant(
                                directive,
                                sourceIdentity,
                                functionalCandidate,
                                applyStack));
                        break;

                    default:
                        throw new ArgumentOutOfRangeException(
                            nameof(directive),
                            directive.Kind,
                            "Unknown utility directive kind.");
                }

                cursor = relativeStart + directive.SourceSpan.Length;
            }

            if (cursor < css.Length)
            {
                builder.Append(
                    ResolveFunctions(
                        css.Substring(cursor),
                        sourceIdentity,
                        sourceOffset + cursor,
                        functionalCandidate));
            }

            return OmitUnresolvedDeclarations(builder.ToString());
        }

        private string ExpandApply(
            UtilityDirective directive,
            IList<string> applyStack)
        {
            var builder = new StringBuilder();
            foreach (var token in SplitCandidateList(
                         directive.Parameters,
                         directive.ParametersSourceSpan.Start))
            {
                cancellationToken.ThrowIfCancellationRequested();
                var parseResult = UtilityCandidateParser.Parse(
                    token.Text,
                    options.Theme.Prefix,
                    GetVariantRegistry(),
                    cancellationToken);
                if (!parseResult.IsSuccess ||
                    parseResult.Candidate is null)
                {
                    Diagnostics.Add(
                        CreateDiagnostic(
                            UtilityProjectStylesheetDiagnosticCode.UnknownAppliedUtility,
                            $"The applied utility '{token.Text}' could not be parsed.",
                            new UtilityStylesheetSourceSpan(
                                directive.SourceSpan.SourceIdentity,
                                token.Start,
                                token.Text.Length)));
                    continue;
                }

                var candidate = parseResult.Candidate;
                var matchingDefinitions = FindMatchingUtilities(candidate).ToArray();
                if (matchingDefinitions.Length != 0)
                {
                    var stackKey = GetDefinitionCandidateText(candidate);
                    if (applyStack.Contains(
                            stackKey,
                            StringComparer.Ordinal))
                    {
                        Diagnostics.Add(
                            CreateDiagnostic(
                                UtilityProjectStylesheetDiagnosticCode.CircularApply,
                                $"The custom utility '{stackKey}' recursively applies itself.",
                                new UtilityStylesheetSourceSpan(
                                    directive.SourceSpan.SourceIdentity,
                                    token.Start,
                                    token.Text.Length)));
                        continue;
                    }

                    var nestedStack = new List<string>(applyStack)
                    {
                        stackKey,
                    };
                    foreach (var definition in matchingDefinitions)
                    {
                        var customBody = ResolveCustomUtilityBody(
                            definition,
                            candidate,
                            nestedStack);
                        if (candidate.IsImportant ||
                            options.Theme.IsImportant)
                        {
                            customBody = AddImportantMarkers(customBody);
                        }

                        if (!TryApplyCandidateVariants(
                                candidate,
                                customBody,
                                new UtilityStylesheetSourceSpan(
                                    directive.SourceSpan.SourceIdentity,
                                    token.Start,
                                    token.Text.Length),
                                out customBody))
                        {
                            continue;
                        }

                        AppendSeparated(
                            builder,
                            customBody);
                    }

                    continue;
                }

                var resolution = options.Registry.Resolve(
                    token.Text,
                    options.Theme,
                    cancellationToken);
                if (!resolution.IsSuccess ||
                    resolution.Metadata is null)
                {
                    Diagnostics.Add(
                        CreateDiagnostic(
                            UtilityProjectStylesheetDiagnosticCode.UnknownAppliedUtility,
                            $"The applied utility '{token.Text}' is not available in the built-in or project registry.",
                            new UtilityStylesheetSourceSpan(
                                directive.SourceSpan.SourceIdentity,
                                token.Start,
                                token.Text.Length)));
                    continue;
                }

                string builtInBody;
                if (candidate.Variants.Any(
                        variant => variant.Kind != UtilityVariantKind.Prefix))
                {
                    if (!TryConvertRuleToNested(
                            resolution.Metadata,
                            out builtInBody))
                    {
                        Diagnostics.Add(
                            CreateDiagnostic(
                                UtilityProjectStylesheetDiagnosticCode.UnsupportedVariant,
                                $"The applied variant utility '{token.Text}' could not be transformed for its containing rule.",
                                new UtilityStylesheetSourceSpan(
                                    directive.SourceSpan.SourceIdentity,
                                    token.Start,
                                    token.Text.Length)));
                        continue;
                    }
                }
                else if (!TryExtractRuleBody(
                             resolution.Metadata,
                             out builtInBody))
                {
                    Diagnostics.Add(
                        CreateDiagnostic(
                            UtilityProjectStylesheetDiagnosticCode.UnknownAppliedUtility,
                            $"The applied utility '{token.Text}' did not produce a declaration rule.",
                            new UtilityStylesheetSourceSpan(
                                directive.SourceSpan.SourceIdentity,
                                token.Start,
                                token.Text.Length)));
                    continue;
                }

                AppendSeparated(
                    builder,
                    builtInBody);
            }

            return builder.ToString();
        }

        private string ExpandVariant(
            UtilityDirective directive,
            string sourceIdentity,
            FunctionalCandidate? functionalCandidate,
            IList<string> applyStack)
        {
            if (directive.Body is null ||
                directive.BodySourceSpan is null)
            {
                return string.Empty;
            }

            var body = RewriteFragment(
                directive.Body,
                sourceIdentity,
                directive.BodySourceSpan.Start,
                functionalCandidate,
                applyStack,
                false);
            var branches = SplitTopLevel(
                directive.Parameters,
                ',');
            var result = new StringBuilder();
            foreach (var branch in branches)
            {
                var value = branch.Trim();
                if (value.Length == 0)
                {
                    continue;
                }

                if (!TryApplyVariantChain(
                        value,
                        body,
                        directive.ParametersSourceSpan,
                        out var transformed))
                {
                    continue;
                }

                AppendSeparated(
                    result,
                    transformed);
            }

            return result.ToString();
        }

        private bool TryApplyCandidateVariants(
            UtilityCandidate candidate,
            string body,
            UtilityStylesheetSourceSpan sourceSpan,
            out string transformed)
        {
            transformed = body;
            var variants = candidate.Variants
                .Where(variant => variant.Kind != UtilityVariantKind.Prefix)
                .ToArray();
            for (var index = variants.Length - 1; index >= 0; index--)
            {
                if (!TryApplySingleVariant(
                        variants[index],
                        transformed,
                        sourceSpan,
                        out transformed))
                {
                    return false;
                }
            }

            return true;
        }

        private bool TryApplyVariantChain(
            string chain,
            string body,
            UtilityStylesheetSourceSpan sourceSpan,
            out string transformed)
        {
            transformed = body;
            var parseResult = UtilityCandidateParser.Parse(
                chain + ":block",
                null,
                GetVariantRegistry(),
                cancellationToken);
            if (!parseResult.IsSuccess ||
                parseResult.Candidate is null)
            {
                Diagnostics.Add(
                    CreateDiagnostic(
                        UtilityProjectStylesheetDiagnosticCode.UnsupportedVariant,
                        $"The @variant chain '{chain}' could not be parsed.",
                        sourceSpan));
                return false;
            }

            var variants = parseResult.Candidate.Variants
                .Where(variant => variant.Kind != UtilityVariantKind.Prefix)
                .ToArray();
            for (var index = variants.Length - 1; index >= 0; index--)
            {
                if (!TryApplySingleVariant(
                        variants[index],
                        transformed,
                        sourceSpan,
                        out transformed))
                {
                    return false;
                }
            }

            return true;
        }

        private bool TryApplySingleVariant(
            UtilityVariant variant,
            string body,
            UtilityStylesheetSourceSpan sourceSpan,
            out string transformed)
        {
            if (GetVariantsByName().TryGetValue(
                    variant.Root,
                    out var customVariant) &&
                variant.Value is null &&
                variant.Modifier is null)
            {
                return TryApplyCustomVariant(
                    customVariant,
                    body,
                    sourceSpan,
                    out transformed);
            }

            var placeholderCandidate =
                (string.IsNullOrEmpty(options.Theme.Prefix)
                    ? string.Empty
                    : options.Theme.Prefix + ":") +
                variant.RawText +
                ":block";
            var resolution = options.Registry.Resolve(
                placeholderCandidate,
                options.Theme,
                cancellationToken);
            if (!resolution.IsSuccess ||
                resolution.Metadata is null ||
                !TryCreateVariantTemplate(
                    resolution.Metadata,
                    body,
                    out transformed))
            {
                Diagnostics.Add(
                    CreateDiagnostic(
                        UtilityProjectStylesheetDiagnosticCode.UnsupportedVariant,
                        $"The variant '{variant.RawText}' could not be transformed for authored or project-defined CSS.",
                        sourceSpan));
                transformed = string.Empty;
                return false;
            }

            return true;
        }

        private string ResolveCustomUtilityBody(
            UtilityCustomUtilityDefinition definition,
            UtilityCandidate candidate,
            IList<string> applyStack)
        {
            if (!TryCreateFunctionalCandidate(
                    definition,
                    candidate,
                    out var functionalCandidate))
            {
                return string.Empty;
            }

            var body = RewriteFragment(
                definition.Body,
                definition.SourceSpan.SourceIdentity ?? string.Empty,
                definition.BodySourceSpan.Start,
                functionalCandidate,
                applyStack,
                false);
            if (functionalCandidate is null)
            {
                return body;
            }

            if (!functionalCandidate.ResolvedValue ||
                (functionalCandidate.Modifier is not null &&
                 !functionalCandidate.ResolvedRatio &&
                 !functionalCandidate.ResolvedModifier) ||
                (functionalCandidate.ResolvedRatio &&
                 functionalCandidate.ResolvedModifier))
            {
                return string.Empty;
            }

            return functionalCandidate.ResolvedRatio
                ? OmitDeclarationsContainingMarker(
                    body,
                    NonRatioValueMarker)
                : body.Replace(
                    NonRatioValueMarker,
                    string.Empty);
        }

        private string ResolveFunctions(
            string text,
            string sourceIdentity,
            int sourceOffset,
            FunctionalCandidate? functionalCandidate)
        {
            var builder = new StringBuilder(text.Length);
            var index = 0;
            while (index < text.Length)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (IsCommentStart(
                        text,
                        index))
                {
                    var commentEnd = FindCommentEnd(
                        text,
                        index + 2);
                    if (commentEnd < 0)
                    {
                        builder.Append(
                            text,
                            index,
                            text.Length - index);
                        break;
                    }

                    builder.Append(
                        text,
                        index,
                        commentEnd - index);
                    index = commentEnd;
                    continue;
                }

                if (text[index] is '\'' or '"')
                {
                    var stringEnd = FindStringEnd(
                        text,
                        index + 1,
                        text[index]);
                    if (stringEnd < 0)
                    {
                        builder.Append(
                            text,
                            index,
                            text.Length - index);
                        break;
                    }

                    builder.Append(
                        text,
                        index,
                        stringEnd - index + 1);
                    index = stringEnd + 1;
                    continue;
                }

                if (!TryReadFunction(
                        text,
                        index,
                        out var functionName,
                        out var openingParenthesis))
                {
                    builder.Append(text[index]);
                    index++;
                    continue;
                }

                var closingParenthesis = FindMatchingParenthesis(
                    text,
                    openingParenthesis);
                if (closingParenthesis < 0)
                {
                    builder.Append(
                        text,
                        index,
                        text.Length - index);
                    break;
                }

                var arguments = text.Substring(
                    openingParenthesis + 1,
                    closingParenthesis - openingParenthesis - 1);
                if (!TryResolveFunction(
                        functionName,
                        arguments,
                        functionalCandidate,
                        out var resolved))
                {
                    builder.Append(UnresolvedFunctionMarker);
                }
                else if (!UtilityCssText.IsSafeValue(resolved))
                {
                    Diagnostics.Add(
                        CreateDiagnostic(
                            UtilityProjectStylesheetDiagnosticCode.UnsafeResolvedValue,
                            $"The {functionName} function resolved to an unsafe CSS value.",
                            new UtilityStylesheetSourceSpan(
                                sourceIdentity,
                                sourceOffset + index,
                                closingParenthesis - index + 1)));
                    builder.Append(UnresolvedFunctionMarker);
                }
                else
                {
                    builder.Append(resolved);
                }

                index = closingParenthesis + 1;
            }

            return builder.ToString();
        }

        private bool TryResolveFunction(
            string functionName,
            string argumentsText,
            FunctionalCandidate? functionalCandidate,
            out string resolved)
        {
            switch (functionName)
            {
                case "--value":
                    return TryResolveFunctionalPart(
                        SplitTopLevel(
                            argumentsText,
                            ','),
                        functionalCandidate,
                        false,
                        out resolved);

                case "--modifier":
                    return TryResolveFunctionalPart(
                        SplitTopLevel(
                            argumentsText,
                            ','),
                        functionalCandidate,
                        true,
                        out resolved);

                case "--spacing":
                    var spacingExpression = ResolveFunctions(
                            argumentsText,
                            string.Empty,
                            0,
                            functionalCandidate)
                        .Trim();
                    if (spacingExpression.Length == 0 ||
                        spacingExpression.Contains(
                            UnresolvedFunctionMarker))
                    {
                        resolved = string.Empty;
                        return false;
                    }

                    if (!TryResolveThemeProperty(
                            "--spacing",
                            out var spacing))
                    {
                        resolved = string.Empty;
                        return false;
                    }

                    resolved = ResolveSpacingFunction(
                        spacing,
                        spacingExpression);
                    return true;

                case "--alpha":
                    var alphaExpression = ResolveFunctions(
                            argumentsText,
                            string.Empty,
                            0,
                            functionalCandidate)
                        .Trim();
                    var separator = FindTopLevelSeparator(
                        alphaExpression,
                        '/');
                    if (separator <= 0 ||
                        separator >= alphaExpression.Length - 1 ||
                        alphaExpression.Contains(
                            UnresolvedFunctionMarker))
                    {
                        resolved = string.Empty;
                        return false;
                    }

                    var color = alphaExpression.Substring(
                            0,
                            separator)
                        .Trim();
                    var opacity = alphaExpression.Substring(separator + 1)
                        .Trim();
                    if (color.Length == 0 ||
                        opacity.Length == 0)
                    {
                        resolved = string.Empty;
                        return false;
                    }

                    if (decimal.TryParse(
                            opacity,
                            NumberStyles.Float,
                            CultureInfo.InvariantCulture,
                            out var numericOpacity))
                    {
                        opacity = FormatDecimal(
                                      numericOpacity * 100m) +
                                  "%";
                    }

                    resolved = opacity == "100%"
                        ? color
                        : "color-mix(in oklab, " +
                          color +
                          " " +
                          opacity +
                          ", transparent)";
                    return true;

                case "--default":
                    resolved = argumentsText.Trim();
                    return resolved.Length != 0;

                default:
                    resolved = string.Empty;
                    return false;
            }
        }

        private bool TryResolveFunctionalPart(
            IEnumerable<string> arguments,
            FunctionalCandidate? candidate,
            bool useModifier,
            out string resolved)
        {
            resolved = string.Empty;
            if (candidate is null)
            {
                return false;
            }

            var rawPart = useModifier
                ? candidate.Modifier
                : candidate.Value;
            if (useModifier)
            {
                candidate.UsedModifier = true;
            }
            else
            {
                candidate.UsedValue = true;
            }

            foreach (var argumentValue in arguments)
            {
                var argument = argumentValue.Trim();
                if (argument.Length == 0)
                {
                    continue;
                }

                if (argument.StartsWith(
                        "--default(",
                        StringComparison.Ordinal) &&
                    argument.EndsWith(
                        ")",
                        StringComparison.Ordinal))
                {
                    if (rawPart is null)
                    {
                        resolved = ResolveFunctions(
                                argument.Substring(
                            "--default(".Length,
                            argument.Length - "--default(".Length - 1),
                                string.Empty,
                                0,
                                candidate)
                            .Trim();
                        if (resolved.Length != 0 &&
                            !resolved.Contains(UnresolvedFunctionMarker))
                        {
                            RecordFunctionalResolution(
                                candidate,
                                useModifier,
                                false,
                                ref resolved);
                            return true;
                        }

                        return false;
                    }

                    continue;
                }

                if (rawPart is null)
                {
                    continue;
                }

                if (argument.StartsWith(
                        "--",
                        StringComparison.Ordinal))
                {
                    var themePattern = NormalizeFunctionalThemePattern(argument);
                    var wildcard = themePattern.IndexOf(
                        '*');
                    var propertyName = wildcard < 0
                        ? string.Empty
                        : themePattern.Substring(
                              0,
                              wildcard) +
                          rawPart.NamedText +
                          themePattern.Substring(wildcard + 1);
                    if (wildcard >= 0 &&
                        rawPart.Kind == FunctionalPartKind.Named &&
                        TryResolveThemeProperty(
                            propertyName,
                            out resolved))
                    {
                        RecordFunctionalResolution(
                            candidate,
                            useModifier,
                            false,
                            ref resolved);
                        return true;
                    }

                    continue;
                }

                if (argument.Length >= 2 &&
                    argument[0] is '\'' or '"' &&
                    argument[argument.Length - 1] == argument[0])
                {
                    var literal = DecodeQuoted(argument);
                    if (rawPart.Kind == FunctionalPartKind.Named &&
                        string.Equals(
                            rawPart.NamedText,
                            literal,
                            StringComparison.Ordinal))
                    {
                        resolved = literal;
                        RecordFunctionalResolution(
                            candidate,
                            useModifier,
                            false,
                            ref resolved);
                        return true;
                    }

                    continue;
                }

                if (argument.Length >= 3 &&
                    argument[0] == '[' &&
                    argument[argument.Length - 1] == ']')
                {
                    var dataType = argument.Substring(
                        1,
                        argument.Length - 2);
                    if (rawPart.Kind == FunctionalPartKind.Arbitrary &&
                        (dataType == "*" ||
                         (rawPart.DataType is not null &&
                          string.Equals(
                              rawPart.DataType,
                              dataType,
                              StringComparison.Ordinal)) ||
                         (rawPart.DataType is null &&
                          IsValueOfType(
                              rawPart.Value,
                              dataType))))
                    {
                        resolved = rawPart.Value;
                        RecordFunctionalResolution(
                            candidate,
                            useModifier,
                            false,
                            ref resolved);
                        return true;
                    }

                    continue;
                }

                var valueForType =
                    argument == "ratio" &&
                    !useModifier &&
                    candidate.Fraction is not null
                        ? candidate.Fraction
                        : rawPart.Value;
                if (rawPart.Kind == FunctionalPartKind.Named &&
                    IsBareValueOfType(
                        valueForType,
                        argument))
                {
                    resolved = valueForType;
                    RecordFunctionalResolution(
                        candidate,
                        useModifier,
                        !useModifier &&
                        argument == "ratio" &&
                        candidate.Fraction is not null,
                        ref resolved);
                    return true;
                }
            }

            return false;
        }

        private static void RecordFunctionalResolution(
            FunctionalCandidate candidate,
            bool useModifier,
            bool isRatio,
            ref string resolved)
        {
            if (useModifier)
            {
                candidate.ResolvedModifier = true;
                return;
            }

            candidate.ResolvedValue = true;
            if (isRatio)
            {
                candidate.ResolvedRatio = true;
                var separator = resolved.IndexOf('/');
                if (separator > 0 &&
                    separator < resolved.Length - 1)
                {
                    resolved =
                        resolved.Substring(
                            0,
                            separator)
                            .Trim() +
                        " / " +
                        resolved.Substring(separator + 1)
                            .Trim();
                }
            }
            else if (candidate.Fraction is not null)
            {
                resolved = NonRatioValueMarker + resolved;
            }
        }

        private static string NormalizeFunctionalThemePattern(string value)
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

        private bool TryResolveThemeProperty(
            string name,
            out string resolved)
        {
            resolved = string.Empty;
            if (!options.Theme.TryGetProperty(
                    name,
                    out var property) ||
                property is null)
            {
                return false;
            }

            if ((property.Options & UtilityThemeOptions.Inline) != 0)
            {
                resolved = property.Value;
                return true;
            }

            var formattedName = options.Theme.FormatCustomPropertyName(property.Name);
            resolved = (property.Options & UtilityThemeOptions.Reference) != 0
                ? "var(" + formattedName + ", " + property.Value + ")"
                : "var(" + formattedName + ")";
            return true;
        }

        private static string ResolveSpacingFunction(
            string spacing,
            string expression)
        {
            if (!decimal.TryParse(
                    expression,
                    NumberStyles.Float,
                    CultureInfo.InvariantCulture,
                    out var multiplier))
            {
                return "calc(" + spacing + " * " + expression + ")";
            }

            if (multiplier == decimal.Zero)
            {
                return "0px";
            }

            if (multiplier == decimal.One)
            {
                return spacing;
            }

            return TryMultiplyCssDimension(
                    spacing,
                    multiplier,
                    out var product)
                ? product
                : "calc(" + spacing + " * " + expression + ")";
        }

        private static bool TryMultiplyCssDimension(
            string cssValue,
            decimal multiplier,
            out string product)
        {
            product = string.Empty;
            var index = 0;
            if (cssValue.Length != 0 &&
                cssValue[0] is '+' or '-')
            {
                index++;
            }

            var sawDigit = false;
            var sawDecimalPoint = false;
            while (index < cssValue.Length)
            {
                var character = cssValue[index];
                if (character >= '0' &&
                    character <= '9')
                {
                    sawDigit = true;
                    index++;
                    continue;
                }

                if (character == '.' &&
                    !sawDecimalPoint)
                {
                    sawDecimalPoint = true;
                    index++;
                    continue;
                }

                break;
            }

            if (!sawDigit ||
                index == cssValue.Length ||
                !decimal.TryParse(
                    cssValue.Substring(
                        0,
                        index),
                    NumberStyles.Float,
                    CultureInfo.InvariantCulture,
                    out var dimension))
            {
                return false;
            }

            for (var unitIndex = index;
                 unitIndex < cssValue.Length;
                 unitIndex++)
            {
                var character = cssValue[unitIndex];
                if (!IsAsciiLetter(character) &&
                    character != '%')
                {
                    return false;
                }
            }

            product =
                FormatDecimal(dimension * multiplier) +
                cssValue.Substring(index);
            return true;
        }

        private IEnumerable<UtilityCustomUtilityDefinition> FindMatchingUtilities(
            UtilityCandidate candidate)
        {
            var candidateText = GetDefinitionCandidateText(candidate);
            foreach (var definition in Utilities)
            {
                if (!definition.IsFunctional)
                {
                    if (string.Equals(
                            candidateText,
                            definition.Name,
                            StringComparison.Ordinal))
                    {
                        yield return definition;
                    }

                    continue;
                }

                var root = definition.Name.Substring(
                    0,
                    definition.Name.Length - 2);
                if (candidateText == root ||
                    candidateText.StartsWith(
                        root + "-",
                        StringComparison.Ordinal))
                {
                    yield return definition;
                }
            }
        }

        private bool TryCreateFunctionalCandidate(
            UtilityCustomUtilityDefinition definition,
            UtilityCandidate candidate,
            out FunctionalCandidate? functionalCandidate)
        {
            functionalCandidate = null;
            if (!definition.IsFunctional)
            {
                return string.Equals(
                    definition.Name,
                    GetDefinitionCandidateText(candidate),
                    StringComparison.Ordinal);
            }

            var candidateText = GetDefinitionCandidateText(candidate);
            var root = definition.Name.Substring(
                0,
                definition.Name.Length - 2);
            if (candidateText == root)
            {
                functionalCandidate = new FunctionalCandidate(
                    null,
                    null,
                    null);
                return true;
            }

            if (!candidateText.StartsWith(
                    root + "-",
                    StringComparison.Ordinal))
            {
                return false;
            }

            var rawValue = candidateText.Substring(root.Length + 1);
            SplitFunctionalValue(
                rawValue,
                cancellationToken,
                out var value,
                out var modifier,
                out var fraction);
            functionalCandidate = new FunctionalCandidate(
                value,
                modifier,
                fraction);
            return true;
        }

        private static string GetDefinitionCandidateText(
            UtilityCandidate candidate) =>
            candidate.IsNegative
                ? "-" + candidate.UtilityText
                : candidate.UtilityText;

        private UtilityVariantRegistry GetVariantRegistry()
        {
            if (variantRegistry is not null)
            {
                return variantRegistry;
            }

            var definitions = options.Theme.VariantRegistry.Definitions.ToList();
            var names = new HashSet<string>(
                definitions.Select(definition => definition.Name),
                StringComparer.Ordinal);
            foreach (var variant in Variants)
            {
                if (names.Add(variant.Name))
                {
                    definitions.Add(
                        new UtilityVariantDefinition(
                            variant.Name,
                            UtilityVariantKind.Static,
                            UtilityVariantCategory.State));
                }
            }

            variantRegistry = new UtilityVariantRegistry(definitions);
            return variantRegistry;
        }

        private Dictionary<string, UtilityCustomVariantDefinition> GetVariantsByName()
        {
            if (variantsByName is not null)
            {
                return variantsByName;
            }

            variantsByName =
                new Dictionary<string, UtilityCustomVariantDefinition>(
                    StringComparer.Ordinal);
            foreach (var variant in Variants)
            {
                // A local definition takes precedence over an imported definition regardless of
                // where the @reference statement appears in the root stylesheet.
                if (!variantsByName.TryGetValue(
                        variant.Name,
                        out var existing) ||
                    (existing.IsReferenced && !variant.IsReferenced) ||
                    existing.IsReferenced == variant.IsReferenced)
                {
                    variantsByName[variant.Name] = variant;
                }
            }

            return variantsByName;
        }

        private static void ParseCustomVariant(
            UtilityDirective directive,
            bool isReferenced,
            out string? selector,
            out string? body)
        {
            _ = isReferenced;
            body = directive.Body;
            selector = null;
            if (directive.HasBlock ||
                directive.Identifier is null)
            {
                return;
            }

            var remainder = directive.Parameters.Substring(
                    directive.Identifier.Length)
                .Trim();
            if (remainder.Length >= 2 &&
                remainder[0] == '(' &&
                remainder[remainder.Length - 1] == ')')
            {
                selector = remainder.Substring(
                    1,
                    remainder.Length - 2)
                    .Trim();
            }
        }

        private bool TryApplyCustomVariant(
            UtilityCustomVariantDefinition definition,
            string body,
            UtilityStylesheetSourceSpan sourceSpan,
            out string transformed)
        {
            if (definition.Selector is not null)
            {
                transformed =
                    definition.Selector +
                    " {\n" +
                    Indent(body, 1) +
                    "\n}";
                return true;
            }

            if (applyingCustomVariantNames.Contains(
                    definition.Name,
                    StringComparer.Ordinal))
            {
                customVariantFailureCount++;
                Diagnostics.Add(
                    CreateDiagnostic(
                        UtilityProjectStylesheetDiagnosticCode.CircularVariant,
                        $"The custom variant '{definition.Name}' recursively composes itself through '{string.Join(" -> ", applyingCustomVariantNames)}'.",
                        sourceSpan));
                transformed = string.Empty;
                return false;
            }

            applyingCustomVariantNames.Add(definition.Name);
            var initialFailureCount = customVariantFailureCount;
            var template = definition.Body ?? string.Empty;
            try
            {
                transformed = RewriteFragment(
                    ReplaceSlot(
                        template,
                        body),
                    definition.SourceSpan.SourceIdentity ?? string.Empty,
                    definition.SourceSpan.Start,
                    null,
                    new List<string>(),
                    false);
                return customVariantFailureCount == initialFailureCount;
            }
            finally
            {
                applyingCustomVariantNames.RemoveAt(
                    applyingCustomVariantNames.Count - 1);
            }
        }

        private static string ReplaceSlot(
            string template,
            string body)
        {
            var builder = new StringBuilder(template.Length + body.Length);
            var cursor = 0;
            while (cursor < template.Length)
            {
                var slot = template.IndexOf(
                    "@slot",
                    cursor,
                    StringComparison.Ordinal);
                if (slot < 0)
                {
                    builder.Append(
                        template,
                        cursor,
                        template.Length - cursor);
                    break;
                }

                builder.Append(
                    template,
                    cursor,
                    slot - cursor);
                var end = slot + "@slot".Length;
                while (end < template.Length &&
                       char.IsWhiteSpace(template[end]))
                {
                    end++;
                }

                if (end < template.Length &&
                    template[end] == ';')
                {
                    end++;
                }

                builder.Append(body);
                cursor = end;
            }

            return builder.ToString().Trim();
        }

        private static bool TryCreateVariantTemplate(
            UtilityClassMetadata metadata,
            string body,
            out string transformed)
        {
            transformed = string.Empty;
            var selector = "." + UtilityCssText.EscapeClassName(metadata.CandidateText);
            var selectorStart = metadata.Css.IndexOf(
                selector,
                StringComparison.Ordinal);
            if (selectorStart < 0)
            {
                return false;
            }

            var openingBrace = metadata.Css.IndexOf(
                '{',
                selectorStart + selector.Length);
            if (openingBrace < 0)
            {
                return false;
            }

            var closingBrace = FindMatchingBrace(
                metadata.Css,
                openingBrace);
            if (closingBrace < 0)
            {
                return false;
            }

            const string placeholderDeclaration = "display: block;";
            var declarationStart = metadata.Css.IndexOf(
                placeholderDeclaration,
                openingBrace + 1,
                closingBrace - openingBrace - 1,
                StringComparison.Ordinal);
            if (declarationStart < 0)
            {
                return false;
            }

            var declarationIndentationStart = declarationStart;
            while (declarationIndentationStart > openingBrace + 1 &&
                   metadata.Css[declarationIndentationStart - 1] is ' ' or '\t')
            {
                declarationIndentationStart--;
            }

            var builder = new StringBuilder(
                metadata.Css.Length + body.Length);
            builder.Append(
                metadata.Css,
                0,
                selectorStart);
            builder.Append('&');
            builder.Append(
                metadata.Css,
                selectorStart + selector.Length,
                declarationIndentationStart - selectorStart - selector.Length);
            builder.Append(
                Indent(
                    body,
                    CountIndentation(
                        metadata.Css,
                        declarationStart)));
            builder.Append(
                metadata.Css,
                declarationStart + placeholderDeclaration.Length,
                metadata.Css.Length - declarationStart - placeholderDeclaration.Length);
            transformed = builder.ToString().Trim();
            return true;
        }

        private static bool TryExtractRuleBody(
            UtilityClassMetadata metadata,
            out string body)
        {
            body = string.Empty;
            var selector = "." + UtilityCssText.EscapeClassName(metadata.CandidateText);
            var selectorStart = metadata.Css.IndexOf(
                selector,
                StringComparison.Ordinal);
            if (selectorStart < 0)
            {
                return false;
            }

            var openingBrace = metadata.Css.IndexOf(
                '{',
                selectorStart + selector.Length);
            if (openingBrace < 0)
            {
                return false;
            }

            var closingBrace = FindMatchingBrace(
                metadata.Css,
                openingBrace);
            if (closingBrace < 0)
            {
                return false;
            }

            body = metadata.Css.Substring(
                    openingBrace + 1,
                    closingBrace - openingBrace - 1)
                .Trim();
            return true;
        }

        private static bool TryConvertRuleToNested(
            UtilityClassMetadata metadata,
            out string nested)
        {
            nested = string.Empty;
            var selector = "." + UtilityCssText.EscapeClassName(metadata.CandidateText);
            var selectorStart = metadata.Css.IndexOf(
                selector,
                StringComparison.Ordinal);
            if (selectorStart < 0)
            {
                return false;
            }

            nested = (
                    metadata.Css.Substring(
                        0,
                        selectorStart) +
                    "&" +
                    metadata.Css.Substring(selectorStart + selector.Length))
                .Trim();
            return true;
        }

        private static UtilityProjectStylesheetDiagnostic CreateDiagnostic(
            UtilityProjectStylesheetDiagnosticCode code,
            string message,
            UtilityStylesheetSourceSpan sourceSpan) =>
            new(
                code,
                UtilityProjectStylesheetDiagnosticSeverity.Error,
                message,
                sourceSpan);
    }

    private static void SplitFunctionalValue(
        string rawValue,
        CancellationToken cancellationToken,
        out FunctionalPart? value,
        out FunctionalPart? modifier,
        out string? fraction)
    {
        var separator = FindTopLevelSeparator(
            rawValue,
            '/');
        var valueText = separator < 0
            ? rawValue
            : rawValue.Substring(
                0,
                separator);
        var modifierText = separator < 0
            ? null
            : rawValue.Substring(separator + 1);
        value = CreateFunctionalPart(
            valueText,
            cancellationToken);
        modifier = modifierText is null
            ? null
            : CreateFunctionalPart(
                modifierText,
                cancellationToken);
        fraction =
            separator > 0 &&
            separator < rawValue.Length - 1 &&
            value?.Kind == FunctionalPartKind.Named &&
            modifier?.Kind == FunctionalPartKind.Named
                ? value.Value + "/" + modifier.Value
                : null;
    }

    private static FunctionalPart? CreateFunctionalPart(
        string rawText,
        CancellationToken cancellationToken)
    {
        var trimmed = rawText.Trim();
        if (trimmed.Length == 0)
        {
            return null;
        }

        if (trimmed.Length >= 2 &&
            trimmed[0] == '[' &&
            trimmed[trimmed.Length - 1] == ']')
        {
            var value = trimmed.Substring(
                1,
                trimmed.Length - 2);
            var typeSeparator = FindTopLevelSeparator(
                value,
                ':');
            string? dataType = null;
            if (typeSeparator > 0 &&
                IsSupportedArbitraryDataType(
                    value.Substring(
                        0,
                        typeSeparator)))
            {
                dataType = value.Substring(
                    0,
                    typeSeparator);
                value = value.Substring(typeSeparator + 1);
            }

            return new FunctionalPart(
                FunctionalPartKind.Arbitrary,
                UtilityTextGrammar.DecodeArbitraryValue(
                    value,
                    cancellationToken),
                trimmed,
                dataType);
        }

        if (trimmed.Length >= 3 &&
            trimmed[0] == '(' &&
            trimmed[trimmed.Length - 1] == ')')
        {
            var content = trimmed.Substring(
                1,
                trimmed.Length - 2);
            var typeSeparator = FindTopLevelSeparator(
                content,
                ':');
            string? dataType = null;
            var property = content;
            if (typeSeparator > 0 &&
                IsSupportedArbitraryDataType(
                    content.Substring(
                        0,
                        typeSeparator)))
            {
                dataType = content.Substring(
                    0,
                    typeSeparator);
                property = content.Substring(typeSeparator + 1);
            }

            if (!property.StartsWith(
                    "--",
                    StringComparison.Ordinal))
            {
                property = "--" + property;
            }

            return new FunctionalPart(
                FunctionalPartKind.Arbitrary,
                "var(" + property + ")",
                trimmed,
                dataType);
        }

        return new FunctionalPart(
            FunctionalPartKind.Named,
            trimmed,
            trimmed,
            null);
    }

    private static bool IsSupportedArbitraryDataType(string value) =>
        value is "*" or
            "absolute-size" or
            "angle" or
            "bg-size" or
            "color" or
            "family-name" or
            "generic-name" or
            "image" or
            "integer" or
            "length" or
            "line-width" or
            "number" or
            "percentage" or
            "position" or
            "ratio" or
            "relative-size" or
            "url" or
            "vector";

    private static string FormatDecimal(decimal value) =>
        value.ToString(
            "0.############################",
            CultureInfo.InvariantCulture);

    private static bool IsBareValueOfType(
        string value,
        string dataType)
    {
        switch (dataType)
        {
            case "integer":
                return IsCanonicalNonNegativeInteger(value);
            case "number":
                return decimal.TryParse(
                           value,
                           NumberStyles.Float,
                           CultureInfo.InvariantCulture,
                           out var number) &&
                       number >= decimal.Zero &&
                       number % 0.25m == decimal.Zero &&
                       string.Equals(
                           FormatDecimal(number),
                           value,
                           StringComparison.Ordinal);
            case "percentage":
                return value.EndsWith(
                           "%",
                           StringComparison.Ordinal) &&
                       IsCanonicalNonNegativeInteger(
                           value.Substring(
                               0,
                               value.Length - 1));
            case "ratio":
                var separator = value.IndexOf('/');
                return separator > 0 &&
                       separator < value.Length - 1 &&
                       IsCanonicalNonNegativeInteger(
                           value.Substring(
                               0,
                               separator)) &&
                       IsCanonicalNonNegativeInteger(
                           value.Substring(separator + 1));
            default:
                return false;
        }
    }

    private static bool IsCanonicalNonNegativeInteger(string value) =>
        int.TryParse(
            value,
            NumberStyles.None,
            CultureInfo.InvariantCulture,
            out var integer) &&
        integer >= 0 &&
        string.Equals(
            integer.ToString(CultureInfo.InvariantCulture),
            value,
            StringComparison.Ordinal);

    private static bool IsValueOfType(
        string value,
        string dataType)
    {
        if (string.IsNullOrWhiteSpace(value) ||
            !UtilityCssText.IsSafeValue(value))
        {
            return false;
        }

        switch (dataType)
        {
            case "*":
                return true;
            case "integer":
                return int.TryParse(
                    value,
                    NumberStyles.Integer,
                    CultureInfo.InvariantCulture,
                    out _);
            case "number":
                return decimal.TryParse(
                    value,
                    NumberStyles.Float,
                    CultureInfo.InvariantCulture,
                    out _);
            case "percentage":
                return value.EndsWith(
                           "%",
                           StringComparison.Ordinal) &&
                       decimal.TryParse(
                           value.Substring(
                               0,
                               value.Length - 1),
                           NumberStyles.Float,
                           CultureInfo.InvariantCulture,
                           out _);
            case "ratio":
                var separator = value.IndexOf('/');
                return separator > 0 &&
                       separator < value.Length - 1 &&
                       decimal.TryParse(
                           value.Substring(
                               0,
                               separator),
                           NumberStyles.Float,
                           CultureInfo.InvariantCulture,
                           out var numerator) &&
                       decimal.TryParse(
                           value.Substring(separator + 1),
                           NumberStyles.Float,
                           CultureInfo.InvariantCulture,
                           out var denominator) &&
                       numerator >= decimal.Zero &&
                       denominator > decimal.Zero;
            case "length":
            case "absolute-size":
            case "relative-size":
            case "angle":
            case "line-width":
                return IsCssDimension(value);
            case "color":
                return IsCssColor(value);
            case "url":
                return value.StartsWith(
                           "url(",
                           StringComparison.OrdinalIgnoreCase) ||
                       value.StartsWith(
                           "var(",
                           StringComparison.OrdinalIgnoreCase);
            case "image":
            case "bg-size":
            case "family-name":
            case "generic-name":
            case "position":
            case "vector":
                return true;
            default:
                return false;
        }
    }

    private static bool IsCssDimension(string value)
    {
        if (value == "0" ||
            value.StartsWith(
                "calc(",
                StringComparison.OrdinalIgnoreCase) ||
            value.StartsWith(
                "min(",
                StringComparison.OrdinalIgnoreCase) ||
            value.StartsWith(
                "max(",
                StringComparison.OrdinalIgnoreCase) ||
            value.StartsWith(
                "clamp(",
                StringComparison.OrdinalIgnoreCase) ||
            value.StartsWith(
                "var(",
                StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        var index = 0;
        if (value.Length != 0 &&
            value[0] is '+' or '-')
        {
            index++;
        }

        var hasDigit = false;
        var hasDecimal = false;
        while (index < value.Length)
        {
            var character = value[index];
            if (char.IsDigit(character))
            {
                hasDigit = true;
                index++;
                continue;
            }

            if (character == '.' &&
                !hasDecimal)
            {
                hasDecimal = true;
                index++;
                continue;
            }

            break;
        }

        return hasDigit &&
               index < value.Length &&
               value.Substring(index).All(
                   character =>
                       char.IsLetter(character) ||
                       character == '%');
    }

    private static bool IsCssColor(string value) =>
        value[0] == '#' ||
        value == "transparent" ||
        value == "currentColor" ||
        value.StartsWith(
            "rgb(",
            StringComparison.OrdinalIgnoreCase) ||
        value.StartsWith(
            "rgba(",
            StringComparison.OrdinalIgnoreCase) ||
        value.StartsWith(
            "hsl(",
            StringComparison.OrdinalIgnoreCase) ||
        value.StartsWith(
            "hsla(",
            StringComparison.OrdinalIgnoreCase) ||
        value.StartsWith(
            "oklab(",
            StringComparison.OrdinalIgnoreCase) ||
        value.StartsWith(
            "oklch(",
            StringComparison.OrdinalIgnoreCase) ||
        value.StartsWith(
            "color(",
            StringComparison.OrdinalIgnoreCase) ||
        value.StartsWith(
            "var(",
            StringComparison.OrdinalIgnoreCase);

    private static string DecodeQuoted(string value)
    {
        if (value.Length < 2)
        {
            return value;
        }

        var builder = new StringBuilder(value.Length - 2);
        var escaped = false;
        for (var index = 1; index < value.Length - 1; index++)
        {
            var character = value[index];
            if (escaped)
            {
                builder.Append(character);
                escaped = false;
                continue;
            }

            if (character == '\\')
            {
                escaped = true;
                continue;
            }

            builder.Append(character);
        }

        return builder.ToString();
    }

    private static string StripConfigurationDirectives(string css)
    {
        var builder = new StringBuilder(css.Length);
        var cursor = 0;
        var index = 0;
        while (index < css.Length)
        {
            if (IsCommentStart(
                    css,
                    index))
            {
                var commentEnd = FindCommentEnd(
                    css,
                    index + 2);
                index = commentEnd < 0
                    ? css.Length
                    : commentEnd;
                continue;
            }

            if (css[index] is '\'' or '"')
            {
                var stringEnd = FindStringEnd(
                    css,
                    index + 1,
                    css[index]);
                index = stringEnd < 0
                    ? css.Length
                    : stringEnd + 1;
                continue;
            }

            if (css[index] != '@' ||
                !TryReadAtRuleName(
                    css,
                    index,
                    out var name,
                    out var nameEnd))
            {
                index++;
                continue;
            }

            var removeEnd = -1;
            if (name == "theme")
            {
                var terminator = FindAtRuleTerminator(
                    css,
                    nameEnd);
                if (terminator >= 0 &&
                    css[terminator] == '{')
                {
                    var closingBrace = FindMatchingBrace(
                        css,
                        terminator);
                    removeEnd = closingBrace < 0
                        ? css.Length
                        : closingBrace + 1;
                }
            }
            else if (name == "source")
            {
                var terminator = FindAtRuleTerminator(
                    css,
                    nameEnd);
                if (terminator >= 0 &&
                    css[terminator] == ';')
                {
                    removeEnd = terminator + 1;
                }
            }
            else if (name == "import")
            {
                var terminator = FindAtRuleTerminator(
                    css,
                    nameEnd);
                if (terminator >= 0 &&
                    css[terminator] == ';' &&
                    TryReadImportSpecifier(
                        css,
                        nameEnd,
                        terminator,
                        out var specifier) &&
                    (specifier == "viu-utilities" ||
                     specifier == "tailwindcss" ||
                     specifier.StartsWith(
                         "tailwindcss/",
                         StringComparison.Ordinal) ||
                     specifier.StartsWith(
                         "@tailwindcss/",
                         StringComparison.Ordinal)))
                {
                    removeEnd = terminator + 1;
                }
            }

            if (removeEnd < 0)
            {
                index = nameEnd;
                continue;
            }

            builder.Append(
                css,
                cursor,
                index - cursor);
            AppendWhitespaceProjection(
                builder,
                css,
                index,
                removeEnd);
            cursor = removeEnd;
            index = removeEnd;
        }

        if (cursor < css.Length)
        {
            builder.Append(
                css,
                cursor,
                css.Length - cursor);
        }

        return builder.ToString();
    }

    private static bool TryReadAtRuleName(
        string css,
        int start,
        out string name,
        out int nameEnd)
    {
        nameEnd = start + 1;
        while (nameEnd < css.Length &&
               (IsAsciiLetter(css[nameEnd]) ||
                css[nameEnd] == '-'))
        {
            nameEnd++;
        }

        if (nameEnd == start + 1)
        {
            name = string.Empty;
            return false;
        }

        name = css.Substring(
            start + 1,
            nameEnd - start - 1);
        return true;
    }

    private static int FindAtRuleTerminator(
        string css,
        int start)
    {
        var parenthesisDepth = 0;
        var bracketDepth = 0;
        var index = start;
        while (index < css.Length)
        {
            if (IsCommentStart(
                    css,
                    index))
            {
                var commentEnd = FindCommentEnd(
                    css,
                    index + 2);
                if (commentEnd < 0)
                {
                    return -1;
                }

                index = commentEnd;
                continue;
            }

            if (css[index] is '\'' or '"')
            {
                var stringEnd = FindStringEnd(
                    css,
                    index + 1,
                    css[index]);
                if (stringEnd < 0)
                {
                    return -1;
                }

                index = stringEnd + 1;
                continue;
            }

            switch (css[index])
            {
                case '(':
                    parenthesisDepth++;
                    break;
                case ')':
                    parenthesisDepth = Math.Max(
                        0,
                        parenthesisDepth - 1);
                    break;
                case '[':
                    bracketDepth++;
                    break;
                case ']':
                    bracketDepth = Math.Max(
                        0,
                        bracketDepth - 1);
                    break;
                case ';' when parenthesisDepth == 0 && bracketDepth == 0:
                case '{' when parenthesisDepth == 0 && bracketDepth == 0:
                    return index;
            }

            index++;
        }

        return -1;
    }

    private static bool TryReadImportSpecifier(
        string css,
        int start,
        int end,
        out string specifier)
    {
        specifier = string.Empty;
        while (start < end &&
               char.IsWhiteSpace(css[start]))
        {
            start++;
        }

        if (start >= end ||
            css[start] is not ('\'' or '"'))
        {
            return false;
        }

        var quote = css[start++];
        var builder = new StringBuilder();
        var escaped = false;
        while (start < end)
        {
            var character = css[start++];
            if (escaped)
            {
                builder.Append(character);
                escaped = false;
                continue;
            }

            if (character == '\\')
            {
                escaped = true;
                continue;
            }

            if (character == quote)
            {
                specifier = builder.ToString();
                return true;
            }

            builder.Append(character);
        }

        return false;
    }

    private static void AppendWhitespaceProjection(
        StringBuilder builder,
        string css,
        int start,
        int end)
    {
        for (var index = start; index < end; index++)
        {
            builder.Append(
                css[index] is '\r' or '\n'
                    ? css[index]
                    : ' ');
        }
    }

    private static string AddImportantMarkers(string css)
    {
        var builder = new StringBuilder(css.Length + 32);
        var statementStart = 0;
        var index = 0;
        var parenthesisDepth = 0;
        var bracketDepth = 0;
        while (index < css.Length)
        {
            if (css[index] is '\'' or '"')
            {
                var stringEnd = FindStringEnd(
                    css,
                    index + 1,
                    css[index]);
                if (stringEnd < 0)
                {
                    break;
                }

                index = stringEnd + 1;
                continue;
            }

            switch (css[index])
            {
                case '(':
                    parenthesisDepth++;
                    break;
                case ')':
                    parenthesisDepth = Math.Max(
                        0,
                        parenthesisDepth - 1);
                    break;
                case '[':
                    bracketDepth++;
                    break;
                case ']':
                    bracketDepth = Math.Max(
                        0,
                        bracketDepth - 1);
                    break;
                case ';' when parenthesisDepth == 0 && bracketDepth == 0:
                    var statement = css.Substring(
                        statementStart,
                        index - statementStart);
                    builder.Append(
                        AddImportantToStatement(statement));
                    builder.Append(';');
                    statementStart = index + 1;
                    break;
                case '{' when parenthesisDepth == 0 && bracketDepth == 0:
                case '}' when parenthesisDepth == 0 && bracketDepth == 0:
                    builder.Append(
                        css,
                        statementStart,
                        index - statementStart + 1);
                    statementStart = index + 1;
                    break;
            }

            index++;
        }

        if (statementStart < css.Length)
        {
            builder.Append(
                css,
                statementStart,
                css.Length - statementStart);
        }

        return builder.ToString();
    }

    private static string AddImportantToStatement(string statement)
    {
        var colon = FindTopLevelSeparator(
            statement,
            ':');
        if (colon < 0 ||
            statement.TrimStart().StartsWith(
                "@",
                StringComparison.Ordinal) ||
            statement.IndexOf(
                "!important",
                StringComparison.OrdinalIgnoreCase) >= 0)
        {
            return statement;
        }

        var end = statement.Length;
        while (end > 0 &&
               char.IsWhiteSpace(statement[end - 1]))
        {
            end--;
        }

        return statement.Substring(
                   0,
                   end) +
               " !important" +
               statement.Substring(end);
    }

    private static string OmitUnresolvedDeclarations(string css) =>
        OmitDeclarationsContainingMarker(
            css,
            UnresolvedFunctionMarker);

    private static string OmitDeclarationsContainingMarker(
        string css,
        string unresolvedMarker)
    {
        var result = css;
        var marker = result.IndexOf(
            unresolvedMarker,
            StringComparison.Ordinal);
        while (marker >= 0)
        {
            var start = marker - 1;
            while (start >= 0 &&
                   result[start] is not (';' or '{' or '}'))
            {
                start--;
            }

            start++;
            var end = marker + unresolvedMarker.Length;
            while (end < result.Length &&
                   result[end] != ';')
            {
                end++;
            }

            if (end < result.Length)
            {
                end++;
            }

            result = result.Remove(
                start,
                end - start);
            marker = result.IndexOf(
                unresolvedMarker,
                StringComparison.Ordinal);
        }

        return result;
    }

    private static IEnumerable<CandidateToken> SplitCandidateList(
        string value,
        int sourceStart)
    {
        var result = new List<CandidateToken>();
        var tokenStart = 0;
        var index = 0;
        var closingDelimiters = new Stack<char>();
        char quote = '\0';
        var escaped = false;
        while (index <= value.Length)
        {
            var atEnd = index == value.Length;
            if (!atEnd)
            {
                var character = value[index];
                if (escaped)
                {
                    escaped = false;
                    index++;
                    continue;
                }

                if (character == '\\')
                {
                    escaped = true;
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

                if (character is '(' or '[' or '{')
                {
                    closingDelimiters.Push(
                        character == '('
                            ? ')'
                            : character == '['
                                ? ']'
                                : '}');
                    index++;
                    continue;
                }

                if (closingDelimiters.Count != 0 &&
                    character == closingDelimiters.Peek())
                {
                    closingDelimiters.Pop();
                    index++;
                    continue;
                }

                if (!char.IsWhiteSpace(character) ||
                    closingDelimiters.Count != 0)
                {
                    index++;
                    continue;
                }
            }

            var start = tokenStart;
            var end = index;
            while (start < end &&
                   char.IsWhiteSpace(value[start]))
            {
                start++;
            }

            while (end > start &&
                   char.IsWhiteSpace(value[end - 1]))
            {
                end--;
            }

            if (end > start)
            {
                result.Add(
                    new CandidateToken(
                        value.Substring(
                            start,
                            end - start),
                        sourceStart + start));
            }

            index++;
            tokenStart = index;
        }

        return result;
    }

    private static string[] SplitTopLevel(
        string value,
        char separator)
    {
        var result = new List<string>();
        var segmentStart = 0;
        var closingDelimiters = new Stack<char>();
        char quote = '\0';
        var escaped = false;
        for (var index = 0; index < value.Length; index++)
        {
            var character = value[index];
            if (escaped)
            {
                escaped = false;
                continue;
            }

            if (character == '\\')
            {
                escaped = true;
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

            if (character is '(' or '[' or '{')
            {
                closingDelimiters.Push(
                    character == '('
                        ? ')'
                        : character == '['
                            ? ']'
                            : '}');
                continue;
            }

            if (closingDelimiters.Count != 0 &&
                character == closingDelimiters.Peek())
            {
                closingDelimiters.Pop();
                continue;
            }

            if (character == separator &&
                closingDelimiters.Count == 0)
            {
                result.Add(
                    value.Substring(
                        segmentStart,
                        index - segmentStart));
                segmentStart = index + 1;
            }
        }

        result.Add(value.Substring(segmentStart));
        return result.ToArray();
    }

    private static int FindTopLevelSeparator(
        string value,
        char separator)
    {
        var closingDelimiters = new Stack<char>();
        char quote = '\0';
        var escaped = false;
        for (var index = 0; index < value.Length; index++)
        {
            var character = value[index];
            if (escaped)
            {
                escaped = false;
                continue;
            }

            if (character == '\\')
            {
                escaped = true;
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

            if (character is '(' or '[' or '{')
            {
                closingDelimiters.Push(
                    character == '('
                        ? ')'
                        : character == '['
                            ? ']'
                            : '}');
                continue;
            }

            if (closingDelimiters.Count != 0 &&
                character == closingDelimiters.Peek())
            {
                closingDelimiters.Pop();
                continue;
            }

            if (character == separator &&
                closingDelimiters.Count == 0)
            {
                return index;
            }
        }

        return -1;
    }

    private static bool TryReadFunction(
        string text,
        int index,
        out string functionName,
        out int openingParenthesis)
    {
        var names = new[]
        {
            "--value",
            "--modifier",
            "--default",
            "--spacing",
            "--alpha",
        };
        foreach (var name in names)
        {
            if (index + name.Length < text.Length &&
                text.Substring(
                    index,
                    name.Length) == name &&
                text[index + name.Length] == '(')
            {
                functionName = name;
                openingParenthesis = index + name.Length;
                return true;
            }
        }

        functionName = string.Empty;
        openingParenthesis = -1;
        return false;
    }

    private static int FindMatchingParenthesis(
        string text,
        int openingParenthesis)
    {
        var depth = 1;
        char quote = '\0';
        var escaped = false;
        for (var index = openingParenthesis + 1; index < text.Length; index++)
        {
            var character = text[index];
            if (escaped)
            {
                escaped = false;
                continue;
            }

            if (character == '\\')
            {
                escaped = true;
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

            if (character == '(')
            {
                depth++;
            }
            else if (character == ')' &&
                     --depth == 0)
            {
                return index;
            }
        }

        return -1;
    }

    private static int FindMatchingBrace(
        string text,
        int openingBrace)
    {
        var depth = 1;
        char quote = '\0';
        var escaped = false;
        for (var index = openingBrace + 1; index < text.Length; index++)
        {
            var character = text[index];
            if (escaped)
            {
                escaped = false;
                continue;
            }

            if (character == '\\')
            {
                escaped = true;
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

            if (IsCommentStart(
                    text,
                    index))
            {
                var commentEnd = FindCommentEnd(
                    text,
                    index + 2);
                if (commentEnd < 0)
                {
                    return -1;
                }

                index = commentEnd - 1;
                continue;
            }

            if (character == '{')
            {
                depth++;
            }
            else if (character == '}' &&
                     --depth == 0)
            {
                return index;
            }
        }

        return -1;
    }

    private static int FindCommentEnd(
        string text,
        int start)
    {
        for (var index = start; index + 1 < text.Length; index++)
        {
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
        char quote)
    {
        var escaped = false;
        for (var index = start; index < text.Length; index++)
        {
            if (escaped)
            {
                escaped = false;
                continue;
            }

            if (text[index] == '\\')
            {
                escaped = true;
                continue;
            }

            if (text[index] == quote)
            {
                return index;
            }
        }

        return -1;
    }

    private static bool IsCommentStart(
        string text,
        int index) =>
        index + 1 < text.Length &&
        text[index] == '/' &&
        text[index + 1] == '*';

    private static bool IsAsciiLetter(char value) =>
        value is >= 'a' and <= 'z' or >= 'A' and <= 'Z';

    private static int CountIndentation(
        string text,
        int index)
    {
        var lineStart = index;
        while (lineStart > 0 &&
               text[lineStart - 1] != '\n')
        {
            lineStart--;
        }

        var spaces = 0;
        while (lineStart + spaces < text.Length &&
               text[lineStart + spaces] == ' ')
        {
            spaces++;
        }

        return spaces / 2;
    }

    private static string Indent(
        string value,
        int depth)
    {
        var indentation = new string(
            ' ',
            Math.Max(
                0,
                depth) * 2);
        var lines = NormalizeLineEndings(value).Split('\n');
        var builder = new StringBuilder(value.Length + (lines.Length * indentation.Length));
        for (var index = 0; index < lines.Length; index++)
        {
            if (index != 0)
            {
                builder.Append('\n');
            }

            if (lines[index].Length != 0)
            {
                builder.Append(indentation);
                builder.Append(lines[index]);
            }
        }

        return builder.ToString();
    }

    private static void AppendSeparated(
        StringBuilder builder,
        string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return;
        }

        if (builder.Length != 0)
        {
            builder.Append('\n');
        }

        builder.Append(value.Trim());
    }

    private sealed class StylesheetDocument
    {
        public StylesheetDocument(
            string sourceIdentity,
            string css,
            UtilityStylesheetParseResult parseResult,
            bool isReferenced)
        {
            SourceIdentity = sourceIdentity;
            Css = css;
            ParseResult = parseResult;
            IsReferenced = isReferenced;
        }

        public string SourceIdentity { get; }

        public string Css { get; }

        public UtilityStylesheetParseResult ParseResult { get; }

        public bool IsReferenced { get; }
    }

    private sealed class FunctionalCandidate
    {
        public FunctionalCandidate(
            FunctionalPart? value,
            FunctionalPart? modifier,
            string? fraction)
        {
            Value = value;
            Modifier = modifier;
            Fraction = fraction;
        }

        public FunctionalPart? Value { get; }

        public FunctionalPart? Modifier { get; }

        public string? Fraction { get; }

        public bool UsedValue { get; set; }

        public bool ResolvedValue { get; set; }

        public bool UsedModifier { get; set; }

        public bool ResolvedModifier { get; set; }

        public bool ResolvedRatio { get; set; }
    }

    private sealed class FunctionalPart
    {
        public FunctionalPart(
            FunctionalPartKind kind,
            string value,
            string rawText,
            string? dataType)
        {
            Kind = kind;
            Value = value;
            RawText = rawText;
            DataType = dataType;
        }

        public FunctionalPartKind Kind { get; }

        public string Value { get; }

        public string NamedText => Value;

        public string RawText { get; }

        public string? DataType { get; }
    }

    private enum FunctionalPartKind
    {
        Named,
        Arbitrary,
    }

    private readonly struct CandidateToken
    {
        public CandidateToken(
            string text,
            int start)
        {
            Text = text;
            Start = start;
        }

        public string Text { get; }

        public int Start { get; }
    }
}
