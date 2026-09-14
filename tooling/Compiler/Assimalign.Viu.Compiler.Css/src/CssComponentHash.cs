using System.Globalization;

namespace Assimalign.Viu.Compiler.Css;

/// <summary>
/// Resolves the deterministic project-relative path hash used to salt CSS Modules and
/// <c>v-bind()</c> names. The eight hexadecimal digits remain stable across content edits,
/// rebuilds, and machines. No element attribute or selector scope condition is derived from this value.
/// Specified by <c>[STY-2]</c> and retained by <c>[V01.01.06.17]</c>.
/// </summary>
public static class CssComponentHash
{
    /// <summary>Hashes the normalized project-relative path, falling back to the leaf name for linked files.</summary>
    /// <param name="filePath">The single-file-component path.</param>
    /// <param name="projectDirectory">The consuming project's directory, or <see langword="null"/> when unknown.</param>
    /// <returns>The eight-digit hexadecimal salt for component-local CSS names.</returns>
    public static string Resolve(string filePath, string? projectDirectory)
        => Hash(RelativePath(filePath, projectDirectory));

    private static string RelativePath(string filePath, string? projectDirectory)
    {
        var normalizedPath = filePath.Replace('\\', '/');

        if (!string.IsNullOrEmpty(projectDirectory))
        {
            var normalizedDirectory = projectDirectory!.Replace('\\', '/').TrimEnd('/');
            var prefix = normalizedDirectory + "/";
            if (normalizedPath.StartsWith(
                    prefix,
                    SingleFileComponentPathComparison.Comparison))
            {
                return normalizedPath.Substring(prefix.Length);
            }
        }

        // The location is unknown or outside the project; hash the leaf name so the id stays machine-stable.
        var lastSlash = normalizedPath.LastIndexOf('/');
        return lastSlash >= 0 ? normalizedPath.Substring(lastSlash + 1) : normalizedPath;
    }

    // FNV-1a over the relative path: deterministic, culture-free, stable across runs and machines.
    private static string Hash(string value)
    {
        unchecked
        {
            var hash = 2166136261u;
            foreach (var character in value)
            {
                hash = (hash ^ character) * 16777619u;
            }

            return hash.ToString("x8", CultureInfo.InvariantCulture);
        }
    }
}
