using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;

namespace Assimalign.Viu.UtilityCss;

internal static class UtilityResolutionEngine
{
    public static UtilityClassResolutionResult Resolve(
        UtilityCssRegistry registry,
        UtilityTheme theme,
        string candidateText,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var sourceText = candidateText ?? string.Empty;
        var diagnostics = new List<UtilityCssDiagnostic>();
        var parseResult = UtilityCandidateParser.Parse(
            sourceText,
            theme.Prefix,
            theme.CandidateVariantRegistry,
            cancellationToken);
        foreach (var parserDiagnostic in parseResult.Diagnostics)
        {
            diagnostics.Add(
                new UtilityCssDiagnostic(
                    sourceText,
                    parserDiagnostic.Severity == UtilityCandidateDiagnosticSeverity.Warning
                        ? UtilityCssDiagnosticCode.DeprecatedSyntax
                        : UtilityCssDiagnosticCode.InvalidCandidate,
                    parserDiagnostic.Severity == UtilityCandidateDiagnosticSeverity.Warning
                        ? UtilityCssDiagnosticSeverity.Warning
                        : UtilityCssDiagnosticSeverity.Error,
                    parserDiagnostic.Message));
        }

        var candidate = parseResult.Candidate;
        if (!parseResult.IsSuccess || candidate is null)
        {
            return Failure(diagnostics);
        }

        if (candidate.Value?.Kind == UtilityValueKind.Named &&
            (candidate.Value.Text.StartsWith(".", StringComparison.Ordinal) ||
             candidate.Value.Text.StartsWith("-", StringComparison.Ordinal)))
        {
            diagnostics.Add(UnsupportedValue(sourceText, candidate));
            return Failure(diagnostics);
        }

        UtilityDefinition definition;
        UtilityResolverKind resolverKind;
        if (candidate.Kind == UtilityCandidateKind.ArbitraryProperty)
        {
            definition = new UtilityDefinition(
                "[]",
                "Sets an arbitrary CSS property.",
                900,
                false,
                UtilityCollection<string>.Empty);
            resolverKind = UtilityResolverKind.Display;
        }
        else if (!registry.TryGetRegistration(
                     candidate.Root,
                     out var registration) ||
                 registration is null)
        {
            diagnostics.Add(
                Error(
                    sourceText,
                    UtilityCssDiagnosticCode.UnsupportedUtility,
                    $"The utility root '{candidate.Root}' is not available in the active Viu Utilities registry."));
            return Failure(diagnostics);
        }
        else
        {
            definition = registration.Definition;
            resolverKind = registration.ResolverKind;
        }

        if (candidate.IsNegative && !definition.SupportsNegativeValues)
        {
            diagnostics.Add(
                Error(
                    sourceText,
                    UtilityCssDiagnosticCode.UnsupportedNegativeForm,
                    $"The '{definition.Root}' utility does not support negative values."));
            return Failure(diagnostics);
        }

        var declarations = new List<UtilityCssDeclaration>();
        if (candidate.Kind == UtilityCandidateKind.ArbitraryProperty)
        {
            if (!TryResolveArbitraryProperty(
                    candidate,
                    sourceText,
                    declarations,
                    diagnostics))
            {
                return Failure(diagnostics);
            }
        }
        else if (!TryResolveNamed(
                     candidate,
                     resolverKind,
                     theme,
                     sourceText,
                     declarations,
                     diagnostics))
        {
            return Failure(diagnostics);
        }

        var selector = "." + UtilityCssText.EscapeClassName(candidate.RawText);
        if (candidate.Root == "placeholder")
        {
            selector += "::placeholder";
        }
        var wrappers = new List<string>();
        var variantOrder = 0;
        if (!TryApplyVariants(
                candidate,
                theme,
                sourceText,
                ref selector,
                declarations,
                wrappers,
                ref variantOrder,
                diagnostics))
        {
            return Failure(diagnostics);
        }

        if (IsSiblingSelectorUtility(candidate.Root))
        {
            selector =
                ":where(" + selector +
                " > :not(:last-child))";
        }

        var isImportant = candidate.IsImportant || theme.IsImportant;
        var css = candidate.Root == "container"
            ? RenderResponsiveContainerRules(
                selector,
                declarations,
                wrappers,
                theme,
                isImportant)
            : UtilityCssText.RenderRule(
                selector,
                declarations,
                wrappers,
                isImportant);
        if (candidate.Root == "outline" &&
            candidate.Value?.Kind == UtilityValueKind.Named &&
            candidate.Value.Text == "hidden")
        {
            var forcedColorWrappers = new List<string>(wrappers)
            {
                "@media (forced-colors: active)",
            };
            css += "\n" +
                UtilityCssText.RenderRule(
                    selector,
                    new[]
                    {
                        new UtilityCssDeclaration(
                            "outline",
                            "2px solid transparent"),
                        new UtilityCssDeclaration(
                            "outline-offset",
                            "2px"),
                    },
                    forcedColorWrappers,
                    isImportant);
        }

        if (UtilityGradientMaskResolution.TryGetLinearGradientInterpolationOverride(
                candidate,
                out var interpolationPosition))
        {
            var interpolationWrappers = new List<string>(wrappers)
            {
                "@supports (background-image: linear-gradient(in lab, red, red))",
            };
            css += "\n" +
                UtilityCssText.RenderRule(
                    selector,
                    new[]
                    {
                        new UtilityCssDeclaration(
                            "--tw-gradient-position",
                            interpolationPosition!),
                    },
                    interpolationWrappers,
                    isImportant);
        }

        var declarationPreview = string.Join(
            " ",
            declarations.Select(
                declaration =>
                    declaration.Property + ": " + declaration.Value + ";"));
        var colorValue = UtilityColorMetadataResolver.Resolve(
            candidate,
            theme,
            declarations);
        var metadata = new UtilityClassMetadata(
            candidate.RawText,
            definition.Description + " " + declarationPreview,
            css,
            (definition.Order * 1000) + variantOrder)
        {
            ColorValue = colorValue,
        };

        return new UtilityClassResolutionResult(
            metadata,
            new UtilityCollection<UtilityCssDiagnostic>(diagnostics));
    }

    private static bool IsSiblingSelectorUtility(string root) =>
        root == "divide" ||
        root.StartsWith("divide-x", StringComparison.Ordinal) ||
        root.StartsWith("divide-y", StringComparison.Ordinal) ||
        root.StartsWith("space-x", StringComparison.Ordinal) ||
        root.StartsWith("space-y", StringComparison.Ordinal);

    private static string RenderResponsiveContainerRules(
        string selector,
        IReadOnlyList<UtilityCssDeclaration> declarations,
        IReadOnlyList<string> wrappers,
        UtilityTheme theme,
        bool isImportant)
    {
        var css = UtilityCssText.RenderRule(
            selector,
            declarations,
            wrappers,
            isImportant);
        foreach (var breakpoint in theme.Breakpoints
                     .OrderBy(
                         breakpoint => GetBreakpointSortValue(
                             breakpoint.Value))
                     .ThenBy(
                         breakpoint => breakpoint.Value,
                         StringComparer.Ordinal)
                     .ThenBy(
                         breakpoint => breakpoint.Name,
                         StringComparer.Ordinal))
        {
            var responsiveWrappers = new List<string>(wrappers)
            {
                "@media (width >= " + breakpoint.Value + ")",
            };
            css += UtilityCssText.RenderRule(
                selector,
                new[]
                {
                    new UtilityCssDeclaration(
                        "max-width",
                        breakpoint.Value),
                },
                responsiveWrappers,
                isImportant);
        }

        return css;
    }

    private static decimal GetBreakpointSortValue(string value)
    {
        var trimmedValue = value.Trim();
        var unitMultiplier = 1m;
        string numberText;
        if (trimmedValue.EndsWith("rem", StringComparison.OrdinalIgnoreCase) ||
            trimmedValue.EndsWith("em", StringComparison.OrdinalIgnoreCase))
        {
            var unitLength = trimmedValue.EndsWith(
                "rem",
                StringComparison.OrdinalIgnoreCase)
                ? 3
                : 2;
            numberText = trimmedValue.Substring(
                0,
                trimmedValue.Length - unitLength);
            unitMultiplier = 16m;
        }
        else if (trimmedValue.EndsWith(
                     "px",
                     StringComparison.OrdinalIgnoreCase))
        {
            numberText = trimmedValue.Substring(
                0,
                trimmedValue.Length - 2);
        }
        else
        {
            numberText = trimmedValue;
        }

        return decimal.TryParse(
            numberText,
            NumberStyles.Number,
            CultureInfo.InvariantCulture,
            out var number)
                ? number * unitMultiplier
                : decimal.MaxValue;
    }

    private static bool TryResolveArbitraryProperty(
        UtilityCandidate candidate,
        string candidateText,
        ICollection<UtilityCssDeclaration> declarations,
        ICollection<UtilityCssDiagnostic> diagnostics)
    {
        if (candidate.Modifier is not null)
        {
            diagnostics.Add(
                Error(
                    candidateText,
                    UtilityCssDiagnosticCode.UnsupportedModifier,
                    "Arbitrary CSS properties do not accept slash modifiers."));
            return false;
        }

        var value = candidate.Value?.Text;
        if (!UtilityCssText.IsSafeProperty(candidate.Root) ||
            string.IsNullOrEmpty(value) ||
            !UtilityCssText.IsSafeValue(value!))
        {
            diagnostics.Add(
                Error(
                    candidateText,
                    UtilityCssDiagnosticCode.UnsafeArbitraryValue,
                    "The arbitrary property or value cannot be emitted as one safe CSS declaration."));
            return false;
        }

        declarations.Add(
            new UtilityCssDeclaration(
                candidate.Root,
                value!));
        return true;
    }

    private static bool TryResolveNamed(
        UtilityCandidate candidate,
        UtilityResolverKind resolverKind,
        UtilityTheme theme,
        string candidateText,
        ICollection<UtilityCssDeclaration> declarations,
        ICollection<UtilityCssDiagnostic> diagnostics)
    {
        if (UtilityAdditionalResolution.TryResolve(
                candidate,
                theme,
                declarations,
                out var isAdditionalResolutionValid))
        {
            if (!isAdditionalResolutionValid)
            {
                diagnostics.Add(UnsupportedValue(candidateText, candidate));
            }

            return isAdditionalResolutionValid;
        }

        switch (resolverKind)
        {
            case UtilityResolverKind.Static:
                return TryResolveStatic(
                    candidate,
                    declarations,
                    candidateText,
                    diagnostics);
            case UtilityResolverKind.Keyword:
                return TryResolveKeyword(
                    candidate,
                    declarations,
                    candidateText,
                    diagnostics);
            case UtilityResolverKind.Display:
                return TryResolveDisplay(
                    candidate,
                    theme,
                    declarations,
                    candidateText,
                    diagnostics);
            case UtilityResolverKind.Position:
                return TryResolvePosition(
                    candidate,
                    declarations,
                    candidateText,
                    diagnostics);
            case UtilityResolverKind.PositionOffset:
                return TryResolvePositionOffset(
                    candidate,
                    theme,
                    declarations,
                    candidateText,
                    diagnostics);
            case UtilityResolverKind.Integer:
                return TryResolveInteger(
                    candidate,
                    theme,
                    declarations,
                    candidateText,
                    diagnostics);
            case UtilityResolverKind.AspectRatio:
                return TryResolveAspectRatio(
                    candidate,
                    theme,
                    declarations,
                    candidateText,
                    diagnostics);
            case UtilityResolverKind.Columns:
                return TryResolveColumns(
                    candidate,
                    theme,
                    declarations,
                    candidateText,
                    diagnostics);
            case UtilityResolverKind.Flex:
                return TryResolveFlex(
                    candidate,
                    declarations,
                    candidateText,
                    diagnostics);
            case UtilityResolverKind.FlexDirection:
                return TryResolveFlexDirection(
                    candidate,
                    declarations,
                    candidateText,
                    diagnostics);
            case UtilityResolverKind.FlexBasis:
                return TryResolveFlexBasis(
                    candidate,
                    theme,
                    declarations,
                    candidateText,
                    diagnostics);
            case UtilityResolverKind.Container:
                return TryResolveContainer(
                    candidate,
                    declarations,
                    candidateText,
                    diagnostics);
            case UtilityResolverKind.Grow:
                return TryResolveFactor(
                    candidate,
                    "flex-grow",
                    declarations,
                    candidateText,
                    diagnostics);
            case UtilityResolverKind.Shrink:
                return TryResolveFactor(
                    candidate,
                    "flex-shrink",
                    declarations,
                    candidateText,
                    diagnostics);
            case UtilityResolverKind.Alignment:
                return TryResolveAlignment(
                    candidate,
                    declarations,
                    candidateText,
                    diagnostics);
            case UtilityResolverKind.GridColumns:
                return TryResolveGridTrack(
                    candidate,
                    "grid-template-columns",
                    declarations,
                    candidateText,
                    diagnostics);
            case UtilityResolverKind.GridRows:
                return TryResolveGridTrack(
                    candidate,
                    "grid-template-rows",
                    declarations,
                    candidateText,
                    diagnostics);
            case UtilityResolverKind.GridSpan:
                return TryResolveGridSpan(
                    candidate,
                    declarations,
                    candidateText,
                    diagnostics);
            case UtilityResolverKind.GridLine:
                return TryResolveGridLine(
                    candidate,
                    declarations,
                    candidateText,
                    diagnostics);
            case UtilityResolverKind.Spacing:
                return TryResolveSpacing(
                    candidate,
                    theme,
                    declarations,
                    candidateText,
                    diagnostics);
            case UtilityResolverKind.SiblingSpacing:
                return TryResolveSiblingSpacing(
                    candidate,
                    theme,
                    declarations,
                    candidateText,
                    diagnostics);
            case UtilityResolverKind.Divide:
                return TryResolveDivide(
                    candidate,
                    theme,
                    declarations,
                    candidateText,
                    diagnostics);
            case UtilityResolverKind.Sizing:
                return TryResolveSizing(
                    candidate,
                    theme,
                    declarations,
                    candidateText,
                    diagnostics);
            case UtilityResolverKind.Text:
                return TryResolveText(
                    candidate,
                    theme,
                    declarations,
                    candidateText,
                    diagnostics);
            case UtilityResolverKind.Typography:
                return TryResolveTypography(
                    candidate,
                    theme,
                    declarations,
                    candidateText,
                    diagnostics);
            case UtilityResolverKind.Background:
                return TryResolveBackground(
                    candidate,
                    theme,
                    declarations,
                    candidateText,
                    diagnostics);
            case UtilityResolverKind.Border:
                return TryResolveBorder(
                    candidate,
                    theme,
                    declarations,
                    candidateText,
                    diagnostics);
            case UtilityResolverKind.Outline:
                return TryResolveOutline(
                    candidate,
                    theme,
                    declarations,
                    candidateText,
                    diagnostics);
            case UtilityResolverKind.Font:
                return TryResolveFont(
                    candidate,
                    theme,
                    declarations,
                    candidateText,
                    diagnostics);
            case UtilityResolverKind.Radius:
                return TryResolveRadius(
                    candidate,
                    theme,
                    declarations,
                    candidateText,
                    diagnostics);
            case UtilityResolverKind.Opacity:
                return TryResolveOpacity(
                    candidate,
                    declarations,
                    candidateText,
                    diagnostics);
            case UtilityResolverKind.Shadow:
                return TryResolveShadow(
                    candidate,
                    theme,
                    declarations,
                    candidateText,
                    diagnostics);
            case UtilityResolverKind.Ring:
                return TryResolveRing(
                    candidate,
                    theme,
                    declarations,
                    candidateText,
                    diagnostics);
            case UtilityResolverKind.Mask:
                return TryResolveMask(
                    candidate,
                    theme,
                    declarations,
                    candidateText,
                    diagnostics);
            case UtilityResolverKind.Filter:
                return TryResolveFilter(
                    candidate,
                    theme,
                    false,
                    declarations,
                    candidateText,
                    diagnostics);
            case UtilityResolverKind.BackdropFilter:
                return TryResolveFilter(
                    candidate,
                    theme,
                    true,
                    declarations,
                    candidateText,
                    diagnostics);
            case UtilityResolverKind.Transition:
                return TryResolveTransition(
                    candidate,
                    theme,
                    declarations,
                    candidateText,
                    diagnostics);
            case UtilityResolverKind.Transform:
                return TryResolveTransform(
                    candidate,
                    theme,
                    declarations,
                    candidateText,
                    diagnostics);
            case UtilityResolverKind.ScrollbarColor:
                return TryResolveScrollbarColor(
                    candidate,
                    theme,
                    declarations,
                    candidateText,
                    diagnostics);
            case UtilityResolverKind.Color:
                return TryResolveSemanticColor(
                    candidate,
                    theme,
                    declarations,
                    candidateText,
                    diagnostics);
            case UtilityResolverKind.Svg:
                return TryResolveSvg(
                    candidate,
                    theme,
                    declarations,
                    candidateText,
                    diagnostics);
            default:
                diagnostics.Add(
                    Error(
                        candidateText,
                        UtilityCssDiagnosticCode.UnsupportedUtility,
                        "The utility family has no resolver."));
                return false;
        }
    }

    private static bool TryResolveStatic(
        UtilityCandidate candidate,
        ICollection<UtilityCssDeclaration> declarations,
        string candidateText,
        ICollection<UtilityCssDiagnostic> diagnostics)
    {
        if (candidate.Value is not null ||
            candidate.Modifier is not null ||
            !UtilityBuiltInCatalog.TryGetStatic(
                candidate.Root,
                out var definition) ||
            definition is null)
        {
            diagnostics.Add(UnsupportedValue(candidateText, candidate));
            return false;
        }

        foreach (var declaration in definition.Declarations)
        {
            declarations.Add(declaration);
        }

        return true;
    }

    private static bool TryResolveKeyword(
        UtilityCandidate candidate,
        ICollection<UtilityCssDeclaration> declarations,
        string candidateText,
        ICollection<UtilityCssDiagnostic> diagnostics)
    {
        if (candidate.Modifier is not null ||
            !UtilityBuiltInCatalog.TryGetKeyword(
                candidate.Root,
                out var definition) ||
            definition is null)
        {
            diagnostics.Add(UnsupportedValue(candidateText, candidate));
            return false;
        }

        var value = candidate.Value?.Text ?? string.Empty;
        if (candidate.Value is null ||
            candidate.Value.Kind == UtilityValueKind.Named)
        {
            if (definition.TryGetDeclarations(
                    value,
                    out var namedDeclarations) &&
                namedDeclarations is not null)
            {
                foreach (var declaration in namedDeclarations)
                {
                    declarations.Add(declaration);
                }

                return true;
            }
        }
        else if (definition.SupportsArbitraryValues &&
                 definition.ArbitraryProperty is not null &&
                 IsSafeCandidateValue(candidate.Value))
        {
            declarations.Add(
                new UtilityCssDeclaration(
                    definition.ArbitraryProperty,
                    candidate.Value.Text));
            return true;
        }

        diagnostics.Add(UnsupportedValue(candidateText, candidate));
        return false;
    }

    private static bool TryResolveInteger(
        UtilityCandidate candidate,
        UtilityTheme theme,
        ICollection<UtilityCssDeclaration> declarations,
        string candidateText,
        ICollection<UtilityCssDiagnostic> diagnostics)
    {
        if (candidate.Modifier is not null ||
            candidate.Value is null)
        {
            diagnostics.Add(UnsupportedValue(candidateText, candidate));
            return false;
        }

        if (candidate.IsNegative &&
            candidate.Root == "z" &&
            candidate.Value.Kind == UtilityValueKind.Named &&
            candidate.Value.Text == "auto")
        {
            diagnostics.Add(UnsupportedValue(candidateText, candidate));
            return false;
        }

        if (candidate.IsNegative &&
            candidate.Root == "order" &&
            candidate.Value.Kind == UtilityValueKind.Named &&
            candidate.Value.Text is "first" or "last" or "none")
        {
            diagnostics.Add(UnsupportedValue(candidateText, candidate));
            return false;
        }

        var property = candidate.Root switch
        {
            "z" => "z-index",
            "order" => "order",
            "tab" => "tab-size",
            "zoom" => "zoom",
            _ => string.Empty,
        };
        string? value;
        var appendPercentage = false;
        if (candidate.Value.Kind is UtilityValueKind.Arbitrary or UtilityValueKind.CssVariable)
        {
            value = IsSafeCandidateValue(candidate.Value)
                ? candidate.Value.Text
                : null;
        }
        else if (candidate.Root == "tab" &&
                 theme.TryGetNamespaceValue(
                     UtilityThemeNamespaceNames.TabSize,
                     candidate.Value.Text,
                     out var tabSize))
        {
            value = tabSize;
        }
        else if (candidate.Root == "zoom" &&
                 theme.TryGetNamespaceValue(
                     UtilityThemeNamespaceNames.Zoom,
                     candidate.Value.Text,
                     out var zoom))
        {
            value = zoom;
        }
        else
        {
            value = candidate.Root switch
            {
                "z" when candidate.Value.Text == "auto" => "auto",
                "order" when candidate.Value.Text == "first" => "-9999",
                "order" when candidate.Value.Text == "last" => "9999",
                "order" when candidate.Value.Text is "none" or "0" => "0",
                _ => TryResolveBareInteger(
                    candidate.Value.Text,
                    candidate.Root == "zoom",
                    out var integerValue)
                        ? integerValue
                        : null,
            };
            appendPercentage =
                candidate.Root == "zoom" &&
                value is not null;
        }

        if (string.IsNullOrEmpty(property) ||
            value is null)
        {
            diagnostics.Add(UnsupportedValue(candidateText, candidate));
            return false;
        }

        if (appendPercentage)
        {
            value += "%";
        }

        if (candidate.IsNegative)
        {
            value = $"calc({value} * -1)";
        }

        declarations.Add(new UtilityCssDeclaration(property, value));
        return true;
    }

