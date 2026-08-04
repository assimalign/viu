namespace Assimalign.Viu.Tooling.UtilityCss;

/// <summary>
/// Identifies a recoverable CSS-first directive parser diagnostic.
/// </summary>
public enum UtilityDirectiveDiagnosticCode
{
    /// <summary>A directive that requires a block used statement syntax.</summary>
    MissingBlock,

    /// <summary>A statement-only directive unexpectedly contained a block.</summary>
    UnexpectedBlock,

    /// <summary>A directive is missing required parameters.</summary>
    MissingParameters,

    /// <summary>A directive statement is missing its terminating semicolon.</summary>
    MissingSemicolon,

    /// <summary>A directive block is not terminated.</summary>
    UnterminatedBlock,

    /// <summary>A quoted CSS string is not terminated.</summary>
    UnterminatedString,

    /// <summary>A CSS comment is not terminated.</summary>
    UnterminatedComment,

    /// <summary>A closing CSS delimiter does not match the opening delimiter.</summary>
    UnbalancedDelimiter,

    /// <summary>A custom utility name does not follow the supported v4 name grammar.</summary>
    InvalidUtilityName,

    /// <summary>A custom variant name does not follow the supported v4 name grammar.</summary>
    InvalidVariantName,

    /// <summary>A block-form custom variant does not contain an <c>@slot</c> placeholder.</summary>
    MissingVariantSlot,

    /// <summary>A directive appears at a CSS nesting depth where it is not supported.</summary>
    InvalidPlacement,

    /// <summary>A reference does not contain one quoted stylesheet specifier.</summary>
    InvalidReference,

    /// <summary>A direct Tailwind package import or reference violates the standalone boundary.</summary>
    UnsupportedFrameworkDependency,

    /// <summary>An executable JavaScript plugin or legacy configuration directive was rejected.</summary>
    UnsupportedExecutableDirective,

    /// <summary>A supported CSS function has malformed or unsupported arguments.</summary>
    InvalidFunctionArguments,

    /// <summary>A supported CSS function call is not terminated.</summary>
    UnterminatedFunction,

    /// <summary>A value, modifier, or default function appears outside its documented context.</summary>
    InvalidFunctionPlacement,

    /// <summary>A functional custom utility does not contain the required <c>--value()</c> call.</summary>
    MissingFunctionalValue,
}
