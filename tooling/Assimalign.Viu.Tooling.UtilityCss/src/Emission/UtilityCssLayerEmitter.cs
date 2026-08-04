using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;

namespace Assimalign.Viu.Tooling.UtilityCss;

/// <summary>
/// Emits deterministic theme and base layers for a Viu Utilities stylesheet.
/// </summary>
/// <remarks>
/// Utility rules remain owned by <see cref="UtilityCssCompiler"/>. Hosts prepend the result of
/// <see cref="EmitDesignSystem(UtilityTheme, CancellationToken)"/> to the compiler result so the
/// shared layer order, theme variables, required keyframes, and Preflight rules are emitted once.
/// </remarks>
public static class UtilityCssLayerEmitter
{
    /// <summary>
    /// Gets the canonical cascade-layer declaration.
    /// </summary>
    public const string LayerOrder =
        "@layer theme, base, components, utilities;\n";

    /// <summary>
    /// Emits the complete theme and base layers.
    /// </summary>
    /// <param name="theme">The immutable design system.</param>
    /// <param name="cancellationToken">The build-host cancellation boundary.</param>
    /// <returns>
    /// The canonical layer-order declaration followed by deterministic theme and base layers.
    /// </returns>
    /// <exception cref="ArgumentNullException"><paramref name="theme"/> is null.</exception>
    /// <exception cref="OperationCanceledException">
    /// <paramref name="cancellationToken"/> was canceled.
    /// </exception>
    public static string EmitDesignSystem(
        UtilityTheme theme,
        CancellationToken cancellationToken)
    {
        ValidateTheme(theme);
        cancellationToken.ThrowIfCancellationRequested();
        return LayerOrder +
            UtilityCustomPropertyCatalog.EmitAll(cancellationToken) +
            EmitTheme(theme, cancellationToken) +
            EmitBase(theme, cancellationToken);
    }

    /// <summary>
    /// Emits theme and base layers using the import-level theme mode and the generated utility CSS
    /// as the used-variable set.
    /// </summary>
    /// <param name="theme">The immutable design system.</param>
    /// <param name="generatedUtilityCss">
    /// Generated utility CSS whose custom-property references select normal-mode theme variables.
    /// </param>
    /// <param name="importedThemeOptions">
    /// Import-level options such as <see cref="UtilityThemeOptions.Static"/>.
    /// </param>
    /// <param name="cancellationToken">The build-host cancellation boundary.</param>
    /// <returns>
    /// The canonical layer-order declaration followed by usage-aware theme and complete base layers.
    /// </returns>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="theme"/> or <paramref name="generatedUtilityCss"/> is null.
    /// </exception>
    /// <exception cref="OperationCanceledException">
    /// <paramref name="cancellationToken"/> was canceled.
    /// </exception>
    public static string EmitDesignSystem(
        UtilityTheme theme,
        string generatedUtilityCss,
        UtilityThemeOptions importedThemeOptions,
        CancellationToken cancellationToken)
    {
        ValidateTheme(theme);
        if (generatedUtilityCss is null)
        {
            throw new ArgumentNullException(nameof(generatedUtilityCss));
        }

        cancellationToken.ThrowIfCancellationRequested();
        var baseCss = EmitBase(theme, cancellationToken);
        var usageCss = generatedUtilityCss + baseCss;
        return LayerOrder +
            UtilityCustomPropertyCatalog.EmitNeeded(
                usageCss,
                cancellationToken) +
            EmitTheme(
                theme,
                usageCss,
                importedThemeOptions,
                cancellationToken) +
            baseCss;
    }

    /// <summary>
    /// Emits all non-reference theme properties and keyframes required by the theme's animation
    /// tokens.
    /// </summary>
    /// <param name="theme">The immutable design system.</param>
    /// <param name="cancellationToken">The build-host cancellation boundary.</param>
    /// <returns>A deterministic <c>theme</c> layer with LF line endings.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="theme"/> is null.</exception>
    /// <exception cref="OperationCanceledException">
    /// <paramref name="cancellationToken"/> was canceled.
    /// </exception>
    public static string EmitTheme(
        UtilityTheme theme,
        CancellationToken cancellationToken)
    {
        ValidateTheme(theme);
        var emittedProperties = new List<UtilityThemeProperty>();
        foreach (var property in theme.Properties)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if ((property.Options & UtilityThemeOptions.Reference) == 0)
            {
                emittedProperties.Add(property);
            }
        }

