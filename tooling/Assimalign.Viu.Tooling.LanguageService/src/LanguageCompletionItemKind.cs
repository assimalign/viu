namespace Assimalign.Viu.Tooling.LanguageService;

/// <summary>The semantic kind of a language completion item.</summary>
/// <remarks>
/// The values are the Language Server Protocol's <c>CompletionItemKind</c> numbers — the protocol is
/// a documented external compatibility target, and the server host serializes this enum's value
/// verbatim, so an editor picks the glyph for a Viu completion exactly as it does for any other
/// language. Only the kinds Viu actually produces are declared; the numbering is the protocol's and
/// is therefore not contiguous.
/// <see href="https://microsoft.github.io/language-server-protocol/specifications/lsp/3.17/specification/#completionItemKind">
/// Language Server Protocol 3.17, <c>CompletionItemKind</c></see>.
/// </remarks>
public enum LanguageCompletionItemKind
{
    /// <summary>Plain text.</summary>
    Text = 1,

    /// <summary>A method.</summary>
    Method = 2,

    /// <summary>A function.</summary>
    Function = 3,

    /// <summary>A field.</summary>
    Field = 5,

    /// <summary>A class or component type.</summary>
    Class = 7,

    /// <summary>
    /// A namespace. The protocol has no namespace kind of its own, and <c>Module</c> is the value
    /// editors render with the namespace glyph — Visual Studio draws it as <c>{}</c>, the same glyph
    /// a C# project's namespace completions carry.
    /// </summary>
    Module = 9,

    /// <summary>A property.</summary>
    Property = 10,

    /// <summary>A language keyword.</summary>
    Keyword = 14,

    /// <summary>An editor snippet.</summary>
    Snippet = 15,
}
