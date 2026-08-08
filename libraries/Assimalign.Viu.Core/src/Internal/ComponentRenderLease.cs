using System.Threading.Tasks;

using Assimalign.Viu.Components;

namespace Assimalign.Viu;

internal sealed class ComponentRenderLease : IComponentRenderScope
{
    private readonly ComponentActivation _activation;

    internal ComponentRenderLease(ComponentActivation activation, VirtualNode? tree)
    {
        _activation = activation;
        Tree = tree;
    }

    public VirtualNode? Tree { get; }

    public ComponentContext Context => _activation.Context;

    // The mount's rendering surface is retained by the shared activation so persistent and
    // one-shot execution use the same per-mount cache and block state.
    internal ComponentRenderFrame Frame => _activation.Frame;

    internal bool IsDisposed => _activation.IsReleased;

    public ValueTask DisposeAsync() => _activation.ReleaseAsync();
}
