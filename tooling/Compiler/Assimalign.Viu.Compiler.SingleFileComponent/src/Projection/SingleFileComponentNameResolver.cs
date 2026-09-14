using System;
using System.Collections.Generic;
using System.Text;

using Assimalign.Viu.Compiler.Css;

namespace Assimalign.Viu.Compiler.SingleFileComponent;

/// <summary>
/// Derives the deterministic, trimming-safe C# names for a generated component from its <c>.viu</c> or
/// <c>.vue</c>
/// file path: the containing namespace (root namespace plus the file's directory segments), the class
/// name (the file name), and the Roslyn <c>AddSource</c> hint name (path-qualified so two same-named
/// files in different folders never collide). Uses only string operations — no <c>System.IO</c> — so it
/// stays inside the analyzer API surface (RS1035). Path containment follows the host operating system:
/// ordinal-ignore-case on Windows and ordinal elsewhere.
/// <para>
/// Hint-name identity is specified by <c>[SFC-CG-5]</c>. Roslyn compares hint names
/// case-insensitively, so a readable hint name is unique only while no other component in the same
/// compilation resolves to one that differs from it by case alone; <see cref="SelectCaseCollidingPaths"/>
/// selects those components and <see cref="Resolve(string, string?, string?, bool)"/> gives each of them
/// the path-hash discriminator ([V01.01.06.10.01]).
/// </para>
/// <para>
/// Generated type identity is specified by <c>[SFC-CG-10]</c>. A component whose original type name
/// equals a namespace prefix emitted by another component moves into a <c>GeneratedComponents</c>
/// child namespace. <see cref="SelectNamespaceCollidingPaths"/> selects only those components;
/// unaffected types, class names, registration names, and hint names retain their existing identities.
/// </para>
/// </summary>
public static class SingleFileComponentNameResolver
{
    private const string ViuExtension = ".viu";
    private const string VueExtension = ".vue";
    private const string GeneratedNamespaceSegment = "GeneratedComponents";

    /// <summary>
    /// Resolves the namespace, class name, and hint name for <paramref name="filePath"/>, taking the
    /// readable hint name (no case discriminator).
    /// </summary>
    /// <param name="filePath">The absolute <c>.viu</c> or <c>.vue</c> file path.</param>
    /// <param name="projectDirectory">The consuming project's directory, or <see langword="null"/> when unknown.</param>
    /// <param name="rootNamespace">The consuming project's root namespace, or <see langword="null"/> when unknown.</param>
    /// <returns>The resolved names.</returns>
    public static SingleFileComponentName Resolve(string filePath, string? projectDirectory, string? rootNamespace)
        => Resolve(filePath, projectDirectory, rootNamespace, requiresCaseDiscriminator: false);

    /// <summary>
    /// Resolves the namespace, class name, and hint name for <paramref name="filePath"/>.
    /// </summary>
    /// <param name="filePath">The absolute <c>.viu</c> or <c>.vue</c> file path.</param>
    /// <param name="projectDirectory">The consuming project's directory, or <see langword="null"/> when unknown.</param>
    /// <param name="rootNamespace">The consuming project's root namespace, or <see langword="null"/> when unknown.</param>
    /// <param name="requiresCaseDiscriminator">
    /// <see langword="true"/> when another component in the same compilation resolves to a hint name
    /// that differs from this one only by case, as reported by <see cref="SelectCaseCollidingPaths"/>.
    /// The namespace and class name never depend on it — only the hint name does.
    /// </param>
    /// <returns>The resolved names.</returns>
    public static SingleFileComponentName Resolve(
        string filePath,
        string? projectDirectory,
        string? rootNamespace,
        bool requiresCaseDiscriminator)
        => Resolve(filePath, projectDirectory, rootNamespace, requiresCaseDiscriminator, requiresNamespaceDiscriminator: false);

    /// <summary>
    /// Resolves generated type and hint names using the compilation's collision selections. Only a
    /// type selected by <see cref="SelectNamespaceCollidingPaths"/> acquires the
    /// <c>GeneratedComponents</c> child namespace; its class and registration names remain unchanged
    /// (<c>[SFC-CG-10]</c>). Namespace selection never changes the hint name (<c>[SFC-CG-5]</c>).
    /// </summary>
    /// <param name="filePath">The absolute <c>.viu</c> or <c>.vue</c> file path.</param>
    /// <param name="projectDirectory">The consuming project's directory, or <see langword="null"/> when unknown.</param>
    /// <param name="rootNamespace">The consuming project's root namespace, or <see langword="null"/> when unknown.</param>
    /// <param name="requiresCaseDiscriminator">Whether <see cref="SelectCaseCollidingPaths"/> selected this file for a hint-name discriminator.</param>
    /// <param name="requiresNamespaceDiscriminator">Whether <see cref="SelectNamespaceCollidingPaths"/> selected this file for the generated child namespace.</param>
    /// <returns>The resolved names, derived entirely from the supplied strings and collision selections.</returns>
    public static SingleFileComponentName Resolve(
        string filePath,
        string? projectDirectory,
        string? rootNamespace,
        bool requiresCaseDiscriminator,
        bool requiresNamespaceDiscriminator)
    {
        var normalizedPath = filePath.Replace('\\', '/');

        var lastSlash = normalizedPath.LastIndexOf('/');
        var fileName = lastSlash >= 0 ? normalizedPath.Substring(lastSlash + 1) : normalizedPath;
        var baseName = StripExtension(fileName);
        var className = EscapeKeyword(Sanitize(baseName));

        var relativeDirectory = ResolveRelativeDirectory(normalizedPath, projectDirectory);

        var namespaceValue = BuildNamespace(rootNamespace, relativeDirectory);
        var hintName = BuildHintName(relativeDirectory, baseName, normalizedPath, requiresCaseDiscriminator);

        if (requiresNamespaceDiscriminator)
        {
            namespaceValue = string.IsNullOrEmpty(namespaceValue)
                ? GeneratedNamespaceSegment
                : namespaceValue + "." + GeneratedNamespaceSegment;
        }

        return new SingleFileComponentName(namespaceValue, className, hintName);
    }

