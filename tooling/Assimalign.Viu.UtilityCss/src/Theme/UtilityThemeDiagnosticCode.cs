namespace Assimalign.Viu.UtilityCss;

/// <summary>
/// Identifies a recoverable CSS-first theme parser diagnostic.
/// </summary>
public enum UtilityThemeDiagnosticCode
{
    /// <summary>An <c>@theme</c> rule does not contain a declaration block.</summary>
    MissingBlock,

    /// <summary>An <c>@theme</c> declaration block is not terminated.</summary>
    UnterminatedBlock,

    /// <summary>An <c>@theme</c> option is unknown or malformed.</summary>
    InvalidOption,

    /// <summary>A configured utility prefix is not lowercase ASCII letters.</summary>
    InvalidPrefix,

    /// <summary>Multiple theme blocks configured different prefixes.</summary>
    ConflictingPrefix,

    /// <summary>A theme statement is not a custom-property declaration.</summary>
    InvalidDeclaration,

    /// <summary>A custom-property name is empty or contains an unsupported character.</summary>
    InvalidCustomProperty,

    /// <summary>A wildcard namespace reset does not use the required <c>initial</c> value.</summary>
    InvalidNamespaceReset,
}
