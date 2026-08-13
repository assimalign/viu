using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace Assimalign.Viu.UtilityCss;

internal static class UtilityCandidateParseEngine
{
    public static UtilityCandidateParseResult Parse(
        string? rawText,
        string? prefix,
        UtilityVariantRegistry registry,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var diagnostics = new List<UtilityCandidateDiagnostic>();

        if (string.IsNullOrWhiteSpace(rawText))
        {
            AddError(
                diagnostics,
                UtilityCandidateDiagnosticCode.EmptyCandidate,
                "A utility candidate cannot be empty.",
                0,
                rawText?.Length ?? 0);
            return Failure(diagnostics);
        }

        var candidateText = rawText!;
        if (!UtilityTextGrammar.TrySegment(
                candidateText,
                ':',
                0,
                diagnostics,
                cancellationToken,
                out var candidateSegments))
        {
            return Failure(diagnostics);
        }

        if (candidateSegments.Any(segment => segment.Text.Length == 0))
        {
            var emptySegment = candidateSegments.First(segment => segment.Text.Length == 0);
            AddError(
                diagnostics,
                UtilityCandidateDiagnosticCode.EmptySegment,
                "A variant or utility segment cannot be empty.",
                emptySegment.Start,
                0);
            return Failure(diagnostics);
        }

        var baseSegment = candidateSegments[candidateSegments.Count - 1];
        var rawVariantCount = candidateSegments.Count - 1;
        var variants = new List<UtilityVariant>();
        var variantStartIndex = 0;

        if (!string.IsNullOrEmpty(prefix))
        {
            var configuredPrefix = prefix!;
            if (!IsValidPrefix(configuredPrefix))
            {
                AddError(
                    diagnostics,
                    UtilityCandidateDiagnosticCode.PrefixMismatch,
                    "The configured prefix must be a lowercase ASCII identifier.",
                    0,
                    Math.Min(candidateText.Length, configuredPrefix.Length));
                return Failure(diagnostics);
            }

            if (rawVariantCount == 0 ||
                !string.Equals(candidateSegments[0].Text, configuredPrefix, StringComparison.Ordinal))
            {
                AddError(
                    diagnostics,
                    UtilityCandidateDiagnosticCode.PrefixMismatch,
                    $"The configured prefix '{configuredPrefix}' must be the first variant.",
                    0,
                    rawVariantCount == 0 ? 0 : candidateSegments[0].Text.Length);
                return Failure(diagnostics);
            }

            variants.Add(
                new UtilityVariant(
                    UtilityVariantKind.Prefix,
                    UtilityVariantCategory.Prefix,
                    configuredPrefix,
                    configuredPrefix,
                    0,
                    null,
                    null,
                    null,
                    null,
                    false));
            variantStartIndex = 1;
        }

        for (var index = variantStartIndex; index < rawVariantCount; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var segment = candidateSegments[index];
            var sourceOrder = variants.Count;
            var variant = ParseVariant(
                segment.Text,
                segment.Start,
                sourceOrder,
                registry,
                diagnostics,
                cancellationToken);
            if (variant is null)
            {
                return Failure(diagnostics);
            }

            variants.Add(variant);
        }

        var baseText = baseSegment.Text;
        var importantMarker = UtilityImportantMarker.None;
        var hasLeadingImportantMarker = baseText.Length != 0 && baseText[0] == '!';
        var hasTrailingImportantMarker = baseText.Length != 0 && baseText[baseText.Length - 1] == '!';

        if (hasLeadingImportantMarker && hasTrailingImportantMarker)
        {
            AddError(
                diagnostics,
                UtilityCandidateDiagnosticCode.InvalidImportantMarker,
                "A utility candidate cannot use both leading and trailing important markers.",
                baseSegment.Start,
                baseText.Length);
            return Failure(diagnostics);
        }

        if (hasTrailingImportantMarker)
        {
            importantMarker = UtilityImportantMarker.Trailing;
            baseText = baseText.Substring(0, baseText.Length - 1);
        }
        else if (hasLeadingImportantMarker)
        {
            importantMarker = UtilityImportantMarker.DeprecatedLeading;
            baseText = baseText.Substring(1);
            diagnostics.Add(
                new UtilityCandidateDiagnostic(
                    UtilityCandidateDiagnosticCode.DeprecatedLeadingImportantMarker,
                    UtilityCandidateDiagnosticSeverity.Warning,
                    "Leading important syntax is deprecated; use a trailing '!' marker.",
                    baseSegment.Start,
                    1));
        }

        if (baseText.Length == 0)
        {
            AddError(
                diagnostics,
                UtilityCandidateDiagnosticCode.InvalidImportantMarker,
                "The important marker must be attached to a utility.",
                baseSegment.Start,
                baseSegment.Text.Length);
            return Failure(diagnostics);
        }

        var canonicalBase = importantMarker == UtilityImportantMarker.None
            ? baseText
            : baseText + "!";
        var canonicalSegments = candidateSegments
            .Take(rawVariantCount)
            .Select(segment => segment.Text)
            .Concat(new[] { canonicalBase });
        var canonicalText = string.Join(":", canonicalSegments);

        var isNegative = baseText[0] == '-';
        var utilityText = isNegative ? baseText.Substring(1) : baseText;
        if (utilityText.Length == 0)
        {
            AddError(
                diagnostics,
                UtilityCandidateDiagnosticCode.InvalidNegativeForm,
                "The negative marker must be followed by a utility.",
                baseSegment.Start,
                baseSegment.Text.Length);
            return Failure(diagnostics);
        }

        if (!UtilityTextGrammar.TrySegment(
                utilityText,
                '/',
                baseSegment.Start + (isNegative ? 1 : 0),
                diagnostics,
                cancellationToken,
                out var utilitySegments))
        {
            return Failure(diagnostics);
        }

        if (utilitySegments.Count > 2)
        {
            AddError(
                diagnostics,
                UtilityCandidateDiagnosticCode.InvalidModifier,
                "A utility candidate can contain only one top-level slash modifier.",
                utilitySegments[2].Start - 1,
                utilitySegments[2].Text.Length + 1);
            return Failure(diagnostics);
        }

        var baseWithoutModifier = utilitySegments[0].Text;
        if (baseWithoutModifier.Length == 0)
        {
            AddError(
                diagnostics,
                UtilityCandidateDiagnosticCode.InvalidUtility,
                "The utility root cannot be empty.",
                utilitySegments[0].Start,
                0);
            return Failure(diagnostics);
        }

        UtilityModifier? modifier = null;
        if (utilitySegments.Count == 2)
        {
            modifier = ParseModifier(
                utilitySegments[1].Text,
                utilitySegments[1].Start,
                diagnostics,
                cancellationToken);
            if (modifier is null)
            {
                return Failure(diagnostics);
            }
        }

        UtilityCandidate? candidate;
        if (baseWithoutModifier[0] == '[')
        {
            candidate = ParseArbitraryProperty(
                candidateText,
                canonicalText,
                baseWithoutModifier,
                utilityText,
                modifier,
                isNegative,
                variants,
                importantMarker,
                baseSegment.Start,
                diagnostics,
                cancellationToken);
        }
        else
        {
            candidate = ParseNamedUtility(
                candidateText,
                canonicalText,
                baseWithoutModifier,
                utilityText,
                modifier,
                isNegative,
                variants,
                importantMarker,
                baseSegment.Start,
                diagnostics,
                cancellationToken);
        }

        return candidate is null
            ? Failure(diagnostics)
            : new UtilityCandidateParseResult(
                candidate,
                new UtilityCollection<UtilityCandidateDiagnostic>(diagnostics));
    }

