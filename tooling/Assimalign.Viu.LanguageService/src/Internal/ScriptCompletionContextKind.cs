namespace Assimalign.Viu.LanguageService;

/// <summary>
/// The syntactic context a completion request falls in inside an <c>@script</c> block. Each kind
/// selects which completion sources apply, so a source is never offered where the C# grammar makes
/// it illegal.
/// </summary>
internal enum ScriptCompletionContextKind
{
    /// <summary>
    /// A position where an identifier is legal — a class-body member declaration, a method body, or
    /// a field initializer. Every source applies: the semantic symbol lookup (which honors the
    /// position's own accessibility, so the component's private members complete inside its own
    /// class body), the Viu scaffold catalog, and the C# keyword catalog.
    /// </summary>
    Expression,

    /// <summary>
    /// A position inside a <c>using</c> directive's namespace-or-type name, after the <c>using</c>
    /// keyword and before the terminating semicolon. Only namespaces and types are legal there, so
    /// members, the Viu scaffold catalog, and the keyword catalog are all suppressed.
    /// </summary>
    UsingDirective,
}
