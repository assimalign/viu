using System;
using System.Runtime.ExceptionServices;
using System.Threading.Tasks;

using Assimalign.Viu.Components;
using Assimalign.Viu.Reactivity;

namespace Assimalign.Viu;

internal sealed class ComponentRenderLease : IComponentRenderScope
{
    private readonly IComponent _instance;
    private readonly ComponentLifecycle _lifecycle;
    private readonly EffectScope _scope;

    internal ComponentRenderLease(
        IComponent instance,
        ComponentContext context,
        VirtualNode? tree,
        EffectScope scope,
        ComponentRenderFrame frame,
        ComponentLifecycle lifecycle)
    {
        _instance = instance;
        _lifecycle = lifecycle;
        Context = context;
        Tree = tree;
        _scope = scope;
        Frame = frame;
    }

    public VirtualNode? Tree { get; }

    public ComponentContext Context { get; }

    // The mount's rendering surface: constructed once per mount and reused by every render of
    // this lease, so cache slots and handler identity survive re-renders.
    internal ComponentRenderFrame Frame { get; }

    internal bool IsDisposed { get; private set; }

    public async ValueTask DisposeAsync()
    {
        if (IsDisposed)
        {
            return;
        }

        IsDisposed = true;
        await ReleaseAsync(_instance, _scope, _lifecycle).ConfigureAwait(false);
    }

    internal static async ValueTask ReleaseAsync(
        IComponent instance,
        EffectScope scope,
        ComponentLifecycle lifecycle)
    {
        ExceptionDispatchInfo? failure = null;
        try
        {
            lifecycle.Cancel();
        }
        catch (Exception exception)
        {
            failure = ExceptionDispatchInfo.Capture(exception);
        }

        try
        {
            scope.Dispose();
        }
        catch (Exception exception)
        {
            failure ??= ExceptionDispatchInfo.Capture(exception);
        }

        try
        {
            switch (instance)
            {
                case IAsyncDisposable asynchronousDisposable:
                    await asynchronousDisposable.DisposeAsync().ConfigureAwait(false);
                    break;
                case IDisposable disposable:
                    disposable.Dispose();
                    break;
            }
        }
        catch (Exception exception)
        {
            failure ??= ExceptionDispatchInfo.Capture(exception);
        }

        try
        {
            await lifecycle.DrainAsync().ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            failure ??= ExceptionDispatchInfo.Capture(exception);
        }

        try
        {
            lifecycle.Dispose();
        }
        catch (Exception exception)
        {
            failure ??= ExceptionDispatchInfo.Capture(exception);
        }

        failure?.Throw();
    }
}
