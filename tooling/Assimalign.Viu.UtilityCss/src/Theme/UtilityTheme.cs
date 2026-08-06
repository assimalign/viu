using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace Assimalign.Viu.UtilityCss;

/// <summary>
/// An immutable, Viu-owned CSS-first design system pinned to the Tailwind CSS v4.3.3 theme
/// namespace contract.
/// </summary>
/// <remarks>
/// This type contains semantic CSS data only. It never loads Tailwind, JavaScript, Node.js,
/// PostCSS, or a dynamic configuration file. Namespace behavior follows the official
/// <see href="https://tailwindcss.com/docs/theme#theme-variable-namespaces">
/// v4 theme-variable namespace documentation</see>.
/// </remarks>
public sealed class UtilityTheme : IEquatable<UtilityTheme>
{
    private static readonly string[] ConventionalSpacingNames =
    {
        "0",
        "0.5",
        "1",
        "1.5",
        "2",
        "2.5",
        "3",
        "3.5",
        "4",
        "5",
        "6",
        "7",
        "8",
        "9",
        "10",
        "11",
        "12",
        "14",
        "16",
        "20",
        "24",
        "28",
        "32",
        "36",
        "40",
        "44",
        "48",
        "52",
        "56",
        "60",
        "64",
        "72",
        "80",
        "96",
    };

    private readonly Dictionary<string, string> breakpoints;
    private readonly Dictionary<string, string> colors;
    private readonly Dictionary<string, string> fontSizes;
    private readonly Dictionary<string, string> fontWeights;
    private readonly Dictionary<string, Dictionary<string, string>> namespaceRawValues;
    private readonly Dictionary<string, UtilityCollection<UtilityThemeToken>> namespaceTokens;
    private readonly Dictionary<string, Dictionary<string, string>> namespaceValues;
    private readonly Dictionary<string, UtilityThemeProperty> properties;
    private readonly Dictionary<string, string> radii;
    private readonly Dictionary<string, string> spacing;

