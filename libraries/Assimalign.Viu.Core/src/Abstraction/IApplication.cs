using System.Threading;
using System.Threading.Tasks;

using Assimalign.Viu.Components;

namespace Assimalign.Viu;

/// <summary>
/// Represents the platform-neutral lifecycle and configuration shared by every Viu host.
/// </summary>
/// <remarks>
/// Mount targets remain on <see cref="IApplication{TNode}"/> so plugins and application
/// configuration do not acquire a Browser or future WebView2 dependency. Not thread-safe.
/// </remarks>
public interface IApplication
{
    /// <summary>Gets the immutable application composition context.</summary>
    IApplicationContext Context { get; }

    /// <summary>Gets whether the root tree is currently mounted.</summary>
    bool IsMounted { get; }

    /// <summary>Gets the mounted root component context, or null while unmounted.</summary>
    IComponentContext? RootContext { get; }

    /// <summary>Records a plugin for installation before the first host render.</summary>
    /// <param name="plugin">The platform-neutral plugin.</param>
    /// <returns>This application.</returns>
    IApplication Use(IApplicationPlugin plugin);

    /// <summary>Synchronously unmounts the application when it is mounted.</summary>
    void Unmount();

    /// <summary>Asynchronously unmounts the application when the host requires asynchronous work.</summary>
    /// <param name="cancellationToken">Cancels host-specific asynchronous teardown.</param>
    /// <returns>A task that completes after host teardown.</returns>
    ValueTask UnmountAsync(CancellationToken cancellationToken = default);
}