    private static bool TryResolveAspectRatio(
        UtilityCandidate candidate,
        UtilityTheme theme,
        ICollection<UtilityCssDeclaration> declarations,
        string candidateText,
        ICollection<UtilityCssDiagnostic> diagnostics)
    {
        if (candidate.Value is null)
        {
            diagnostics.Add(UnsupportedValue(candidateText, candidate));
            return false;
        }

        string? value = null;
        if (candidate.Value.Kind is UtilityValueKind.Arbitrary or UtilityValueKind.CssVariable)
        {
            if (candidate.Modifier is null &&
                IsSafeCandidateValue(candidate.Value))
            {
                value = candidate.Value.Text;
            }
        }
        else if (candidate.Value.Fraction is not null &&
                 candidate.Modifier is not null &&
                 IsPositiveInteger(candidate.Value.Text) &&
                 IsPositiveInteger(candidate.Modifier.Text))
        {
            value = candidate.Value.Text + " / " + candidate.Modifier.Text;
        }
        else if (candidate.Modifier is null &&
                 theme.TryGetNamespaceValue(
                     UtilityThemeNamespaceNames.AspectRatio,
                     candidate.Value.Text,
                     out var themeValue))
        {
            value = themeValue;
        }
        else if (candidate.Modifier is null)
        {
            value = candidate.Value.Text switch
            {
                "auto" => "auto",
                "square" => "1 / 1",
                "video" => "16 / 9",
                _ => null,
            };
        }

        if (value is null)
        {
            diagnostics.Add(UnsupportedValue(candidateText, candidate));
            return false;
        }

        declarations.Add(new UtilityCssDeclaration("aspect-ratio", value));
        return true;
    }

    private static bool TryResolveColumns(
        UtilityCandidate candidate,
        UtilityTheme theme,
        ICollection<UtilityCssDeclaration> declarations,
        string candidateText,
        ICollection<UtilityCssDiagnostic> diagnostics)
    {
        if (candidate.Modifier is not null ||
            candidate.Value is null)
        {
            diagnostics.Add(UnsupportedValue(candidateText, candidate));
            return false;
        }

        string? value;
        if (candidate.Value.Kind is UtilityValueKind.Arbitrary or UtilityValueKind.CssVariable)
        {
            value = IsSafeCandidateValue(candidate.Value)
                ? candidate.Value.Text
                : null;
        }
        else if (candidate.Value.Text == "auto" ||
                 IsPositiveInteger(candidate.Value.Text))
        {
            value = candidate.Value.Text;
        }
        else if (theme.TryGetNamespaceValue(
                     UtilityThemeNamespaceNames.Container,
                     candidate.Value.Text,
                     out var container))
        {
            value = container;
        }
        else
        {
            value = candidate.Value.Text switch
            {
                "3xs" => "16rem",
                "2xs" => "18rem",
                "xs" => "20rem",
                "sm" => "24rem",
                "md" => "28rem",
                "lg" => "32rem",
                "xl" => "36rem",
                "2xl" => "42rem",
                "3xl" => "48rem",
                "4xl" => "56rem",
                "5xl" => "64rem",
                "6xl" => "72rem",
                "7xl" => "80rem",
                _ => null,
            };
        }

        if (value is null)
        {
            diagnostics.Add(UnsupportedValue(candidateText, candidate));
            return false;
        }

        declarations.Add(new UtilityCssDeclaration("columns", value));
        return true;
    }

    private static bool TryResolveDisplay(
        UtilityCandidate candidate,
        UtilityTheme theme,
        ICollection<UtilityCssDeclaration> declarations,
        string candidateText,
        ICollection<UtilityCssDiagnostic> diagnostics)
    {
        if (candidate.Root is "inline" or "block" &&
            candidate.Value is not null)
        {
            if (!TryResolveSizeValue(candidate, theme, out var logicalSize))
            {
                diagnostics.Add(UnsupportedValue(candidateText, candidate));
                return false;
            }

            declarations.Add(
                new UtilityCssDeclaration(
                    candidate.Root == "inline"
                        ? "inline-size"
                        : "block-size",
                    logicalSize!));
            return true;
        }

        if (candidate.Root == "table" &&
            candidate.Value is not null)
        {
            if (candidate.Modifier is not null)
            {
                diagnostics.Add(UnsupportedValue(candidateText, candidate));
                return false;
            }

            var property = candidate.Value.Text is "auto" or "fixed"
                ? "table-layout"
                : "display";
            var tableValue = candidate.Value.Text switch
            {
                "auto" => "auto",
                "fixed" => "fixed",
                "caption" => "table-caption",
                "cell" => "table-cell",
                "column" => "table-column",
                "column-group" => "table-column-group",
                "footer-group" => "table-footer-group",
                "header-group" => "table-header-group",
                "row-group" => "table-row-group",
                "row" => "table-row",
                _ => null,
            };
            if (tableValue is null)
            {
                diagnostics.Add(UnsupportedValue(candidateText, candidate));
                return false;
            }

            declarations.Add(new UtilityCssDeclaration(property, tableValue));
            return true;
        }

        if (candidate.Value is not null ||
            candidate.Modifier is not null)
        {
            diagnostics.Add(UnsupportedValue(candidateText, candidate));
            return false;
        }

        var value = candidate.Root == "hidden"
            ? "none"
            : candidate.Root == "flow-root"
                ? "flow-root"
                : candidate.Root;
        declarations.Add(new UtilityCssDeclaration("display", value));
        return true;
    }

    private static bool TryResolvePosition(
        UtilityCandidate candidate,
        ICollection<UtilityCssDeclaration> declarations,
        string candidateText,
        ICollection<UtilityCssDiagnostic> diagnostics)
    {
        if (candidate.Value is not null ||
            candidate.Modifier is not null)
        {
            diagnostics.Add(UnsupportedValue(candidateText, candidate));
            return false;
        }

        declarations.Add(
            new UtilityCssDeclaration(
                "position",
                candidate.Root));
        return true;
    }

    private static bool TryResolvePositionOffset(
        UtilityCandidate candidate,
        UtilityTheme theme,
        ICollection<UtilityCssDeclaration> declarations,
        string candidateText,
        ICollection<UtilityCssDiagnostic> diagnostics)
    {
        string? value;
        if (candidate.Value is not null &&
            (candidate.Value.Fraction is not null ||
             candidate.Value.Text == "full") &&
            TryResolveSizeValue(
                candidate,
                theme,
                out value))
        {
            if (candidate.IsNegative)
            {
                value = $"calc({value} * -1)";
            }
        }
        else if (!TryResolveSpacingValue(
                     candidate,
                     theme,
                     true,
                     out value))
        {
            diagnostics.Add(UnsupportedValue(candidateText, candidate));
            return false;
        }

        foreach (var property in GetPositionProperties(candidate.Root))
        {
            declarations.Add(new UtilityCssDeclaration(property, value!));
        }

        return true;
    }

    private static bool TryResolveFlex(
        UtilityCandidate candidate,
        ICollection<UtilityCssDeclaration> declarations,
        string candidateText,
        ICollection<UtilityCssDiagnostic> diagnostics)
    {
        if (candidate.Modifier is not null &&
            candidate.Value?.Fraction is null)
        {
            diagnostics.Add(UnsupportedValue(candidateText, candidate));
            return false;
        }

        if (candidate.Root == "flex" && candidate.Value is null)
        {
            declarations.Add(new UtilityCssDeclaration("display", "flex"));
            return true;
        }

        if (candidate.Root == "flex-auto")
        {
            declarations.Add(new UtilityCssDeclaration("flex", "1 1 auto"));
            return true;
        }

        if (candidate.Root == "flex-initial")
        {
            declarations.Add(new UtilityCssDeclaration("flex", "0 1 auto"));
            return true;
        }

        if (candidate.Root == "flex-none")
        {
            declarations.Add(new UtilityCssDeclaration("flex", "none"));
            return true;
        }

        var value = candidate.Value?.Text;
        switch (value)
        {
            case "1":
                declarations.Add(new UtilityCssDeclaration("flex", "1 1 0%"));
                return true;
            case "auto":
                declarations.Add(new UtilityCssDeclaration("flex", "1 1 auto"));
                return true;
            case "initial":
                declarations.Add(new UtilityCssDeclaration("flex", "0 1 auto"));
                return true;
            case "none":
                declarations.Add(new UtilityCssDeclaration("flex", "none"));
                return true;
            case "wrap":
            case "nowrap":
            case "wrap-reverse":
                declarations.Add(new UtilityCssDeclaration("flex-wrap", value));
                return true;
            default:
                if (candidate.Value?.Kind is UtilityValueKind.Arbitrary or UtilityValueKind.CssVariable &&
                    IsSafeCandidateValue(candidate.Value))
                {
                    declarations.Add(new UtilityCssDeclaration("flex", candidate.Value.Text));
                    return true;
                }

                diagnostics.Add(UnsupportedValue(candidateText, candidate));
                return false;
        }
    }

    private static bool TryResolveFlexDirection(
        UtilityCandidate candidate,
        ICollection<UtilityCssDeclaration> declarations,
        string candidateText,
        ICollection<UtilityCssDiagnostic> diagnostics)
    {
        if (candidate.Modifier is not null)
        {
            diagnostics.Add(UnsupportedValue(candidateText, candidate));
            return false;
        }

        var value = candidate.Root.Substring("flex-".Length);
        if (value.StartsWith("col", StringComparison.Ordinal))
        {
            value = "column" + value.Substring("col".Length);
        }

        declarations.Add(
            new UtilityCssDeclaration(
                "flex-direction",
                value));
        return true;
    }

    private static bool TryResolveFlexBasis(
        UtilityCandidate candidate,
        UtilityTheme theme,
        ICollection<UtilityCssDeclaration> declarations,
        string candidateText,
        ICollection<UtilityCssDiagnostic> diagnostics)
    {
        if (!TryResolveSizeValue(candidate, theme, out var value))
        {
            diagnostics.Add(UnsupportedValue(candidateText, candidate));
            return false;
        }

        declarations.Add(new UtilityCssDeclaration("flex-basis", value!));
        return true;
    }

    private static bool TryResolveContainer(
        UtilityCandidate candidate,
        ICollection<UtilityCssDeclaration> declarations,
        string candidateText,
        ICollection<UtilityCssDiagnostic> diagnostics)
    {
        if (candidate.Root == "container")
        {
            if (candidate.Value is not null ||
                candidate.Modifier is not null)
            {
                diagnostics.Add(UnsupportedValue(candidateText, candidate));
                return false;
            }

            declarations.Add(new UtilityCssDeclaration("width", "100%"));
            return true;
        }

        if (candidate.Root != "@container")
        {
            diagnostics.Add(UnsupportedValue(candidateText, candidate));
            return false;
        }

        string? containerType;
        if (candidate.Value is null)
        {
            containerType = "inline-size";
        }
        else if (candidate.Value.Kind is UtilityValueKind.Arbitrary or UtilityValueKind.CssVariable &&
                 IsSafeCandidateValue(candidate.Value))
        {
            containerType = candidate.Value.Text;
        }
        else
        {
            containerType = candidate.Value.Text switch
            {
                "normal" => "normal",
                "size" => "size",
                _ => null,
            };
        }

        if (containerType is null ||
            candidate.Modifier is not null &&
            !UtilityCssText.IsSafeValue(candidate.Modifier.Text))
        {
            diagnostics.Add(UnsupportedValue(candidateText, candidate));
            return false;
        }

        declarations.Add(
            new UtilityCssDeclaration(
                "container-type",
                containerType));
        if (candidate.Modifier is not null)
        {
            declarations.Add(
                new UtilityCssDeclaration(
                    "container-name",
                    candidate.Modifier.Text));
        }

        return true;
    }

    private static bool TryResolveFactor(
        UtilityCandidate candidate,
        string property,
        ICollection<UtilityCssDeclaration> declarations,
        string candidateText,
        ICollection<UtilityCssDiagnostic> diagnostics)
    {
        if (candidate.Modifier is not null)
        {
            diagnostics.Add(UnsupportedValue(candidateText, candidate));
            return false;
        }

        var value = candidate.Value?.Text ?? "1";
        if (value != "0" &&
            value != "1" &&
            !(candidate.Value?.Kind is UtilityValueKind.Arbitrary or UtilityValueKind.CssVariable &&
              IsSafeCandidateValue(candidate.Value)))
        {
            diagnostics.Add(UnsupportedValue(candidateText, candidate));
            return false;
        }

        declarations.Add(new UtilityCssDeclaration(property, value));
        return true;
    }

    private static bool TryResolveAlignment(
        UtilityCandidate candidate,
        ICollection<UtilityCssDeclaration> declarations,
        string candidateText,
        ICollection<UtilityCssDiagnostic> diagnostics)
    {
        if (candidate.Modifier is not null)
        {
            diagnostics.Add(UnsupportedValue(candidateText, candidate));
            return false;
        }

        if (candidate.Root == "content" &&
            candidate.Value is not null &&
            candidate.Value.Kind is UtilityValueKind.Arbitrary or UtilityValueKind.CssVariable &&
            IsSafeCandidateValue(candidate.Value))
        {
            declarations.Add(
                new UtilityCssDeclaration(
                    "content",
                    candidate.Value.Text));
            return true;
        }

        var value = candidate.Value?.Text;
        if (value is null)
        {
            diagnostics.Add(UnsupportedValue(candidateText, candidate));
            return false;
        }

        var property = candidate.Root switch
        {
            "items" => "align-items",
            "justify" => "justify-content",
            "self" => "align-self",
            "content" => "align-content",
            "justify-items" => "justify-items",
            "justify-self" => "justify-self",
            "place-content" => "place-content",
            "place-items" => "place-items",
            "place-self" => "place-self",
            _ => string.Empty,
        };
        var usesFlexNames = candidate.Root is
            "items" or
            "justify" or
            "self" or
            "content";
        var translatedValue = value switch
        {
            "start" when usesFlexNames => "flex-start",
            "end" when usesFlexNames => "flex-end",
            "between" => "space-between",
            "around" => "space-around",
            "evenly" => "space-evenly",
            _ => value,
        };
        var supported = translatedValue is
            "flex-start" or
            "flex-end" or
            "center" or
            "space-between" or
            "space-around" or
            "space-evenly" or
            "baseline" or
            "stretch" or
            "auto" or
            "start" or
            "end" or
            "normal";
        if (!supported ||
            string.IsNullOrEmpty(property) ||
            candidate.Root == "justify-self" &&
            translatedValue == "baseline")
        {
            diagnostics.Add(UnsupportedValue(candidateText, candidate));
            return false;
        }

        declarations.Add(new UtilityCssDeclaration(property, translatedValue));
        return true;
    }

    private static bool TryResolveGridTrack(
        UtilityCandidate candidate,
        string property,
        ICollection<UtilityCssDeclaration> declarations,
        string candidateText,
        ICollection<UtilityCssDiagnostic> diagnostics)
    {
        if (candidate.Modifier is not null)
        {
            diagnostics.Add(UnsupportedValue(candidateText, candidate));
            return false;
        }

        var value = candidate.Value?.Text;
        if (candidate.Value?.Kind is UtilityValueKind.Arbitrary or UtilityValueKind.CssVariable &&
            IsSafeCandidateValue(candidate.Value))
        {
            declarations.Add(new UtilityCssDeclaration(property, candidate.Value.Text));
            return true;
        }

        if (value is "none" or "subgrid")
        {
            declarations.Add(new UtilityCssDeclaration(property, value));
            return true;
        }

        if (int.TryParse(
                value,
                NumberStyles.None,
                CultureInfo.InvariantCulture,
                out var count) &&
            count >= 1 &&
            count <= 12)
        {
            declarations.Add(
                new UtilityCssDeclaration(
                    property,
                    $"repeat({count.ToString(CultureInfo.InvariantCulture)}, minmax(0, 1fr))"));
            return true;
        }

        diagnostics.Add(UnsupportedValue(candidateText, candidate));
        return false;
    }

    private static bool TryResolveGridSpan(
        UtilityCandidate candidate,
        ICollection<UtilityCssDeclaration> declarations,
        string candidateText,
        ICollection<UtilityCssDiagnostic> diagnostics)
    {
        var property = candidate.Root == "col-span"
            ? "grid-column"
            : "grid-row";
        var value = candidate.Value?.Text;
        if (value == "full")
        {
            declarations.Add(new UtilityCssDeclaration(property, "1 / -1"));
            return true;
        }

        if (int.TryParse(
                value,
                NumberStyles.None,
                CultureInfo.InvariantCulture,
                out var count) &&
            count >= 1 &&
            count <= 12)
        {
            declarations.Add(
                new UtilityCssDeclaration(
                    property,
                    $"span {count.ToString(CultureInfo.InvariantCulture)} / span {count.ToString(CultureInfo.InvariantCulture)}"));
            return true;
        }

        diagnostics.Add(UnsupportedValue(candidateText, candidate));
        return false;
    }

    private static bool TryResolveGridLine(
        UtilityCandidate candidate,
        ICollection<UtilityCssDeclaration> declarations,
        string candidateText,
        ICollection<UtilityCssDiagnostic> diagnostics)
    {
        if (candidate.Modifier is not null ||
            candidate.Value is null)
        {
            diagnostics.Add(UnsupportedValue(candidateText, candidate));
            return false;
        }

        string? value;
        if (candidate.Value.Kind is UtilityValueKind.Arbitrary or UtilityValueKind.CssVariable)
        {
            value = IsSafeCandidateValue(candidate.Value)
                ? candidate.Value.Text
                : null;
        }
        else if (candidate.Value.Text == "auto")
        {
            value = "auto";
        }
        else if (IsPositiveInteger(candidate.Value.Text))
        {
            value = candidate.Value.Text;
        }
        else
        {
            value = null;
        }

        if (value is null)
        {
            diagnostics.Add(UnsupportedValue(candidateText, candidate));
            return false;
        }

        if (candidate.IsNegative)
        {
            value = $"calc({value} * -1)";
        }

        var property = candidate.Root switch
        {
            "col" => "grid-column",
            "col-start" => "grid-column-start",
            "col-end" => "grid-column-end",
            "row" => "grid-row",
            "row-start" => "grid-row-start",
            "row-end" => "grid-row-end",
            _ => string.Empty,
        };
        if (property.Length == 0)
        {
            diagnostics.Add(UnsupportedValue(candidateText, candidate));
            return false;
        }

        declarations.Add(new UtilityCssDeclaration(property, value));
        return true;
    }

    private static bool TryResolveSpacing(
        UtilityCandidate candidate,
        UtilityTheme theme,
        ICollection<UtilityCssDeclaration> declarations,
        string candidateText,
        ICollection<UtilityCssDiagnostic> diagnostics)
    {
        if (!TryResolveSpacingValue(
                candidate,
                theme,
                candidate.Root[0] == 'm',
                out var value))
        {
            diagnostics.Add(UnsupportedValue(candidateText, candidate));
            return false;
        }

        if (candidate.Root.StartsWith("border-spacing", StringComparison.Ordinal))
        {
            if (candidate.Root is "border-spacing" or "border-spacing-x")
            {
                declarations.Add(
                    new UtilityCssDeclaration(
                        "--tw-border-spacing-x",
                        value!));
            }

            if (candidate.Root is "border-spacing" or "border-spacing-y")
            {
                declarations.Add(
                    new UtilityCssDeclaration(
                        "--tw-border-spacing-y",
                        value!));
            }

            declarations.Add(
                new UtilityCssDeclaration(
                    "border-spacing",
                    "var(--tw-border-spacing-x) var(--tw-border-spacing-y)"));
            return true;
        }

        foreach (var property in GetSpacingProperties(candidate.Root))
        {
            declarations.Add(new UtilityCssDeclaration(property, value!));
        }

        return true;
    }

    private static bool TryResolveSiblingSpacing(
        UtilityCandidate candidate,
        UtilityTheme theme,
        ICollection<UtilityCssDeclaration> declarations,
        string candidateText,
        ICollection<UtilityCssDiagnostic> diagnostics)
    {
        if (!TryResolveSpacingValue(
                candidate,
                theme,
                false,
                out var value))
        {
            diagnostics.Add(UnsupportedValue(candidateText, candidate));
            return false;
        }

        if (candidate.Root == "space-x")
        {
            declarations.Add(
                new UtilityCssDeclaration(
                    "--tw-space-x-reverse",
                    "0"));
            declarations.Add(
                new UtilityCssDeclaration(
                    "margin-inline-start",
                    $"calc({value} * var(--tw-space-x-reverse))"));
            declarations.Add(
                new UtilityCssDeclaration(
                    "margin-inline-end",
                    $"calc({value} * calc(1 - var(--tw-space-x-reverse)))"));
            return true;
        }

        if (candidate.Root == "space-y")
        {
            declarations.Add(
                new UtilityCssDeclaration(
                    "--tw-space-y-reverse",
                    "0"));
            declarations.Add(
                new UtilityCssDeclaration(
                    "margin-block-start",
                    $"calc({value} * var(--tw-space-y-reverse))"));
            declarations.Add(
                new UtilityCssDeclaration(
                    "margin-block-end",
                    $"calc({value} * calc(1 - var(--tw-space-y-reverse)))"));
            return true;
        }

        diagnostics.Add(UnsupportedValue(candidateText, candidate));
        return false;
    }

