using System;
using System.Collections.Generic;
using System.Threading;

using Assimalign.Viu.Compiler.Css;
using Assimalign.Viu.Compiler.SingleFileComponent;

namespace Assimalign.Viu.LanguageService;

/// <summary>
/// Projects one live language-service document through the same single-file-component pipeline the
/// build uses. The host-fed project context supplies the build's namespace and path inputs when it is
/// available; a loose document still receives deterministic parser, template, script, and style
/// diagnostics through the context-free name resolver ([V01.01.12.07.15], #336).
/// </summary>
internal static class LanguageDocumentProjection
{
    /// <summary>Projects <paramref name="document"/> into the shared host-neutral model.</summary>
    /// <param name="document">The immutable live document snapshot.</param>
    /// <param name="context">The resolved project context, or <see langword="null"/> for a loose file.</param>
    /// <param name="cancellationToken">The token cancelling the projection.</param>
    /// <returns>The build-identical projection result.</returns>
    internal static SingleFileComponentProjectionResult Project(
        LanguageDocument document,
        LanguageProjectContext? context,
        CancellationToken cancellationToken)
    {
        var filePath = GetFilePath(document.DocumentUri);
        var input = CreateInput(
            document.Syntax.Format == LanguageDocumentFormat.Vue
                ? SingleFileComponentFormat.Vue
                : SingleFileComponentFormat.Viu,
            filePath,
            document.Text,
            context);
        return SingleFileComponentProjection.Project(input, cancellationToken);
    }

    internal static SingleFileComponentProjectionInput CreateInput(
        SingleFileComponentFormat format,
        string filePath,
        string text,
        LanguageProjectContext? context)
    {
        var componentPaths = GetComponentPaths(filePath, context);
        var identityPath = ResolveIdentityPath(filePath, componentPaths);
        var canonicalBasePaths = new HashSet<string>(SingleFileComponentPathComparison.Comparer);
        foreach (var path in componentPaths)
        {
            if (path.EndsWith(".viu", StringComparison.OrdinalIgnoreCase))
            {
                canonicalBasePaths.Add(GetComponentBasePath(path));
            }
        }

        var emittedPaths = new List<string>(componentPaths.Length);
        foreach (var path in componentPaths)
        {
            if (!HasCanonicalPeer(path, canonicalBasePaths))
            {
                emittedPaths.Add(path);
            }
        }

        var caseCollisions = SingleFileComponentNameResolver.SelectCaseCollidingPaths(
            emittedPaths, context?.ProjectDirectory);
        var namespaceCollisions = SingleFileComponentNameResolver.SelectNamespaceCollidingPaths(
            emittedPaths, context?.ProjectDirectory, context?.RootNamespace);
        var identityCollisions = SingleFileComponentNameResolver.SelectIdentityCollidingPaths(
            emittedPaths, context?.ProjectDirectory, context?.RootNamespace);
        var name = SingleFileComponentNameResolver.Resolve(
            identityPath,
            context?.ProjectDirectory,
            context?.RootNamespace,
            ContainsPath(caseCollisions, identityPath),
            ContainsPath(namespaceCollisions, identityPath));
        return new SingleFileComponentProjectionInput(
            format,
            filePath,
            GetLeafFileName(filePath),
            text,
            name.Namespace,
            name.ClassName,
            name.HintName,
            CssComponentHash.Resolve(identityPath, context?.ProjectDirectory),
            HotReloadComponentIdentifier: null,
            HasCanonicalPeer: HasCanonicalPeer(filePath, canonicalBasePaths))
        {
            HasIdentityCollision = ContainsPath(identityCollisions, identityPath),
        };
    }

    internal static string[] GetComponentPaths(string filePath, LanguageProjectContext? context)
    {
        var paths = new HashSet<string>(StringComparer.Ordinal);
        if (context is not null)
        {
            var declaredPaths = new List<string>(context.ComponentFilePaths.Count);
            foreach (var path in context.ComponentFilePaths)
            {
                var normalizedPath = path.Replace('\\', '/');
                declaredPaths.Add(normalizedPath);
                paths.Add(normalizedPath);
            }

            foreach (var document in context.SourceDocuments)
            {
                if (document.IsComponent)
                {
                    paths.Add(ResolveIdentityPath(document.FilePath, declaredPaths));
                }
            }
        }

        // The open URI can spell a Windows path differently from the host's inventory. Reuse a
        // unique declared identity, while preserving explicit case-different component entries.
        paths.Add(ResolveIdentityPath(filePath, paths));
        var orderedPaths = new string[paths.Count];
        paths.CopyTo(orderedPaths);
        Array.Sort(orderedPaths, StringComparer.Ordinal);
        return orderedPaths;
    }

    private static string ResolveIdentityPath(string filePath, IEnumerable<string> componentPaths)
    {
        var normalizedPath = filePath.Replace('\\', '/');
        string? matchingPath = null;
        var matchCount = 0;
        foreach (var path in componentPaths)
        {
            if (string.Equals(path, normalizedPath, StringComparison.Ordinal))
            {
                return path;
            }

            if (SingleFileComponentPathComparison.Comparer.Equals(path, normalizedPath))
            {
                matchingPath = path;
                matchCount++;
            }
        }

        return matchCount == 1 ? matchingPath! : normalizedPath;
    }

    private static bool ContainsPath(string[] paths, string filePath)
    {
        var normalizedPath = filePath.Replace('\\', '/');
        foreach (var path in paths)
        {
            if (string.Equals(path, normalizedPath, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    private static bool HasCanonicalPeer(string filePath, HashSet<string> canonicalBasePaths)
        => filePath.EndsWith(".vue", StringComparison.OrdinalIgnoreCase) &&
            canonicalBasePaths.Contains(GetComponentBasePath(filePath));

    private static string GetComponentBasePath(string filePath)
        => filePath.Replace('\\', '/').Substring(0, filePath.Length - ".viu".Length);

    /// <summary>Resolves an editor URI into the file path used by projection and line mapping.</summary>
    /// <param name="documentUri">The editor document URI.</param>
    /// <returns>The local file path for a file URI; otherwise the URI text itself.</returns>
    internal static string GetFilePath(string documentUri)
        => Uri.TryCreate(documentUri, UriKind.Absolute, out var uri) && uri.IsFile
            ? uri.LocalPath
            : documentUri;

    private static string GetLeafFileName(string filePath)
    {
        var lastSeparator = filePath.LastIndexOfAny(['/', '\\']);
        return lastSeparator >= 0 ? filePath.Substring(lastSeparator + 1) : filePath;
    }
}
