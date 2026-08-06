using System;
using System.Globalization;

namespace Assimalign.Viu.Compiler.Css;

/// <summary>
/// Derives a component's scoped-CSS scope id — the <c>data-v-&lt;hash&gt;</c> attribute the renderer stamps
/// on the component's elements and the scoped rewrite appends to selectors ([V01.01.06.04]). The hash is a
/// deterministic FNV-1a over the component's <b>project-relative</b> <c>.viu</c> or <c>.vue</c> path
/// (normalized to forward slashes). Hashing the short relative path — rather than an absolute path or the
/// file's content — is what makes the id stable across machines and rebuilds, which asset caching depends
/// on, while staying unique per component file. String-only (no <c>System.IO</c>), so it stays inside the
/// analyzer API surface (RS1035).
/// Project containment follows the host operating system: ordinal-ignore-case on Windows and ordinal
/// elsewhere.
/// </summary>
/// <remarks>
/// Lives in the Tooling core because both build-time hosts need the identical id ([V01.01.12.12]): the
/// generator resolves it to emit the <c>ScopeId</c> constant and to salt the module/v-bind hashes, and the
/// <c>ViuBundleCss</c> task resolves it to reproduce the same scoped CSS byte-for-byte. A path-based hash
/// intentionally does not change when only the file's <em>content</em> changes — the scope id identifies the
/// component, not a content revision, so an edit does not invalidate every cached asset that mentions it.
/// Folding content in for release-mode cache-busting is a later optimization, tracked with the
/// static-web-asset emission. When the file sits outside the project directory (a linked file whose relative
/// path is unknown), the leaf file name is hashed instead so the id stays machine-independent.
/// </remarks>
public static class StyleScopeId
{
    private const string Prefix = "data-v-";

    /// <summary>Resolves the <c>data-v-&lt;hash&gt;</c> scope id for <paramref name="filePath"/>.</summary>
    /// <param name="filePath">The single-file-component path.</param>
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
