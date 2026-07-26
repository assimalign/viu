namespace Assimalign.Viu.Tooling.UtilityCss;

/// <summary>
/// Immutable host context for one I/O-free source-configuration parse.
/// </summary>
public sealed record UtilitySourceParseOptions
{
    /// <summary>
    /// Gets the default parse context.
    /// </summary>
    public static UtilitySourceParseOptions Default { get; } = new();

    /// <summary>
    /// Gets the optional source identity copied onto parsed nodes and diagnostics.
    /// </summary>
    public string? SourceIdentity { get; init; }

    /// <summary>
    /// Gets the non-negative absolute offset added to source-relative spans.
    /// </summary>
    public int ContentOffset { get; init; }

    /// <summary>
    /// Gets the positive maximum number of brace-expanded strings produced by one inline
    /// directive. The default protects build and editor hosts from accidental combinatorial input.
    /// </summary>
    public int MaximumExpansionCount { get; init; } = 10000;

    /// <summary>
    /// Gets an optional resolved theme prefix used to validate inline candidates. When omitted, the
    /// prefix declared by the virtual import is used.
    /// </summary>
    public string? Prefix { get; init; }

    /// <summary>
    /// Gets the immutable variant registry used to validate candidates produced by inline source
    /// expansion. Hosts can supply project-defined variants after their definition pass.
    /// </summary>
    public UtilityVariantRegistry VariantRegistry { get; init; } =
        UtilityVariantRegistry.BuiltIn;
}
