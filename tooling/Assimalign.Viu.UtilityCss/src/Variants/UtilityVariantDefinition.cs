namespace Assimalign.Viu.UtilityCss;

/// <summary>
/// Classifies one built-in variant root without embedding selector-generation behavior.
/// </summary>
/// <param name="Name">The case-sensitive variant root.</param>
/// <param name="Kind">The variant grammar shape.</param>
/// <param name="Category">The later resolver family.</param>
public sealed record UtilityVariantDefinition(
    string Name,
    UtilityVariantKind Kind,
    UtilityVariantCategory Category);
