namespace Assimalign.Viu.UtilityCss;

/// <summary>
/// Identifies a recoverable CSS-first source-configuration diagnostic.
/// </summary>
public enum UtilitySourceDiagnosticCode
{
    /// <summary>An import or source statement has no terminating semicolon.</summary>
    UnterminatedStatement,

    /// <summary>The Viu utility compiler sentinel was imported more than once.</summary>
    DuplicateUtilitiesImport,

    /// <summary>A Tailwind package import was authored instead of the standalone Viu sentinel.</summary>
    TailwindDependencyNotAllowed,

    /// <summary>A virtual-import modifier is unknown, malformed, or duplicated.</summary>
    InvalidImportModifier,

    /// <summary>A configured utility prefix is not lowercase ASCII letters.</summary>
    InvalidPrefix,

    /// <summary>An <c>@source</c> directive is nested inside another CSS block.</summary>
    InvalidPlacement,

    /// <summary>An <c>@source</c> directive has invalid path or inline syntax.</summary>
    InvalidSourceDirective,

    /// <summary>An inline source expression contains an invalid brace or numeric-range form.</summary>
    InvalidInlineExpression,

    /// <summary>An inline source expression exceeds the configured deterministic expansion limit.</summary>
    ExpansionLimitExceeded,
}