    private static bool TryResolveDivide(
        UtilityCandidate candidate,
        UtilityTheme theme,
        ICollection<UtilityCssDeclaration> declarations,
        string candidateText,
        ICollection<UtilityCssDiagnostic> diagnostics)
    {
        if (candidate.Root == "divide")
        {
            return TryResolveColor(
                candidate,
                theme,
                "border-color",
                declarations,
                candidateText,
                diagnostics);
        }

        if (candidate.Modifier is not null)
        {
            diagnostics.Add(UnsupportedValue(candidateText, candidate));
            return false;
        }

        string? width;
        if (candidate.Value is null)
        {
            width = "1px";
        }
        else if (candidate.Value.Kind is UtilityValueKind.Arbitrary or UtilityValueKind.CssVariable &&
                 IsSafeCandidateValue(candidate.Value))
        {
            width = candidate.Value.Text;
        }
        else if (IsPositiveInteger(candidate.Value.Text) ||
                 candidate.Value.Text == "0")
        {
            width = candidate.Value.Text + "px";
        }
        else
        {
            width = null;
        }

        if (width is null)
        {
            diagnostics.Add(UnsupportedValue(candidateText, candidate));
            return false;
        }

        if (candidate.Root == "divide-x")
        {
            declarations.Add(
                new UtilityCssDeclaration(
                    "--tw-divide-x-reverse",
                    "0"));
            declarations.Add(
                new UtilityCssDeclaration(
                    "border-inline-style",
                    "var(--tw-border-style)"));
            declarations.Add(
                new UtilityCssDeclaration(
                    "border-inline-start-width",
                    $"calc({width} * var(--tw-divide-x-reverse))"));
            declarations.Add(
                new UtilityCssDeclaration(
                    "border-inline-end-width",
                    $"calc({width} * calc(1 - var(--tw-divide-x-reverse)))"));
            return true;
        }

        if (candidate.Root == "divide-y")
        {
            declarations.Add(
                new UtilityCssDeclaration(
                    "--tw-divide-y-reverse",
                    "0"));
            declarations.Add(
                new UtilityCssDeclaration(
                    "border-bottom-style",
                    "var(--tw-border-style)"));
            declarations.Add(
                new UtilityCssDeclaration(
                    "border-top-style",
                    "var(--tw-border-style)"));
            declarations.Add(
                new UtilityCssDeclaration(
                    "border-top-width",
                    $"calc({width} * var(--tw-divide-y-reverse))"));
            declarations.Add(
                new UtilityCssDeclaration(
                    "border-bottom-width",
                    $"calc({width} * calc(1 - var(--tw-divide-y-reverse)))"));
            return true;
        }

        diagnostics.Add(UnsupportedValue(candidateText, candidate));
        return false;
    }

    private static bool TryResolveSizing(
        UtilityCandidate candidate,
        UtilityTheme theme,
        ICollection<UtilityCssDeclaration> declarations,
        string candidateText,
        ICollection<UtilityCssDiagnostic> diagnostics)
    {
        if (candidate.Root.StartsWith("max-", StringComparison.Ordinal) &&
            candidate.Value?.Kind == UtilityValueKind.Named &&
            candidate.Value.Text == "auto")
        {
            diagnostics.Add(UnsupportedValue(candidateText, candidate));
            return false;
        }

        if (!TryResolveSizeValue(candidate, theme, out var value))
        {
            diagnostics.Add(UnsupportedValue(candidateText, candidate));
            return false;
        }

        if (candidate.Root == "size")
        {
            declarations.Add(new UtilityCssDeclaration("width", value!));
            declarations.Add(new UtilityCssDeclaration("height", value!));
            return true;
        }

        var property = candidate.Root switch
        {
            "w" => "width",
            "h" => "height",
            "min-w" => "min-width",
            "min-h" => "min-height",
            "max-w" => "max-width",
            "max-h" => "max-height",
            "min-inline" => "min-inline-size",
            "min-block" => "min-block-size",
            "max-inline" => "max-inline-size",
            "max-block" => "max-block-size",
            _ => string.Empty,
        };
        if (property.Length == 0)
        {
            diagnostics.Add(UnsupportedValue(candidateText, candidate));
            return false;
        }

        declarations.Add(new UtilityCssDeclaration(property, value!));

        return true;
    }

    private static bool TryResolveText(
        UtilityCandidate candidate,
        UtilityTheme theme,
        ICollection<UtilityCssDeclaration> declarations,
        string candidateText,
        ICollection<UtilityCssDiagnostic> diagnostics)
    {
        if (candidate.Value is not null &&
            candidate.Value.Kind == UtilityValueKind.Named &&
            theme.TryGetFontSize(candidate.Value.Text, out var fontSize))
        {
            declarations.Add(new UtilityCssDeclaration("font-size", fontSize!));
            if (candidate.Modifier is not null)
            {
                if (!TryResolveLineHeightModifier(
                        candidate.Modifier,
                        theme,
                        out var lineHeight))
                {
                    diagnostics.Add(
                        Error(
                            candidateText,
                            UtilityCssDiagnosticCode.UnsupportedModifier,
                            "The font-size modifier must resolve to a line height."));
                    return false;
                }

                declarations.Add(
                    new UtilityCssDeclaration(
                        "line-height",
                        lineHeight!));
            }
            else if (TryResolveThemeProperty(
                         theme,
                         "--text-" + candidate.Value.Text + "--line-height",
                         out var defaultLineHeight))
            {
                declarations.Add(
                    new UtilityCssDeclaration(
                        "line-height",
                        "var(--tw-leading, " + defaultLineHeight + ")"));
            }

            return true;
        }

        if (candidate.Value?.Kind == UtilityValueKind.Named &&
            candidate.Modifier is null)
        {
            var namedProperty = candidate.Value.Text switch
            {
                "left" or "center" or "right" or "justify" or "start" or "end" =>
                    "text-align",
                "ellipsis" or "clip" => "text-overflow",
                "wrap" or "nowrap" or "balance" or "pretty" => "text-wrap",
                _ => null,
            };
            if (namedProperty is not null)
            {
                declarations.Add(
                    new UtilityCssDeclaration(
                        namedProperty,
                        candidate.Value.Text));
                return true;
            }
        }

        if (candidate.Value?.Kind == UtilityValueKind.Arbitrary &&
            candidate.Value.DataType != "color")
        {
            if (!IsSafeCandidateValue(candidate.Value) ||
                candidate.Modifier is not null)
            {
                diagnostics.Add(UnsupportedValue(candidateText, candidate));
                return false;
            }

            declarations.Add(
                new UtilityCssDeclaration(
                    "font-size",
                    candidate.Value.Text));
            return true;
        }

        return TryResolveColor(
            candidate,
            theme,
            "color",
            declarations,
            candidateText,
            diagnostics);
    }

    private static bool TryResolveTypography(
        UtilityCandidate candidate,
        UtilityTheme theme,
        ICollection<UtilityCssDeclaration> declarations,
        string candidateText,
        ICollection<UtilityCssDiagnostic> diagnostics)
    {
        if (candidate.Root is not "tracking" and not "indent" and not "underline-offset" &&
            candidate.IsNegative)
        {
            diagnostics.Add(UnsupportedValue(candidateText, candidate));
            return false;
        }

        switch (candidate.Root)
        {
            case "leading":
                return TryResolveLeading(
                    candidate,
                    theme,
                    declarations,
                    candidateText,
                    diagnostics);
            case "tracking":
                return TryResolveTracking(
                    candidate,
                    theme,
                    declarations,
                    candidateText,
                    diagnostics);
            case "indent":
                return TryResolveLengthProperty(
                    candidate,
                    theme,
                    "text-indent",
                    true,
                    declarations,
                    candidateText,
                    diagnostics);
            case "line-clamp":
                return TryResolveLineClamp(
                    candidate,
                    declarations,
                    candidateText,
                    diagnostics);
            case "align":
                return TryResolveNamedOrArbitraryProperty(
                    candidate,
                    "vertical-align",
                    new[]
                    {
                        "baseline",
                        "top",
                        "middle",
                        "bottom",
                        "text-top",
                        "text-bottom",
                        "sub",
                        "super",
                    },
                    declarations,
                    candidateText,
                    diagnostics);
            case "list":
                return TryResolveList(
                    candidate,
                    declarations,
                    candidateText,
                    diagnostics);
            case "list-image":
                return TryResolveNamedOrArbitraryProperty(
                    candidate,
                    "list-style-image",
                    new[] { "none" },
                    declarations,
                    candidateText,
                    diagnostics);
            case "decoration":
                return TryResolveDecoration(
                    candidate,
                    theme,
                    declarations,
                    candidateText,
                    diagnostics);
            case "underline-offset":
                return TryResolveUnderlineOffset(
                    candidate,
                    declarations,
                    candidateText,
                    diagnostics);
            case "content":
                return TryResolveNamedOrArbitraryProperty(
                    candidate,
                    "content",
                    new[] { "none" },
                    declarations,
                    candidateText,
                    diagnostics);
            case "font-stretch":
                return TryResolveFontStretch(
                    candidate,
                    declarations,
                    candidateText,
                    diagnostics);
            case "font-features":
                return TryResolveArbitraryPropertyValue(
                    candidate,
                    "font-feature-settings",
                    declarations,
                    candidateText,
                    diagnostics);
            default:
                diagnostics.Add(UnsupportedValue(candidateText, candidate));
                return false;
        }
    }

    private static bool TryResolveBackground(
        UtilityCandidate candidate,
        UtilityTheme theme,
        ICollection<UtilityCssDeclaration> declarations,
        string candidateText,
        ICollection<UtilityCssDiagnostic> diagnostics)
    {
        if (UtilityGradientMaskResolution.IsGradientStopRoot(candidate.Root))
        {
            if (UtilityGradientMaskResolution.TryResolveGradientStop(
                    candidate,
                    theme,
                    declarations))
            {
                return true;
            }

            diagnostics.Add(UnsupportedValue(candidateText, candidate));
            return false;
        }

        if (candidate.Root is "bg-linear" or "bg-radial" or "bg-conic")
        {
            if (candidate.Root == "bg-linear" &&
                candidate.IsNegative &&
                candidate.Value is not null &&
                ((candidate.Value.Kind == UtilityValueKind.Named &&
                  candidate.Value.Text.StartsWith("to-", StringComparison.Ordinal)) ||
                 (candidate.Value.Kind == UtilityValueKind.Arbitrary &&
                  candidate.Value.Text.StartsWith("to ", StringComparison.Ordinal))))
            {
                diagnostics.Add(UnsupportedValue(candidateText, candidate));
                return false;
            }

            if (UtilityGradientMaskResolution.TryResolveBackgroundGradient(
                    candidate,
                    declarations))
            {
                return true;
            }

            diagnostics.Add(UnsupportedValue(candidateText, candidate));
            return false;
        }

        if (candidate.Root == "bg")
        {
            if (candidate.Value?.Kind is UtilityValueKind.Arbitrary or UtilityValueKind.CssVariable)
            {
                if (UtilityGradientMaskResolution.TryResolveArbitraryBackground(
                        candidate,
                        theme,
                        declarations))
                {
                    return true;
                }

                diagnostics.Add(UnsupportedValue(candidateText, candidate));
                return false;
            }

            if (candidate.Value?.Kind == UtilityValueKind.Named &&
                candidate.Modifier is null)
            {
                var namedDeclaration = candidate.Value.Text switch
                {
                    "fixed" => new UtilityCssDeclaration("background-attachment", "fixed"),
                    "local" => new UtilityCssDeclaration("background-attachment", "local"),
                    "scroll" => new UtilityCssDeclaration("background-attachment", "scroll"),
                    "clip-border" => new UtilityCssDeclaration("background-clip", "border-box"),
                    "clip-padding" => new UtilityCssDeclaration("background-clip", "padding-box"),
                    "clip-content" => new UtilityCssDeclaration("background-clip", "content-box"),
                    "clip-text" => new UtilityCssDeclaration("background-clip", "text"),
                    "origin-border" => new UtilityCssDeclaration("background-origin", "border-box"),
                    "origin-padding" => new UtilityCssDeclaration("background-origin", "padding-box"),
                    "origin-content" => new UtilityCssDeclaration("background-origin", "content-box"),
                    "repeat" => new UtilityCssDeclaration("background-repeat", "repeat"),
                    "no-repeat" => new UtilityCssDeclaration("background-repeat", "no-repeat"),
                    "repeat-x" => new UtilityCssDeclaration("background-repeat", "repeat-x"),
                    "repeat-y" => new UtilityCssDeclaration("background-repeat", "repeat-y"),
                    "repeat-round" => new UtilityCssDeclaration("background-repeat", "round"),
                    "repeat-space" => new UtilityCssDeclaration("background-repeat", "space"),
                    "auto" => new UtilityCssDeclaration("background-size", "auto"),
                    "cover" => new UtilityCssDeclaration("background-size", "cover"),
                    "contain" => new UtilityCssDeclaration("background-size", "contain"),
                    "bottom" => new UtilityCssDeclaration("background-position", "bottom"),
                    "center" => new UtilityCssDeclaration("background-position", "center"),
                    "left" => new UtilityCssDeclaration("background-position", "left"),
                    "left-bottom" => new UtilityCssDeclaration("background-position", "left bottom"),
                    "left-top" => new UtilityCssDeclaration("background-position", "left top"),
                    "right" => new UtilityCssDeclaration("background-position", "right"),
                    "right-bottom" => new UtilityCssDeclaration("background-position", "right bottom"),
                    "right-top" => new UtilityCssDeclaration("background-position", "right top"),
                    "top" => new UtilityCssDeclaration("background-position", "top"),
                    "none" => new UtilityCssDeclaration("background-image", "none"),
                    _ => null,
                };
                if (namedDeclaration is not null)
                {
                    declarations.Add(namedDeclaration);
                    return true;
                }
            }

            if (candidate.Value is not null &&
                candidate.Modifier is null &&
                candidate.Value.Kind is UtilityValueKind.Arbitrary or UtilityValueKind.CssVariable &&
                IsSafeCandidateValue(candidate.Value) &&
                (candidate.Value.DataType == "image" ||
                 candidate.Value.Text.StartsWith("url(", StringComparison.Ordinal) ||
                 candidate.Value.Text.IndexOf("gradient(", StringComparison.Ordinal) >= 0))
            {
                declarations.Add(
                    new UtilityCssDeclaration(
                        "background-image",
                        candidate.Value.Text));
                return true;
            }

            return TryResolveColor(
                candidate,
                theme,
                "background-color",
                declarations,
                candidateText,
                diagnostics);
        }

        if (candidate.Root is "bg-position" or "bg-size")
        {
            return TryResolveArbitraryPropertyValue(
                candidate,
                candidate.Root == "bg-position"
                    ? "background-position"
                    : "background-size",
                declarations,
                candidateText,
                diagnostics);
        }

        if (candidate.Modifier is not null)
        {
            diagnostics.Add(UnsupportedValue(candidateText, candidate));
            return false;
        }

        var functionName = candidate.Root switch
        {
            "bg-linear" => "linear-gradient",
            "bg-radial" => "radial-gradient",
            "bg-conic" => "conic-gradient",
            _ => null,
        };
        if (functionName is null)
        {
            diagnostics.Add(UnsupportedValue(candidateText, candidate));
            return false;
        }

        string? arguments;
        if (candidate.Value is null)
        {
            arguments = "var(--tw-gradient-stops)";
        }
        else if (candidate.Value.Kind is UtilityValueKind.Arbitrary or UtilityValueKind.CssVariable &&
                 IsSafeCandidateValue(candidate.Value))
        {
            arguments = candidate.Value.Text;
        }
        else if (candidate.Root == "bg-linear")
        {
            arguments = candidate.Value.Text switch
            {
                "to-t" => "to top, var(--tw-gradient-stops)",
                "to-tr" => "to top right, var(--tw-gradient-stops)",
                "to-r" => "to right, var(--tw-gradient-stops)",
                "to-br" => "to bottom right, var(--tw-gradient-stops)",
                "to-b" => "to bottom, var(--tw-gradient-stops)",
                "to-bl" => "to bottom left, var(--tw-gradient-stops)",
                "to-l" => "to left, var(--tw-gradient-stops)",
                "to-tl" => "to top left, var(--tw-gradient-stops)",
                _ => null,
            };
        }
        else if (candidate.Root == "bg-conic" &&
                 decimal.TryParse(
                     candidate.Value.Text,
                     NumberStyles.Number,
                     CultureInfo.InvariantCulture,
                     out var degrees))
        {
            arguments =
                $"from {degrees.ToString("0.###", CultureInfo.InvariantCulture)}deg, var(--tw-gradient-stops)";
        }
        else
        {
            arguments = null;
        }

        if (arguments is null)
        {
            diagnostics.Add(UnsupportedValue(candidateText, candidate));
            return false;
        }

        declarations.Add(
            new UtilityCssDeclaration(
                "background-image",
                functionName + "(" + arguments + ")"));
        return true;
    }

    private static bool TryResolveColor(
        UtilityCandidate candidate,
        UtilityTheme theme,
        string property,
        ICollection<UtilityCssDeclaration> declarations,
        string candidateText,
        ICollection<UtilityCssDiagnostic> diagnostics)
    {
        if (!TryGetColor(candidate, theme, out var color))
        {
            diagnostics.Add(UnsupportedValue(candidateText, candidate));
            return false;
        }

        if (!TryApplyColorModifier(
                color!,
                candidate.Modifier,
                out var modifiedColor))
        {
            diagnostics.Add(
                Error(
                    candidateText,
                    UtilityCssDiagnosticCode.UnsupportedModifier,
                    "The color opacity modifier must be a percentage, a number from 0 through 100, or an arbitrary CSS value."));
            return false;
        }

        declarations.Add(new UtilityCssDeclaration(property, modifiedColor!));
        return true;
    }

    private static bool TryResolveBorder(
        UtilityCandidate candidate,
        UtilityTheme theme,
        ICollection<UtilityCssDeclaration> declarations,
        string candidateText,
        ICollection<UtilityCssDiagnostic> diagnostics)
    {
        var properties = GetBorderProperties(candidate.Root);
        var value = candidate.Value?.Text;
        if (candidate.Value?.Kind == UtilityValueKind.Named &&
            candidate.Modifier is null &&
            value is "solid" or "dashed" or "dotted" or "double" or "hidden" or "none")
        {
            declarations.Add(
                new UtilityCssDeclaration(
                    "--tw-border-style",
                    value));
            foreach (var property in properties.Select(property => property + "-style"))
            {
                declarations.Add(new UtilityCssDeclaration(property, value));
            }

            return true;
        }

        var isWidth =
            candidate.Value is null ||
            value is "0" or "2" or "4" or "8" ||
            (candidate.Value?.Kind == UtilityValueKind.Arbitrary &&
             candidate.Value.DataType != "color");
        if (isWidth)
        {
            if (candidate.Modifier is not null)
            {
                diagnostics.Add(
                    Error(
                        candidateText,
                        UtilityCssDiagnosticCode.UnsupportedModifier,
                        "Border-width utilities do not accept slash modifiers."));
                return false;
            }

            var width = candidate.Value is null
                ? "1px"
                : candidate.Value.Kind is UtilityValueKind.Arbitrary or UtilityValueKind.CssVariable
                    ? candidate.Value.Text
                    : value + "px";
            if (!UtilityCssText.IsSafeValue(width))
            {
                diagnostics.Add(UnsupportedValue(candidateText, candidate));
                return false;
            }

            foreach (var property in properties)
            {
                declarations.Add(
                    new UtilityCssDeclaration(
                        property + "-style",
                        "var(--tw-border-style)"));
                declarations.Add(
                    new UtilityCssDeclaration(
                        property + "-width",
                        width));
            }

            return true;
        }

        if (!TryGetColor(candidate, theme, out var color) ||
            !TryApplyColorModifier(color!, candidate.Modifier, out var modifiedColor))
        {
            diagnostics.Add(UnsupportedValue(candidateText, candidate));
            return false;
        }

        foreach (var property in properties.Select(property => property + "-color"))
        {
            declarations.Add(new UtilityCssDeclaration(property, modifiedColor!));
        }

        return true;
    }

