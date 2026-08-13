namespace Assimalign.Viu.LanguageService;

/// <summary>
/// Describes the class token owned by a template caret, using absolute document offsets.
/// </summary>
internal sealed record TemplateClassTokenContext(
    int TokenStart,
    int TokenEnd,
    string TokenText,
    string Prefix);