    internal UtilityTheme(
        IEnumerable<UtilityThemeProperty> properties,
        string? prefix,
        IEnumerable<UtilityThemeKeyframe>? keyframes = null,
        bool isImportant = false)
    {
        Prefix = prefix;
        IsImportant = isImportant;

        var propertiesByName = new Dictionary<string, UtilityThemeProperty>(
            StringComparer.Ordinal);
        foreach (var property in properties)
        {
            propertiesByName[property.Name] = property;
        }

        Properties = new UtilityCollection<UtilityThemeProperty>(
            propertiesByName.Values.OrderBy(
                property => property.Name,
                StringComparer.Ordinal));
        this.properties = propertiesByName;

        var keyframesByName = new Dictionary<string, UtilityThemeKeyframe>(
            StringComparer.Ordinal);
        foreach (var keyframe in keyframes ?? Array.Empty<UtilityThemeKeyframe>())
        {
            keyframesByName[keyframe.Name] = keyframe;
        }

        Keyframes = new UtilityCollection<UtilityThemeKeyframe>(
            keyframesByName.Values.OrderBy(
                keyframe => keyframe.Name,
                StringComparer.Ordinal));

        var resolvedTokensByNamespace =
            new Dictionary<string, Dictionary<string, string>>(
                StringComparer.Ordinal);
        var rawTokensByNamespace =
            new Dictionary<string, Dictionary<string, string>>(
                StringComparer.Ordinal);
        foreach (var namespaceName in UtilityThemePropertyCatalog.Names)
        {
            resolvedTokensByNamespace.Add(
                namespaceName,
                new Dictionary<string, string>(StringComparer.Ordinal));
            rawTokensByNamespace.Add(
                namespaceName,
                new Dictionary<string, string>(StringComparer.Ordinal));
        }

        AddCalculatedSpacingTokens(
            propertiesByName,
            resolvedTokensByNamespace[UtilityThemeNamespaceNames.Spacing],
            rawTokensByNamespace[UtilityThemeNamespaceNames.Spacing]);

        foreach (var property in Properties)
        {
            if (!UtilityThemePropertyCatalog.TryGetToken(
                    property.Name,
                    out var themeNamespace,
                    out var tokenName))
            {
                continue;
            }

            var namespaceName = UtilityThemePropertyCatalog.GetName(themeNamespace);
            resolvedTokensByNamespace[namespaceName][tokenName!] =
                ResolvePropertyValue(property);
            rawTokensByNamespace[namespaceName][tokenName!] = property.Value;
        }

        NamespaceNames = new UtilityCollection<string>(
            resolvedTokensByNamespace.Keys.OrderBy(
                name => name,
                StringComparer.Ordinal));
        namespaceTokens =
            new Dictionary<string, UtilityCollection<UtilityThemeToken>>(
                StringComparer.Ordinal);
        namespaceValues =
            new Dictionary<string, Dictionary<string, string>>(
                StringComparer.Ordinal);
        namespaceRawValues =
            new Dictionary<string, Dictionary<string, string>>(
                StringComparer.Ordinal);
        foreach (var namespaceName in NamespaceNames)
        {
            var resolvedValues = resolvedTokensByNamespace[namespaceName];
            namespaceTokens.Add(
                namespaceName,
                new UtilityCollection<UtilityThemeToken>(
                    resolvedValues
                        .OrderBy(
                            pair => pair.Key,
                            StringComparer.Ordinal)
                        .Select(
                            pair => new UtilityThemeToken(
                                pair.Key,
                                pair.Value))));
            namespaceValues.Add(namespaceName, resolvedValues);
            namespaceRawValues.Add(
                namespaceName,
                rawTokensByNamespace[namespaceName]);
        }

        Spacing = GetNamespaceTokens(UtilityThemeNamespaceNames.Spacing);
        Colors = GetNamespaceTokens(UtilityThemeNamespaceNames.Color);
        FontSizes = GetNamespaceTokens(UtilityThemeNamespaceNames.FontSize);
        FontWeights = GetNamespaceTokens(UtilityThemeNamespaceNames.FontWeight);
        Radii = GetNamespaceTokens(UtilityThemeNamespaceNames.Radius);
        Breakpoints = new UtilityCollection<UtilityThemeToken>(
            namespaceRawValues[UtilityThemeNamespaceNames.Breakpoint]
                .OrderBy(
                    pair => pair.Key,
                    StringComparer.Ordinal)
                .Select(
                    pair => new UtilityThemeToken(
                        pair.Key,
                        pair.Value)));

        spacing = ToDictionary(Spacing);
        colors = ToDictionary(Colors);
        fontSizes = ToDictionary(FontSizes);
        fontWeights = ToDictionary(FontWeights);
        radii = ToDictionary(Radii);
        breakpoints = ToDictionary(Breakpoints);
        CandidateVariantRegistry = CreateCandidateVariantRegistry(Breakpoints);
    }

    /// <summary>
    /// Gets the complete built-in design system pinned to Tailwind CSS v4.3.3.
    /// </summary>
    public static UtilityTheme Default { get; } =
        UtilityDefaultThemeCatalog.CreateTheme();

    /// <summary>
    /// Gets the optional lowercase prefix used by candidate parsing and emitted custom properties.
    /// </summary>
    public string? Prefix { get; }

    /// <summary>
    /// Gets whether every generated utility declaration receives an important marker.
    /// </summary>
    public bool IsImportant { get; }

    /// <summary>
    /// Gets every semantic custom property in stable ordinal name order.
    /// </summary>
    public UtilityCollection<UtilityThemeProperty> Properties { get; }

    /// <summary>
    /// Gets every recognized namespace name in stable ordinal order.
    /// </summary>
    public UtilityCollection<string> NamespaceNames { get; }

    /// <summary>
    /// Gets built-in and authored animation keyframes in stable ordinal name order.
    /// </summary>
    public UtilityCollection<UtilityThemeKeyframe> Keyframes { get; }

