using System;
using System.Collections.Generic;

using Assimalign.Viu;
using Assimalign.Viu.Components;

namespace Assimalign.Viu.Testing;

/// <summary>
/// A ready-to-use in-memory renderer for DOM-free component-tree tests.
/// </summary>
/// <remarks>
/// This is the C# counterpart of Vue's runtime-test host:
/// https://github.com/vuejs/core/tree/main/packages/runtime-test. Every host operation is recorded
/// in <see cref="OperationLog"/>.
/// </remarks>
public sealed class TestRenderer
{
    private readonly List<TestElement> _queryRoots = [];

    /// <summary>Creates an in-memory renderer.</summary>
    /// <param name="snapshotSemantics">
    /// Whether hydration uses an immutable pre-walk matching a batched browser snapshot.
    /// </param>
    /// <param name="strictRemoval">
    /// Whether duplicate host removals throw. Snapshot mode always enables this check.
    /// </param>
    public TestRenderer(
        bool snapshotSemantics = false,
        bool strictRemoval = false)
    {
        OperationLog = new TestNodeOperationLog();
        Renderer = RendererFactory.CreateRenderer(
            TestNodeOperations.Create(
                OperationLog,
                _queryRoots,
                strictRemoval || snapshotSemantics,
                snapshotSemantics));
    }

    /// <summary>Gets the underlying host-neutral renderer.</summary>
    public Renderer<TestNode> Renderer { get; }

    /// <summary>Gets the recorded host operations.</summary>
    public TestNodeOperationLog OperationLog { get; }

    /// <summary>Creates a detached render container without recording a host operation.</summary>
    /// <param name="tag">The diagnostic container tag.</param>
    /// <returns>The container.</returns>
    public TestElement CreateContainer(string tag = "root")
    {
        return new TestElement(tag, elementNamespace: null);
    }

    /// <summary>
    /// Registers a root and its subtree for Teleport target selector queries.
    /// </summary>
    /// <param name="root">The query root.</param>
    public void RegisterQueryRoot(TestElement root)
    {
        ArgumentNullException.ThrowIfNull(root);
        if (!_queryRoots.Contains(root))
        {
            _queryRoots.Add(root);
        }
    }

    /// <summary>Renders an immutable component tree, or unmounts when null.</summary>
    /// <param name="component">The component tree.</param>
    /// <param name="container">The render container.</param>
    /// <param name="application">
    /// The application context used by templates and directives, or null for a primitive-only
    /// tree.
    /// </param>
    /// <returns>The root template context, or null when the root is not a template.</returns>
    public IComponentContext? Render(
        IComponent? component,
        TestElement container,
        IApplicationContext? application = null)
    {
        ArgumentNullException.ThrowIfNull(container);
        RegisterQueryRoot(container);
        return Renderer.Render(component, container, application);
    }

    /// <summary>Hydrates a component tree over existing server-rendered host nodes.</summary>
    /// <param name="component">The client component tree.</param>
    /// <param name="container">The server-populated container.</param>
    /// <param name="application">
    /// The application context used by templates, directives, and warnings, or null for a
    /// primitive-only tree.
    /// </param>
    /// <returns>The root template context, or null when the root is not a template.</returns>
    public IComponentContext? Hydrate(
        IComponent component,
        TestElement container,
        IApplicationContext? application = null)
    {
        ArgumentNullException.ThrowIfNull(component);
        ArgumentNullException.ThrowIfNull(container);
        RegisterQueryRoot(container);
        return Renderer.Hydrate(component, container, application);
    }
}
