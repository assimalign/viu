using System.Threading;
using System.Threading.Tasks;

using Assimalign.Viu.Components;

namespace Assimalign.Viu;

/// <summary>
/// Represents a Viu application that mounts into a host-specific node type.
/// </summary>
/// <typeparam name="TNode">
/// The host node type. Browser hosts commonly use an opaque integer DOM handle; a WebView2 host
/// can use its own session-scoped node handle without changing Core or component APIs.
/// </typeparam>
public interface IApplication<TNode> : IApplication
    where TNode : notnull
{
    /// <summary>Synchronously mounts the root component tree into a host node.</summary>
    /// <param name="container">The host container.</param>
    /// <returns>The mounted root component context, when the root is a template.</returns>
    IComponentContext? Mount(TNode container);

    /// <summary>Mounts the root component tree after asynchronous plugin and host initialization.</summary>
    /// <param name="container">The host container.</param>
    /// <param name="cancellationToken">Cancels initialization before the first render completes.</param>
    /// <returns>The mounted root component context, when the root is a template.</returns>
    ValueTask<IComponentContext?> MountAsync(
        TNode container,
        CancellationToken cancellationToken = default);
}