    private static UtilityCandidate? ParseArbitraryProperty(
        string rawText,
        string canonicalText,
        string baseWithoutModifier,
        string utilityText,
        UtilityModifier? modifier,
        bool isNegative,
        IEnumerable<UtilityVariant> variants,
        UtilityImportantMarker importantMarker,
        int sourceStart,
        ICollection<UtilityCandidateDiagnostic> diagnostics,
        CancellationToken cancellationToken)
    {
        if (isNegative)
        {
            AddError(
                diagnostics,
                UtilityCandidateDiagnosticCode.InvalidArbitraryProperty,
                "An arbitrary property cannot use the negative form.",
                sourceStart,
                baseWithoutModifier.Length);
            return null;
        }

        if (baseWithoutModifier.Length < 3 ||
            baseWithoutModifier[baseWithoutModifier.Length - 1] != ']')
        {
            AddError(
                diagnostics,
                UtilityCandidateDiagnosticCode.InvalidArbitraryProperty,
                "An arbitrary property must end with ']'.",
                sourceStart,
                baseWithoutModifier.Length);
            return null;
        }

        var content = baseWithoutModifier.Substring(1, baseWithoutModifier.Length - 2);
        var separatorIndex = content.IndexOf(':');
        if (separatorIndex <= 0 || separatorIndex == content.Length - 1)
        {
            AddError(
                diagnostics,
                UtilityCandidateDiagnosticCode.InvalidArbitraryProperty,
                "An arbitrary property requires a property and value separated by ':'.",
                sourceStart,
                baseWithoutModifier.Length);
            return null;
        }

        var property = content.Substring(0, separatorIndex);
        var firstPropertyCharacter = property[0];
        if (firstPropertyCharacter != '-' &&
            (firstPropertyCharacter < 'a' || firstPropertyCharacter > 'z'))
        {
            AddError(
                diagnostics,
                UtilityCandidateDiagnosticCode.InvalidArbitraryProperty,
                "An arbitrary property must begin with a lowercase letter or vendor-prefix dash.",
                sourceStart + 1,
                property.Length);
            return null;
        }

        var rawValue = content.Substring(separatorIndex + 1);
        if (!UtilityTextGrammar.IsValidArbitraryValue(rawValue, cancellationToken))
        {
            AddError(
                diagnostics,
                UtilityCandidateDiagnosticCode.InvalidArbitraryValue,
                "The arbitrary property value is structurally invalid.",
                sourceStart + separatorIndex + 2,
                rawValue.Length);
            return null;
        }

        var decodedValue = UtilityTextGrammar.DecodeArbitraryValue(
            rawValue,
            cancellationToken);
        if (string.IsNullOrWhiteSpace(decodedValue))
        {
            AddError(
                diagnostics,
                UtilityCandidateDiagnosticCode.InvalidArbitraryValue,
                "The arbitrary property value cannot be empty.",
                sourceStart + separatorIndex + 2,
                rawValue.Length);
            return null;
        }

        return new UtilityCandidate(
            rawText,
            canonicalText,
            UtilityCandidateKind.ArbitraryProperty,
            property,
            utilityText,
            new UtilityValue(
                UtilityValueKind.Arbitrary,
                decodedValue,
                "[" + rawValue + "]",
                null,
                null),
            modifier,
            false,
            new UtilityCollection<UtilityVariant>(variants),
            importantMarker);
    }