    /// <summary>
    /// Selects, out of every component a single compilation emits, the ones whose readable hint names
    /// are equal ignoring case — the set Roslyn's <c>AddSource</c> would reject as duplicates, because it
    /// compares hint names with <see cref="StringComparison.OrdinalIgnoreCase"/>. Only members of such a
    /// colliding group take the discriminator; every other component keeps its readable hint name
    /// verbatim, so this rule never churns an existing generated-file identity ([V01.01.06.10.01],
    /// <c>[SFC-CG-5]</c>).
    /// </summary>
    /// <param name="componentPaths">
    /// The paths of the components the compilation actually emits — a shadowed <c>.vue</c> peer
    /// ([VUE-7]) emits nothing and must not be offered here, or it would discriminate the canonical
    /// <c>.viu</c> component that suppressed it.
    /// </param>
    /// <param name="projectDirectory">The consuming project's directory, or <see langword="null"/> when unknown.</param>
    /// <returns>
    /// The colliding paths, ordinally sorted so the result is a pure function of the input set and never
    /// of the order MSBuild presented the files in. Empty when no two components collide.
    /// </returns>
    public static string[] SelectCaseCollidingPaths(
        IReadOnlyList<string> componentPaths,
        string? projectDirectory)
    {
        if (componentPaths.Count < 2)
        {
            return Array.Empty<string>();
        }

        var groups = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
        foreach (var path in componentPaths)
        {
            var hintName = Resolve(path, projectDirectory, rootNamespace: null).HintName;
            if (!groups.TryGetValue(hintName, out var members))
            {
                members = new List<string>();
                groups.Add(hintName, members);
            }

            members.Add(path);
        }

        List<string>? colliding = null;
        foreach (var group in groups)
        {
            if (group.Value.Count > 1)
            {
                colliding ??= new List<string>();
                colliding.AddRange(group.Value);
            }
        }

        if (colliding is null)
        {
            return Array.Empty<string>();
        }

        var result = colliding.ToArray();
        Array.Sort(result, StringComparer.Ordinal);
        return result;
    }

    /// <summary>
    /// Selects components whose original fully qualified type names equal a generated namespace
    /// prefix. C# identity comparison is ordinal and ignores verbatim-identifier escapes; directory
    /// sanitization uses the same rule as emission. Only these components move under
    /// <c>GeneratedComponents</c>, preserving every previously noncolliding type (<c>[SFC-CG-10]</c>).
    /// No directory is inspected: only the supplied emitted component paths contribute namespaces.
    /// </summary>
    /// <param name="componentPaths">All paths the compilation emits, excluding shadowed <c>.vue</c> peers under <c>[VUE-7]</c>.</param>
    /// <param name="projectDirectory">The consuming project's directory, or <see langword="null"/> when unknown.</param>
    /// <param name="rootNamespace">The consuming project's root namespace, or <see langword="null"/> when unknown.</param>
    /// <returns>The selected paths in ordinal order, independent of input enumeration order; empty when no type conflicts with a namespace.</returns>
    public static string[] SelectNamespaceCollidingPaths(
        IReadOnlyList<string> componentPaths,
        string? projectDirectory,
        string? rootNamespace)
    {
        var namespacePrefixes = new HashSet<string>(StringComparer.Ordinal);
        var names = new SingleFileComponentName[componentPaths.Count];
        for (var index = 0; index < componentPaths.Count; index++)
        {
            names[index] = Resolve(componentPaths[index], projectDirectory, rootNamespace);
            foreach (var namespacePrefix in EnumerateNamespacePrefixes(names[index].Namespace))
            {
                namespacePrefixes.Add(namespacePrefix);
            }
        }

        var selected = new HashSet<string>(StringComparer.Ordinal);
        for (var index = 0; index < componentPaths.Count; index++)
        {
            if (namespacePrefixes.Contains(GetTypeIdentity(names[index])))
            {
                selected.Add(componentPaths[index]);
            }
        }

        return SortPaths(selected);
    }

