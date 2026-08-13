using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Assimalign.Viu.DevTools;

/// <summary>
/// Supplies one named custom diagnostic tree and the state associated with its nodes.
/// </summary>
/// <remarks>
/// Providers are invoked only in response to client requests. Returned state is normalized by
/// the same depth-limited, non-throwing value encoder as component snapshots. Specified by
/// <c>[DVT-7]</c>.
/// </remarks>
public interface IDevToolsInspector
{
    /// <summary>Gets the stable protocol identifier for this inspector.</summary>
    string Identifier { get; }

    /// <summary>Gets the human-readable inspector name.</summary>
    string DisplayName { get; }

    /// <summary>Gets the current custom tree.</summary>
    /// <param name="cancellationToken">Cancels this request.</param>
    /// <returns>The current root nodes in display order.</returns>
    ValueTask<IReadOnlyList<DevToolsInspectorNode>> GetTreeAsync(
        CancellationToken cancellationToken = default);

    /// <summary>Gets the current named state for one custom node.</summary>
    /// <param name="nodeIdentifier">The node identifier supplied by <see cref="GetTreeAsync"/>.</param>
    /// <param name="cancellationToken">Cancels this request.</param>
    /// <returns>The current named state values.</returns>
    ValueTask<IReadOnlyDictionary<string, object?>> GetStateAsync(
        string nodeIdentifier,
        CancellationToken cancellationToken = default);
}