    private static bool TryResolveOutline(
        UtilityCandidate candidate,
        UtilityTheme theme,
        ICollection<UtilityCssDeclaration> declarations,
        string candidateText,
        ICollection<UtilityCssDiagnostic> diagnostics)
    {
        if (candidate.Root == "outline-offset")
        {
            if (candidate.Modifier is not null ||
                candidate.Value is null ||
                !TryResolvePixelOrArbitrary(
                    candidate.Value,
                    out var offset))
            {
                diagnostics.Add(UnsupportedValue(candidateText, candidate));
                return false;
            }

            if (candidate.IsNegative)
            {
                offset = $"calc({offset} * -1)";
            }

            declarations.Add(new UtilityCssDeclaration("outline-offset", offset!));
            return true;
        }

        if (candidate.Value is null)
        {
            if (candidate.Modifier is not null)
            {
                diagnostics.Add(UnsupportedValue(candidateText, candidate));
                return false;
            }

            TryResolveThemeProperty(
                theme,
                "--default-outline-width",
                out var defaultWidth);
            declarations.Add(
                new UtilityCssDeclaration(
                    "outline-style",
                    "var(--tw-outline-style)"));
            declarations.Add(
                new UtilityCssDeclaration(
                    "outline-width",
                    defaultWidth ?? "1px"));
            return true;
        }

        if (candidate.Value.Kind == UtilityValueKind.Named &&
            candidate.Modifier is null)
        {
            if (candidate.Value.Text is "none" or "hidden")
            {
                declarations.Add(
                    new UtilityCssDeclaration(
                        "--tw-outline-style",
                        "none"));
                declarations.Add(
                    new UtilityCssDeclaration(
                        "outline-style",
                        "none"));
                return true;
            }

            if (candidate.Value.Text is "solid" or "dashed" or "dotted" or "double")
            {
                declarations.Add(
                    new UtilityCssDeclaration(
                        "--tw-outline-style",
                        candidate.Value.Text));
                declarations.Add(
                    new UtilityCssDeclaration(
                        "outline-style",
                        candidate.Value.Text));
                return true;
            }

            if (TryResolveThemeProperty(
                    theme,
                    "--outline-width-" + candidate.Value.Text,
                    out var themeWidth) ||
                TryResolvePixelOrArbitrary(
                    candidate.Value,
                    out themeWidth))
            {
                declarations.Add(
                    new UtilityCssDeclaration(
                        "outline-style",
                        "var(--tw-outline-style)"));
                declarations.Add(
                    new UtilityCssDeclaration(
                        "outline-width",
                        themeWidth!));
                return true;
            }
        }

        if (candidate.Value.Kind == UtilityValueKind.Arbitrary &&
            candidate.Value.DataType != "color" &&
            candidate.Modifier is null &&
            IsSafeCandidateValue(candidate.Value))
        {
            var width = candidate.Value.Text;
            if (candidate.Value.DataType == "number" ||
                (candidate.Value.DataType is null &&
                 decimal.TryParse(
                     width,
                     NumberStyles.Number,
                     CultureInfo.InvariantCulture,
                     out _)))
            {
                width += "px";
            }

            declarations.Add(
                new UtilityCssDeclaration(
                    "outline-style",
                    "var(--tw-outline-style)"));
            declarations.Add(
                new UtilityCssDeclaration(
                    "outline-width",
                    width));
            return true;
        }

        return TryResolveColor(
            candidate,
            theme,
            "outline-color",
            declarations,
            candidateText,
            diagnostics);
    }

    private static bool TryResolveFont(
        UtilityCandidate candidate,
        UtilityTheme theme,
        ICollection<UtilityCssDeclaration> declarations,
        string candidateText,
        ICollection<UtilityCssDiagnostic> diagnostics)
    {
        if (candidate.Modifier is not null ||
            candidate.Value is null)
        {
            diagnostics.Add(UnsupportedValue(candidateText, candidate));
            return false;
        }

        if (candidate.Value.Kind == UtilityValueKind.Named &&
            theme.TryGetFontWeight(candidate.Value.Text, out var weight))
        {
            declarations.Add(new UtilityCssDeclaration("font-weight", weight!));
            return true;
        }

        string? family;
        if (candidate.Value.Kind is UtilityValueKind.Arbitrary or UtilityValueKind.CssVariable &&
            IsSafeCandidateValue(candidate.Value))
        {
            family = candidate.Value.Text;
        }
        else
        {
            family = theme.TryGetNamespaceValue(
                UtilityThemeNamespaceNames.FontFamily,
                candidate.Value.Text,
                out var themeFamily)
                    ? themeFamily
                    : candidate.Value.Text switch
                    {
                        "sans" => "ui-sans-serif, system-ui, sans-serif",
                        "serif" => "ui-serif, Georgia, Cambria, \"Times New Roman\", Times, serif",
                        "mono" => "ui-monospace, SFMono-Regular, Menlo, Monaco, Consolas, \"Liberation Mono\", \"Courier New\", monospace",
                        _ => null,
                    };
        }

        if (family is null)
        {
            diagnostics.Add(UnsupportedValue(candidateText, candidate));
            return false;
        }

        declarations.Add(new UtilityCssDeclaration("font-family", family));
        return true;
    }

    private static bool TryResolveRadius(
        UtilityCandidate candidate,
        UtilityTheme theme,
        ICollection<UtilityCssDeclaration> declarations,
        string candidateText,
        ICollection<UtilityCssDiagnostic> diagnostics)
    {
        if (candidate.Modifier is not null)
        {
            diagnostics.Add(
                Error(
                    candidateText,
                    UtilityCssDiagnosticCode.UnsupportedModifier,
                    "Border-radius utilities do not accept slash modifiers."));
            return false;
        }

        string? value;
        if (candidate.Value is null)
        {
            theme.TryGetRadius("DEFAULT", out value);
        }
        else if (candidate.Value.Kind is UtilityValueKind.Arbitrary or UtilityValueKind.CssVariable)
        {
            value = IsSafeCandidateValue(candidate.Value)
                ? candidate.Value.Text
                : null;
        }
        else if (candidate.Value.Text == "none")
        {
            value = "0";
        }
        else if (candidate.Value.Text == "full")
        {
            value = "calc(infinity * 1px)";
        }
        else
        {
            theme.TryGetRadius(candidate.Value.Text, out value);
        }

        if (value is null)
        {
            diagnostics.Add(UnsupportedValue(candidateText, candidate));
            return false;
        }

        foreach (var property in GetRadiusProperties(candidate.Root))
        {
            declarations.Add(new UtilityCssDeclaration(property, value));
        }

        return true;
    }

    private static bool TryResolveOpacity(
        UtilityCandidate candidate,
        ICollection<UtilityCssDeclaration> declarations,
        string candidateText,
        ICollection<UtilityCssDiagnostic> diagnostics)
    {
        if (candidate.Modifier is not null || candidate.Value is null)
        {
            diagnostics.Add(UnsupportedValue(candidateText, candidate));
            return false;
        }

        string? value;
        if (candidate.Value.Kind is UtilityValueKind.Arbitrary or UtilityValueKind.CssVariable)
        {
            value = IsSafeCandidateValue(candidate.Value)
                ? candidate.Value.Text
                : null;
        }
        else if (decimal.TryParse(
                     candidate.Value.Text,
                     NumberStyles.AllowDecimalPoint,
                     CultureInfo.InvariantCulture,
                     out var percentage) &&
                  percentage >= decimal.Zero &&
                  percentage <= 100m &&
                  IsValidOpacityValue(
                      candidate.Value.Text,
                      percentage))
        {
            value = (percentage / 100m).ToString(
                "0.############################",
                CultureInfo.InvariantCulture);
        }
        else
        {
            value = null;
        }

        if (value is null)
        {
            diagnostics.Add(UnsupportedValue(candidateText, candidate));
            return false;
        }

        declarations.Add(new UtilityCssDeclaration("opacity", value));
        return true;
    }

    private static bool TryResolveShadow(
        UtilityCandidate candidate,
        UtilityTheme theme,
        ICollection<UtilityCssDeclaration> declarations,
        string candidateText,
        ICollection<UtilityCssDiagnostic> diagnostics)
    {
        if (candidate.Root == "inset-shadow" &&
            candidate.Value is null &&
            !theme.TryGetNamespaceRawValue(
                UtilityThemeNamespaceNames.InsetShadow,
                "DEFAULT",
                out _) &&
            !theme.TryGetNamespaceValue(
                UtilityThemeNamespaceNames.InsetShadow,
                "DEFAULT",
                out _))
        {
            diagnostics.Add(UnsupportedValue(candidateText, candidate));
            return false;
        }

        if (IsShadowColorCandidate(candidate) &&
            TryGetColor(candidate, theme, out var color) &&
            TryApplyColorModifier(
                color!,
                candidate.Modifier,
                out var modifiedColor))
        {
            var colorProperty = candidate.Root switch
            {
                "shadow" => "--tw-shadow-color",
                "inset-shadow" => "--tw-inset-shadow-color",
                "text-shadow" => "--tw-text-shadow-color",
                _ => string.Empty,
            };
            if (colorProperty.Length != 0)
            {
                var alphaProperty = colorProperty.Replace(
                    "-color",
                    "-alpha");
                var composableColor =
                    modifiedColor == "inherit"
                        ? modifiedColor
                        : "color-mix(in oklab, " +
                          modifiedColor +
                          " var(" +
                          alphaProperty +
                          "), transparent)";
                declarations.Add(
                    new UtilityCssDeclaration(
                        colorProperty,
                        composableColor!));
                return true;
            }
        }

        string? alpha = null;
        if (candidate.Modifier is not null &&
            !TryResolveOpacityModifier(
                candidate.Modifier,
                out alpha))
        {
            diagnostics.Add(UnsupportedValue(candidateText, candidate));
            return false;
        }

        string? shadow;
        if (candidate.Value?.Kind is UtilityValueKind.Arbitrary or UtilityValueKind.CssVariable &&
            IsSafeCandidateValue(candidate.Value))
        {
            shadow = candidate.Value.Text;
        }
        else
        {
            var themeNamespace = candidate.Root switch
            {
                "shadow" => UtilityThemeNamespaceNames.Shadow,
                "inset-shadow" => UtilityThemeNamespaceNames.InsetShadow,
                "text-shadow" => UtilityThemeNamespaceNames.TextShadow,
                _ => string.Empty,
            };
            var tokenName = candidate.Value?.Text ?? "DEFAULT";
            if (themeNamespace.Length != 0 &&
                theme.TryGetNamespaceRawValue(
                    themeNamespace,
                    tokenName,
                    out var rawThemeShadow))
            {
                shadow = rawThemeShadow;
            }
            else
            {
                shadow =
                    themeNamespace.Length != 0 &&
                    theme.TryGetNamespaceValue(
                        themeNamespace,
                        tokenName,
                        out var themeShadow)
                        ? themeShadow
                        : ResolveNamedShadow(
                            candidate.Root,
                            candidate.Value?.Text);
            }
        }

        if (shadow is null)
        {
            diagnostics.Add(UnsupportedValue(candidateText, candidate));
            return false;
        }

        if (alpha is not null)
        {
            var alphaProperty = candidate.Root switch
            {
                "shadow" => "--tw-shadow-alpha",
                "inset-shadow" => "--tw-inset-shadow-alpha",
                "text-shadow" => "--tw-text-shadow-alpha",
                _ => null,
            };
            if (alphaProperty is null)
            {
                diagnostics.Add(UnsupportedValue(candidateText, candidate));
                return false;
            }

            declarations.Add(
                new UtilityCssDeclaration(
                    alphaProperty,
                    alpha));
            shadow = ReplaceShadowColorAlpha(
                shadow,
                alpha);
        }

        var isNullShadow =
            shadow.Equals(
                "0 0 #0000",
                StringComparison.OrdinalIgnoreCase);
        var colorPropertyName = candidate.Root switch
        {
            "shadow" => "--tw-shadow-color",
            "inset-shadow" => "--tw-inset-shadow-color",
            "text-shadow" => "--tw-text-shadow-color",
            _ => string.Empty,
        };
        if (candidate.Root == "inset-shadow" &&
            !isNullShadow)
        {
            shadow = UtilityShadowColorRewriter.EnsureInset(shadow);
        }

        if (!isNullShadow &&
            !shadow.Equals("none", StringComparison.OrdinalIgnoreCase))
        {
            shadow = UtilityShadowColorRewriter.ReplaceColors(
                shadow,
                colorPropertyName);
        }

        if (candidate.Root == "text-shadow")
        {
            declarations.Add(
                new UtilityCssDeclaration(
                    "text-shadow",
                    shadow));
            return true;
        }

        declarations.Add(
            new UtilityCssDeclaration(
                candidate.Root == "shadow"
                    ? "--tw-shadow"
                    : "--tw-inset-shadow",
                shadow));
        declarations.Add(
            new UtilityCssDeclaration(
                "box-shadow",
                "var(--tw-inset-shadow), var(--tw-inset-ring-shadow), var(--tw-ring-offset-shadow), var(--tw-ring-shadow), var(--tw-shadow)"));
        return true;
    }

    private static bool TryResolveRing(
        UtilityCandidate candidate,
        UtilityTheme theme,
        ICollection<UtilityCssDeclaration> declarations,
        string candidateText,
        ICollection<UtilityCssDiagnostic> diagnostics)
    {
        if (candidate.Value is not null &&
            TryGetColor(candidate, theme, out var color))
        {
            if (!TryApplyColorModifier(
                    color!,
                    candidate.Modifier,
                    out var modifiedColor))
            {
                diagnostics.Add(UnsupportedValue(candidateText, candidate));
                return false;
            }

            var colorProperty = candidate.Root switch
            {
                "ring" => "--tw-ring-color",
                "inset-ring" => "--tw-inset-ring-color",
                "ring-offset" => "--tw-ring-offset-color",
                _ => null,
            };
            if (colorProperty is null)
            {
                diagnostics.Add(UnsupportedValue(candidateText, candidate));
                return false;
            }

            declarations.Add(
                new UtilityCssDeclaration(
                    colorProperty,
                    modifiedColor!));
            return true;
        }

        if (candidate.Modifier is not null)
        {
            diagnostics.Add(UnsupportedValue(candidateText, candidate));
            return false;
        }

        string? width;
        if (candidate.Value is null)
        {
            width = candidate.Root is "ring" or "inset-ring"
                ? "1px"
                : null;
        }
        else if (candidate.Value.Kind is UtilityValueKind.Arbitrary or UtilityValueKind.CssVariable &&
                 candidate.Value.DataType != "color" &&
                 IsSafeCandidateValue(candidate.Value))
        {
            width = candidate.Value.Text;
        }
        else if (theme.TryGetNamespaceValue(
                     candidate.Root == "ring-offset"
                         ? "ring-offset-width"
                         : "ring-width",
                     candidate.Value.Text,
                     out var themeWidth))
        {
            width = themeWidth;
        }
        else if (IsPositiveInteger(candidate.Value.Text) ||
                 candidate.Value.Text == "0")
        {
            width = candidate.Value.Text + "px";
        }
        else
        {
            width = null;
        }

        if (width is null)
        {
            diagnostics.Add(UnsupportedValue(candidateText, candidate));
            return false;
        }

        if (candidate.Root == "ring")
        {
            declarations.Add(
                new UtilityCssDeclaration(
                    "--tw-ring-shadow",
                    $"var(--tw-ring-inset,) 0 0 0 calc({width} + var(--tw-ring-offset-width, 0px)) var(--tw-ring-color, currentcolor)"));
            declarations.Add(
                new UtilityCssDeclaration(
                    "box-shadow",
                    "var(--tw-inset-shadow, 0 0 #0000), var(--tw-inset-ring-shadow, 0 0 #0000), var(--tw-ring-offset-shadow, 0 0 #0000), var(--tw-ring-shadow, 0 0 #0000), var(--tw-shadow, 0 0 #0000)"));
            return true;
        }

        if (candidate.Root == "inset-ring")
        {
            declarations.Add(
                new UtilityCssDeclaration(
                    "--tw-inset-ring-shadow",
                    $"inset 0 0 0 {width} var(--tw-inset-ring-color, currentcolor)"));
            declarations.Add(
                new UtilityCssDeclaration(
                    "box-shadow",
                    "var(--tw-inset-shadow, 0 0 #0000), var(--tw-inset-ring-shadow, 0 0 #0000), var(--tw-ring-offset-shadow, 0 0 #0000), var(--tw-ring-shadow, 0 0 #0000), var(--tw-shadow, 0 0 #0000)"));
            return true;
        }

        if (candidate.Root == "ring-offset")
        {
            declarations.Add(
                new UtilityCssDeclaration(
                    "--tw-ring-offset-width",
                    width));
            declarations.Add(
                new UtilityCssDeclaration(
                    "--tw-ring-offset-shadow",
                    "var(--tw-ring-inset,) 0 0 0 var(--tw-ring-offset-width) var(--tw-ring-offset-color)"));
            return true;
        }

        diagnostics.Add(UnsupportedValue(candidateText, candidate));
        return false;
    }

    private static bool TryResolveMask(
        UtilityCandidate candidate,
        UtilityTheme theme,
        ICollection<UtilityCssDeclaration> declarations,
        string candidateText,
        ICollection<UtilityCssDiagnostic> diagnostics)
    {
        if (UtilityGradientMaskResolution.IsMaskStopRoot(candidate.Root))
        {
            if (UtilityGradientMaskResolution.TryResolveMaskStop(
                    candidate,
                    theme,
                    declarations))
            {
                return true;
            }

            diagnostics.Add(UnsupportedValue(candidateText, candidate));
            return false;
        }

        if (candidate.Modifier is not null)
        {
            diagnostics.Add(UnsupportedValue(candidateText, candidate));
            return false;
        }

        if (candidate.Root == "mask")
        {
            if (candidate.Value?.Kind == UtilityValueKind.Named)
            {
                var namedDeclaration = candidate.Value.Text switch
                {
                    "none" => new UtilityCssDeclaration("mask-image", "none"),
                    "add" => new UtilityCssDeclaration("mask-composite", "add"),
                    "subtract" => new UtilityCssDeclaration("mask-composite", "subtract"),
                    "intersect" => new UtilityCssDeclaration("mask-composite", "intersect"),
                    "exclude" => new UtilityCssDeclaration("mask-composite", "exclude"),
                    "alpha" => new UtilityCssDeclaration("mask-mode", "alpha"),
                    "luminance" => new UtilityCssDeclaration("mask-mode", "luminance"),
                    "match" => new UtilityCssDeclaration("mask-mode", "match-source"),
                    "type-alpha" => new UtilityCssDeclaration("mask-type", "alpha"),
                    "type-luminance" => new UtilityCssDeclaration("mask-type", "luminance"),
                    "auto" => new UtilityCssDeclaration("mask-size", "auto"),
                    "cover" => new UtilityCssDeclaration("mask-size", "cover"),
                    "contain" => new UtilityCssDeclaration("mask-size", "contain"),
                    "top" => new UtilityCssDeclaration("mask-position", "top"),
                    "top-left" => new UtilityCssDeclaration("mask-position", "left top"),
                    "top-right" => new UtilityCssDeclaration("mask-position", "right top"),
                    "bottom" => new UtilityCssDeclaration("mask-position", "bottom"),
                    "bottom-left" => new UtilityCssDeclaration("mask-position", "left bottom"),
                    "bottom-right" => new UtilityCssDeclaration("mask-position", "right bottom"),
                    "left" => new UtilityCssDeclaration("mask-position", "left"),
                    "right" => new UtilityCssDeclaration("mask-position", "right"),
                    "center" => new UtilityCssDeclaration("mask-position", "center"),
                    "repeat" => new UtilityCssDeclaration("mask-repeat", "repeat"),
                    "no-repeat" => new UtilityCssDeclaration("mask-repeat", "no-repeat"),
                    "repeat-x" => new UtilityCssDeclaration("mask-repeat", "repeat-x"),
                    "repeat-y" => new UtilityCssDeclaration("mask-repeat", "repeat-y"),
                    "repeat-round" => new UtilityCssDeclaration("mask-repeat", "round"),
                    "repeat-space" => new UtilityCssDeclaration("mask-repeat", "space"),
                    "circle" => new UtilityCssDeclaration("--tw-mask-radial-shape", "circle"),
                    "ellipse" => new UtilityCssDeclaration("--tw-mask-radial-shape", "ellipse"),
                    "radial-closest-side" => new UtilityCssDeclaration("--tw-mask-radial-size", "closest-side"),
                    "radial-farthest-side" => new UtilityCssDeclaration("--tw-mask-radial-size", "farthest-side"),
                    "radial-closest-corner" => new UtilityCssDeclaration("--tw-mask-radial-size", "closest-corner"),
                    "radial-farthest-corner" => new UtilityCssDeclaration("--tw-mask-radial-size", "farthest-corner"),
                    _ => null,
                };
                if (namedDeclaration is not null)
                {
                    declarations.Add(namedDeclaration);
                    return true;
                }
            }

            return TryResolveArbitraryPropertyValue(
                candidate,
                "mask-image",
                declarations,
                candidateText,
                diagnostics);
        }

        if (candidate.Root == "mask-radial-at")
        {
            string? position;
            if (candidate.Value?.Kind is UtilityValueKind.Arbitrary or UtilityValueKind.CssVariable &&
                IsSafeCandidateValue(candidate.Value))
            {
                position = candidate.Value.Text;
            }
            else
            {
                position = candidate.Value?.Text switch
                {
                    "top" => "top",
                    "top-left" => "top left",
                    "top-right" => "top right",
                    "bottom" => "bottom",
                    "bottom-left" => "bottom left",
                    "bottom-right" => "bottom right",
                    "left" => "left",
                    "right" => "right",
                    "center" => "center",
                    _ => null,
                };
            }

            if (position is null)
            {
                diagnostics.Add(UnsupportedValue(candidateText, candidate));
                return false;
            }

            declarations.Add(
                new UtilityCssDeclaration(
                    "--tw-mask-radial-position",
                    position));
            return true;
        }

        if (candidate.Root == "mask-radial")
        {
            var namedSize = candidate.Value?.Kind == UtilityValueKind.Named
                ? candidate.Value.Text switch
                {
                    "closest-side" => "closest-side",
                    "farthest-side" => "farthest-side",
                    "closest-corner" => "closest-corner",
                    "farthest-corner" => "farthest-corner",
                    _ => null,
                }
                : null;
            if (namedSize is not null &&
                !candidate.IsNegative)
            {
                declarations.Add(
                    new UtilityCssDeclaration(
                        "--tw-mask-radial-size",
                        namedSize));
                return true;
            }

            if (candidate.IsNegative ||
                candidate.Value is null ||
                candidate.Value.Kind is not (UtilityValueKind.Arbitrary or UtilityValueKind.CssVariable) ||
                !IsSafeCandidateValue(candidate.Value))
            {
                diagnostics.Add(UnsupportedValue(candidateText, candidate));
                return false;
            }

            AddMaskComposition(declarations);
            declarations.Add(
                new UtilityCssDeclaration(
                    "--tw-mask-radial",
                    "radial-gradient(var(--tw-mask-radial-stops, var(--tw-mask-radial-size)))"));
            declarations.Add(
                new UtilityCssDeclaration(
                    "--tw-mask-radial-size",
                    candidate.Value.Text));
            return true;
        }

        if (candidate.Root is not ("mask-linear" or "mask-conic") ||
            !TryResolveMaskAngle(
                candidate,
                out var angle))
        {
            diagnostics.Add(UnsupportedValue(candidateText, candidate));
            return false;
        }
        var geometry = candidate.Root.Substring("mask-".Length);
        AddMaskComposition(declarations);
        declarations.Add(
            new UtilityCssDeclaration(
                "--tw-mask-" + geometry,
                geometry +
                "-gradient(var(--tw-mask-" +
                geometry +
                "-stops, var(--tw-mask-" +
                geometry +
                "-position)))"));
        declarations.Add(
            new UtilityCssDeclaration(
                "--tw-mask-" + geometry + "-position",
                angle!));
        return true;
    }