    /// <summary>
    /// Gets spacing tokens in stable ordinal name order.
    /// </summary>
    public UtilityCollection<UtilityThemeToken> Spacing { get; }

    /// <summary>
    /// Gets color tokens in stable ordinal name order.
    /// </summary>
    public UtilityCollection<UtilityThemeToken> Colors { get; }

    /// <summary>
    /// Gets font-size tokens in stable ordinal name order.
    /// </summary>
    public UtilityCollection<UtilityThemeToken> FontSizes { get; }

    /// <summary>
    /// Gets font-weight tokens in stable ordinal name order.
    /// </summary>
    public UtilityCollection<UtilityThemeToken> FontWeights { get; }

    /// <summary>
    /// Gets border-radius tokens in stable ordinal name order.
    /// </summary>
    public UtilityCollection<UtilityThemeToken> Radii { get; }

    /// <summary>
    /// Gets responsive breakpoint tokens as raw media-query values in stable ordinal name order.
    /// </summary>
    public UtilityCollection<UtilityThemeToken> Breakpoints { get; }

    /// <summary>
    /// Gets the immutable candidate-variant registry containing built-in and responsive variants.
    /// </summary>
    public UtilityVariantRegistry VariantRegistry => CandidateVariantRegistry;

    internal UtilityVariantRegistry CandidateVariantRegistry { get; }

    /// <summary>
    /// Gets the compiler-facing tokens in one CSS-first namespace.
    /// </summary>
    /// <param name="namespaceName">
    /// A name from <see cref="UtilityThemeNamespaceNames"/>.
    /// </param>
    /// <returns>
    /// Resolved tokens in ordinal name order, or an empty collection for an unknown namespace.
    /// </returns>
    /// <exception cref="ArgumentNullException"><paramref name="namespaceName"/> is null.</exception>
    public UtilityCollection<UtilityThemeToken> GetNamespaceTokens(
        string namespaceName)
    {
        if (namespaceName is null)
        {
            throw new ArgumentNullException(nameof(namespaceName));
        }

        return namespaceTokens.TryGetValue(namespaceName, out var tokens)
            ? tokens
            : UtilityCollection<UtilityThemeToken>.Empty;
    }

    /// <summary>
    /// Tries to resolve a compiler-facing value from one CSS-first namespace.
    /// </summary>
    /// <param name="namespaceName">The canonical namespace name.</param>
    /// <param name="tokenName">The class-facing token name.</param>
    /// <param name="value">
    /// The direct value for an inline declaration or a formatted <c>var(...)</c> reference for a
    /// normal declaration.
    /// </param>
    /// <returns><see langword="true"/> when the token exists.</returns>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="namespaceName"/> or <paramref name="tokenName"/> is null.
    /// </exception>
    public bool TryGetNamespaceValue(
        string namespaceName,
        string tokenName,
        out string? value)
    {
        ValidateNamespaceLookupArguments(namespaceName, tokenName);
        value = null;
        return namespaceValues.TryGetValue(
                namespaceName,
                out var values) &&
            values.TryGetValue(tokenName, out value);
    }

    /// <summary>
    /// Tries to get the authored value from one CSS-first namespace.
    /// </summary>
    /// <param name="namespaceName">The canonical namespace name.</param>
    /// <param name="tokenName">The class-facing token name.</param>
    /// <param name="value">The unwrapped authored CSS value.</param>
    /// <returns><see langword="true"/> when the token exists.</returns>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="namespaceName"/> or <paramref name="tokenName"/> is null.
    /// </exception>
    public bool TryGetNamespaceRawValue(
        string namespaceName,
        string tokenName,
        out string? value)
    {
        ValidateNamespaceLookupArguments(namespaceName, tokenName);
        value = null;
        return namespaceRawValues.TryGetValue(
                namespaceName,
                out var values) &&
            values.TryGetValue(tokenName, out value);
    }

