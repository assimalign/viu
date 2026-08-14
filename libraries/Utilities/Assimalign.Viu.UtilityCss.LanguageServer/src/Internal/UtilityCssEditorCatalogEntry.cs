namespace Assimalign.Viu.UtilityCss.LanguageServer;

internal sealed record UtilityCssEditorCatalogEntry(
    string CandidateText,
    string Css,
    string? ColorValue,
    int Index);
