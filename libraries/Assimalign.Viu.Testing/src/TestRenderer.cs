using System.Collections.Generic;

using Assimalign.Viu.RuntimeCore;

namespace Assimalign.Viu.Testing;

/// <summary>
/// The ready-to-use in-memory renderer for unit tests — the C# counterpart of
/// <c>@vue/runtime-test</c>'s exported <c>render</c> + <c>nodeOps</c> pair
/// (https://github.com/vuejs/core/tree/main/packages/runtime-test). Mount, patch, unmount, and
/// scheduler-driven updates all run on a plain CoreCLR test host with no browser, WASM
/// toolchain, or JS interop, and every node operation lands in <see cref="OperationLog"/>.
/// </summary>
public sealed class TestRenderer
{
    // The live roots the querySelector node-op searches to resolve a <Teleport> string target; every
    // render container is auto-registered, and RegisterQueryRoot adds detached target containers.
    private readonly List<TestElement> _teleportTargetRoots = [];

    /// <summary>Creates a renderer over a fresh op log.</summary>
    public TestRenderer()
    {
        OperationLog = new TestNodeOperationLog();
        Renderer = RendererFactory.CreateRenderer(TestNodeOperations.Create(OperationLog, _teleportTargetRoots));
    }

    /// <summary>The underlying platform-agnostic renderer.</summary>
    public Renderer<TestNode> Renderer { get; }

    /// <summary>The log every node operation is recorded into.</summary>
    public TestNodeOperationLog OperationLog { get; }

    /// <summary>
    /// Creates a detached container element to render into. Container creation is not recorded —
    /// the log isolates what the renderer did.
    /// </summary>
    /// <param name="tag">The container tag.</param>
    public TestElement CreateContainer(string tag = "root") => new(tag, null);

    /// <summary>
    /// Registers <paramref name="root"/> (and its subtree) as searchable by a <c>&lt;Teleport&gt;</c>
    /// string <c>to</c> target — the in-memory analogue of an element being present in the document the
    /// browser's <c>querySelector</c> searches. Render containers are registered automatically; use this
    /// for a detached target container that a Teleport selects by <c>#id</c>/<c>.class</c>/tag rather than
    /// by a direct node reference ([V01.01.03.17]).
    /// </summary>
    /// <param name="root">The element to make findable.</param>
    public void RegisterQueryRoot(TestElement root)
    {
        if (!_teleportTargetRoots.Contains(root))
        {
            _teleportTargetRoots.Add(root);
        }
    }

    /// <summary>Renders <paramref name="node"/> into <paramref name="container"/> (null unmounts).</summary>
    /// <param name="node">The tree to render, or null to unmount.</param>
    /// <param name="container">The container element.</param>
    public void Render(VirtualNode? node, TestElement container)
    {
        // A render container is a queryable root for Teleport string targets (an in-tree #id/.class/tag
        // resolves against the tree the renderer just built).
        RegisterQueryRoot(container);
        Renderer.Render(node, container);
    }

    /// <summary>
    /// Hydrates <paramref name="node"/> against the existing server-rendered children already present in
    /// <paramref name="container"/> — the DOM-free counterpart of a browser <c>CreateSSRApp(...).Mount</c>
    /// (upstream: <c>createSSRApp</c>'s hydrating mount, https://vuejs.org/guide/scaling-up/ssr.html#client-hydration).
    /// Populate <paramref name="container"/> with the server tree first (by hand or by parsing SSR output),
    /// then hydrate: matching nodes are adopted with zero structural mutations, and a mismatch recovers per
    /// subtree ([V01.01.07.03]).
    /// </summary>
    /// <param name="node">The client vnode tree to hydrate onto the server nodes.</param>
    /// <param name="container">The container holding the pre-rendered server tree.</param>
    public void Hydrate(VirtualNode node, TestElement container)
    {
        RegisterQueryRoot(container);
        Renderer.Hydrate(node, container);
    }
}
