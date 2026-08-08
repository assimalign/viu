using System;
using System.Runtime.Versioning;

using Assimalign.Viu;

namespace Assimalign.Viu.Browser;

/// <summary>
/// Owns a low-level Browser renderer and its exclusive event, directive, and transition routing.
/// </summary>
/// <remarks>
/// The lease is single-threaded and must be disposed before another Browser renderer or application
/// mounts. Call <see cref="Renderer{TNode}.Render"/> with a null tree for every mounted container
/// before disposal so host nodes and listeners are released. Specified by <c>[RND-HOST-1]</c>,
/// <c>[RND-IO-1]</c>, and <c>[EXE-14]</c>.
/// </remarks>
[SupportedOSPlatform("browser")]
public sealed class BrowserRendererLease : IDisposable
{
    private IDisposable? _activation;

    internal BrowserRendererLease(
        Renderer<int> renderer,
        IDisposable activation)
    {
        ArgumentNullException.ThrowIfNull(renderer);
        ArgumentNullException.ThrowIfNull(activation);
        Renderer = renderer;
        _activation = activation;
    }

    /// <summary>Gets the renderer whose Browser routing is owned by this lease.</summary>
    public Renderer<int> Renderer { get; }

    /// <summary>Releases this lease's exclusive Browser routing. Idempotent.</summary>
    public void Dispose()
    {
        IDisposable? activation = _activation;
        if (activation is null)
        {
            return;
        }

        _activation = null;
        activation.Dispose();
        GC.SuppressFinalize(this);
    }
}
