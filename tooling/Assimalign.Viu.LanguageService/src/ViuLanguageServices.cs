namespace Assimalign.Viu.LanguageService;

/// <summary>Creates editor-neutral Viu language-service workspaces.</summary>
public static class ViuLanguageServices
{
    /// <summary>Creates an isolated workspace for one editor client.</summary>
    /// <returns>A new language service.</returns>
    public static IViuLanguageService Create() => new ViuLanguageService();
}
