namespace Assimalign.Viu.Syntax.Templates;

/// <summary>
/// Selects how the tokenizer treats special elements — which tags hold raw text or RCDATA rather than
/// markup. The mode is fixed for a whole parse; it is not inferred per element.
/// </summary>
public enum TemplateParseMode
{
    /// <summary>
    /// Platform-agnostic: every tag is treated the same, with no raw-text/RCDATA special casing.
    /// </summary>
    Base = 0,

    /// <summary>
    /// HTML: adds the special parsing behaviour for <c>&lt;script&gt;</c>/<c>&lt;style&gt;</c> (raw text)
    /// and <c>&lt;title&gt;</c>/<c>&lt;textarea&gt;</c> (RCDATA).
    /// </summary>
    Html = 1,

    /// <summary>
    /// Single-file-component: content of all root-level tags except <c>&lt;template&gt;</c> is raw text.
    /// </summary>
    SingleFileComponent = 2,
}
