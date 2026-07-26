using System;
using System.Collections.Generic;
using System.Linq;

namespace Assimalign.Viu.Tooling.UtilityCss;

/// <summary>
/// An immutable variant-root registry. <see cref="BuiltIn"/> pins the complete built-in Viu
/// Utilities v4.3.3 surface while keeping selector and at-rule application in the later resolver.
/// </summary>
public sealed class UtilityVariantRegistry : IEquatable<UtilityVariantRegistry>
{
    private readonly Dictionary<string, UtilityVariantDefinition> definitionsByName;

    /// <summary>
    /// Gets the built-in Viu Utilities v4.3.3 registry.
    /// </summary>
    public static UtilityVariantRegistry BuiltIn { get; } =
        new(CreateBuiltInDefinitions());

    /// <summary>
    /// Creates an immutable registry from <paramref name="definitions"/>.
    /// </summary>
    /// <param name="definitions">The variant definitions to copy.</param>
    public UtilityVariantRegistry(IEnumerable<UtilityVariantDefinition> definitions)
    {
        var orderedDefinitions = definitions
            .OrderBy(definition => definition.Name, StringComparer.Ordinal)
            .ToArray();

        definitionsByName = new Dictionary<string, UtilityVariantDefinition>(
            orderedDefinitions.Length,
            StringComparer.Ordinal);

        foreach (var definition in orderedDefinitions)
        {
            if (string.IsNullOrEmpty(definition.Name))
            {
                throw new ArgumentException(
                    "A variant definition name cannot be empty.",
                    nameof(definitions));
            }

            if (definition.Kind is UtilityVariantKind.Prefix or UtilityVariantKind.Arbitrary)
            {
                throw new ArgumentException(
                    "Prefix and arbitrary variants are candidate-specific and cannot be registered.",
                    nameof(definitions));
            }

            if (definitionsByName.ContainsKey(definition.Name))
            {
                throw new ArgumentException(
                    $"The variant root '{definition.Name}' is registered more than once.",
                    nameof(definitions));
            }

            definitionsByName.Add(definition.Name, definition);
        }

        Definitions = new UtilityCollection<UtilityVariantDefinition>(orderedDefinitions);
    }

    /// <summary>
    /// Gets all definitions in stable ordinal name order.
    /// </summary>
    public UtilityCollection<UtilityVariantDefinition> Definitions { get; }

    /// <summary>
    /// Looks up an exact, case-sensitive variant root.
    /// </summary>
    /// <param name="name">The root to find.</param>
    /// <param name="definition">The matching definition when found.</param>
    /// <returns><see langword="true"/> when the root is registered.</returns>
    public bool TryGetDefinition(
        string name,
        out UtilityVariantDefinition? definition) =>
        definitionsByName.TryGetValue(name, out definition);

    /// <summary>
    /// Determines whether this registry contains the same definitions as <paramref name="other"/>.
    /// </summary>
    /// <param name="other">The registry to compare.</param>
    /// <returns><see langword="true"/> when both registries are structurally equal.</returns>
    public bool Equals(UtilityVariantRegistry? other) =>
        other is not null && Definitions.Equals(other.Definitions);

    /// <inheritdoc />
    public override bool Equals(object? value) =>
        value is UtilityVariantRegistry other && Equals(other);

    /// <inheritdoc />
    public override int GetHashCode() => Definitions.GetHashCode();

    private static UtilityVariantDefinition[] CreateBuiltInDefinitions()
    {
        var definitions = new List<UtilityVariantDefinition>();

        Add(
            definitions,
            UtilityVariantKind.Static,
            UtilityVariantCategory.Child,
            "*");
        Add(
            definitions,
            UtilityVariantKind.Static,
            UtilityVariantCategory.Descendant,
            "**");

        Add(
            definitions,
            UtilityVariantKind.Static,
            UtilityVariantCategory.PseudoElement,
            "after",
            "backdrop",
            "before",
            "details-content",
            "file",
            "first-letter",
            "first-line",
            "marker",
            "placeholder",
            "selection");

        Add(
            definitions,
            UtilityVariantKind.Static,
            UtilityVariantCategory.Structural,
            "even",
            "first",
            "first-of-type",
            "last",
            "last-of-type",
            "odd",
            "only",
            "only-of-type");
        Add(
            definitions,
            UtilityVariantKind.Functional,
            UtilityVariantCategory.Structural,
            "nth",
            "nth-last",
            "nth-last-of-type",
            "nth-of-type");

        Add(
            definitions,
            UtilityVariantKind.Static,
            UtilityVariantCategory.State,
            "active",
            "autofill",
            "checked",
            "default",
            "disabled",
            "empty",
            "enabled",
            "focus",
            "focus-visible",
            "focus-within",
            "hover",
            "in-range",
            "indeterminate",
            "inert",
            "invalid",
            "open",
            "optional",
            "out-of-range",
            "placeholder-shown",
            "read-only",
            "required",
            "target",
            "user-invalid",
            "user-valid",
            "valid",
            "visited");

        Add(
            definitions,
            UtilityVariantKind.Compound,
            UtilityVariantCategory.Compound,
            "group",
            "has",
            "in",
            "not",
            "peer");

        Add(
            definitions,
            UtilityVariantKind.Functional,
            UtilityVariantCategory.Attribute,
            "aria",
            "data");
        Add(
            definitions,
            UtilityVariantKind.Functional,
            UtilityVariantCategory.Supports,
            "supports");

        Add(
            definitions,
            UtilityVariantKind.Static,
            UtilityVariantCategory.Responsive,
            "2xl",
            "lg",
            "md",
            "sm",
            "xl");
        Add(
            definitions,
            UtilityVariantKind.Functional,
            UtilityVariantCategory.Responsive,
            "max",
            "min");
        Add(
            definitions,
            UtilityVariantKind.Functional,
            UtilityVariantCategory.ContainerQuery,
            "@",
            "@max",
            "@min");

        Add(
            definitions,
            UtilityVariantKind.Static,
            UtilityVariantCategory.Environment,
            "any-pointer-coarse",
            "any-pointer-fine",
            "any-pointer-none",
            "contrast-less",
            "contrast-more",
            "dark",
            "forced-colors",
            "inverted-colors",
            "landscape",
            "motion-reduce",
            "motion-safe",
            "noscript",
            "pointer-coarse",
            "pointer-fine",
            "pointer-none",
            "portrait",
            "starting");
        Add(
            definitions,
            UtilityVariantKind.Static,
            UtilityVariantCategory.Direction,
            "ltr",
            "rtl");
        Add(
            definitions,
            UtilityVariantKind.Static,
            UtilityVariantCategory.Print,
            "print");

        return definitions.ToArray();
    }

    private static void Add(
        ICollection<UtilityVariantDefinition> definitions,
        UtilityVariantKind kind,
        UtilityVariantCategory category,
        params string[] names)
    {
        foreach (var name in names)
        {
            definitions.Add(new UtilityVariantDefinition(name, kind, category));
        }
    }
}
