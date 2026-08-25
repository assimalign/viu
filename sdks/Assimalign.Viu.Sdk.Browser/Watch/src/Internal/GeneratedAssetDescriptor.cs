using System.Collections.Generic;

namespace Assimalign.Viu.Sdk.CssHotReload;

internal sealed class GeneratedAssetDescriptor
{
    public GeneratedAssetDescriptor(
        string identity,
        IReadOnlyList<string> watchFiles,
        IReadOnlyList<string> watchRoots,
        IReadOnlyList<string> watchExtensions,
        string regenerationTarget,
        string dependencyManifestPath,
        string staticWebAssetPath,
        string removalBehavior)
    {
        Identity = identity;
        WatchFiles = watchFiles;
        WatchRoots = watchRoots;
        WatchExtensions = watchExtensions;
        RegenerationTarget = regenerationTarget;
        DependencyManifestPath = dependencyManifestPath;
        StaticWebAssetPath = staticWebAssetPath;
        RemovalBehavior = removalBehavior;
    }

    public string Identity { get; }

    public IReadOnlyList<string> WatchFiles { get; }

    public IReadOnlyList<string> WatchRoots { get; }

    public IReadOnlyList<string> WatchExtensions { get; }

    public string RegenerationTarget { get; }

    public string DependencyManifestPath { get; }

    public string StaticWebAssetPath { get; }

    public string RemovalBehavior { get; }
}
