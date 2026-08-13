using System;
using System.Collections.Generic;
using System.Threading.Tasks;

using Assimalign.Viu;
using Assimalign.Viu.Components;

namespace Assimalign.Viu.Testing;

/// <summary>Provides a ready-to-use renderer over the DOM-free in-memory host.</summary>
/// <remarks>
/// The renderer owns a <see cref="TestSynchronizationContext"/>, scopes it around render and
/// hydrate calls, and records every write and commit. Callers can explicitly drain, pump, or run
/// asynchronous component work; disposal reports forgotten continuations. Specified by
/// <c>[RND-HOST-1]</c> through <c>[RND-HOST-4]</c>, <c>[CONF-3]</c>, and
/// <c>[V01.01.11.05]</c>.
/// </remarks>
public sealed class TestRenderer : IDisposable
{
    private readonly List<TestElement> _queryRoots = [];
    private bool _isDisposed;

    /// <summary>Initializes an in-memory renderer with its own deterministic continuation queue.</summary>
    /// <param name="options">Named test-host behavior, or <see langword="null"/> for live hydration and ordinary removal.</param>
    public TestRenderer(TestRendererOptions? options = null)
    {
        options ??= new TestRendererOptions();
        SynchronizationContext = TestSynchronizationContext.CreateDetached();
        OperationLog = new TestNodeOperationLog();
        Renderer = RendererFactory.CreateRenderer(
            TestNodeOperations.Create(
                OperationLog,
                _queryRoots,
                new TestRendererOptions
                {
                    SnapshotSemantics = options.SnapshotSemantics,
                    StrictRemoval = options.StrictRemoval || options.SnapshotSemantics,
                    HydrationTriggers = options.HydrationTriggers,
                }));
    }

    /// <summary>Gets the host-neutral production renderer.</summary>
    public Renderer<TestNode> Renderer { get; }

    /// <summary>Gets the recorded host operations and commit boundaries.</summary>
    public TestNodeOperationLog OperationLog { get; }

    /// <summary>
    /// Gets the renderer-owned deterministic continuation queue used by render and hydrate calls.
    /// </summary>
    public TestSynchronizationContext SynchronizationContext { get; }

    /// <summary>Creates a detached container without logging a renderer operation.</summary>
    /// <param name="tag">The diagnostic local tag name.</param>
    /// <returns>The detached container.</returns>
    public TestElement CreateContainer(string tag = "root")
    {
        ArgumentException.ThrowIfNullOrEmpty(tag);
        return new TestElement(new QualifiedName(tag));
    }

    /// <summary>Registers a root and its descendants for teleport selector resolution.</summary>
    /// <param name="root">The selector query root.</param>
    public void RegisterQueryRoot(TestElement root)
    {
        ArgumentNullException.ThrowIfNull(root);
        if (!_queryRoots.Contains(root))
        {
            _queryRoots.Add(root);
        }
    }

    /// <summary>Renders a fresh immutable tree, or unmounts when the tree is null.</summary>
    /// <param name="node">The next immutable root.</param>
    /// <param name="container">The render container.</param>
    /// <param name="application">The optional application composition for authored components.</param>
    /// <returns>The root component context when the root is authored, otherwise null.</returns>
    public ComponentContext? Render(
        VirtualNode? node,
        TestElement container,
        IApplicationContext? application = null)
    {
        ArgumentNullException.ThrowIfNull(container);
        RegisterQueryRoot(container);
        return SynchronizationContext.Run(
            () => Renderer.Render(node, container, application));
    }

    /// <summary>Hydrates an immutable client tree over existing server host nodes.</summary>
    /// <param name="node">The client root.</param>
    /// <param name="container">The server-populated container.</param>
    /// <param name="application">The optional application composition for authored components.</param>
    /// <returns>The root component context when the root is authored, otherwise null.</returns>
    public ComponentContext? Hydrate(
        VirtualNode node,
        TestElement container,
        IApplicationContext? application = null)
    {
        ArgumentNullException.ThrowIfNull(node);
        ArgumentNullException.ThrowIfNull(container);
        RegisterQueryRoot(container);
        return SynchronizationContext.Run(
            () => Renderer.Hydrate(node, container, application));
    }

    /// <summary>Drains every continuation currently queued by renderer or component work.</summary>
    /// <returns>The number of continuations executed.</returns>
    public int Drain() => SynchronizationContext.Drain();

    /// <summary>Pumps an asynchronous renderer or component operation to completion.</summary>
    /// <param name="operation">The operation to complete without a wall-clock wait.</param>
    public void Pump(Task operation) => SynchronizationContext.Pump(operation);

    /// <summary>Runs an asynchronous component action and pumps it to completion.</summary>
    /// <param name="action">The action to start under the renderer-owned context.</param>
    public void Run(Func<Task> action) => SynchronizationContext.Run(action);

    /// <summary>
    /// Restores all scoped context installations and fails when component continuations were
    /// forgotten instead of drained.
    /// </summary>
    public void Dispose()
    {
        if (_isDisposed)
        {
            return;
        }

        SynchronizationContext.Dispose();
        _isDisposed = true;
    }
}