    private static UtilityCandidate? ParseNamedUtility(
        string rawText,
        string canonicalText,
        string baseWithoutModifier,
        string utilityText,
        UtilityModifier? modifier,
        bool isNegative,
        IEnumerable<UtilityVariant> variants,
        UtilityImportantMarker importantMarker,
        int sourceStart,
        ICollection<UtilityCandidateDiagnostic> diagnostics,
        CancellationToken cancellationToken)
    {
        string root;
        UtilityValue? value;

        if (baseWithoutModifier[baseWithoutModifier.Length - 1] == ']')
        {
            var markerIndex = baseWithoutModifier.IndexOf("-[", StringComparison.Ordinal);
            if (markerIndex <= 0)
            {
                AddError(
                    diagnostics,
                    UtilityCandidateDiagnosticCode.InvalidArbitraryValue,
                    "An arbitrary utility value must follow a named root and '-['.",
                    sourceStart,
                    baseWithoutModifier.Length);
                return null;
            }

            root = baseWithoutModifier.Substring(0, markerIndex);
            var rawValue = baseWithoutModifier.Substring(
                markerIndex + 2,
                baseWithoutModifier.Length - markerIndex - 3);
            if (!TryParseArbitraryUtilityValue(
                    rawValue,
                    sourceStart + markerIndex + 2,
                    diagnostics,
                    cancellationToken,
                    out value))
            {
                return null;
            }
        }
        else if (baseWithoutModifier[baseWithoutModifier.Length - 1] == ')')
        {
            var markerIndex = baseWithoutModifier.IndexOf("-(", StringComparison.Ordinal);
            if (markerIndex <= 0)
            {
                AddError(
                    diagnostics,
                    UtilityCandidateDiagnosticCode.InvalidArbitraryValue,
                    "CSS-variable shorthand must follow a named root and '-('.",
                    sourceStart,
                    baseWithoutModifier.Length);
                return null;
            }

            root = baseWithoutModifier.Substring(0, markerIndex);
            var rawValue = baseWithoutModifier.Substring(
                markerIndex + 2,
                baseWithoutModifier.Length - markerIndex - 3);
            value = ParseCssVariableUtilityValue(
                rawValue,
                sourceStart + markerIndex + 2,
                diagnostics,
                cancellationToken);
            if (value is null)
            {
                return null;
            }
        }
        else
        {
            UtilityRootCatalog.SplitNamedUtility(
                baseWithoutModifier,
                out root,
                out var namedValue);
            if (namedValue is null)
            {
                value = null;
            }
            else
            {
                if (!UtilityTextGrammar.IsValidNamedValue(namedValue))
                {
                    AddError(
                        diagnostics,
                        UtilityCandidateDiagnosticCode.InvalidUtility,
                        "The named utility value contains an unsupported character.",
                        sourceStart + root.Length + 1,
                        namedValue.Length);
                    return null;
                }

                var fraction = modifier?.Kind == UtilityModifierKind.Named
                    ? namedValue + "/" + modifier.RawText
                    : null;
                value = new UtilityValue(
                    UtilityValueKind.Named,
                    namedValue,
                    namedValue,
                    null,
                    fraction);
            }
        }

        if (!IsValidRoot(root))
        {
            AddError(
                diagnostics,
                UtilityCandidateDiagnosticCode.InvalidUtility,
                "The utility root is malformed.",
                sourceStart,
                root.Length);
            return null;
        }

        return new UtilityCandidate(
            rawText,
            canonicalText,
            UtilityCandidateKind.Named,
            root,
            utilityText,
            value,
            modifier,
            isNegative,
            new UtilityCollection<UtilityVariant>(variants),
            importantMarker);
    }

