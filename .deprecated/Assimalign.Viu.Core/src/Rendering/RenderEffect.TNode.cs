using System;


namespace Assimalign.Viu;

/// <summary>
/// A root-level reactive render binding — the C# port of the render-effect wiring in
/// <c>setupRenderEffect</c> (<c>packages/runtime-core/src/renderer.ts</c>,
/// https://vuejs.org/guide/extras/rendering-mechanism.html). The render function runs inside a
/// <see cref="ReactiveEffect"/>, so every reactive dependency it reads is tracked; a later
/// mutation enqueues the update job on the <see cref="Scheduler"/> instead of re-running
/// synchronously, and the flush re-invokes the render function and patches the diff. State
/// mutation alone drives re-rendering — no polling, no manual render loop. W01 scope is this
/// root-level effect; per-component effects with uid ordering land with [V01.01.03.06].
/// Created through <see cref="Renderer{TNode}.CreateRenderEffect"/>.
/// Not thread-safe (single-threaded JS event-loop model).
/// </summary>
/// <typeparam name="TNode">The platform node type.</typeparam>
public sealed class RenderEffect<TNode> : IDisposable
    where TNode : notnull
{
    private readonly Renderer<TNode> _renderer;
    private readonly Func<VirtualNode> _renderFunction;
    private readonly TNode _container;
    private readonly ReactiveEffect _effect;
    private readonly SchedulerJob _updateJob;

    internal RenderEffect(Renderer<TNode> renderer, Func<VirtualNode> renderFunction, TNode container)
    {
        _renderer = renderer;
        _renderFunction = renderFunction;
        _container = container;
        // The root render job: id 0 (a component's job carries its uid from [V01.01.03.06] on),
        // ALLOW_RECURSE per upstream toggleRecurse — a render is allowed to write state that
        // re-queues it; the scheduler's recursion limit still catches infinite loops.
        _updateJob = new SchedulerJob(Update)
        {
            Identifier = 0,
            AllowRecurse = true,
            Name = "root-render-effect",
        };
        _effect = new ReactiveEffect(RenderAndPatch)
        {
            AllowRecurse = true,
            // Invalidation enqueues the update job instead of re-running inline (upstream:
            // effect.scheduler = () => queueJob(job)); the flush deduplicates, so any number
            // of mutations in one turn produce one re-render.
            Scheduler = ScheduleUpdate,
        };
        try
        {
            // Initial tracked mount (upstream parity: the first componentUpdateFn run).
            _effect.Run();
        }
        catch
        {
            // A throwing first render leaves no live subscriptions (Reactive.Effect parity).
            Stop();
            throw;
        }
    }

    /// <summary>Whether the effect still reacts to dependency changes.</summary>
    public bool IsActive => _effect.IsActive;

    /// <summary>
    /// Detaches dependency tracking and cancels any queued update: subsequent mutations
    /// trigger no further renders. The rendered tree stays in the container — call
    /// <see cref="Unmount"/> to also tear it down.
    /// </summary>
    public void Stop()
    {
        _updateJob.IsDisposed = true;
        _effect.Stop();
    }

    /// <summary>Stops the effect and unmounts the rendered tree from the container.</summary>
    public void Unmount()
    {
        Stop();
        _renderer.Render(null, _container);
    }

    /// <summary>Equivalent to <see cref="Unmount"/>.</summary>
    public void Dispose() => Unmount();

    /// <summary>Test seam: the underlying effect, for leak assertions.</summary>
    internal ReactiveEffect Effect => _effect;

    private void RenderAndPatch() => _renderer.Render(_renderFunction(), _container);

    private void ScheduleUpdate() => Scheduler.QueueJob(_updateJob);

    private void Update() => _effect.Run();
}
