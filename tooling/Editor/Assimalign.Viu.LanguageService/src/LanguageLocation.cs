namespace Assimalign.Viu.LanguageService;

/// <summary>A range in a named file, as a definition or reference result reports it.</summary>
/// <param name="FilePath">
/// The absolute path of the file the range belongs to. A definition may leave the document it was
/// asked from — a component's member is declared in that component's own file — so the path travels
/// with the range rather than being assumed.
/// </param>
/// <param name="Range">
/// The zero-based authored range of the declaration's name. Positions are always the author's, never
/// the generated document's: a definition the editor cannot navigate to in the file the author wrote
/// is not a definition it can offer.
/// </param>
public readonly record struct LanguageLocation(string FilePath, LanguageRange Range);
