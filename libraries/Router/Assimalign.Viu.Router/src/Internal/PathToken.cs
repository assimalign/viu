namespace Assimalign.Viu.Router;

/// <summary>
/// A single token within one path segment — either literal text or a dynamic parameter, with the
/// optional, repeatable, and custom-pattern modifiers the parameter form carries.
/// </summary>
/// <remarks>
/// A <see langword="readonly struct"/> so a tokenized path allocates only its backing arrays, never
/// a boxed token per character run — the tokenizer runs once per route record at table-build time.
/// </remarks>
internal readonly struct PathToken
{
    private PathToken(PathTokenKind kind, string value, string customPattern, bool optional, bool repeatable)
    {
        Kind = kind;
        Value = value;
        CustomPattern = customPattern;
        Optional = optional;
        Repeatable = repeatable;
    }

    /// <summary>Whether this token is literal text (<see cref="PathTokenKind.Static"/>) or a parameter.</summary>
    public PathTokenKind Kind { get; }

    /// <summary>
    /// For a <see cref="PathTokenKind.Static"/> token, the literal text; for a
    /// <see cref="PathTokenKind.Parameter"/> token, the parameter name.
    /// </summary>
    public string Value { get; }

    /// <summary>
    /// The user-supplied custom pattern for a parameter (the <c>\d+</c> in <c>:id(\d+)</c>), or the
    /// empty string when none was supplied. Only meaningful for
    /// <see cref="PathTokenKind.Parameter"/> tokens.
    /// </summary>
    public string CustomPattern { get; }

    /// <summary>Whether the parameter is optional — the <c>?</c> or <c>*</c> modifier.</summary>
    public bool Optional { get; }

    /// <summary>Whether the parameter is repeatable — the <c>+</c> or <c>*</c> modifier.</summary>
    public bool Repeatable { get; }

    /// <summary>Creates a literal-text token.</summary>
    /// <param name="value">The literal text.</param>
    public static PathToken Static(string value)
        => new(PathTokenKind.Static, value, customPattern: string.Empty, optional: false, repeatable: false);

    /// <summary>Creates a dynamic-parameter token.</summary>
    /// <param name="name">The parameter name.</param>
    /// <param name="customPattern">The custom pattern, or the empty string for the default <c>[^/]+?</c>.</param>
    /// <param name="optional">Whether the parameter is optional (<c>?</c>/<c>*</c>).</param>
    /// <param name="repeatable">Whether the parameter is repeatable (<c>+</c>/<c>*</c>).</param>
    public static PathToken Parameter(string name, string customPattern, bool optional, bool repeatable)
        => new(PathTokenKind.Parameter, name, customPattern, optional, repeatable);
}
