namespace Assimalign.Viu.LanguageService;

/// <summary>One validated build-contributed class entry.</summary>
internal sealed record ClassCatalogEntry(
    string ClassName,
    string Css,
    string? ColorValue,
    string? SortText,
    int Order);