    /// <summary>
    /// Selects every participant in a generated type collision that remains after applying
    /// <see cref="SelectNamespaceCollidingPaths"/>. This includes duplicate sanitized type names and
    /// types conflicting with any final namespace prefix, including the fixed
    /// <c>GeneratedComponents</c> segment. Hosts report a located error and suppress these components
    /// instead of emitting conflicting C# declarations (<c>[SFC-CG-10]</c>).
    /// </summary>
    /// <param name="componentPaths">All paths the compilation emits, excluding shadowed <c>.vue</c> peers under <c>[VUE-7]</c>.</param>
    /// <param name="projectDirectory">The consuming project's directory, or <see langword="null"/> when unknown.</param>
    /// <param name="rootNamespace">The consuming project's root namespace, or <see langword="null"/> when unknown.</param>
    /// <returns>Every contributing path in ordinal order, independent of input enumeration order; empty when final generated identities are distinct.</returns>
    public static string[] SelectIdentityCollidingPaths(
        IReadOnlyList<string> componentPaths,
        string? projectDirectory,
        string? rootNamespace)
    {
        var namespaceCollidingPaths = new HashSet<string>(
            SelectNamespaceCollidingPaths(componentPaths, projectDirectory, rootNamespace),
            StringComparer.Ordinal);
        var typePaths = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        var namespacePaths = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        foreach (var path in componentPaths)
        {
            var names = Resolve(
                path,
                projectDirectory,
                rootNamespace,
                requiresCaseDiscriminator: false,
                requiresNamespaceDiscriminator: namespaceCollidingPaths.Contains(path));
            AddIdentityPath(typePaths, GetTypeIdentity(names), path);
            foreach (var namespacePrefix in EnumerateNamespacePrefixes(names.Namespace))
            {
                AddIdentityPath(namespacePaths, namespacePrefix, path);
            }
        }

        var selected = new HashSet<string>(StringComparer.Ordinal);
        foreach (var type in typePaths)
        {
            if (type.Value.Count > 1)
            {
                selected.UnionWith(type.Value);
            }

            if (namespacePaths.TryGetValue(type.Key, out var contributors))
            {
                selected.UnionWith(type.Value);
                selected.UnionWith(contributors);
            }
        }

        return SortPaths(selected);
    }

    private static string GetTypeIdentity(SingleFileComponentName names)
        => (string.IsNullOrEmpty(names.Namespace)
            ? names.ClassName
            : names.Namespace + "." + names.ClassName).Replace("@", string.Empty);

    private static IEnumerable<string> EnumerateNamespacePrefixes(string? namespaceValue)
    {
        if (string.IsNullOrEmpty(namespaceValue))
        {
            yield break;
        }

        var identity = namespaceValue!.Replace("@", string.Empty);
        var length = identity.Length;
        while (length > 0)
        {
            yield return identity.Substring(0, length);
            length = identity.LastIndexOf('.', length - 1);
        }
    }

    private static void AddIdentityPath(Dictionary<string, List<string>> identities, string identity, string path)
    {
        if (!identities.TryGetValue(identity, out var paths))
        {
            paths = new List<string>();
            identities.Add(identity, paths);
        }

        paths.Add(path);
    }

    private static string[] SortPaths(HashSet<string> paths)
    {
        if (paths.Count == 0)
        {
            return Array.Empty<string>();
        }

        var result = new string[paths.Count];
        paths.CopyTo(result);
        Array.Sort(result, StringComparer.Ordinal);
        return result;
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
        if (!normalizedPath.StartsWith(
                prefix,
                SingleFileComponentPathComparison.Comparison))
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

    private static string BuildHintName(
        string? relativeDirectory,
        string baseName,
        string normalizedPath,
        bool requiresCaseDiscriminator)
    {
        // Roslyn's AddSource throws on a duplicate hint name and the exception kills the entire
        // generator run, so hint names must be unique BY CONSTRUCTION: whenever the relative directory
        // is unknown (linked/out-of-project files), sanitizing was lossy (distinct names collapsing
        // to one identifier, e.g. Foo-Bar and Foo_Bar), or a sibling component's hint name differs from
        // this one only by case (Roslyn compares hint names case-INSENSITIVELY, so Choice and choice are
        // one name to it), a short stable hash of the full normalized path disambiguates. Because that
        // hash reads only this file's own exact-cased path, the discriminated name is the same on every
        // build and in any file order. Files properly under the project with clean, non-colliding names
        // keep readable hints.
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
        if (relativeDirectory is null || lossy || requiresCaseDiscriminator)
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
    {
        if (fileName.EndsWith(ViuExtension, StringComparison.OrdinalIgnoreCase))
        {
            return fileName.Substring(0, fileName.Length - ViuExtension.Length);
        }

        return fileName.EndsWith(VueExtension, StringComparison.OrdinalIgnoreCase)
            ? fileName.Substring(0, fileName.Length - VueExtension.Length)
            : fileName;
    }

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
