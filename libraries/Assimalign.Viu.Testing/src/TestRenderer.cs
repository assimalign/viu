using System;
using System.Collections.Generic;

using Assimalign.Viu;
using Assimalign.Viu.Components;

namespace Assimalign.Viu.Testing;

/// <summary>Provides a ready-to-use renderer over the DOM-free in-memory host.</summary>
/// <remarks>
/// The renderer uses only <see cref="RendererOptions{TNode}"/> and records every write and commit.
/// Specified by <c>[RND-HOST-1]</c> through <c>[RND-HOST-4]</c> and <c>[CONF-3]</c>.
/// </remarks>
public sealed class TestRenderer
{
    private readonly List<TestElement> _queryRoots = [];

    /// <summary>Initializes an in-memory renderer.</summary>
    /// <param name="options">Named test-host behavior, or <see langword="null"/> for live hydration and ordinary removal.</param>
    public TestRenderer(TestRendererOptions? options = null)
    {
        options ??= new TestRendererOptions();
        OperationLog = new TestNodeOperationLog();
        Renderer = RendererFactory.CreateRenderer(
            TestNodeOperations.Create(
                OperationLog,
                _queryRoots,
                new TestRendererOptions
                {
                    SnapshotSemantics = options.SnapshotSemantics,
                    StrictRemoval = options.StrictRemoval || options.SnapshotSemantics,
                }));
    }

    /// <summary>Gets the host-neutral production renderer.</summary>
    public Renderer<TestNode> Renderer { get; }

    /// <summary>Gets the recorded host operations and commit boundaries.</summary>
    public TestNodeOperationLog OperationLog { get; }

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
        return Renderer.Render(node, container, application);
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
        return Renderer.Hydrate(node, container, application);
    }
}
