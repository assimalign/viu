namespace Assimalign.Viu.Compiler.Css;

/// <summary>
/// Resolves the stable scope attribute shared by selectors and generated element bindings.
/// The project-relative path hash is identical to the CSS Modules salt. Specified by <c>[STY-1]</c>.
/// </summary>
public static class StyleScopeId
{
    /// <summary>Returns the component's deterministic <c>data-v-</c> attribute name.</summary>
    /// <param name="filePath">The component path.</param>
    /// <param name="projectDirectory">The containing project directory, when known.</param>
    /// <returns>The scope attribute, stable across machines and content edits.</returns>
    public static string Resolve(string filePath, string? projectDirectory)
        => "data-v-" + CssComponentHash.Resolve(filePath, projectDirectory);
}
