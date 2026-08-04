using System;
using System.Collections.Generic;

namespace Assimalign.Viu.Tooling.LanguageServer;

/// <summary>
/// The parsed, path-resolved model of one project's restore artifacts ([V01.01.12.23], #259):
/// everything <see cref="ViuProjectContextReader"/> needs to materialize a
/// <see cref="Assimalign.Viu.Tooling.LanguageService.LanguageProjectContext"/> except the source text
/// itself. The resolution outputs below are populated only when <see cref="Status"/> is
/// <see cref="ViuProjectContextStatusKind.SemanticsActive"/> or
/// <see cref="ViuProjectContextStatusKind.AssetsStale"/>.
/// </summary>
internal sealed record ViuProjectAssets
{
    /// <summary>Gets the degradation-ladder outcome of the resolution.</summary>
    internal required ViuProjectContextStatus Status { get; init; }

    /// <summary>Gets the absolute path of the project file the assets belong to.</summary>
    internal required string ProjectFilePath { get; init; }

    /// <summary>Gets the RID-less target framework moniker the restore produced.</summary>
    internal string TargetFramework { get; init; } = string.Empty;

    /// <summary>
    /// Gets the project directory as the build publishes it (<c>build_property.ProjectDir</c>,
    /// trailing separator included), falling back to the project file's directory.
    /// </summary>
    internal string ProjectDirectory { get; init; } = string.Empty;

    /// <summary>
    /// Gets the root namespace: the generator's own <c>build_property.RootNamespace</c> channel,
    /// falling back to the literal <c>&lt;RootNamespace&gt;</c> in the project XML, then the
    /// project file's base name.
    /// </summary>
    internal string RootNamespace { get; init; } = string.Empty;

    /// <summary>Gets the build configuration from the generated editorconfig, when present.</summary>
    internal string? Configuration { get; init; }

    /// <summary>
    /// Gets the absolute compile-time reference paths: package compile assets, framework
    /// reference assemblies, and built outputs of referenced projects.
    /// </summary>
    internal IReadOnlyList<string> ReferenceAssemblyPaths { get; init; } = Array.Empty<string>();

    /// <summary>Gets the absolute paths of the project's C# sources (<c>bin</c>/<c>obj</c> excluded).</summary>
    internal IReadOnlyList<string> SourceFilePaths { get; init; } = Array.Empty<string>();

    /// <summary>
    /// Gets the absolute paths of the project's single-file components — the source generator's
    /// own <c>**\*.viu;**\*.vue</c> AdditionalFiles cone.
    /// </summary>
    internal IReadOnlyList<string> ComponentFilePaths { get; init; } = Array.Empty<string>();

    /// <summary>
    /// Gets the cache fingerprint: SHA-256 over the assets bytes, the name inputs, and the sorted
    /// source-file list with per-file (last write, length) stamps. Recomputed by cheap stats on
    /// every request, which also implements recovery on the next request without a file watcher.
    /// </summary>
    internal string CacheStamp { get; init; } = string.Empty;
}
