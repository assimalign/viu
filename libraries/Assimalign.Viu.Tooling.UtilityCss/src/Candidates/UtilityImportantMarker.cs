namespace Assimalign.Viu.Tooling.UtilityCss;

/// <summary>
/// Identifies which important marker syntax appeared on a utility candidate.
/// </summary>
public enum UtilityImportantMarker
{
    /// <summary>The candidate is not important.</summary>
    None,

    /// <summary>The canonical v4 trailing marker, such as <c>mt-4!</c>.</summary>
    Trailing,

    /// <summary>The accepted but deprecated leading marker, such as <c>!mt-4</c>.</summary>
    DeprecatedLeading,
}