    private static void AddMaskComposition(
        ICollection<UtilityCssDeclaration> declarations)
    {
        declarations.Add(
            new UtilityCssDeclaration(
                "mask-image",
                "var(--tw-mask-linear), var(--tw-mask-radial), var(--tw-mask-conic)"));
        declarations.Add(
            new UtilityCssDeclaration(
                "mask-composite",
                "intersect"));
    }

    private static bool TryResolveMaskAngle(
        UtilityCandidate candidate,
        out string? angle)
    {
        angle = null;
        if (candidate.Modifier is not null ||
            candidate.Value is null)
        {
            return false;
        }

        if (candidate.Value.Kind is UtilityValueKind.Arbitrary or UtilityValueKind.CssVariable)
        {
            if (!IsSafeCandidateValue(candidate.Value))
            {
                return false;
            }

            angle = candidate.IsNegative
                ? "calc(" + candidate.Value.Text + " * -1)"
                : candidate.Value.Text;
            return true;
        }

        if (!int.TryParse(
                candidate.Value.Text,
                NumberStyles.None,
                CultureInfo.InvariantCulture,
                out var degrees) ||
            degrees < 0 ||
            !string.Equals(
                degrees.ToString(CultureInfo.InvariantCulture),
                candidate.Value.Text,
                StringComparison.Ordinal))
        {
            return false;
        }

        if (degrees == 0)
        {
            angle = "0deg";
        }
        else if (degrees == 1)
        {
            angle = candidate.IsNegative
                ? "-1deg"
                : "1deg";
        }
        else
        {
            angle =
                "calc(1deg * " +
                (candidate.IsNegative
                    ? "-"
                    : string.Empty) +
                candidate.Value.Text +
                ")";
        }

        return true;
    }

    private static bool TryResolveFilter(
        UtilityCandidate candidate,
        UtilityTheme theme,
        bool isBackdrop,
        ICollection<UtilityCssDeclaration> declarations,
        string candidateText,
        ICollection<UtilityCssDiagnostic> diagnostics)
    {
        var plainRoot = isBackdrop
            ? candidate.Root.Substring("backdrop-".Length)
            : candidate.Root;
        if (plainRoot == "filter")
        {
            string? filter;
            if (candidate.Modifier is not null)
            {
                filter = null;
            }
            else if (candidate.Value is null)
            {
                filter = GetFilterComposition(
                    isBackdrop
                        ? "--tw-backdrop-"
                        : "--tw-");
            }
            else if (candidate.Value.Kind is UtilityValueKind.Arbitrary or UtilityValueKind.CssVariable &&
                     IsSafeCandidateValue(candidate.Value))
            {
                filter = candidate.Value.Text;
            }
            else
            {
                filter = candidate.Value.Text == "none"
                    ? "none"
                    : null;
            }

            if (filter is null)
            {
                diagnostics.Add(UnsupportedValue(candidateText, candidate));
                return false;
            }

            if (isBackdrop)
            {
                declarations.Add(
                    new UtilityCssDeclaration(
                        "-webkit-backdrop-filter",
                        filter));
            }

            declarations.Add(
                new UtilityCssDeclaration(
                    isBackdrop
                        ? "backdrop-filter"
                        : "filter",
                    filter));
            return true;
        }

        if (plainRoot == "drop-shadow" &&
            !isBackdrop)
        {
            return TryResolveDropShadow(
                candidate,
                theme,
                declarations,
                candidateText,
                diagnostics);
        }

        if (candidate.IsNegative &&
            plainRoot != "hue-rotate")
        {
            diagnostics.Add(UnsupportedValue(candidateText, candidate));
            return false;
        }

        if (plainRoot == "drop-shadow" &&
            candidate.Modifier is not null)
        {
            if (!TryResolveOpacityModifier(
                    candidate.Modifier,
                    out var alpha))
            {
                diagnostics.Add(UnsupportedValue(candidateText, candidate));
                return false;
            }

            declarations.Add(
                new UtilityCssDeclaration(
                    "--tw-drop-shadow-alpha",
                    alpha!));
        }

        if (!TryResolveFilterFunction(
                candidate,
                theme,
                plainRoot,
                out var function))
        {
            diagnostics.Add(UnsupportedValue(candidateText, candidate));
            return false;
        }

        if (plainRoot == "blur" &&
            candidate.Value?.Text == "none")
        {
            function = string.Empty;
        }

        var variablePrefix = isBackdrop
            ? "--tw-backdrop-"
            : "--tw-";
        declarations.Add(
            new UtilityCssDeclaration(
                variablePrefix + plainRoot,
                function!));
        if (isBackdrop)
        {
            declarations.Add(
                new UtilityCssDeclaration(
                    "-webkit-backdrop-filter",
                    GetFilterComposition(variablePrefix)));
        }

        declarations.Add(
            new UtilityCssDeclaration(
                isBackdrop
                    ? "backdrop-filter"
                    : "filter",
                GetFilterComposition(variablePrefix)));
        return true;
    }

    private static bool TryResolveDropShadow(
        UtilityCandidate candidate,
        UtilityTheme theme,
        ICollection<UtilityCssDeclaration> declarations,
        string candidateText,
        ICollection<UtilityCssDiagnostic> diagnostics)
    {
        if (IsShadowColorCandidate(candidate) &&
            TryGetColor(candidate, theme, out var color) &&
            TryApplyColorModifier(
                color!,
                candidate.Modifier,
                out var modifiedColor))
        {
            var composableColor =
                modifiedColor == "inherit"
                    ? modifiedColor
                    : "color-mix(in oklab, " +
                      modifiedColor +
                      " var(--tw-drop-shadow-alpha), transparent)";
            declarations.Add(
                new UtilityCssDeclaration(
                    "--tw-drop-shadow-color",
                    composableColor!));
            declarations.Add(
                new UtilityCssDeclaration(
                    "--tw-drop-shadow",
                    "var(--tw-drop-shadow-size)"));
            return true;
        }

        string? alpha = null;
        if (candidate.Modifier is not null &&
            !TryResolveOpacityModifier(
                candidate.Modifier,
                out alpha) &&
            candidate.Value is not null)
        {
            diagnostics.Add(UnsupportedValue(candidateText, candidate));
            return false;
        }

        string? shadow;
        if (candidate.Value?.Kind is UtilityValueKind.Arbitrary or UtilityValueKind.CssVariable &&
            IsSafeCandidateValue(candidate.Value))
        {
            shadow = candidate.Value.Text;
        }
        else
        {
            var tokenName = candidate.Value?.Text ?? "DEFAULT";
            shadow =
                theme.TryGetNamespaceRawValue(
                    UtilityThemeNamespaceNames.DropShadow,
                    tokenName,
                    out var rawThemeShadow)
                    ? rawThemeShadow
                    : theme.TryGetNamespaceValue(
                        UtilityThemeNamespaceNames.DropShadow,
                        tokenName,
                        out var themeShadow)
                        ? themeShadow
                        : candidate.Value?.Text switch
                        {
                            null => "0 1px 2px rgb(0 0 0 / 0.1)",
                            "xs" => "0 1px 1px rgb(0 0 0 / 0.05)",
                            "sm" => "0 1px 2px rgb(0 0 0 / 0.15)",
                            "md" => "0 3px 3px rgb(0 0 0 / 0.12)",
                            "lg" => "0 4px 4px rgb(0 0 0 / 0.15)",
                            "xl" => "0 9px 7px rgb(0 0 0 / 0.1)",
                            "2xl" => "0 25px 25px rgb(0 0 0 / 0.15)",
                            "none" => null,
                            _ => null,
                        };
        }

        if (candidate.Value?.Text == "none" &&
            candidate.Modifier is null)
        {
            declarations.Add(
                new UtilityCssDeclaration(
                    "--tw-drop-shadow",
                    string.Empty));
            declarations.Add(
                new UtilityCssDeclaration(
                    "filter",
                    GetFilterComposition("--tw-")));
            return true;
        }

        if (shadow is null)
        {
            diagnostics.Add(UnsupportedValue(candidateText, candidate));
            return false;
        }

        if (alpha is not null)
        {
            declarations.Add(
                new UtilityCssDeclaration(
                    "--tw-drop-shadow-alpha",
                    alpha));
            shadow = ReplaceShadowColorAlpha(
                shadow,
                alpha);
        }

        var size = UtilityShadowColorRewriter.ToDropShadowFunctions(
            shadow,
            "--tw-drop-shadow-color");
        declarations.Add(
            new UtilityCssDeclaration(
                "--tw-drop-shadow-size",
                size));
        declarations.Add(
            new UtilityCssDeclaration(
                "--tw-drop-shadow",
                "var(--tw-drop-shadow-size)"));
        declarations.Add(
            new UtilityCssDeclaration(
                "filter",
                GetFilterComposition("--tw-")));
        return true;
    }

    private static bool TryResolveTransition(
        UtilityCandidate candidate,
        UtilityTheme theme,
        ICollection<UtilityCssDeclaration> declarations,
        string candidateText,
        ICollection<UtilityCssDiagnostic> diagnostics)
    {
        if (candidate.IsNegative ||
            candidate.Modifier is not null)
        {
            diagnostics.Add(UnsupportedValue(candidateText, candidate));
            return false;
        }

        if (candidate.Root is "duration" or "delay")
        {
            string? time = null;
            if (candidate.Value?.Kind == UtilityValueKind.Named)
            {
                TryResolveThemeProperty(
                    theme,
                    "--transition-" + candidate.Root + "-" + candidate.Value.Text,
                    out time);
            }

            if (time is null &&
                !TryResolveTime(candidate.Value, out time))
            {
                diagnostics.Add(UnsupportedValue(candidateText, candidate));
                return false;
            }

            if (candidate.Root == "duration")
            {
                declarations.Add(
                    new UtilityCssDeclaration(
                        "--tw-duration",
                        time!));
            }

            declarations.Add(
                new UtilityCssDeclaration(
                    candidate.Root == "duration"
                        ? "transition-duration"
                        : "transition-delay",
                    time!));
            return true;
        }

        if (candidate.Root == "ease")
        {
            if (candidate.Value?.Kind == UtilityValueKind.Named &&
                candidate.Value.Text == "initial")
            {
                declarations.Add(
                    new UtilityCssDeclaration(
                        "--tw-ease",
                        "initial"));
                return true;
            }

            string? easing;
            if (candidate.Value?.Kind == UtilityValueKind.Named &&
                theme.TryGetNamespaceValue(
                    UtilityThemeNamespaceNames.TransitionTiming,
                    candidate.Value.Text,
                    out var themeEasing))
            {
                easing = themeEasing;
            }
            else if (!TryResolveTimingFunction(candidate.Value, out easing))
            {
                diagnostics.Add(UnsupportedValue(candidateText, candidate));
                return false;
            }

            declarations.Add(
                new UtilityCssDeclaration(
                    "--tw-ease",
                    easing!));
            declarations.Add(
                new UtilityCssDeclaration(
                    "transition-timing-function",
                    easing!));
            return true;
        }

        if (candidate.Root == "animate")
        {
            if (candidate.Value?.Kind is UtilityValueKind.Arbitrary or UtilityValueKind.CssVariable &&
                IsSafeCandidateValue(candidate.Value))
            {
                declarations.Add(
                    new UtilityCssDeclaration(
                        "animation",
                        candidate.Value.Text));
                return true;
            }

            if (candidate.Value?.Text == "none")
            {
                declarations.Add(new UtilityCssDeclaration("animation", "none"));
                return true;
            }

            if (candidate.Value?.Kind == UtilityValueKind.Named &&
                theme.TryGetNamespaceValue(
                    UtilityThemeNamespaceNames.Animation,
                    candidate.Value.Text,
                    out var animation))
            {
                declarations.Add(
                    new UtilityCssDeclaration(
                        "animation",
                        animation!));
                return true;
            }

            diagnostics.Add(UnsupportedValue(candidateText, candidate));
            return false;
        }

        if (candidate.Root != "transition")
        {
            diagnostics.Add(UnsupportedValue(candidateText, candidate));
            return false;
        }

        if (candidate.Value?.Text is "discrete" or "normal")
        {
            declarations.Add(
                new UtilityCssDeclaration(
                    "transition-behavior",
                    candidate.Value.Text == "discrete"
                        ? "allow-discrete"
                        : "normal"));
            return true;
        }

        string? property = null;
        if (candidate.Value?.Kind is UtilityValueKind.Arbitrary or UtilityValueKind.CssVariable &&
            IsSafeCandidateValue(candidate.Value))
        {
            property = candidate.Value.Text;
        }
        else if (candidate.Value?.Kind == UtilityValueKind.Named)
        {
            TryResolveThemeProperty(
                theme,
                "--transition-property-" + candidate.Value.Text,
                out property);
        }

        if (property is null)
        {
            property = candidate.Value?.Text switch
            {
                null => "color, background-color, border-color, outline-color, text-decoration-color, fill, stroke, --tw-gradient-from, --tw-gradient-via, --tw-gradient-to, opacity, box-shadow, transform, translate, scale, rotate, filter, -webkit-backdrop-filter, backdrop-filter, display, content-visibility, overlay, pointer-events",
                "all" => "all",
                "colors" => "color, background-color, border-color, outline-color, text-decoration-color, fill, stroke, --tw-gradient-from, --tw-gradient-via, --tw-gradient-to",
                "opacity" => "opacity",
                "shadow" => "box-shadow",
                "transform" => "transform, translate, scale, rotate",
                "none" => "none",
                _ => null,
            };
        }

        if (property is null)
        {
            diagnostics.Add(UnsupportedValue(candidateText, candidate));
            return false;
        }

        declarations.Add(new UtilityCssDeclaration("transition-property", property));
        if (property != "none")
        {
            TryResolveThemeProperty(
                theme,
                "--default-transition-timing-function",
                out var defaultTimingFunction);
            TryResolveThemeProperty(
                theme,
                "--default-transition-duration",
                out var defaultDuration);
            declarations.Add(
                new UtilityCssDeclaration(
                    "transition-timing-function",
                    "var(--tw-ease, " +
                    (defaultTimingFunction ?? "ease") +
                    ")"));
            declarations.Add(
                new UtilityCssDeclaration(
                    "transition-duration",
                    "var(--tw-duration, " +
                    (defaultDuration ?? "0s") +
                    ")"));
        }

        return true;
    }

    private static bool TryResolveTransform(
        UtilityCandidate candidate,
        UtilityTheme theme,
        ICollection<UtilityCssDeclaration> declarations,
        string candidateText,
        ICollection<UtilityCssDiagnostic> diagnostics)
    {
        if (UtilityGradientMaskResolution.IsDepthTransformRoot(candidate.Root))
        {
            if (UtilityGradientMaskResolution.TryResolveDepthTransform(
                    candidate,
                    theme,
                    declarations))
            {
                return true;
            }

            diagnostics.Add(UnsupportedValue(candidateText, candidate));
            return false;
        }

        if (candidate.Root.StartsWith("translate", StringComparison.Ordinal))
        {
            if (!TryResolveSizeValue(candidate, theme, out var translate))
            {
                diagnostics.Add(UnsupportedValue(candidateText, candidate));
                return false;
            }

            if (candidate.IsNegative)
            {
                translate = $"calc({translate} * -1)";
            }

            if (candidate.Root is "translate" or "translate-x")
            {
                declarations.Add(
                    new UtilityCssDeclaration(
                        "--tw-translate-x",
                        translate!));
            }

            if (candidate.Root is "translate" or "translate-y")
            {
                declarations.Add(
                    new UtilityCssDeclaration(
                        "--tw-translate-y",
                        translate!));
            }

            declarations.Add(
                new UtilityCssDeclaration(
                    "translate",
                    "var(--tw-translate-x) var(--tw-translate-y)"));
            return true;
        }

        if (candidate.Root.StartsWith("rotate", StringComparison.Ordinal))
        {
            if (!TryResolveAngle(candidate, out var rotate))
            {
                diagnostics.Add(UnsupportedValue(candidateText, candidate));
                return false;
            }

            if (candidate.Root is "rotate-x" or "rotate-y")
            {
                declarations.Add(
                    new UtilityCssDeclaration(
                        candidate.Root == "rotate-x"
                            ? "--tw-rotate-x"
                            : "--tw-rotate-y",
                        (candidate.Root == "rotate-x"
                            ? "rotateX("
                            : "rotateY(") +
                        rotate +
                        ")"));
                declarations.Add(
                    new UtilityCssDeclaration(
                        "transform",
                        "var(--tw-rotate-x,) var(--tw-rotate-y,) var(--tw-rotate-z,) var(--tw-skew-x,) var(--tw-skew-y,)"));
                return true;
            }

            declarations.Add(
                new UtilityCssDeclaration(
                    "rotate",
                    rotate!));
            return true;
        }

        if (candidate.Root.StartsWith("skew", StringComparison.Ordinal))
        {
            if (!TryResolveAngle(candidate, out var skew))
            {
                diagnostics.Add(UnsupportedValue(candidateText, candidate));
                return false;
            }

            if (candidate.Root is "skew" or "skew-x")
            {
                declarations.Add(
                    new UtilityCssDeclaration(
                        "--tw-skew-x",
                        "skewX(" + skew + ")"));
            }

            if (candidate.Root is "skew" or "skew-y")
            {
                declarations.Add(
                    new UtilityCssDeclaration(
                        "--tw-skew-y",
                        "skewY(" + skew + ")"));
            }

            declarations.Add(
                new UtilityCssDeclaration(
                    "transform",
                    "var(--tw-rotate-x,) var(--tw-rotate-y,) var(--tw-rotate-z,) var(--tw-skew-x,) var(--tw-skew-y,)"));
            return true;
        }

        if (candidate.Root.StartsWith("scale", StringComparison.Ordinal))
        {
            if (!TryResolveScale(candidate, out var scale))
            {
                diagnostics.Add(UnsupportedValue(candidateText, candidate));
                return false;
            }

            if (candidate.Root == "scale" &&
                candidate.Value?.Kind is UtilityValueKind.Arbitrary or UtilityValueKind.CssVariable)
            {
                declarations.Add(
                    new UtilityCssDeclaration(
                        "scale",
                        scale!));
                return true;
            }

            if (candidate.Root is "scale" or "scale-x")
            {
                declarations.Add(
                    new UtilityCssDeclaration(
                        "--tw-scale-x",
                        scale!));
            }

            if (candidate.Root is "scale" or "scale-y")
            {
                declarations.Add(
                    new UtilityCssDeclaration(
                        "--tw-scale-y",
                        scale!));
            }

            if (candidate.Root == "scale")
            {
                declarations.Add(
                    new UtilityCssDeclaration(
                        "--tw-scale-z",
                        scale!));
            }

            declarations.Add(
                new UtilityCssDeclaration(
                    "scale",
                    "var(--tw-scale-x) var(--tw-scale-y)"));
            return true;
        }

        if (candidate.Root is "origin" or "perspective-origin")
        {
            if (candidate.Modifier is not null ||
                !TryResolvePosition(candidate.Value, out var origin))
            {
                diagnostics.Add(UnsupportedValue(candidateText, candidate));
                return false;
            }

            declarations.Add(
                new UtilityCssDeclaration(
                    candidate.Root == "origin"
                        ? "transform-origin"
                        : "perspective-origin",
                    origin!));
            return true;
        }

        if (candidate.Root == "perspective")
        {
            if (candidate.Modifier is not null ||
                !TryResolvePerspective(
                    candidate.Value,
                    theme,
                    out var perspective))
            {
                diagnostics.Add(UnsupportedValue(candidateText, candidate));
                return false;
            }

            declarations.Add(new UtilityCssDeclaration("perspective", perspective!));
            return true;
        }

        if (candidate.Root == "transform")
        {
            if (candidate.Modifier is not null)
            {
                diagnostics.Add(UnsupportedValue(candidateText, candidate));
                return false;
            }

            if (candidate.Value?.Kind == UtilityValueKind.Named)
            {
                var transformMetadata = candidate.Value.Text switch
                {
                    "flat" => new UtilityCssDeclaration("transform-style", "flat"),
                    "3d" => new UtilityCssDeclaration("transform-style", "preserve-3d"),
                    "content" => new UtilityCssDeclaration("transform-box", "content-box"),
                    "border" => new UtilityCssDeclaration("transform-box", "border-box"),
                    "fill" => new UtilityCssDeclaration("transform-box", "fill-box"),
                    "stroke" => new UtilityCssDeclaration("transform-box", "stroke-box"),
                    "view" => new UtilityCssDeclaration("transform-box", "view-box"),
                    _ => null,
                };
                if (transformMetadata is not null)
                {
                    declarations.Add(transformMetadata);
                    return true;
                }
            }

            if (candidate.Value?.Text == "none")
            {
                declarations.Add(new UtilityCssDeclaration("transform", "none"));
                return true;
            }

            var transformValue =
                "var(--tw-rotate-x,) var(--tw-rotate-y,) var(--tw-rotate-z,) var(--tw-skew-x,) var(--tw-skew-y,)";
            if (candidate.Value?.Text == "gpu")
            {
                transformValue = "translateZ(0) " + transformValue;
            }
            else if (candidate.Value?.Text is not null and not "cpu")
            {
                if (candidate.Value.Kind is UtilityValueKind.Arbitrary or UtilityValueKind.CssVariable &&
                    IsSafeCandidateValue(candidate.Value))
                {
                    transformValue = candidate.Value.Text;
                }
                else
                {
                    diagnostics.Add(UnsupportedValue(candidateText, candidate));
                    return false;
                }
            }

            declarations.Add(new UtilityCssDeclaration("transform", transformValue));
            return true;
        }

        diagnostics.Add(UnsupportedValue(candidateText, candidate));
        return false;
    }