    /// <summary>
    /// Tries to resolve a spacing token or a canonical nonnegative quarter-step multiplier against
    /// <c>--spacing</c>.
    /// </summary>
    /// <param name="name">The token name or invariant numeric multiplier.</param>
    /// <param name="value">The CSS value when found.</param>
    /// <returns><see langword="true"/> when the value can be resolved.</returns>
    public bool TryGetSpacing(string name, out string? value)
    {
        if (spacing.TryGetValue(name, out value))
        {
            return true;
        }

        if (!TryParseSpacingMultiplier(name) ||
            !properties.TryGetValue("--spacing", out var baseSpacing))
        {
            value = null;
            return false;
        }

        value = CalculateSpacing(
            ResolvePropertyValue(baseSpacing),
            name);
        return true;
    }

    /// <summary>
    /// Tries to resolve a color token.
    /// </summary>
    /// <param name="name">The token name.</param>
    /// <param name="value">The CSS value when found.</param>
    /// <returns><see langword="true"/> when the token exists.</returns>
    public bool TryGetColor(string name, out string? value) =>
        colors.TryGetValue(name, out value);

    /// <summary>
    /// Tries to resolve a font-size token.
    /// </summary>
    /// <param name="name">The token name.</param>
    /// <param name="value">The CSS value when found.</param>
    /// <returns><see langword="true"/> when the token exists.</returns>
    public bool TryGetFontSize(string name, out string? value) =>
        fontSizes.TryGetValue(name, out value);

    /// <summary>
    /// Tries to resolve a font-weight token.
    /// </summary>
    /// <param name="name">The token name.</param>
    /// <param name="value">The CSS value when found.</param>
    /// <returns><see langword="true"/> when the token exists.</returns>
    public bool TryGetFontWeight(string name, out string? value) =>
        fontWeights.TryGetValue(name, out value);

    /// <summary>
    /// Tries to resolve a border-radius token.
    /// </summary>
    /// <param name="name">The token name.</param>
    /// <param name="value">The CSS value when found.</param>
    /// <returns><see langword="true"/> when the token exists.</returns>
    public bool TryGetRadius(string name, out string? value) =>
        radii.TryGetValue(name, out value);

    /// <summary>
    /// Tries to get a responsive breakpoint as a raw media-query value.
    /// </summary>
    /// <param name="name">The token name.</param>
    /// <param name="value">The CSS value when found.</param>
    /// <returns><see langword="true"/> when the token exists.</returns>
    public bool TryGetBreakpoint(string name, out string? value) =>
        breakpoints.TryGetValue(name, out value);

    /// <summary>
    /// Tries to get one semantic custom property by its unprefixed name.
    /// </summary>
    /// <param name="name">The canonical custom-property name.</param>
    /// <param name="property">The property when found.</param>
    /// <returns><see langword="true"/> when the property exists.</returns>
    public bool TryGetProperty(
        string name,
        out UtilityThemeProperty? property) =>
        properties.TryGetValue(name, out property);

    /// <summary>
    /// Formats an unprefixed semantic custom-property name for CSS emission.
    /// </summary>
    /// <param name="name">The canonical name beginning with <c>--</c>.</param>
    /// <returns>The prefixed or unchanged custom-property name.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="name"/> is null.</exception>
    /// <exception cref="ArgumentException">
    /// <paramref name="name"/> is not a named CSS custom property.
    /// </exception>
    public string FormatCustomPropertyName(string name)
    {
        if (name is null)
        {
            throw new ArgumentNullException(nameof(name));
        }

        if (!name.StartsWith("--", StringComparison.Ordinal) ||
            name.Length == 2)
        {
            throw new ArgumentException(
                "A theme custom-property name must begin with '--' and contain a name.",
                nameof(name));
        }

        return string.IsNullOrEmpty(Prefix)
            ? name
            : "--" + Prefix + "-" + name.Substring(2);
    }

    /// <summary>
    /// Determines whether this theme has the same prefix, important mode, properties, and keyframes as
    /// <paramref name="other"/>.
    /// </summary>
    /// <param name="other">The theme to compare.</param>
    /// <returns><see langword="true"/> when both themes are structurally equal.</returns>
    public bool Equals(UtilityTheme? other) =>
        other is not null &&
        string.Equals(Prefix, other.Prefix, StringComparison.Ordinal) &&
        IsImportant == other.IsImportant &&
        Properties.Equals(other.Properties) &&
        Keyframes.Equals(other.Keyframes);

