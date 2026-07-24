namespace Assimalign.Viu.LanguageService;

/// <summary>The semantic kind of a language completion item.</summary>
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

    /// <summary>A property.</summary>
    Property = 10,

    /// <summary>A language keyword.</summary>
    Keyword = 14,

    /// <summary>An editor snippet.</summary>
    Snippet = 15,
}