    private static bool TryResolveScrollbarColor(
        UtilityCandidate candidate,
        UtilityTheme theme,
        ICollection<UtilityCssDeclaration> declarations,
        string candidateText,
        ICollection<UtilityCssDiagnostic> diagnostics)
    {
        if (!TryGetColor(candidate, theme, out var color) ||
            !TryApplyColorModifier(
                color!,
                candidate.Modifier,
                out var modifiedColor))
        {
            diagnostics.Add(UnsupportedValue(candidateText, candidate));
            return false;
        }

        var variable = candidate.Root == "scrollbar-thumb"
            ? "--tw-scrollbar-thumb"
            : "--tw-scrollbar-track";
        declarations.Add(new UtilityCssDeclaration(variable, modifiedColor!));
        declarations.Add(
            new UtilityCssDeclaration(
                "scrollbar-color",
                "var(--tw-scrollbar-thumb, transparent) var(--tw-scrollbar-track, transparent)"));
        return true;
    }

    private static bool TryResolveSemanticColor(
        UtilityCandidate candidate,
        UtilityTheme theme,
        ICollection<UtilityCssDeclaration> declarations,
        string candidateText,
        ICollection<UtilityCssDiagnostic> diagnostics)
    {
        var property = candidate.Root switch
        {
            "accent" => "accent-color",
            "caret" => "caret-color",
            "placeholder" => "color",
            _ => string.Empty,
        };
        if (candidate.Root == "accent" &&
            candidate.Value?.Text == "auto" &&
            candidate.Modifier is null)
        {
            declarations.Add(new UtilityCssDeclaration(property, "auto"));
            return true;
        }

        return property.Length != 0 &&
               TryResolveColor(
                   candidate,
                   theme,
                   property,
                   declarations,
                   candidateText,
                   diagnostics);
    }

    private static bool TryResolveSvg(
        UtilityCandidate candidate,
        UtilityTheme theme,
        ICollection<UtilityCssDeclaration> declarations,
        string candidateText,
        ICollection<UtilityCssDiagnostic> diagnostics)
    {
        if (candidate.Root == "stroke" &&
            candidate.Value is not null &&
            candidate.Modifier is null &&
            (candidate.Value.Kind == UtilityValueKind.Arbitrary &&
             candidate.Value.DataType != "color" ||
             candidate.Value.Kind == UtilityValueKind.Named &&
             candidate.Value.Text is "0" or "1" or "2"))
        {
            var width = candidate.Value.Kind == UtilityValueKind.Named
                ? candidate.Value.Text
                : candidate.Value.Text;
            if (!UtilityCssText.IsSafeValue(width))
            {
                diagnostics.Add(UnsupportedValue(candidateText, candidate));
                return false;
            }

            declarations.Add(new UtilityCssDeclaration("stroke-width", width));
            return true;
        }

        if (candidate.Value?.Text == "none" &&
            candidate.Modifier is null)
        {
            declarations.Add(new UtilityCssDeclaration(candidate.Root, "none"));
            return true;
        }

        return TryResolveColor(
            candidate,
            theme,
            candidate.Root,
            declarations,
            candidateText,
            diagnostics);
    }

    private static bool TryResolveLeading(
        UtilityCandidate candidate,
        UtilityTheme theme,
        ICollection<UtilityCssDeclaration> declarations,
        string candidateText,
        ICollection<UtilityCssDiagnostic> diagnostics)
    {
        if (candidate.Modifier is not null ||
            candidate.Value is null)
        {
            diagnostics.Add(UnsupportedValue(candidateText, candidate));
            return false;
        }

        string? value;
        if (candidate.Value.Kind is UtilityValueKind.Arbitrary or UtilityValueKind.CssVariable)
        {
            value = IsSafeCandidateValue(candidate.Value)
                ? candidate.Value.Text
                : null;
        }
        else
        {
            value = theme.TryGetNamespaceValue(
                UtilityThemeNamespaceNames.LineHeight,
                candidate.Value.Text,
                out var themeValue)
                    ? themeValue
                    : candidate.Value.Text switch
                    {
                        "none" => "1",
                        "tight" => "1.25",
                        "snug" => "1.375",
                        "normal" => "1.5",
                        "relaxed" => "1.625",
                        "loose" => "2",
                        _ => theme.TryGetSpacing(candidate.Value.Text, out var spacing)
                            ? spacing
                            : null,
                    };
        }

        if (value is null)
        {
            diagnostics.Add(UnsupportedValue(candidateText, candidate));
            return false;
        }

        declarations.Add(
            new UtilityCssDeclaration(
                "--tw-leading",
                value));
        declarations.Add(new UtilityCssDeclaration("line-height", value));
        return true;
    }

    private static bool TryResolveTracking(
        UtilityCandidate candidate,
        UtilityTheme theme,
        ICollection<UtilityCssDeclaration> declarations,
        string candidateText,
        ICollection<UtilityCssDiagnostic> diagnostics)
    {
        if (candidate.Modifier is not null ||
            candidate.Value is null)
        {
            diagnostics.Add(UnsupportedValue(candidateText, candidate));
            return false;
        }

        string? value;
        if (candidate.Value.Kind is UtilityValueKind.Arbitrary or UtilityValueKind.CssVariable)
        {
            value = IsSafeCandidateValue(candidate.Value)
                ? candidate.Value.Text
                : null;
        }
        else
        {
            value = theme.TryGetNamespaceValue(
                UtilityThemeNamespaceNames.LetterSpacing,
                candidate.Value.Text,
                out var themeValue)
                    ? themeValue
                    : candidate.Value.Text switch
                    {
                        "tighter" => "-0.05em",
                        "tight" => "-0.025em",
                        "normal" => "0em",
                        "wide" => "0.025em",
                        "wider" => "0.05em",
                        "widest" => "0.1em",
                        _ => null,
                    };
        }

        if (value is null)
        {
            diagnostics.Add(UnsupportedValue(candidateText, candidate));
            return false;
        }

        if (candidate.IsNegative)
        {
            value = $"calc({value} * -1)";
        }

        declarations.Add(new UtilityCssDeclaration("letter-spacing", value));
        return true;
    }

    private static bool TryResolveLengthProperty(
        UtilityCandidate candidate,
        UtilityTheme theme,
        string property,
        bool supportsNegative,
        ICollection<UtilityCssDeclaration> declarations,
        string candidateText,
        ICollection<UtilityCssDiagnostic> diagnostics)
    {
        if (candidate.Modifier is not null ||
            candidate.Value is null ||
            !TryResolveSpacingValue(
                candidate,
                theme,
                false,
                out var value))
        {
            diagnostics.Add(UnsupportedValue(candidateText, candidate));
            return false;
        }

        if (candidate.IsNegative && !supportsNegative)
        {
            diagnostics.Add(UnsupportedValue(candidateText, candidate));
            return false;
        }

        declarations.Add(new UtilityCssDeclaration(property, value!));
        return true;
    }

    private static bool TryResolveLineClamp(
        UtilityCandidate candidate,
        ICollection<UtilityCssDeclaration> declarations,
        string candidateText,
        ICollection<UtilityCssDiagnostic> diagnostics)
    {
        if (candidate.Modifier is not null ||
            candidate.Value is null)
        {
            diagnostics.Add(UnsupportedValue(candidateText, candidate));
            return false;
        }

        if (candidate.Value.Text == "none")
        {
            declarations.Add(new UtilityCssDeclaration("overflow", "visible"));
            declarations.Add(new UtilityCssDeclaration("display", "block"));
            declarations.Add(new UtilityCssDeclaration("-webkit-box-orient", "unset"));
            declarations.Add(new UtilityCssDeclaration("-webkit-line-clamp", "unset"));
            return true;
        }

        string? value;
        if (candidate.Value.Kind is UtilityValueKind.Arbitrary or UtilityValueKind.CssVariable)
        {
            value = IsSafeCandidateValue(candidate.Value)
                ? candidate.Value.Text
                : null;
        }
        else
        {
            value = IsPositiveInteger(candidate.Value.Text)
                ? candidate.Value.Text
                : null;
        }

        if (value is null)
        {
            diagnostics.Add(UnsupportedValue(candidateText, candidate));
            return false;
        }

        declarations.Add(new UtilityCssDeclaration("overflow", "hidden"));
        declarations.Add(new UtilityCssDeclaration("display", "-webkit-box"));
        declarations.Add(new UtilityCssDeclaration("-webkit-box-orient", "vertical"));
        declarations.Add(new UtilityCssDeclaration("-webkit-line-clamp", value));
        return true;
    }

    private static bool TryResolveNamedOrArbitraryProperty(
        UtilityCandidate candidate,
        string property,
        IEnumerable<string> namedValues,
        ICollection<UtilityCssDeclaration> declarations,
        string candidateText,
        ICollection<UtilityCssDiagnostic> diagnostics)
    {
        if (candidate.Modifier is not null ||
            candidate.Value is null)
        {
            diagnostics.Add(UnsupportedValue(candidateText, candidate));
            return false;
        }

        string? value = null;
        if (candidate.Value.Kind is UtilityValueKind.Arbitrary or UtilityValueKind.CssVariable)
        {
            if (IsSafeCandidateValue(candidate.Value))
            {
                value = candidate.Value.Text;
            }
        }
        else if (namedValues.Contains(
                     candidate.Value.Text,
                     StringComparer.Ordinal))
        {
            value = candidate.Value.Text;
        }

        if (value is null)
        {
            diagnostics.Add(UnsupportedValue(candidateText, candidate));
            return false;
        }

        declarations.Add(new UtilityCssDeclaration(property, value));
        return true;
    }

    private static bool TryResolveList(
        UtilityCandidate candidate,
        ICollection<UtilityCssDeclaration> declarations,
        string candidateText,
        ICollection<UtilityCssDiagnostic> diagnostics)
    {
        if (candidate.Modifier is not null ||
            candidate.Value is null)
        {
            diagnostics.Add(UnsupportedValue(candidateText, candidate));
            return false;
        }

        if (candidate.Value.Kind is UtilityValueKind.Arbitrary or UtilityValueKind.CssVariable)
        {
            if (!IsSafeCandidateValue(candidate.Value))
            {
                diagnostics.Add(UnsupportedValue(candidateText, candidate));
                return false;
            }

            declarations.Add(
                new UtilityCssDeclaration(
                    "list-style-type",
                    candidate.Value.Text));
            return true;
        }

        var property = candidate.Value.Text is "inside" or "outside"
            ? "list-style-position"
            : candidate.Value.Text is "none" or "disc" or "decimal"
                ? "list-style-type"
                : null;
        if (property is null)
        {
            diagnostics.Add(UnsupportedValue(candidateText, candidate));
            return false;
        }

        declarations.Add(
            new UtilityCssDeclaration(
                property,
                candidate.Value.Text));
        return true;
    }

    private static bool TryResolveDecoration(
        UtilityCandidate candidate,
        UtilityTheme theme,
        ICollection<UtilityCssDeclaration> declarations,
        string candidateText,
        ICollection<UtilityCssDiagnostic> diagnostics)
    {
        if (candidate.Value is null)
        {
            diagnostics.Add(UnsupportedValue(candidateText, candidate));
            return false;
        }

        if (candidate.Value.Kind == UtilityValueKind.Named &&
            candidate.Modifier is null)
        {
            if (candidate.Value.Text is "solid" or "double" or "dotted" or "dashed" or "wavy")
            {
                declarations.Add(
                    new UtilityCssDeclaration(
                        "text-decoration-style",
                        candidate.Value.Text));
                return true;
            }

            if (candidate.Value.Text is "auto" or "from-font")
            {
                declarations.Add(
                    new UtilityCssDeclaration(
                        "text-decoration-thickness",
                        candidate.Value.Text));
                return true;
            }

            if (candidate.Value.Text is "0" or "1" or "2" or "4" or "8")
            {
                declarations.Add(
                    new UtilityCssDeclaration(
                        "text-decoration-thickness",
                        candidate.Value.Text + "px"));
                return true;
            }
        }

        if (candidate.Value.Kind == UtilityValueKind.Arbitrary &&
            candidate.Value.DataType != "color" &&
            candidate.Modifier is null &&
            IsSafeCandidateValue(candidate.Value))
        {
            declarations.Add(
                new UtilityCssDeclaration(
                    "text-decoration-thickness",
                    candidate.Value.Text));
            return true;
        }

        return TryResolveColor(
            candidate,
            theme,
            "text-decoration-color",
            declarations,
            candidateText,
            diagnostics);
    }

    private static bool TryResolveUnderlineOffset(
        UtilityCandidate candidate,
        ICollection<UtilityCssDeclaration> declarations,
        string candidateText,
        ICollection<UtilityCssDiagnostic> diagnostics)
    {
        if (candidate.Modifier is not null ||
            candidate.Value is null)
        {
            diagnostics.Add(UnsupportedValue(candidateText, candidate));
            return false;
        }

        string? value;
        if (candidate.Value.Text == "auto" &&
            candidate.Value.Kind == UtilityValueKind.Named)
        {
            if (candidate.IsNegative)
            {
                diagnostics.Add(UnsupportedValue(candidateText, candidate));
                return false;
            }

            value = "auto";
        }
        else if (!TryResolvePixelOrArbitrary(
                     candidate.Value,
                     out value))
        {
            diagnostics.Add(UnsupportedValue(candidateText, candidate));
            return false;
        }

        if (candidate.IsNegative)
        {
            value = $"calc({value} * -1)";
        }

        declarations.Add(
            new UtilityCssDeclaration(
                "text-underline-offset",
                value!));
        return true;
    }

    private static bool TryResolveFontStretch(
        UtilityCandidate candidate,
        ICollection<UtilityCssDeclaration> declarations,
        string candidateText,
        ICollection<UtilityCssDiagnostic> diagnostics)
    {
        if (candidate.Modifier is not null ||
            candidate.Value is null)
        {
            diagnostics.Add(UnsupportedValue(candidateText, candidate));
            return false;
        }

        string? value;
        if (candidate.Value.Kind is UtilityValueKind.Arbitrary or UtilityValueKind.CssVariable)
        {
            value = IsSafeCandidateValue(candidate.Value)
                ? candidate.Value.Text
                : null;
        }
        else
        {
            value = candidate.Value.Text switch
            {
                "ultra-condensed" => "50%",
                "extra-condensed" => "62.5%",
                "condensed" => "75%",
                "semi-condensed" => "87.5%",
                "normal" => "100%",
                "semi-expanded" => "112.5%",
                "expanded" => "125%",
                "extra-expanded" => "150%",
                "ultra-expanded" => "200%",
                _ => null,
            };
        }

        if (value is null)
        {
            diagnostics.Add(UnsupportedValue(candidateText, candidate));
            return false;
        }

        declarations.Add(new UtilityCssDeclaration("font-stretch", value));
        return true;
    }

    private static bool TryResolveArbitraryPropertyValue(
        UtilityCandidate candidate,
        string property,
        ICollection<UtilityCssDeclaration> declarations,
        string candidateText,
        ICollection<UtilityCssDiagnostic> diagnostics)
    {
        if (candidate.Modifier is not null ||
            candidate.Value is null ||
            candidate.Value.Kind is not UtilityValueKind.Arbitrary and not UtilityValueKind.CssVariable ||
            !IsSafeCandidateValue(candidate.Value))
        {
            diagnostics.Add(UnsupportedValue(candidateText, candidate));
            return false;
        }

        declarations.Add(
            new UtilityCssDeclaration(
                property,
                candidate.Value.Text));
        return true;
    }

    private static bool TryResolveLineHeightModifier(
        UtilityModifier modifier,
        UtilityTheme theme,
        out string? value)
    {
        if (modifier.Kind == UtilityModifierKind.Named)
        {
            if (theme.TryGetNamespaceValue(
                    UtilityThemeNamespaceNames.LineHeight,
                    modifier.Text,
                    out value))
            {
                return true;
            }

            if (theme.TryGetSpacing(modifier.Text, out value))
            {
                return true;
            }

            value = modifier.Text switch
            {
                "none" => "1",
                "tight" => "1.25",
                "snug" => "1.375",
                "normal" => "1.5",
                "relaxed" => "1.625",
                "loose" => "2",
                _ => null,
            };
            return value is not null;
        }

        value = UtilityCssText.IsSafeValue(modifier.Text)
            ? modifier.Text
            : null;
        return value is not null;
    }

    private static bool TryResolveThemeProperty(
        UtilityTheme theme,
        string propertyName,
        out string? value)
    {
        value = null;
        if (!theme.TryGetProperty(
                propertyName,
                out var property) ||
            property is null)
        {
            return false;
        }

        if ((property.Options & UtilityThemeOptions.Inline) != 0)
        {
            value = property.Value;
            return true;
        }

        var formattedName =
            theme.FormatCustomPropertyName(property.Name);
        value = (property.Options & UtilityThemeOptions.Reference) != 0
            ? "var(" + formattedName + ", " + property.Value + ")"
            : "var(" + formattedName + ")";
        return true;
    }

    private static bool TryResolvePixelOrArbitrary(
        UtilityValue value,
        out string? resolved)
    {
        if (value.Kind is UtilityValueKind.Arbitrary or UtilityValueKind.CssVariable)
        {
            resolved = IsSafeCandidateValue(value)
                ? value.Text
                : null;
            return resolved is not null;
        }

        if (decimal.TryParse(
                value.Text,
                NumberStyles.Number,
                CultureInfo.InvariantCulture,
                out var number) &&
            number >= decimal.Zero)
        {
            resolved =
                number.ToString(
                    "0.###",
                    CultureInfo.InvariantCulture) + "px";
            return true;
        }

        resolved = null;
        return false;
    }

    private static bool TryResolveBareInteger(
        string text,
        bool mustBePositive,
        out string? value)
    {
        if (int.TryParse(
                text,
                NumberStyles.None,
                CultureInfo.InvariantCulture,
                out var number) &&
            number >= (mustBePositive ? 1 : 0))
        {
            value = number.ToString(CultureInfo.InvariantCulture);
            return true;
        }

        value = null;
        return false;
    }

    private static bool TryResolveFilterFunction(
        UtilityCandidate candidate,
        UtilityTheme theme,
        string root,
        out string? function)
    {
        function = null;
        if (candidate.Modifier is not null &&
            root != "drop-shadow")
        {
            return false;
        }

        if (root == "drop-shadow")
        {
            string? alpha = null;
            if (candidate.Modifier is not null &&
                !TryResolveOpacityModifier(
                    candidate.Modifier,
                    out alpha))
            {
                return false;
            }

            string? shadow;
            if (candidate.Value?.Kind is UtilityValueKind.Arbitrary or UtilityValueKind.CssVariable &&
                IsSafeCandidateValue(candidate.Value))
            {
                shadow = candidate.Value.Text;
            }
            else
            {
                var tokenName = candidate.Value?.Text ?? "DEFAULT";
                shadow = alpha is not null &&
                    theme.TryGetNamespaceRawValue(
                        UtilityThemeNamespaceNames.DropShadow,
                        tokenName,
                        out var rawThemeShadow)
                        ? rawThemeShadow
                        : theme.TryGetNamespaceValue(
                            UtilityThemeNamespaceNames.DropShadow,
                            tokenName,
                            out var themeShadow)
                            ? themeShadow
                        : candidate.Value?.Text switch
                        {
                            null => "0 1px 2px rgb(0 0 0 / 0.1)",
                            "xs" => "0 1px 1px rgb(0 0 0 / 0.05)",
                            "sm" => "0 1px 2px rgb(0 0 0 / 0.15)",
                            "md" => "0 3px 3px rgb(0 0 0 / 0.12)",
                            "lg" => "0 4px 4px rgb(0 0 0 / 0.15)",
                            "xl" => "0 9px 7px rgb(0 0 0 / 0.1)",
                            "2xl" => "0 25px 25px rgb(0 0 0 / 0.15)",
                            "none" => "0 0 #0000",
                            _ => null,
                        };
            }

            if (shadow is not null &&
                alpha is not null)
            {
                shadow = ReplaceShadowColorAlpha(
                    shadow,
                    alpha);
            }

            function = shadow is null
                ? null
                : "drop-shadow(" + shadow + ")";
            return function is not null;
        }

        string? value;
        if (candidate.Value?.Kind is UtilityValueKind.Arbitrary or UtilityValueKind.CssVariable &&
            IsSafeCandidateValue(candidate.Value))
        {
            value = candidate.Value.Text;
        }
        else
        {
            value = root switch
            {
                "blur" when theme.TryGetNamespaceValue(
                    UtilityThemeNamespaceNames.Blur,
                    candidate.Value?.Text ?? "DEFAULT",
                    out var blur) => blur,
                "blur" => ResolveBlur(candidate.Value?.Text),
                "brightness" or "contrast" or "saturate" =>
                    ResolvePercentage(candidate.Value?.Text, "100"),
                "grayscale" or "invert" or "sepia" =>
                    ResolvePercentage(candidate.Value?.Text, "100"),
                "opacity" => ResolvePercentage(candidate.Value?.Text, null),
                "hue-rotate" => ResolveAngleValue(candidate.Value?.Text),
                _ => null,
            };
        }

        if (value is null)
        {
            return false;
        }

        if (candidate.IsNegative)
        {
            value = $"calc({value} * -1)";
        }

        function = root + "(" + value + ")";
        return true;
    }