    private static bool TryParseArbitraryUtilityValue(
        string rawValue,
        int sourceStart,
        ICollection<UtilityCandidateDiagnostic> diagnostics,
        CancellationToken cancellationToken,
        out UtilityValue? value)
    {
        value = null;
        if (!UtilityTextGrammar.IsValidArbitraryValue(rawValue, cancellationToken))
        {
            AddError(
                diagnostics,
                UtilityCandidateDiagnosticCode.InvalidArbitraryValue,
                "The arbitrary utility value is structurally invalid.",
                sourceStart,
                rawValue.Length);
            return false;
        }

        var decodedValue = UtilityTextGrammar.DecodeArbitraryValue(
            rawValue,
            cancellationToken);
        if (!UtilityTextGrammar.TryGetArbitraryTypeHint(
                decodedValue,
                out var dataType,
                out var valueText) ||
            string.IsNullOrWhiteSpace(valueText))
        {
            AddError(
                diagnostics,
                UtilityCandidateDiagnosticCode.InvalidArbitraryValue,
                "The arbitrary utility value cannot be empty and any type hint must be named.",
                sourceStart,
                rawValue.Length);
            return false;
        }

        valueText = UtilityCssText.AddWhitespaceAroundMathOperators(
            valueText);
        value = new UtilityValue(
            UtilityValueKind.Arbitrary,
            valueText,
            "[" + rawValue + "]",
            dataType,
            null);
        return true;
    }

    private static UtilityValue? ParseCssVariableUtilityValue(
        string rawValue,
        int sourceStart,
        ICollection<UtilityCandidateDiagnostic> diagnostics,
        CancellationToken cancellationToken)
    {
        if (!UtilityTextGrammar.TrySegment(
                rawValue,
                ':',
                sourceStart,
                diagnostics,
                cancellationToken,
                out var valueSegments))
        {
            return null;
        }

        string? dataType = null;
        string variable;
        if (valueSegments.Count == 2)
        {
            dataType = valueSegments[0].Text;
            variable = valueSegments[1].Text;
            if (dataType.Length == 0)
            {
                AddError(
                    diagnostics,
                    UtilityCandidateDiagnosticCode.InvalidArbitraryValue,
                    "A CSS-variable type hint cannot be empty.",
                    sourceStart,
                    0);
                return null;
            }
        }
        else if (valueSegments.Count == 1)
        {
            variable = valueSegments[0].Text;
        }
        else
        {
            AddError(
                diagnostics,
                UtilityCandidateDiagnosticCode.InvalidArbitraryValue,
                "CSS-variable shorthand can contain at most one top-level type hint.",
                sourceStart,
                rawValue.Length);
            return null;
        }

        if (dataType is not null &&
            variable.Length != 0 &&
            !variable.StartsWith("--", StringComparison.Ordinal))
        {
            // Compatibility reference:
            // https://tailwindcss.com/docs/adding-custom-styles#functional-utilities
            variable = "--" + variable;
        }

        if (!variable.StartsWith("--", StringComparison.Ordinal) ||
            variable.Length == 2 ||
            !UtilityTextGrammar.IsValidArbitraryValue(variable, cancellationToken))
        {
            AddError(
                diagnostics,
                UtilityCandidateDiagnosticCode.InvalidArbitraryValue,
                "CSS-variable shorthand must begin with '--' and contain a valid value.",
                sourceStart + (dataType is null ? 0 : dataType.Length + 1),
                variable.Length);
            return null;
        }

        var decodedVariable = UtilityTextGrammar.DecodeArbitraryValue(
            "var(" + variable + ")",
            cancellationToken);
        return new UtilityValue(
            UtilityValueKind.CssVariable,
            decodedVariable,
            "(" + rawValue + ")",
            dataType,
            null);
    }

