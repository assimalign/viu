using System;
using System.Collections.Generic;
using System.Text;

namespace Assimalign.Vue.Syntax.Generators;

/// <summary>
/// Derives the deterministic, trimming-safe C# names for a generated component from its <c>.viu</c>
/// file path: the containing namespace (root namespace plus the file's directory segments), the class
/// name (the file name), and the Roslyn <c>AddSource</c> hint name (path-qualified so two same-named
/// files in different folders never collide). Uses only string operations — no <c>System.IO</c> — so it
/// stays inside the analyzer API surface (RS1035) and produces identical output on every platform.
/// </summary>
internal static class SingleFileComponentNameResolver
{
    private const string Extension = ".viu";

    /// <summary>
    /// Resolves the namespace, class name, and hint name for <paramref name="filePath"/>.
    /// </summary>
    /// <param name="filePath">The absolute <c>.viu</c> file path.</param>
    /// <param name="projectDirectory">The consuming project's directory, or <see langword="null"/> when unknown.</param>
    /// <param name="rootNamespace">The consuming project's root namespace, or <see langword="null"/> when unknown.</param>
    /// <returns>The resolved names.</returns>
    public static SingleFileComponentName Resolve(string filePath, string? projectDirectory, string? rootNamespace)
    {
        var normalizedPath = filePath.Replace('\\', '/');

        var lastSlash = normalizedPath.LastIndexOf('/');
        var fileName = lastSlash >= 0 ? normalizedPath.Substring(lastSlash + 1) : normalizedPath;
        var baseName = StripExtension(fileName);
        var className = EscapeKeyword(Sanitize(baseName));

        var relativeDirectory = ResolveRelativeDirectory(normalizedPath, projectDirectory);

        var namespaceValue = BuildNamespace(rootNamespace, relativeDirectory);
        var hintName = BuildHintName(relativeDirectory, baseName, normalizedPath);

        return new SingleFileComponentName(namespaceValue, className, hintName);
    }

    // Returns null (not empty) when the directory is unknown or the file sits outside it, so hint
    // naming can tell "project root" (safe, unique) apart from "location unknown" (needs the path-hash
    // disambiguator - two linked files with the same leaf name must not collide in AddSource).
    private static string? ResolveRelativeDirectory(string normalizedPath, string? projectDirectory)
    {
        if (string.IsNullOrEmpty(projectDirectory))
        {
            return null;
        }

        var normalizedDirectory = projectDirectory!.Replace('\\', '/').TrimEnd('/');
        var prefix = normalizedDirectory + "/";
        if (!normalizedPath.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var relative = normalizedPath.Substring(prefix.Length);
        var lastSlash = relative.LastIndexOf('/');
        return lastSlash >= 0 ? relative.Substring(0, lastSlash) : string.Empty;
    }

    private static string? BuildNamespace(string? rootNamespace, string? relativeDirectory)
    {
        var parts = new List<string>();
        if (!string.IsNullOrEmpty(rootNamespace))
        {
            // The root namespace is already a valid namespace (possibly dotted); keep it verbatim.
            parts.Add(rootNamespace!);
        }

        foreach (var segment in (relativeDirectory ?? string.Empty).Split('/'))
        {
            if (segment.Length > 0)
            {
                parts.Add(EscapeKeyword(Sanitize(segment)));
            }
        }

        return parts.Count == 0 ? null : string.Join(".", parts);
    }

    private static string BuildHintName(string? relativeDirectory, string baseName, string normalizedPath)
    {
        // Roslyn's AddSource throws on a duplicate hint name and the exception kills the entire
        // generator run, so hint names must be unique BY CONSTRUCTION: whenever the relative directory
        // is unknown (linked/out-of-project files) or sanitizing was lossy (distinct names collapsing
        // to one identifier, e.g. Foo-Bar and Foo_Bar), a short stable hash of the full normalized path
        // disambiguates. Files properly under the project with clean names keep readable hints.
        var lossy = false;
        var builder = new StringBuilder();
        foreach (var segment in (relativeDirectory ?? string.Empty).Split('/'))
        {
            if (segment.Length > 0)
            {
                builder.Append(SanitizeTracked(segment, ref lossy)).Append('.');
            }
        }

        builder.Append(SanitizeTracked(baseName, ref lossy));
        if (relativeDirectory is null || lossy)
        {
            builder.Append('.').Append(HashPath(normalizedPath));
        }

        builder.Append(".SingleFileComponent.g.cs");
        return builder.ToString();
    }

    private static string SanitizeTracked(string candidate, ref bool lossy)
    {
        var sanitized = Sanitize(candidate);
        lossy |= !string.Equals(sanitized, candidate, StringComparison.Ordinal);
        return sanitized;
    }

    // FNV-1a over the normalized path: deterministic, culture-free, stable across runs and machines -
    // the incremental-caching contract requires the hint name to be a pure function of the inputs.
    private static string HashPath(string normalizedPath)
    {
        unchecked
        {
            var hash = 2166136261u;
            foreach (var character in normalizedPath)
            {
                hash = (hash ^ character) * 16777619u;
            }

            return hash.ToString("x8", System.Globalization.CultureInfo.InvariantCulture);
        }
    }

    // Generated namespaces and class names must survive C# keywords ("class.viu" emits "@class"); the
    // fully-qualified reference avoids pulling the whole CSharp namespace into scope for one check.
    private static string EscapeKeyword(string identifier)
        => Microsoft.CodeAnalysis.CSharp.SyntaxFacts.GetKeywordKind(identifier) == Microsoft.CodeAnalysis.CSharp.SyntaxKind.None
            ? identifier
            : "@" + identifier;

    private static string StripExtension(string fileName)
        => fileName.EndsWith(Extension, StringComparison.OrdinalIgnoreCase)
            ? fileName.Substring(0, fileName.Length - Extension.Length)
            : fileName;

    private static string Sanitize(string candidate)
    {
        if (candidate.Length == 0)
        {
            return "_";
        }

        var builder = new StringBuilder(candidate.Length);
        foreach (var character in candidate)
        {
            builder.Append(char.IsLetterOrDigit(character) || character == '_' ? character : '_');
        }

        if (char.IsDigit(builder[0]))
        {
            builder.Insert(0, '_');
        }

        return builder.ToString();
    }
}

/// <summary>The resolved names for a generated component: its namespace (or <see langword="null"/>), class, and hint name.</summary>
/// <param name="Namespace">The containing namespace, or <see langword="null"/> for the global namespace.</param>
/// <param name="ClassName">The generated partial class name.</param>
/// <param name="HintName">The stable <c>AddSource</c> hint name, unique by construction (a path hash disambiguates out-of-project files and lossy sanitizations).</param>
internal readonly record struct SingleFileComponentName(string? Namespace, string ClassName, string HintName);