    private static string GetFilterComposition(string variablePrefix)
    {
        var roots = variablePrefix == "--tw-backdrop-"
            ? new[]
            {
                "blur",
                "brightness",
                "contrast",
                "grayscale",
                "hue-rotate",
                "invert",
                "opacity",
                "saturate",
                "sepia",
            }
            : new[]
        {
            "blur",
            "brightness",
            "contrast",
            "grayscale",
            "hue-rotate",
            "invert",
            "saturate",
            "sepia",
            "drop-shadow",
        };
        return string.Join(
            " ",
            roots.Select(root => "var(" + variablePrefix + root + ",)"));
    }

    private static bool TryResolveTime(
        UtilityValue? value,
        out string? result)
    {
        if (value is null)
        {
            result = null;
            return false;
        }

        if (value.Kind is UtilityValueKind.Arbitrary or UtilityValueKind.CssVariable)
        {
            result = IsSafeCandidateValue(value)
                ? value.Text
                : null;
            return result is not null;
        }

        if (decimal.TryParse(
                value.Text,
                NumberStyles.Number,
                CultureInfo.InvariantCulture,
                out var milliseconds) &&
            milliseconds >= decimal.Zero)
        {
            result =
                milliseconds.ToString(
                    "0.###",
                    CultureInfo.InvariantCulture) + "ms";
            return true;
        }

        result = null;
        return false;
    }

    private static bool TryResolveTimingFunction(
        UtilityValue? value,
        out string? result)
    {
        if (value?.Kind is UtilityValueKind.Arbitrary or UtilityValueKind.CssVariable &&
            IsSafeCandidateValue(value))
        {
            result = value.Text;
            return true;
        }

        result = value?.Text switch
        {
            "linear" => "linear",
            "in" => "cubic-bezier(0.4, 0, 1, 1)",
            "out" => "cubic-bezier(0, 0, 0.2, 1)",
            "in-out" => "cubic-bezier(0.4, 0, 0.2, 1)",
            _ => null,
        };
        return result is not null;
    }

    private static bool TryResolveAngle(
        UtilityCandidate candidate,
        out string? value)
    {
        value = null;
        if (candidate.Modifier is not null ||
            candidate.Value is null)
        {
            return false;
        }

        if (candidate.Value.Kind is UtilityValueKind.Arbitrary or UtilityValueKind.CssVariable)
        {
            value = IsSafeCandidateValue(candidate.Value)
                ? candidate.Value.Text
                : null;
        }
        else
        {
            value = ResolveAngleValue(candidate.Value.Text);
        }

        if (value is not null &&
            candidate.IsNegative)
        {
            value = $"calc({value} * -1)";
        }

        return value is not null;
    }

    private static bool TryResolveScale(
        UtilityCandidate candidate,
        out string? value)
    {
        value = null;
        if (candidate.Modifier is not null ||
            candidate.Value is null)
        {
            return false;
        }

        if (candidate.Value.Kind is UtilityValueKind.Arbitrary or UtilityValueKind.CssVariable)
        {
            value = IsSafeCandidateValue(candidate.Value)
                ? candidate.Value.Text
                : null;
        }
        else if (decimal.TryParse(
                     candidate.Value.Text,
                     NumberStyles.None,
                     CultureInfo.InvariantCulture,
                     out var percentage))
        {
            value =
                (percentage / 100m).ToString(
                    "0.###",
                    CultureInfo.InvariantCulture);
        }

        if (value is not null &&
            candidate.IsNegative)
        {
            value = $"calc({value} * -1)";
        }

        return value is not null;
    }

    private static bool TryResolvePosition(
        UtilityValue? value,
        out string? result)
    {
        if (value?.Kind is UtilityValueKind.Arbitrary or UtilityValueKind.CssVariable &&
            IsSafeCandidateValue(value))
        {
            result = value.Text;
            return true;
        }

        result = value?.Text switch
        {
            "center" => "center",
            "top" => "top",
            "top-right" => "top right",
            "right" => "right",
            "bottom-right" => "bottom right",
            "bottom" => "bottom",
            "bottom-left" => "bottom left",
            "left" => "left",
            "top-left" => "top left",
            _ => null,
        };
        return result is not null;
    }

    private static bool TryResolvePerspective(
        UtilityValue? value,
        UtilityTheme theme,
        out string? result)
    {
        if (value?.Kind is UtilityValueKind.Arbitrary or UtilityValueKind.CssVariable &&
            IsSafeCandidateValue(value))
        {
            result = value.Text;
            return true;
        }

        result =
            value?.Kind == UtilityValueKind.Named &&
            theme.TryGetNamespaceValue(
                UtilityThemeNamespaceNames.Perspective,
                value.Text,
                out var perspective)
                ? perspective
                : value?.Text switch
                {
                    "none" => "none",
                    "dramatic" => "100px",
                    "near" => "300px",
                    "normal" => "500px",
                    "midrange" => "800px",
                    "distant" => "1200px",
                    _ => null,
                };
        return result is not null;
    }

    private static string? ResolveNamedShadow(
        string root,
        string? value)
    {
        var colorVariable = root switch
        {
            "shadow" => "--tw-shadow-color",
            "inset-shadow" => "--tw-inset-shadow-color",
            "text-shadow" => "--tw-text-shadow-color",
            _ => "--tw-shadow-color",
        };
        var prefix = root == "inset-shadow"
            ? "inset "
            : string.Empty;
        var color = "var(" + colorVariable + ", rgb(0 0 0 / 0.15))";
        if (value == "none")
        {
            return root == "text-shadow"
                ? "none"
                : "0 0 #0000";
        }

        if (root == "text-shadow")
        {
            return value switch
            {
                null or "sm" => "0 1px 2px " + color,
                "md" => "0 2px 4px " + color,
                "lg" => "0 4px 8px " + color,
                _ => null,
            };
        }

        return value switch
        {
            "2xs" => prefix + "0 1px " + color,
            "xs" => prefix + "0 1px 2px 0 " + color,
            null or "sm" => prefix + "0 1px 3px 0 " + color + ", " + prefix + "0 1px 2px -1px " + color,
            "md" => prefix + "0 4px 6px -1px " + color + ", " + prefix + "0 2px 4px -2px " + color,
            "lg" => prefix + "0 10px 15px -3px " + color + ", " + prefix + "0 4px 6px -4px " + color,
            "xl" => prefix + "0 20px 25px -5px " + color + ", " + prefix + "0 8px 10px -6px " + color,
            "2xl" => prefix + "0 25px 50px -12px " + color,
            _ => null,
        };
    }

    private static string? ResolveBlur(string? value) =>
        value switch
        {
            null or "sm" => "8px",
            "none" => "0px",
            "xs" => "4px",
            "md" => "12px",
            "lg" => "16px",
            "xl" => "24px",
            "2xl" => "40px",
            "3xl" => "64px",
            _ => null,
        };

    private static string? ResolvePercentage(
        string? value,
        string? defaultValue)
    {
        var text = value ?? defaultValue;
        if (text is null ||
            !decimal.TryParse(
                text,
                NumberStyles.Number,
                CultureInfo.InvariantCulture,
                out var percentage) ||
            percentage < decimal.Zero)
        {
            return null;
        }

        return percentage.ToString(
            "0.###",
            CultureInfo.InvariantCulture) + "%";
    }

    private static string? ResolveAngleValue(string? value)
    {
        if (value is null ||
            !decimal.TryParse(
                value,
                NumberStyles.Number,
                CultureInfo.InvariantCulture,
                out var degrees))
        {
            return null;
        }

        return degrees.ToString(
            "0.###",
            CultureInfo.InvariantCulture) + "deg";
    }

    private static string NormalizePosition(string value) =>
        value.Replace("-", " ");

    private static bool IsPositiveInteger(string value) =>
        int.TryParse(
            value,
            NumberStyles.None,
            CultureInfo.InvariantCulture,
            out var number) &&
        number > 0;

    private static bool IsBlockAxis(string root) =>
        root is "h" or "min-h" or "max-h" or "block" or "min-block" or "max-block";

    private static bool TryResolveSpacingValue(
        UtilityCandidate candidate,
        UtilityTheme theme,
        bool supportsAuto,
        out string? value)
    {
        value = null;
        if (candidate.Modifier is not null || candidate.Value is null)
        {
            return false;
        }

        if (supportsAuto &&
            candidate.Value.Kind == UtilityValueKind.Named &&
            candidate.Value.Text == "auto")
        {
            value = "auto";
        }
        else if (candidate.Value.Kind is UtilityValueKind.Arbitrary or UtilityValueKind.CssVariable)
        {
            if (!IsSafeCandidateValue(candidate.Value))
            {
                return false;
            }

            value = candidate.Value.Text;
        }
        else if (!theme.TryGetSpacing(candidate.Value.Text, out value))
        {
            return false;
        }

        if (candidate.IsNegative)
        {
            value = $"calc({value} * -1)";
        }

        return true;
    }

    private static bool TryResolveSizeValue(
        UtilityCandidate candidate,
        UtilityTheme theme,
        out string? value)
    {
        value = null;
        if (candidate.Value is null)
        {
            return false;
        }

        if (candidate.Value.Fraction is not null &&
            candidate.Modifier is not null &&
            int.TryParse(
                candidate.Value.Text,
                NumberStyles.None,
                CultureInfo.InvariantCulture,
                out var numerator) &&
            int.TryParse(
                candidate.Modifier.Text,
                NumberStyles.None,
                CultureInfo.InvariantCulture,
                out var denominator) &&
            numerator >= 0 &&
            denominator > 0)
        {
            value =
                $"calc({numerator.ToString(CultureInfo.InvariantCulture)} / {denominator.ToString(CultureInfo.InvariantCulture)} * 100%)";
            return true;
        }

        if (candidate.Modifier is not null)
        {
            return false;
        }

        if (candidate.Value.Kind is UtilityValueKind.Arbitrary or UtilityValueKind.CssVariable)
        {
            if (!IsSafeCandidateValue(candidate.Value))
            {
                return false;
            }

            value = candidate.Value.Text;
            return true;
        }

        switch (candidate.Value.Text)
        {
            case "auto":
                value = "auto";
                return true;
            case "full":
                value = "100%";
                return true;
            case "screen":
                value = IsBlockAxis(candidate.Root)
                    ? "100vh"
                    : "100vw";
                return true;
            case "svw":
                value = "100svw";
                return true;
            case "lvw":
                value = "100lvw";
                return true;
            case "dvh":
                value = "100dvh";
                return true;
            case "dvw":
                value = "100dvw";
                return true;
            case "svh":
                value = "100svh";
                return true;
            case "lvh":
                value = "100lvh";
                return true;
            case "lh":
                value = "1lh";
                return true;
            case "min":
                value = "min-content";
                return true;
            case "max":
                value = "max-content";
                return true;
            case "fit":
                value = "fit-content";
                return true;
            case "none":
                value = "none";
                return true;
            default:
                return theme.TryGetNamespaceValue(
                           UtilityThemeNamespaceNames.Container,
                           candidate.Value.Text,
                           out value) ||
                       theme.TryGetSpacing(candidate.Value.Text, out value);
        }
    }