    private static UtilityModifier? ParseModifier(
        string text,
        int sourceStart,
        ICollection<UtilityCandidateDiagnostic> diagnostics,
        CancellationToken cancellationToken)
    {
        if (text.Length == 0)
        {
            AddError(
                diagnostics,
                UtilityCandidateDiagnosticCode.InvalidModifier,
                "A slash modifier cannot be empty.",
                sourceStart,
                0);
            return null;
        }

        if (text[0] == '[')
        {
            if (text.Length < 3 || text[text.Length - 1] != ']')
            {
                AddError(
                    diagnostics,
                    UtilityCandidateDiagnosticCode.InvalidModifier,
                    "An arbitrary slash modifier must end with ']'.",
                    sourceStart,
                    text.Length);
                return null;
            }

            var rawValue = text.Substring(1, text.Length - 2);
            if (!UtilityTextGrammar.IsValidArbitraryValue(rawValue, cancellationToken))
            {
                AddError(
                    diagnostics,
                    UtilityCandidateDiagnosticCode.InvalidModifier,
                    "The arbitrary slash modifier is structurally invalid.",
                    sourceStart + 1,
                    rawValue.Length);
                return null;
            }

            var decodedValue = UtilityTextGrammar.DecodeArbitraryValue(
                rawValue,
                cancellationToken);
            if (string.IsNullOrWhiteSpace(decodedValue))
            {
                AddError(
                    diagnostics,
                    UtilityCandidateDiagnosticCode.InvalidModifier,
                    "An arbitrary slash modifier cannot be empty.",
                    sourceStart + 1,
                    rawValue.Length);
                return null;
            }

            return new UtilityModifier(
                UtilityModifierKind.Arbitrary,
                decodedValue,
                text);
        }

        if (text[0] == '(')
        {
            if (text.Length < 4 || text[text.Length - 1] != ')')
            {
                AddError(
                    diagnostics,
                    UtilityCandidateDiagnosticCode.InvalidModifier,
                    "A CSS-variable slash modifier must end with ')'.",
                    sourceStart,
                    text.Length);
                return null;
            }

            var variable = text.Substring(1, text.Length - 2);
            if (!variable.StartsWith("--", StringComparison.Ordinal) ||
                !UtilityTextGrammar.IsValidArbitraryValue(variable, cancellationToken))
            {
                AddError(
                    diagnostics,
                    UtilityCandidateDiagnosticCode.InvalidModifier,
                    "A CSS-variable slash modifier must begin with '--'.",
                    sourceStart + 1,
                    variable.Length);
                return null;
            }

            return new UtilityModifier(
                UtilityModifierKind.CssVariable,
                UtilityTextGrammar.DecodeArbitraryValue(
                    "var(" + variable + ")",
                    cancellationToken),
                text);
        }

        if (!UtilityTextGrammar.IsValidNamedValue(text))
        {
            AddError(
                diagnostics,
                UtilityCandidateDiagnosticCode.InvalidModifier,
                "The named slash modifier contains an unsupported character.",
                sourceStart,
                text.Length);
            return null;
        }

        return new UtilityModifier(
            UtilityModifierKind.Named,
            text,
            text);
    }

