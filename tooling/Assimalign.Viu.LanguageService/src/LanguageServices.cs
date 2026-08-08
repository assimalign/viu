namespace Assimalign.Viu.LanguageService;

/// <summary>Creates editor-neutral Viu language-service workspaces.</summary>
public static class LanguageServices
{
    /// <summary>Creates an isolated workspace for one editor client.</summary>
    /// <returns>A new language service.</returns>
    public static ILanguageService Create() => new ViuLanguageService();
}