        return EmitTheme(
            theme,
            emittedProperties,
            cancellationToken);
    }

    /// <summary>
    /// Emits a usage-aware theme layer for normal or static import mode.
    /// </summary>
    /// <param name="theme">The immutable design system.</param>
    /// <param name="usageCss">CSS whose <c>var(...)</c> references select normal-mode variables.</param>
    /// <param name="importedThemeOptions">The import-level theme options.</param>
    /// <param name="cancellationToken">The build-host cancellation boundary.</param>
    /// <returns>A deterministic <c>theme</c> layer with LF line endings.</returns>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="theme"/> or <paramref name="usageCss"/> is null.
    /// </exception>
    /// <exception cref="OperationCanceledException">
    /// <paramref name="cancellationToken"/> was canceled.
    /// </exception>
    public static string EmitTheme(
        UtilityTheme theme,
        string usageCss,
        UtilityThemeOptions importedThemeOptions,
        CancellationToken cancellationToken)
    {
        ValidateTheme(theme);
        if (usageCss is null)
        {
            throw new ArgumentNullException(nameof(usageCss));
        }

        var emitAll =
            (importedThemeOptions & UtilityThemeOptions.Static) != 0;
        var usedPropertyNames = FindUsedThemeProperties(
            theme,
            usageCss,
            cancellationToken);
        var emittedProperties = new List<UtilityThemeProperty>();
        foreach (var property in theme.Properties)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if ((property.Options & UtilityThemeOptions.Reference) != 0)
            {
                continue;
            }

            if (emitAll ||
                (property.Options & UtilityThemeOptions.Static) != 0 ||
                usedPropertyNames.Contains(
                    theme.FormatCustomPropertyName(property.Name)))
            {
                emittedProperties.Add(property);
            }
        }

        return EmitTheme(
            theme,
            emittedProperties,
            cancellationToken);
    }

    /// <summary>
    /// Emits the complete Preflight compatibility rules inside the <c>base</c> layer.
    /// </summary>
    /// <param name="theme">The immutable design system.</param>
    /// <param name="cancellationToken">The build-host cancellation boundary.</param>
    /// <returns>A deterministic <c>base</c> layer with LF line endings.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="theme"/> is null.</exception>
    /// <exception cref="OperationCanceledException">
    /// <paramref name="cancellationToken"/> was canceled.
    /// </exception>
    public static string EmitBase(
        UtilityTheme theme,
        CancellationToken cancellationToken)
    {
        ValidateTheme(theme);
        var preflight = UtilityPreflight.Emit(
            theme,
            cancellationToken);
        var result = new StringBuilder();
        result.Append("@layer base {\n");
        foreach (var line in preflight.Split('\n'))
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (line.Length == 0)
            {
                result.Append('\n');
                continue;
            }

            result.Append("  ");
            result.Append(line);
            result.Append('\n');
        }

        result.Append("}\n");
        return result.ToString();
    }

    private static HashSet<string> FindUsedThemeProperties(
        UtilityTheme theme,
        string usageCss,
        CancellationToken cancellationToken)
    {
        var usedNames = new HashSet<string>(StringComparer.Ordinal);
        foreach (var property in theme.Properties)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var formattedName =
                theme.FormatCustomPropertyName(property.Name);
            if (ContainsCustomPropertyReference(
                    usageCss,
                    formattedName))
            {
                usedNames.Add(formattedName);
            }
        }

        var addedDependency = true;
        while (addedDependency)
        {
            cancellationToken.ThrowIfCancellationRequested();
            addedDependency = false;
            foreach (var property in theme.Properties)
            {
                var formattedName =
                    theme.FormatCustomPropertyName(property.Name);
                if (!usedNames.Contains(formattedName))
                {
                    continue;
                }

                foreach (var dependency in theme.Properties)
                {
                    var formattedDependencyName =
                        theme.FormatCustomPropertyName(
                            dependency.Name);
                    if (!usedNames.Contains(formattedDependencyName) &&
                        ContainsCustomPropertyReference(
                            property.Value,
                            dependency.Name))
                    {
                        usedNames.Add(formattedDependencyName);
                        addedDependency = true;
                    }
                }
            }
        }

        return usedNames;
    }

    private static bool ContainsCustomPropertyReference(
        string css,
        string propertyName) =>
        css.IndexOf(
            "var(" + propertyName,
            StringComparison.Ordinal) >= 0;

    private static string EmitTheme(
        UtilityTheme theme,
        IReadOnlyList<UtilityThemeProperty> emittedProperties,
        CancellationToken cancellationToken)
    {
        var requiredKeyframeNames = GetRequiredKeyframeNames(
            theme,
            emittedProperties);
        if (emittedProperties.Count == 0 &&
            requiredKeyframeNames.Count == 0)
        {
            return string.Empty;
        }

        var result = new StringBuilder();
        result.Append("@layer theme {\n");
        if (emittedProperties.Count > 0)
        {
            result.Append("  :root, :host {\n");
            foreach (var property in emittedProperties)
            {
                cancellationToken.ThrowIfCancellationRequested();
                result.Append("    ");
                result.Append(
                    theme.FormatCustomPropertyName(property.Name));
                result.Append(": ");
                result.Append(
                    FormatPropertyValue(
                        theme,
                        property.Value));
                result.Append(";\n");
            }

            result.Append("  }\n");
        }

        foreach (var keyframe in theme.Keyframes)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!requiredKeyframeNames.Contains(keyframe.Name))
            {
                continue;
            }

            result.Append("  @keyframes ");
            result.Append(keyframe.Name);
            result.Append(" {\n");
            foreach (var line in NormalizeLineEndings(keyframe.Body).Split('\n'))
            {
                result.Append("    ");
                result.Append(line);
                result.Append('\n');
            }

            result.Append("  }\n");
        }

        result.Append("}\n");
        return result.ToString();
    }

    private static HashSet<string> GetRequiredKeyframeNames(
        UtilityTheme theme,
        IReadOnlyList<UtilityThemeProperty> emittedProperties)
    {
        var names = new HashSet<string>(StringComparer.Ordinal);
        var animationValues = new List<string>();
        foreach (var property in emittedProperties)
        {
            if (!property.Name.StartsWith(
                    "--animate-",
                    StringComparison.Ordinal) ||
                string.IsNullOrWhiteSpace(property.Value))
            {
                continue;
            }

            animationValues.Add(property.Value);
        }

        foreach (var keyframe in theme.Keyframes)
        {
            foreach (var animationValue in animationValues)
            {
                if (ContainsCssIdentifier(
                        animationValue,
                        keyframe.Name))
                {
                    names.Add(keyframe.Name);
                    break;
                }
            }
        }

        return names;
    }

    private static bool ContainsCssIdentifier(
        string value,
        string identifier)
    {
        var index = 0;
        while (index < value.Length)
        {
            index = value.IndexOf(
                identifier,
                index,
                StringComparison.Ordinal);
            if (index < 0)
            {
                return false;
            }

            var beforeIsIdentifier =
                index > 0 &&
                IsCssIdentifierCharacter(value[index - 1]);
            var afterIndex = index + identifier.Length;
            var afterIsIdentifier =
                afterIndex < value.Length &&
                IsCssIdentifierCharacter(value[afterIndex]);
            if (!beforeIsIdentifier &&
                !afterIsIdentifier)
            {
                return true;
            }

            index = afterIndex;
        }

        return false;
    }

    private static bool IsCssIdentifierCharacter(char value) =>
        char.IsLetterOrDigit(value) ||
        value is '-' or '_';

    private static string FormatPropertyValue(
        UtilityTheme theme,
        string value)
    {
        if (string.IsNullOrEmpty(theme.Prefix) ||
            value.IndexOf(
                "var(--",
                StringComparison.Ordinal) < 0)
        {
            return NormalizeLineEndings(value);
        }

        var result = new StringBuilder();
        var index = 0;
        while (index < value.Length)
        {
            var variableStart = value.IndexOf(
                "var(--",
                index,
                StringComparison.Ordinal);
            if (variableStart < 0)
            {
                result.Append(
                    value,
                    index,
                    value.Length - index);
                break;
            }

            result.Append(
                value,
                index,
                variableStart - index);
            result.Append("var(");
            var propertyStart = variableStart + 4;
            var propertyEnd = propertyStart;
            while (propertyEnd < value.Length &&
                   value[propertyEnd] != ',' &&
                   value[propertyEnd] != ')' &&
                   !char.IsWhiteSpace(value[propertyEnd]))
            {
                propertyEnd++;
            }

            var propertyName = value.Substring(
                propertyStart,
                propertyEnd - propertyStart);
            result.Append(
                theme.TryGetProperty(propertyName, out _)
                    ? theme.FormatCustomPropertyName(propertyName)
                    : propertyName);
            index = propertyEnd;
        }

        return NormalizeLineEndings(result.ToString());
    }

    private static string NormalizeLineEndings(string value) =>
        value.Replace("\r\n", "\n").Replace('\r', '\n');

    private static void ValidateTheme(UtilityTheme theme)
    {
        if (theme is null)
        {
            throw new ArgumentNullException(nameof(theme));
        }
    }
}