    private static UtilityVariant? ParseVariant(
        string text,
        int sourceStart,
        int sourceOrder,
        UtilityVariantRegistry registry,
        ICollection<UtilityCandidateDiagnostic> diagnostics,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (text.Length == 0)
        {
            AddError(
                diagnostics,
                UtilityCandidateDiagnosticCode.EmptySegment,
                "A variant cannot be empty.",
                sourceStart,
                0);
            return null;
        }

        if (text[0] == '[')
        {
            return ParseArbitraryVariant(
                text,
                sourceStart,
                sourceOrder,
                diagnostics,
                cancellationToken);
        }

        if (!UtilityTextGrammar.TrySegment(
                text,
                '/',
                sourceStart,
                diagnostics,
                cancellationToken,
                out var modifierSegments))
        {
            return null;
        }

        if (modifierSegments.Count > 2)
        {
            AddError(
                diagnostics,
                UtilityCandidateDiagnosticCode.InvalidVariant,
                "A variant can contain only one top-level slash modifier.",
                modifierSegments[2].Start - 1,
                modifierSegments[2].Text.Length + 1);
            return null;
        }

        var variantWithoutModifier = modifierSegments[0].Text;
        UtilityModifier? modifier = null;
        if (modifierSegments.Count == 2)
        {
            modifier = ParseModifier(
                modifierSegments[1].Text,
                modifierSegments[1].Start,
                diagnostics,
                cancellationToken);
            if (modifier is null)
            {
                return null;
            }
        }

        if (!TryFindVariantRoot(
                variantWithoutModifier,
                registry,
                out var definition,
                out var rawValue))
        {
            AddError(
                diagnostics,
                UtilityCandidateDiagnosticCode.UnknownVariant,
                $"The variant '{variantWithoutModifier}' is not in the v4.3.3 registry.",
                sourceStart,
                variantWithoutModifier.Length);
            return null;
        }

        if (definition.Kind == UtilityVariantKind.Static)
        {
            if (rawValue is not null || modifier is not null)
            {
                AddError(
                    diagnostics,
                    UtilityCandidateDiagnosticCode.InvalidVariant,
                    $"The static variant '{definition.Name}' cannot have a value or modifier.",
                    sourceStart,
                    text.Length);
                return null;
            }

            return new UtilityVariant(
                UtilityVariantKind.Static,
                definition.Category,
                definition.Name,
                text,
                sourceOrder,
                null,
                null,
                null,
                null,
                false);
        }

        if (definition.Kind == UtilityVariantKind.Functional)
        {
            UtilityVariantValue? value = null;
            if (rawValue is not null)
            {
                value = ParseVariantValue(
                    rawValue,
                    sourceStart + definition.Name.Length + 1,
                    diagnostics,
                    cancellationToken);
                if (value is null)
                {
                    return null;
                }

                if (definition.Category == UtilityVariantCategory.Attribute &&
                    value.Kind == UtilityValueKind.Arbitrary &&
                    !IsValidArbitraryAttributeValue(value.Text))
                {
                    AddError(
                        diagnostics,
                        UtilityCandidateDiagnosticCode.InvalidVariant,
                        $"The arbitrary attribute variant '{definition.Name}' does not contain a valid CSS attribute selector value.",
                        sourceStart,
                        text.Length);
                    return null;
                }
            }

            return new UtilityVariant(
                UtilityVariantKind.Functional,
                definition.Category,
                definition.Name,
                text,
                sourceOrder,
                value,
                modifier,
                null,
                null,
                false);
        }

        if (definition.Kind == UtilityVariantKind.Compound)
        {
            if (rawValue is null || rawValue.Length == 0)
            {
                AddError(
                    diagnostics,
                    UtilityCandidateDiagnosticCode.InvalidVariant,
                    $"The compound variant '{definition.Name}' requires a nested variant.",
                    sourceStart,
                    text.Length);
                return null;
            }

            var nestedText = rawValue;
            UtilityModifier? outerModifier = modifier;
            if (modifier is not null &&
                (definition.Name == "not" ||
                 definition.Name == "has" ||
                 definition.Name == "in"))
            {
                nestedText += "/" + modifier.RawText;
                outerModifier = null;
            }

            var nestedVariant = ParseVariant(
                nestedText,
                sourceStart + definition.Name.Length + 1,
                sourceOrder,
                registry,
                diagnostics,
                cancellationToken);
            if (nestedVariant is null)
            {
                return null;
            }

            if (!CanCompoundWith(
                    definition.Name,
                    nestedVariant))
            {
                AddError(
                    diagnostics,
                    UtilityCandidateDiagnosticCode.InvalidVariant,
                    $"The compound variant '{definition.Name}' cannot wrap '{nestedVariant.RawText}'.",
                    sourceStart,
                    text.Length);
                return null;
            }

            return new UtilityVariant(
                UtilityVariantKind.Compound,
                definition.Category,
                definition.Name,
                text,
                sourceOrder,
                null,
                outerModifier,
                nestedVariant,
                null,
                false);
        }

        AddError(
            diagnostics,
            UtilityCandidateDiagnosticCode.InvalidVariant,
            $"The registered variant '{definition.Name}' has an unsupported parser kind.",
            sourceStart,
            text.Length);
        return null;
    }

    private static bool CanCompoundWith(
        string parent,
        UtilityVariant child)
    {
        // Compatibility reference:
        // https://github.com/tailwindlabs/tailwindcss/blob/v4.3.3/packages/tailwindcss/src/variants.ts
        var acceptedKinds = parent == "not"
            ? UtilityCompoundRuleKind.StyleRule | UtilityCompoundRuleKind.AtRule
            : UtilityCompoundRuleKind.StyleRule;
        var generatedKinds = GetGeneratedRuleKinds(child);
        if ((acceptedKinds & generatedKinds) == UtilityCompoundRuleKind.Never)
        {
            return false;
        }

        if (child.Kind == UtilityVariantKind.Arbitrary &&
            child.IsRelativeSelector &&
            parent is "group" or "not" or "peer")
        {
            return false;
        }

        // Tailwind v4.3.3 rejects group-not-hover and peer-not-hover: hover
        // produces a style rule containing a media rule, and applying not
        // produces nested rules that selector compounds cannot transform.
        return parent is not ("group" or "has" or "in" or "peer") ||
               child.Root != "not" ||
               child.NestedVariant?.Root != "hover";
    }

    private static UtilityCompoundRuleKind GetGeneratedRuleKinds(
        UtilityVariant variant)
    {
        if (variant.Kind == UtilityVariantKind.Arbitrary)
        {
            var selector = variant.Selector;
            if (string.IsNullOrEmpty(selector))
            {
                return UtilityCompoundRuleKind.Never;
            }

            if (selector![0] == '@')
            {
                return selector.StartsWith("@media", StringComparison.Ordinal) ||
                       selector.StartsWith("@supports", StringComparison.Ordinal) ||
                       selector.StartsWith("@container", StringComparison.Ordinal)
                    ? UtilityCompoundRuleKind.AtRule
                    : UtilityCompoundRuleKind.Never;
            }

            return selector.IndexOf(
                    "::",
                    StringComparison.Ordinal) >= 0
                ? UtilityCompoundRuleKind.Never
                : UtilityCompoundRuleKind.StyleRule;
        }

        switch (variant.Category)
        {
            case UtilityVariantCategory.Child:
            case UtilityVariantCategory.Descendant:
            case UtilityVariantCategory.PseudoElement:
                return UtilityCompoundRuleKind.Never;

            case UtilityVariantCategory.ContainerQuery:
            case UtilityVariantCategory.Print:
            case UtilityVariantCategory.Responsive:
            case UtilityVariantCategory.Supports:
                return UtilityCompoundRuleKind.AtRule;

            case UtilityVariantCategory.Environment:
                return variant.Root == "starting"
                    ? UtilityCompoundRuleKind.Never
                    : UtilityCompoundRuleKind.AtRule;

            default:
                return UtilityCompoundRuleKind.StyleRule;
        }
    }

