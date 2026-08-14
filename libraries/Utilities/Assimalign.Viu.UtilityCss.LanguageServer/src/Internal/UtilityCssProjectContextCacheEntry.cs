using System.Collections.Generic;
using System.Linq;

namespace Assimalign.Viu.UtilityCss.LanguageServer;

internal sealed record UtilityCssProjectContextCacheEntry(
    string ManifestPath,
    WatchedFileState ManifestState,
    IReadOnlyList<WatchedFileState> DependencyStates,
    UtilityCssProjectContext Context)
{
    internal bool IsCurrent(
        string manifestPath,
        WatchedFileState manifestState,
        System.StringComparer pathComparer)
        => pathComparer.Equals(ManifestPath, manifestPath) &&
           ManifestState.Equals(manifestState) &&
           DependencyStates.All(state => state.IsCurrent());
}
