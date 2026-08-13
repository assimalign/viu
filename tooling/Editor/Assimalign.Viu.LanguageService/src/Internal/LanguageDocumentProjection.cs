using System;
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
        var name = SingleFileComponentNameResolver.Resolve(
            filePath,
            context?.ProjectDirectory,
            context?.RootNamespace);
        var input = new SingleFileComponentProjectionInput(
            document.Syntax.Format == LanguageDocumentFormat.Vue
                ? SingleFileComponentFormat.Vue
                : SingleFileComponentFormat.Viu,
            filePath,
            GetLeafFileName(filePath),
            document.Text,
            name.Namespace,
            name.ClassName,
            name.HintName,
            StyleScopeId.Resolve(filePath, context?.ProjectDirectory),
            HotReloadComponentIdentifier: null,
            HasCanonicalPeer: false);
        return SingleFileComponentProjection.Project(input, cancellationToken);
    }

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