    private static bool IsValidArbitraryAttributeValue(string value)
    {
        var quote = '\0';
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

            if (character != '=')
            {
                continue;
            }

            var previousIndex = index - 1;
            while (previousIndex >= 0 &&
                   char.IsWhiteSpace(value[previousIndex]))
            {
                previousIndex--;
            }

            if (previousIndex < 0)
            {
                return false;
            }

            if (value[previousIndex] is '~' or '|' or '^' or '$' or '*' &&
                previousIndex != index - 1)
            {
                return false;
            }

            return true;
        }

        return quote == '\0' &&
               !escaped;
    }

    private static UtilityVariant? ParseArbitraryVariant(
        string text,
        int sourceStart,
        int sourceOrder,
        ICollection<UtilityCandidateDiagnostic> diagnostics,
        CancellationToken cancellationToken)
    {
        if (text.Length < 3 || text[text.Length - 1] != ']')
        {
            AddError(
                diagnostics,
                UtilityCandidateDiagnosticCode.InvalidVariant,
                "An arbitrary variant must end with ']'.",
                sourceStart,
                text.Length);
            return null;
        }

        var rawSelector = text.Substring(1, text.Length - 2);
        if (!UtilityTextGrammar.IsValidArbitraryValue(rawSelector, cancellationToken))
        {
            AddError(
                diagnostics,
                UtilityCandidateDiagnosticCode.InvalidVariant,
                "The arbitrary variant selector is structurally invalid.",
                sourceStart + 1,
                rawSelector.Length);
            return null;
        }

        var selector = UtilityTextGrammar.DecodeArbitraryValue(
            rawSelector,
            cancellationToken);
        if (string.IsNullOrWhiteSpace(selector))
        {
            AddError(
                diagnostics,
                UtilityCandidateDiagnosticCode.InvalidVariant,
                "An arbitrary variant selector cannot be empty.",
                sourceStart + 1,
                rawSelector.Length);
            return null;
        }

        if (selector[0] == '@' &&
            selector.IndexOf('&') >= 0)
        {
            AddError(
                diagnostics,
                UtilityCandidateDiagnosticCode.InvalidVariant,
                "An arbitrary at-rule cannot also contain a selector; stack separate variants instead.",
                sourceStart + 1,
                rawSelector.Length);
            return null;
        }

        var isRelative = selector[0] is '>' or '+' or '~';
        if (!isRelative &&
            selector[0] != '@' &&
            selector.IndexOf('&') < 0)
        {
            selector = "&:is(" + selector + ")";
        }

        return new UtilityVariant(
            UtilityVariantKind.Arbitrary,
            UtilityVariantCategory.Arbitrary,
            string.Empty,
            text,
            sourceOrder,
            null,
            null,
            null,
            selector,
            isRelative);
    }

    private static UtilityVariantValue? ParseVariantValue(
        string text,
        int sourceStart,
        ICollection<UtilityCandidateDiagnostic> diagnostics,
        CancellationToken cancellationToken)
    {
        if (text.Length == 0)
        {
            AddError(
                diagnostics,
                UtilityCandidateDiagnosticCode.InvalidVariant,
                "A functional variant value cannot be empty.",
                sourceStart,
                0);
            return null;
        }

        if (text[0] == '[')
        {
            if (text.Length < 3 || text[text.Length - 1] != ']')
            {
                AddError(
                    diagnostics,
                    UtilityCandidateDiagnosticCode.InvalidVariant,
                    "An arbitrary variant value must end with ']'.",
                    sourceStart,
                    text.Length);
                return null;
            }

            var rawValue = text.Substring(1, text.Length - 2);
            if (!UtilityTextGrammar.IsValidArbitraryValue(rawValue, cancellationToken))
            {
                AddError(
                    diagnostics,
                    UtilityCandidateDiagnosticCode.InvalidVariant,
                    "The arbitrary variant value is structurally invalid.",
                    sourceStart + 1,
                    rawValue.Length);
                return null;
            }

            var decodedValue = UtilityTextGrammar.DecodeArbitraryValue(
                rawValue,
                cancellationToken);
            if (string.IsNullOrWhiteSpace(decodedValue))
            {
                AddError(
                    diagnostics,
                    UtilityCandidateDiagnosticCode.InvalidVariant,
                    "An arbitrary variant value cannot be empty.",
                    sourceStart + 1,
                    rawValue.Length);
                return null;
            }

            return new UtilityVariantValue(
                UtilityValueKind.Arbitrary,
                decodedValue,
                text);
        }

        if (text[0] == '(')
        {
            if (text.Length < 4 || text[text.Length - 1] != ')')
            {
                AddError(
                    diagnostics,
                    UtilityCandidateDiagnosticCode.InvalidVariant,
                    "A CSS-variable variant value must end with ')'.",
                    sourceStart,
                    text.Length);
                return null;
            }

            var variable = text.Substring(1, text.Length - 2);
            if (!variable.StartsWith("--", StringComparison.Ordinal) ||
                !UtilityTextGrammar.IsValidArbitraryValue(variable, cancellationToken))
            {
                AddError(
                    diagnostics,
                    UtilityCandidateDiagnosticCode.InvalidVariant,
                    "A CSS-variable variant value must begin with '--'.",
                    sourceStart + 1,
                    variable.Length);
                return null;
            }

            return new UtilityVariantValue(
                UtilityValueKind.CssVariable,
                UtilityTextGrammar.DecodeArbitraryValue(
                    "var(" + variable + ")",
                    cancellationToken),
                text);
        }

        if (!UtilityTextGrammar.IsValidNamedValue(text))
        {
            AddError(
                diagnostics,
                UtilityCandidateDiagnosticCode.InvalidVariant,
                "The named variant value contains an unsupported character.",
                sourceStart,
                text.Length);
            return null;
        }

        return new UtilityVariantValue(
            UtilityValueKind.Named,
            text,
            text);
    }

    private static bool TryFindVariantRoot(
        string text,
        UtilityVariantRegistry registry,
        out UtilityVariantDefinition definition,
        out string? value)
    {
        if (registry.TryGetDefinition(text, out var exactDefinition) &&
            exactDefinition is not null)
        {
            definition = exactDefinition;
            value = null;
            return true;
        }

        UtilityVariantDefinition? bestDefinition = null;
        string? bestValue = null;
        foreach (var possibleDefinition in registry.Definitions)
        {
            var name = possibleDefinition.Name;
            if (name == "@")
            {
                if (text.Length > 1 &&
                    text[0] == '@' &&
                    (bestDefinition is null || name.Length > bestDefinition.Name.Length))
                {
                    bestDefinition = possibleDefinition;
                    bestValue = text.Substring(1);
                }

                continue;
            }

            if (text.Length > name.Length &&
                text.StartsWith(name, StringComparison.Ordinal) &&
                text[name.Length] == '-' &&
                (bestDefinition is null || name.Length > bestDefinition.Name.Length))
            {
                bestDefinition = possibleDefinition;
                bestValue = text.Substring(name.Length + 1);
            }
        }

        if (bestDefinition is not null)
        {
            definition = bestDefinition;
            value = bestValue;
            return true;
        }

        definition = null!;
        value = null;
        return false;
    }

    private static bool IsValidPrefix(string prefix)
    {
        foreach (var character in prefix)
        {
            if (character < 'a' || character > 'z')
            {
                return false;
            }
        }

        return prefix.Length != 0;
    }

    private static bool IsValidRoot(string root)
    {
        if (root.Length == 0)
        {
            return false;
        }

        var start = root[0] == '@'
            ? 1
            : 0;
        if (start == root.Length)
        {
            return false;
        }

        for (var index = start; index < root.Length; index++)
        {
            var character = root[index];
            if ((character >= 'a' && character <= 'z') ||
                (character >= 'A' && character <= 'Z') ||
                (character >= '0' && character <= '9') ||
                character is '-' or '_')
            {
                continue;
            }

            return false;
        }

        return true;
    }

    private static UtilityCandidateParseResult Failure(
        IEnumerable<UtilityCandidateDiagnostic> diagnostics) =>
        new(
            null,
            new UtilityCollection<UtilityCandidateDiagnostic>(diagnostics));

    private static void AddError(
        ICollection<UtilityCandidateDiagnostic> diagnostics,
        UtilityCandidateDiagnosticCode code,
        string message,
        int start,
        int length)
    {
        diagnostics.Add(
            new UtilityCandidateDiagnostic(
                code,
                UtilityCandidateDiagnosticSeverity.Error,
                message,
                Math.Max(0, start),
                Math.Max(0, length)));
    }

    [Flags]
    private enum UtilityCompoundRuleKind
    {
        Never = 0,
        AtRule = 1,
        StyleRule = 2,
    }
}