    /// <inheritdoc />
    public override bool Equals(object? value) =>
        value is UtilityTheme other && Equals(other);

    /// <inheritdoc />
    public override int GetHashCode()
    {
        var hash = 17;
        hash = (hash * 31) + (Prefix is null ? 0 : StringComparer.Ordinal.GetHashCode(Prefix));
        hash = (hash * 31) + IsImportant.GetHashCode();
        hash = (hash * 31) + Properties.GetHashCode();
        hash = (hash * 31) + Keyframes.GetHashCode();
        return hash;
    }

    private static void ValidateNamespaceLookupArguments(
        string namespaceName,
        string tokenName)
    {
        if (namespaceName is null)
        {
            throw new ArgumentNullException(nameof(namespaceName));
        }

        if (tokenName is null)
        {
            throw new ArgumentNullException(nameof(tokenName));
        }
    }

    private void AddCalculatedSpacingTokens(
        IReadOnlyDictionary<string, UtilityThemeProperty> propertiesByName,
        IDictionary<string, string> resolvedValues,
        IDictionary<string, string> rawValues)
    {
        if (!propertiesByName.TryGetValue(
                "--spacing",
                out var baseSpacing))
        {
            return;
        }

        var resolvedBase = ResolvePropertyValue(baseSpacing);
        foreach (var name in ConventionalSpacingNames)
        {
            resolvedValues[name] = CalculateSpacing(resolvedBase, name);
            rawValues[name] = CalculateSpacing(baseSpacing.Value, name);
        }

        resolvedValues["px"] = "1px";
        rawValues["px"] = "1px";
    }

    private static string CalculateSpacing(
        string baseValue,
        string multiplier) =>
        multiplier == "0"
            ? "0px"
            : "calc(" + baseValue + " * " + multiplier + ")";

    private static bool TryParseSpacingMultiplier(string name)
    {
        if (!decimal.TryParse(
                name,
                NumberStyles.AllowDecimalPoint,
                CultureInfo.InvariantCulture,
                out var multiplier) ||
            multiplier < decimal.Zero ||
            multiplier % 0.25m != decimal.Zero)
        {
            return false;
        }

        return name == multiplier.ToString(
            "0.############################",
            CultureInfo.InvariantCulture);
    }

    private string ResolvePropertyValue(UtilityThemeProperty property)
    {
        if ((property.Options & UtilityThemeOptions.Inline) != 0)
        {
            return property.Value;
        }

        var propertyName = FormatCustomPropertyName(property.Name);
        return (property.Options & UtilityThemeOptions.Reference) != 0
            ? "var(" + propertyName + ", " + property.Value + ")"
            : "var(" + propertyName + ")";
    }

    private static Dictionary<string, string> ToDictionary(
        IEnumerable<UtilityThemeToken> tokens)
    {
        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var token in tokens)
        {
            result.Add(token.Name, token.Value);
        }

        return result;
    }

    private static UtilityVariantRegistry CreateCandidateVariantRegistry(
        IEnumerable<UtilityThemeToken> breakpointTokens)
    {
        var definitions = UtilityVariantRegistry.BuiltIn.Definitions.ToList();
        var names = new HashSet<string>(
            definitions.Select(definition => definition.Name),
            StringComparer.Ordinal);

        foreach (var token in breakpointTokens)
        {
            if (names.Add(token.Name))
            {
                definitions.Add(
                    new UtilityVariantDefinition(
                        token.Name,
                        UtilityVariantKind.Static,
                        UtilityVariantCategory.Responsive));
            }
        }

        return definitions.Count == UtilityVariantRegistry.BuiltIn.Definitions.Count
            ? UtilityVariantRegistry.BuiltIn
            : new UtilityVariantRegistry(definitions);
    }
}
