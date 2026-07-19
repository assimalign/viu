using System;
using System.Globalization;

namespace Assimalign.Viu.Tooling.Css;

/// <summary>
/// Derives a component's scoped-CSS scope id — the <c>data-v-&lt;hash&gt;</c> attribute the renderer stamps
/// on the component's elements and the scoped rewrite appends to selectors ([V01.01.06.04]). The hash is a
/// deterministic FNV-1a over the component's <b>project-relative</b> <c>.viu</c> path (normalized to
/// forward slashes), mirroring Vue's dev-mode scheme of hashing the short file path
/// (<c>@vitejs/plugin-vue</c>): stable across machines and rebuilds for asset caching, and unique per
/// component file. String-only (no <c>System.IO</c>), so it stays inside the analyzer API surface (RS1035)
/// and produces identical output on every platform.
/// </summary>
/// <remarks>
/// Lives in the Tooling core because both build-time hosts need the identical id ([V01.01.12.12]): the
/// generator resolves it to emit the <c>ScopeId</c> constant and to salt the module/v-bind hashes, and the
/// <c>ViuBundleCss</c> task resolves it to reproduce the same scoped CSS byte-for-byte. A path-based hash
/// intentionally does not change when only the file's <em>content</em> changes — the scope id identifies the
/// component, not a content revision. This matches Vue's non-production hashing (production additionally
/// folds in the source for cache-busting); folding content in is a later optimization, tracked with the
/// static-web-asset emission. When the file sits outside the project directory (a linked file whose relative
/// path is unknown), the leaf file name is hashed instead so the id stays machine-independent.
/// </remarks>
public static class StyleScopeId
{
    private const string Prefix = "data-v-";

    /// <summary>Resolves the <c>data-v-&lt;hash&gt;</c> scope id for <paramref name="filePath"/>.</summary>
    /// <param name="filePath">The <c>.viu</c> file path.</param>
    /// <param name="projectDirectory">The consuming project's directory, or <see langword="null"/> when unknown.</param>
    /// <returns>The scope id (e.g. <c>data-v-7ba5bd90</c>).</returns>
    public static string Resolve(string filePath, string? projectDirectory)
        => Prefix + Hash(RelativePath(filePath, projectDirectory));

    private static string RelativePath(string filePath, string? projectDirectory)
    {
        var normalizedPath = filePath.Replace('\\', '/');

        if (!string.IsNullOrEmpty(projectDirectory))
        {
            var normalizedDirectory = projectDirectory!.Replace('\\', '/').TrimEnd('/');
            var prefix = normalizedDirectory + "/";
            if (normalizedPath.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
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
