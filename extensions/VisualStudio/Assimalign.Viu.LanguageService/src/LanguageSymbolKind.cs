namespace Assimalign.Viu.LanguageService;

/// <summary>
/// The semantic kind of a document symbol. Values are pinned to the Language Server Protocol's
/// <c>SymbolKind</c> numbering so protocol hosts can cast directly, matching
/// <see cref="LanguageCompletionItemKind"/>.
/// </summary>
public enum LanguageSymbolKind
{
    /// <summary>A module-like container, such as a template or style block.</summary>
    Module = 2,

    /// <summary>A class-like container, such as a script block's component class body.</summary>
    Class = 5,

    /// <summary>A property declared inside a container.</summary>
    Property = 7,

    /// <summary>A function or method declared inside a container.</summary>
    Function = 12,
}
