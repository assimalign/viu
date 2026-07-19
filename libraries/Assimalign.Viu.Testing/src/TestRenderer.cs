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
    /// <summary>Creates a renderer over a fresh op log.</summary>
    public TestRenderer()
    {
        OperationLog = new TestNodeOperationLog();
        Renderer = RendererFactory.CreateRenderer(TestNodeOperations.Create(OperationLog));
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

    /// <summary>Renders <paramref name="node"/> into <paramref name="container"/> (null unmounts).</summary>
    /// <param name="node">The tree to render, or null to unmount.</param>
    /// <param name="container">The container element.</param>
    public void Render(VirtualNode? node, TestElement container) => Renderer.Render(node, container);
}