    private static bool IsShadowColorCandidate(
        UtilityCandidate candidate)
    {
        if (candidate.Value is null)
        {
            return false;
        }

        if (candidate.Value.Kind == UtilityValueKind.Named)
        {
            return true;
        }

        if (candidate.Value.DataType is not null)
        {
            return candidate.Value.DataType == "color";
        }

        if (candidate.Value.Kind == UtilityValueKind.CssVariable)
        {
            return false;
        }

        var value = candidate.Value.Text;
        if (value.StartsWith("#", StringComparison.Ordinal))
        {
            return true;
        }

        foreach (var functionName in new[]
                 {
                     "color",
                     "color-mix",
                     "hsl",
                     "hsla",
                     "hwb",
                     "lab",
                     "lch",
                     "oklab",
                     "oklch",
                     "rgb",
                     "rgba",
                 })
        {
            if (value.StartsWith(
                    functionName + "(",
                    StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return value.IndexOf(' ') < 0 &&
            value.IndexOf('(') < 0 &&
            value.IndexOf(',') < 0;
    }

    private static bool TryGetColor(
        UtilityCandidate candidate,
        UtilityTheme theme,
        out string? value)
    {
        value = null;
        if (candidate.Value is null)
        {
            return false;
        }

        if (candidate.Value.Kind is UtilityValueKind.Arbitrary or UtilityValueKind.CssVariable)
        {
            if (candidate.Value.DataType is not null &&
                candidate.Value.DataType != "color")
            {
                return false;
            }

            if (!IsSafeCandidateValue(candidate.Value))
            {
                return false;
            }

            value = candidate.Value.Text;
            return true;
        }

        value = candidate.Value.Text switch
        {
            "current" => "currentcolor",
            "inherit" => "inherit",
            "transparent" => "transparent",
            _ => null,
        };
        if (value is not null)
        {
            return true;
        }

        return theme.TryGetColor(candidate.Value.Text, out value);
    }

    internal static bool TryApplyColorModifier(
        string color,
        UtilityModifier? modifier,
        out string? value)
    {
        if (modifier is null)
        {
            value = color;
            return true;
        }

        if (!TryResolveOpacityModifier(
                modifier,
                out var percentage))
        {
            value = null;
            return false;
        }

        value =
            $"color-mix(in oklab, {color} {percentage}, transparent)";
        return true;
    }

    private static bool TryResolveOpacityModifier(
        UtilityModifier modifier,
        out string? percentage)
    {
        if (modifier.Kind == UtilityModifierKind.Named)
        {
            if (!decimal.TryParse(
                    modifier.Text,
                    NumberStyles.AllowDecimalPoint,
                    CultureInfo.InvariantCulture,
                    out var number) ||
                number < decimal.Zero ||
                number > 100m ||
                !IsValidOpacityValue(
                    modifier.Text,
                    number))
            {
                percentage = null;
                return false;
            }

            percentage = number.ToString(
                "0.###",
                CultureInfo.InvariantCulture) + "%";
            return true;
        }

        if (!UtilityCssText.IsSafeValue(modifier.Text))
        {
            percentage = null;
            return false;
        }

        if (modifier.Text.EndsWith("%", StringComparison.Ordinal) ||
            modifier.Kind == UtilityModifierKind.CssVariable)
        {
            percentage = modifier.Text;
            return true;
        }

        percentage = $"calc({modifier.Text} * 100%)";
        return true;
    }

    private static bool IsValidOpacityValue(
        string text,
        decimal number) =>
        number * 4m == decimal.Truncate(number * 4m) &&
        text == number.ToString(
            "0.############################",
            CultureInfo.InvariantCulture);

    private static string ReplaceShadowColorAlpha(
        string shadow,
        string alpha)
    {
        var result = shadow;
        foreach (var colorFunctionName in new[]
                 {
                     "color",
                     "hsl",
                     "hsla",
                     "lab",
                     "lch",
                     "oklab",
                     "oklch",
                     "rgb",
                     "rgba",
                 })
        {
            var searchStart = 0;
            var functionPrefix = colorFunctionName + "(";
            while (searchStart < result.Length)
            {
                var functionStart = result.IndexOf(
                    functionPrefix,
                    searchStart,
                    StringComparison.OrdinalIgnoreCase);
                if (functionStart < 0)
                {
                    break;
                }

                var depth = 1;
                var functionEnd = functionStart + functionPrefix.Length;
                while (functionEnd < result.Length &&
                       depth > 0)
                {
                    if (result[functionEnd] == '(')
                    {
                        depth++;
                    }
                    else if (result[functionEnd] == ')')
                    {
                        depth--;
                    }

                    functionEnd++;
                }

                if (depth != 0)
                {
                    break;
                }

                var color = result.Substring(
                    functionStart,
                    functionEnd - functionStart);
                var replacement =
                    $"oklab(from {color} l a b / {alpha})";
                result = result.Substring(0, functionStart) +
                    replacement +
                    result.Substring(functionEnd);
                searchStart = functionStart + replacement.Length;
            }
        }

        return result;
    }

    private static bool TryApplyVariants(
        UtilityCandidate candidate,
        UtilityTheme theme,
        string candidateText,
        ref string selector,
        ICollection<UtilityCssDeclaration> declarations,
        ICollection<string> wrappers,
        ref int variantOrder,
        ICollection<UtilityCssDiagnostic> diagnostics)
    {
        foreach (var variant in candidate.Variants)
        {
            if (variant.Category == UtilityVariantCategory.Prefix)
            {
                continue;
            }

            if (TryApplyVariant(
                    variant,
                    theme,
                    ref selector,
                    declarations,
                    wrappers,
                    ref variantOrder))
            {
                continue;
            }

            diagnostics.Add(
                Error(
                    candidateText,
                    UtilityCssDiagnosticCode.UnsupportedVariant,
                    $"The '{variant.RawText}' variant cannot be applied to this candidate."));
            return false;
        }

        return true;
    }

    private static bool TryApplyVariant(
        UtilityVariant variant,
        UtilityTheme theme,
        ref string selector,
        ICollection<UtilityCssDeclaration> declarations,
        ICollection<string> wrappers,
        ref int variantOrder)
    {
        if (variant.Kind == UtilityVariantKind.Static)
        {
            switch (variant.Category)
            {
                case UtilityVariantCategory.Child:
                    selector = ":is(" + selector + " > *)";
                    CombineVariantOrder(ref variantOrder, 10);
                    return true;
                case UtilityVariantCategory.Descendant:
                    selector = ":is(" + selector + " *)";
                    CombineVariantOrder(ref variantOrder, 11);
                    return true;
                case UtilityVariantCategory.PseudoElement:
                    return TryApplyPseudoElementVariant(
                        variant,
                        ref selector,
                        declarations,
                        ref variantOrder);
                case UtilityVariantCategory.Structural:
                    return TryApplyStaticStructuralVariant(
                        variant,
                        ref selector,
                        ref variantOrder);
                case UtilityVariantCategory.State:
                    return TryApplyStateVariant(
                        variant,
                        ref selector,
                        wrappers,
                        ref variantOrder);
                case UtilityVariantCategory.Responsive:
                    if (theme.TryGetBreakpoint(
                            variant.Root,
                            out var breakpoint))
                    {
                        wrappers.Add(
                            "@media (width >= " + breakpoint + ")");
                        CombineVariantOrder(
                            ref variantOrder,
                            200 + GetBreakpointOrder(variant.Root));
                        return true;
                    }

                    return false;
                case UtilityVariantCategory.Environment:
                    return TryApplyEnvironmentVariant(
                        variant,
                        wrappers,
                        ref variantOrder);
                case UtilityVariantCategory.Direction:
                    selector +=
                        ":where(:dir(" + variant.Root +
                        "), [dir=\"" + variant.Root +
                        "\"], [dir=\"" + variant.Root + "\"] *)";
                    CombineVariantOrder(ref variantOrder, 700);
                    return true;
                case UtilityVariantCategory.Print:
                    wrappers.Add("@media print");
                    CombineVariantOrder(ref variantOrder, 710);
                    return true;
            }
        }

        if (variant.Kind == UtilityVariantKind.Functional)
        {
            switch (variant.Category)
            {
                case UtilityVariantCategory.Structural:
                    return TryApplyFunctionalStructuralVariant(
                        variant,
                        ref selector,
                        ref variantOrder);
                case UtilityVariantCategory.Attribute:
                    return TryApplyAttributeVariant(
                        variant,
                        ref selector,
                        ref variantOrder);
                case UtilityVariantCategory.Supports:
                    return TryApplySupportsVariant(
                        variant,
                        wrappers,
                        ref variantOrder);
                case UtilityVariantCategory.Responsive:
                    return TryApplyResponsiveVariant(
                        variant,
                        theme,
                        wrappers,
                        ref variantOrder);
                case UtilityVariantCategory.ContainerQuery:
                    return TryApplyContainerVariant(
                        variant,
                        theme,
                        wrappers,
                        ref variantOrder);
            }
        }

        if (variant.Kind == UtilityVariantKind.Compound &&
            variant.Category == UtilityVariantCategory.Compound)
        {
            return TryApplyCompoundVariant(
                variant,
                theme,
                ref selector,
                declarations,
                wrappers,
                ref variantOrder);
        }

        if (variant.Kind == UtilityVariantKind.Arbitrary)
        {
            return TryApplyArbitraryVariant(
                variant,
                ref selector,
                wrappers,
                ref variantOrder);
        }

        return false;
    }

    private static bool TryApplyPseudoElementVariant(
        UtilityVariant variant,
        ref string selector,
        ICollection<UtilityCssDeclaration> declarations,
        ref int variantOrder)
    {
        var pseudoElement = variant.Root switch
        {
            "after" => "::after",
            "backdrop" => "::backdrop",
            "before" => "::before",
            "details-content" => "::details-content",
            "file" => "::file-selector-button",
            "first-letter" => "::first-letter",
            "first-line" => "::first-line",
            "placeholder" => "::placeholder",
            _ => null,
        };
        if (variant.Root is "marker" or "selection")
        {
            selector =
                ":is(" + selector + ", " + selector + " *)::" +
                variant.Root;
        }
        else if (pseudoElement is not null)
        {
            selector += pseudoElement;
        }
        else
        {
            return false;
        }

        if (variant.Root is "before" or "after" &&
            !declarations.Any(
                declaration =>
                    declaration.Property == "content"))
        {
            declarations.Add(
                new UtilityCssDeclaration(
                    "content",
                    "var(--tw-content)"));
        }

        CombineVariantOrder(ref variantOrder, 20);
        return true;
    }

    private static bool TryApplyStaticStructuralVariant(
        UtilityVariant variant,
        ref string selector,
        ref int variantOrder)
    {
        var pseudoClass = variant.Root switch
        {
            "even" => ":nth-child(even)",
            "first" => ":first-child",
            "first-of-type" => ":first-of-type",
            "last" => ":last-child",
            "last-of-type" => ":last-of-type",
            "odd" => ":nth-child(odd)",
            "only" => ":only-child",
            "only-of-type" => ":only-of-type",
            _ => null,
        };
        if (pseudoClass is null)
        {
            return false;
        }

        selector += pseudoClass;
        CombineVariantOrder(ref variantOrder, 30);
        return true;
    }

    private static bool TryApplyFunctionalStructuralVariant(
        UtilityVariant variant,
        ref string selector,
        ref int variantOrder)
    {
        if (variant.Value is null ||
            variant.Modifier is not null ||
            !UtilityCssText.IsSafeValue(variant.Value.Text) ||
            variant.Value.Kind == UtilityValueKind.Named &&
            !IsPositiveInteger(variant.Value.Text))
        {
            return false;
        }

        var functionName = variant.Root switch
        {
            "nth" => "nth-child",
            "nth-last" => "nth-last-child",
            "nth-of-type" => "nth-of-type",
            "nth-last-of-type" => "nth-last-of-type",
            _ => null,
        };
        if (functionName is null)
        {
            return false;
        }

        selector +=
            ":" + functionName + "(" + variant.Value.Text + ")";
        CombineVariantOrder(ref variantOrder, 40);
        return true;
    }

    private static bool TryApplyStateVariant(
        UtilityVariant variant,
        ref string selector,
        ICollection<string> wrappers,
        ref int variantOrder)
    {
        if (variant.Root == "open")
        {
            selector += ":is([open], :popover-open, :open)";
        }
        else if (variant.Root == "inert")
        {
            selector += ":is([inert], [inert] *)";
        }
        else
        {
            selector += ":" + variant.Root;
        }

        CombineVariantOrder(
            ref variantOrder,
            50 + GetStateOrder(variant.Root));
        if (variant.Root == "hover")
        {
            wrappers.Add("@media (hover: hover)");
        }

        return true;
    }

    private static bool TryApplyAttributeVariant(
        UtilityVariant variant,
        ref string selector,
        ref int variantOrder)
    {
        if (variant.Value is null ||
            variant.Modifier is not null ||
            !UtilityCssText.IsSafeValue(variant.Value.Text))
        {
            return false;
        }

        string attribute;
        if (variant.Root == "aria")
        {
            attribute = variant.Value.Kind == UtilityValueKind.Named
                ? "aria-" + variant.Value.Text + "=\"true\""
                : "aria-" + QuoteAttributeValue(variant.Value.Text);
        }
        else if (variant.Root == "data")
        {
            attribute = "data-" + QuoteAttributeValue(variant.Value.Text);
        }
        else
        {
            return false;
        }

        selector += "[" + attribute + "]";
        CombineVariantOrder(ref variantOrder, 90);
        return true;
    }

    private static bool TryApplySupportsVariant(
        UtilityVariant variant,
        ICollection<string> wrappers,
        ref int variantOrder)
    {
        if (variant.Value is null ||
            variant.Modifier is not null ||
            !UtilityCssText.IsSafeValue(variant.Value.Text))
        {
            return false;
        }

        var query = variant.Value.Text;
        var functionStart = query.IndexOf('(');
        if (functionStart <= 0)
        {
            if (query.IndexOf(':') < 0)
            {
                query += ": var(--tw)";
            }

            if (query[0] != '(' ||
                query[query.Length - 1] != ')')
            {
                query = "(" + query + ")";
            }
        }

        wrappers.Add("@supports " + query);
        CombineVariantOrder(ref variantOrder, 300);
        return true;
    }

    private static bool TryApplyResponsiveVariant(
        UtilityVariant variant,
        UtilityTheme theme,
        ICollection<string> wrappers,
        ref int variantOrder)
    {
        if (variant.Value is null ||
            variant.Modifier is not null ||
            !TryResolveVariantThreshold(
                variant.Value,
                theme,
                UtilityThemeNamespaceNames.Breakpoint,
                out var threshold))
        {
            return false;
        }

        var comparison = variant.Root == "max"
            ? "<"
            : variant.Root == "min"
                ? ">="
                : null;
        if (comparison is null)
        {
            return false;
        }

        wrappers.Add(
            "@media (width " + comparison + " " + threshold + ")");
        CombineVariantOrder(
            ref variantOrder,
            variant.Root == "max" ? 190 : 210);
        return true;
    }

    private static bool TryApplyContainerVariant(
        UtilityVariant variant,
        UtilityTheme theme,
        ICollection<string> wrappers,
        ref int variantOrder)
    {
        if (variant.Value is null ||
            !TryResolveVariantThreshold(
                variant.Value,
                theme,
                UtilityThemeNamespaceNames.Container,
                out var threshold))
        {
            return false;
        }

        var comparison = variant.Root == "@max"
            ? "<"
            : variant.Root is "@" or "@min"
                ? ">="
                : null;
        if (comparison is null)
        {
            return false;
        }

        var name = variant.Modifier is null
            ? string.Empty
            : variant.Modifier.Text + " ";
        if (variant.Modifier is not null &&
            !UtilityCssText.IsSafeValue(variant.Modifier.Text))
        {
            return false;
        }

        wrappers.Add(
            "@container " + name +
            "(width " + comparison + " " + threshold + ")");
        CombineVariantOrder(
            ref variantOrder,
            variant.Root == "@max" ? 220 : 230);
        return true;
    }

    private static bool TryApplyEnvironmentVariant(
        UtilityVariant variant,
        ICollection<string> wrappers,
        ref int variantOrder)
    {
        var wrapper = variant.Root switch
        {
            "any-pointer-coarse" => "@media (any-pointer: coarse)",
            "any-pointer-fine" => "@media (any-pointer: fine)",
            "any-pointer-none" => "@media (any-pointer: none)",
            "contrast-less" => "@media (prefers-contrast: less)",
            "contrast-more" => "@media (prefers-contrast: more)",
            "dark" => "@media (prefers-color-scheme: dark)",
            "forced-colors" => "@media (forced-colors: active)",
            "inverted-colors" => "@media (inverted-colors: inverted)",
            "landscape" => "@media (orientation: landscape)",
            "motion-reduce" => "@media (prefers-reduced-motion: reduce)",
            "motion-safe" => "@media (prefers-reduced-motion: no-preference)",
            "noscript" => "@media (scripting: none)",
            "pointer-coarse" => "@media (pointer: coarse)",
            "pointer-fine" => "@media (pointer: fine)",
            "pointer-none" => "@media (pointer: none)",
            "portrait" => "@media (orientation: portrait)",
            "starting" => "@starting-style",
            _ => null,
        };
        if (wrapper is null)
        {
            return false;
        }

        wrappers.Add(wrapper);
        CombineVariantOrder(ref variantOrder, 600);
        return true;
    }

    private static bool TryApplyCompoundVariant(
        UtilityVariant variant,
        UtilityTheme theme,
        ref string selector,
        ICollection<UtilityCssDeclaration> declarations,
        ICollection<string> wrappers,
        ref int variantOrder)
    {
        if (variant.NestedVariant is null)
        {
            return false;
        }

        if (variant.Root is "group" or "peer")
        {
            var marker = "." +
                (string.IsNullOrEmpty(theme.Prefix)
                    ? variant.Root
                    : theme.Prefix + "\\:" + variant.Root);
            if (variant.Modifier is not null)
            {
                marker += "\\/" +
                    UtilityCssText.EscapeClassName(
                        variant.Modifier.RawText);
            }

            var nestedSelector = ":where(" + marker + ")";
            var nestedWrappers = new List<string>();
            var nestedOrder = 0;
            if (!TryApplyVariant(
                    variant.NestedVariant,
                    theme,
                    ref nestedSelector,
                    declarations,
                    nestedWrappers,
                    ref nestedOrder) ||
                nestedSelector.IndexOf(
                    "::",
                    StringComparison.Ordinal) >= 0)
            {
                return false;
            }

            foreach (var wrapper in nestedWrappers)
            {
                wrappers.Add(wrapper);
            }

            selector = variant.Root == "group"
                ? nestedSelector + " " + selector
                : nestedSelector + " ~ " + selector;
            CombineVariantOrder(
                ref variantOrder,
                100 + nestedOrder);
            return true;
        }

        if (variant.Modifier is not null)
        {
            return false;
        }

        if (variant.NestedVariant.Kind == UtilityVariantKind.Arbitrary &&
            variant.NestedVariant.IsRelativeSelector)
        {
            if (variant.Root != "has" ||
                variant.NestedVariant.Selector is null ||
                !UtilityCssText.IsSafeValue(
                    variant.NestedVariant.Selector))
            {
                return false;
            }

            selector +=
                ":has(" + variant.NestedVariant.Selector + ")";
            CombineVariantOrder(ref variantOrder, 130);
            return true;
        }

        var targetSelector = "*";
        var targetWrappers = new List<string>();
        var targetOrder = 0;
        if (!TryApplyVariant(
                variant.NestedVariant,
                theme,
                ref targetSelector,
                declarations,
                targetWrappers,
                ref targetOrder))
        {
            return false;
        }

        if (targetWrappers.Count > 0)
        {
            if (variant.Root != "not" ||
                targetWrappers.Count != 1 ||
                !TryNegateConditionalWrapper(
                    targetWrappers[0],
                    out var negatedWrapper))
            {
                return false;
            }

            wrappers.Add(negatedWrapper!);
            CombineVariantOrder(
                ref variantOrder,
                140 + targetOrder);
            return true;
        }

        if (variant.Root == "has")
        {
            selector += ":has(" + targetSelector + ")";
        }
        else if (variant.Root == "in")
        {
            selector =
                ":where(" + targetSelector + ") " + selector;
        }
        else if (variant.Root == "not")
        {
            if (targetSelector.IndexOf(
                    "::",
                    StringComparison.Ordinal) >= 0)
            {
                return false;
            }

            selector += ":not(" + targetSelector + ")";
        }
        else
        {
            return false;
        }

        CombineVariantOrder(
            ref variantOrder,
            120 + targetOrder);
        return true;
    }

    private static bool TryApplyArbitraryVariant(
        UtilityVariant variant,
        ref string selector,
        ICollection<string> wrappers,
        ref int variantOrder)
    {
        if (variant.Selector is null ||
            !UtilityCssText.IsSafeValue(variant.Selector))
        {
            return false;
        }

        if (variant.Selector[0] == '@')
        {
            var wrapper = variant.Selector;
            var parenthesis = wrapper.IndexOf('(');
            if (parenthesis > 0 &&
                wrapper[parenthesis - 1] != ' ')
            {
                wrapper =
                    wrapper.Substring(0, parenthesis) + " " +
                    wrapper.Substring(parenthesis);
            }

            wrappers.Add(wrapper);
            CombineVariantOrder(ref variantOrder, 900);
            return true;
        }

        if (variant.IsRelativeSelector)
        {
            return false;
        }

        selector = variant.Selector.IndexOf(
                "&",
                StringComparison.Ordinal) >= 0
            ? variant.Selector.Replace("&", selector)
            : selector + variant.Selector;
        CombineVariantOrder(ref variantOrder, 910);
        return true;
    }

    private static bool TryResolveVariantThreshold(
        UtilityVariantValue value,
        UtilityTheme theme,
        string namespaceName,
        out string? threshold)
    {
        if (value.Kind == UtilityValueKind.Arbitrary &&
            UtilityCssText.IsSafeValue(value.Text))
        {
            threshold = value.Text;
            return true;
        }

        if (value.Kind == UtilityValueKind.Named &&
            theme.TryGetNamespaceRawValue(
                namespaceName,
                value.Text,
                out threshold) &&
            threshold is not null &&
            threshold.IndexOf(
                "var(",
                StringComparison.Ordinal) < 0)
        {
            return true;
        }

        threshold = null;
        return false;
    }

    private static bool TryNegateConditionalWrapper(
        string wrapper,
        out string? negatedWrapper)
    {
        var separator = wrapper.IndexOf(' ');
        if (separator <= 0 ||
            wrapper.IndexOf(',', separator) >= 0)
        {
            negatedWrapper = null;
            return false;
        }

        var rule = wrapper.Substring(0, separator);
        if (rule is not "@media" and not "@supports" and not "@container")
        {
            negatedWrapper = null;
            return false;
        }

        var condition = wrapper.Substring(separator + 1).Trim();
        negatedWrapper = condition.StartsWith(
            "not ",
            StringComparison.Ordinal)
                ? rule + " " + condition.Substring(4)
                : rule + " not " + condition;
        return true;
    }

    private static string QuoteAttributeValue(string value)
    {
        var equals = value.IndexOf('=');
        if (equals < 0 ||
            equals == value.Length - 1)
        {
            return value;
        }

        var attribute = value.Substring(0, equals);
        var attributeValue = value.Substring(equals + 1).Trim();
        if (attributeValue.Length > 0 &&
            attributeValue[0] is '\'' or '"')
        {
            return value;
        }

        var flag = string.Empty;
        if (attributeValue.Length > 2 &&
            attributeValue[attributeValue.Length - 2] == ' ' &&
            attributeValue[attributeValue.Length - 1] is 'i' or 'I' or 's' or 'S')
        {
            flag =
                " " + attributeValue[attributeValue.Length - 1];
            attributeValue =
                attributeValue.Substring(
                    0,
                    attributeValue.Length - 2);
        }

        return attribute + "=\"" + attributeValue + "\"" + flag;
    }

    private static void CombineVariantOrder(
        ref int variantOrder,
        int component)
    {
        variantOrder =
            ((variantOrder * 67) + component) % 997;
    }

    private static int GetStateOrder(string root) =>
        root switch
        {
            "hover" => 10,
            "focus" => 20,
            "focus-visible" => 21,
            "focus-within" => 22,
            "active" => 30,
            "checked" => 35,
            "disabled" => 40,
            _ => 45,
        };

    private static int GetBreakpointOrder(string root) =>
        root switch
        {
            "sm" => 1,
            "md" => 2,
            "lg" => 3,
            "xl" => 4,
            "2xl" => 5,
            _ => 9,
        };

    private static IEnumerable<string> GetPositionProperties(string root)
    {
        switch (root)
        {
            case "inset":
                return new[] { "inset" };
            case "inset-x":
                return new[] { "inset-inline" };
            case "inset-y":
                return new[] { "inset-block" };
            case "start":
                return new[] { "inset-inline-start" };
            case "inset-s":
                return new[] { "inset-inline-start" };
            case "inset-e":
                return new[] { "inset-inline-end" };
            case "inset-bs":
                return new[] { "inset-block-start" };
            case "inset-be":
                return new[] { "inset-block-end" };
            default:
                return new[] { root };
        }
    }

    private static IEnumerable<string> GetSpacingProperties(string root)
    {
        if (root.StartsWith("border-spacing", StringComparison.Ordinal))
        {
            return root switch
            {
                "border-spacing-x" => new[] { "--tw-border-spacing-x", "border-spacing" },
                "border-spacing-y" => new[] { "--tw-border-spacing-y", "border-spacing" },
                _ => new[]
                {
                    "--tw-border-spacing-x",
                    "--tw-border-spacing-y",
                    "border-spacing",
                },
            };
        }

        if (root.StartsWith("scroll-m", StringComparison.Ordinal) ||
            root.StartsWith("scroll-p", StringComparison.Ordinal))
        {
            var isScrollMargin = root.StartsWith("scroll-m", StringComparison.Ordinal);
            var baseRootLength = "scroll-m".Length;
            var scrollProperty = isScrollMargin
                ? "scroll-margin"
                : "scroll-padding";
            var scrollSuffix = root.Length > baseRootLength
                ? root.Substring(baseRootLength)
                : string.Empty;
            return GetPhysicalAndLogicalProperties(
                scrollProperty,
                scrollSuffix);
        }

        var isMargin = root[0] == 'm';
        var baseProperty = isMargin
            ? "margin"
            : root.StartsWith("gap", StringComparison.Ordinal)
                ? "gap"
                : "padding";
        var suffix = root.StartsWith("gap", StringComparison.Ordinal)
            ? root.Length > 3
                ? root.Substring(4)
                : string.Empty
            : root.Length > 1
                ? root.Substring(1)
                : string.Empty;

        switch (suffix)
        {
            case "x":
                return baseProperty == "gap"
                    ? new[] { "column-gap" }
                    : new[] { baseProperty + "-inline" };
            case "y":
                return baseProperty == "gap"
                    ? new[] { "row-gap" }
                    : new[] { baseProperty + "-block" };
            case "t":
                return new[] { baseProperty + "-top" };
            case "r":
                return new[] { baseProperty + "-right" };
            case "b":
                return new[] { baseProperty + "-bottom" };
            case "l":
                return new[] { baseProperty + "-left" };
            case "s":
                return new[] { baseProperty + "-inline-start" };
            case "e":
                return new[] { baseProperty + "-inline-end" };
            case "bs":
                return new[] { baseProperty + "-block-start" };
            case "be":
                return new[] { baseProperty + "-block-end" };
            default:
                return new[] { baseProperty };
        }
    }

    private static IEnumerable<string> GetPhysicalAndLogicalProperties(
        string baseProperty,
        string suffix)
    {
        switch (suffix)
        {
            case "x":
                return new[] { baseProperty + "-inline" };
            case "y":
                return new[] { baseProperty + "-block" };
            case "s":
                return new[] { baseProperty + "-inline-start" };
            case "e":
                return new[] { baseProperty + "-inline-end" };
            case "bs":
                return new[] { baseProperty + "-block-start" };
            case "be":
                return new[] { baseProperty + "-block-end" };
            case "t":
                return new[] { baseProperty + "-top" };
            case "r":
                return new[] { baseProperty + "-right" };
            case "b":
                return new[] { baseProperty + "-bottom" };
            case "l":
                return new[] { baseProperty + "-left" };
            default:
                return new[] { baseProperty };
        }
    }

    private static IEnumerable<string> GetBorderProperties(string root)
    {
        switch (root)
        {
            case "border-x":
                return new[] { "border-inline" };
            case "border-y":
                return new[] { "border-block" };
            case "border-t":
                return new[] { "border-top" };
            case "border-r":
                return new[] { "border-right" };
            case "border-b":
                return new[] { "border-bottom" };
            case "border-l":
                return new[] { "border-left" };
            case "border-s":
                return new[] { "border-inline-start" };
            case "border-e":
                return new[] { "border-inline-end" };
            case "border-bs":
                return new[] { "border-block-start" };
            case "border-be":
                return new[] { "border-block-end" };
            default:
                return new[] { "border" };
        }
    }

    private static IEnumerable<string> GetRadiusProperties(string root)
    {
        switch (root)
        {
            case "rounded-t":
                return new[] { "border-top-left-radius", "border-top-right-radius" };
            case "rounded-r":
                return new[] { "border-top-right-radius", "border-bottom-right-radius" };
            case "rounded-b":
                return new[] { "border-bottom-right-radius", "border-bottom-left-radius" };
            case "rounded-l":
                return new[] { "border-top-left-radius", "border-bottom-left-radius" };
            case "rounded-tl":
                return new[] { "border-top-left-radius" };
            case "rounded-tr":
                return new[] { "border-top-right-radius" };
            case "rounded-br":
                return new[] { "border-bottom-right-radius" };
            case "rounded-bl":
                return new[] { "border-bottom-left-radius" };
            case "rounded-s":
                return new[]
                {
                    "border-start-start-radius",
                    "border-end-start-radius",
                };
            case "rounded-e":
                return new[]
                {
                    "border-start-end-radius",
                    "border-end-end-radius",
                };
            case "rounded-bs":
                return new[]
                {
                    "border-start-start-radius",
                    "border-start-end-radius",
                };
            case "rounded-be":
                return new[]
                {
                    "border-end-start-radius",
                    "border-end-end-radius",
                };
            case "rounded-ss":
                return new[] { "border-start-start-radius" };
            case "rounded-se":
                return new[] { "border-start-end-radius" };
            case "rounded-es":
                return new[] { "border-end-start-radius" };
            case "rounded-ee":
                return new[] { "border-end-end-radius" };
            default:
                return new[] { "border-radius" };
        }
    }

    private static bool IsSafeCandidateValue(UtilityValue value) =>
        !string.IsNullOrEmpty(value.Text) &&
        UtilityCssText.IsSafeValue(value.Text);

    private static UtilityCssDiagnostic UnsupportedValue(
        string candidateText,
        UtilityCandidate candidate) =>
        Error(
            candidateText,
            UtilityCssDiagnosticCode.UnsupportedValue,
            $"The value on '{candidate.Root}' cannot be applied by the active Viu Utilities registry.");

    private static UtilityCssDiagnostic Error(
        string candidateText,
        UtilityCssDiagnosticCode code,
        string message) =>
        new(
            candidateText ?? string.Empty,
            code,
            UtilityCssDiagnosticSeverity.Error,
            message);

    private static UtilityClassResolutionResult Failure(
        IEnumerable<UtilityCssDiagnostic> diagnostics) =>
        new(
            null,
            new UtilityCollection<UtilityCssDiagnostic>(diagnostics));
}
