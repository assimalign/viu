namespace Assimalign.Viu.Router;

/// <summary>
/// A single token within one path segment — either literal text or a dynamic parameter. The C#
/// port of vue-router's <c>Token</c> union (<c>TokenStatic</c> / <c>TokenParam</c>) from
/// <c>packages/router/src/matcher/pathTokenizer.ts</c>.
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
    /// <see cref="PathTokenKind.Parameter"/> token, the parameter name (upstream <c>value</c>).
    /// </summary>
    public string Value { get; }

    /// <summary>
    /// The user-supplied custom pattern for a parameter (the <c>\d+</c> in <c>:id(\d+)</c>), or the
    /// empty string when none was supplied (upstream <c>regexp</c>). Only meaningful for
    /// <see cref="PathTokenKind.Parameter"/> tokens.
    /// </summary>
    public string CustomPattern { get; }

    /// <summary>Whether the parameter is optional — the <c>?</c> or <c>*</c> modifier (upstream <c>optional</c>).</summary>
    public bool Optional { get; }

    /// <summary>Whether the parameter is repeatable — the <c>+</c> or <c>*</c> modifier (upstream <c>repeatable</c>).</summary>
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
