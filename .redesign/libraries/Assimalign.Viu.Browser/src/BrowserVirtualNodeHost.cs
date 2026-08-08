using System;

using Assimalign.Viu;
using Assimalign.Viu.Components;

namespace Assimalign.Viu.Browser;

/// <summary>
/// Defines the browser ownership boundary for DOM creation, binding policy, events, and cleanup.
/// </summary>
/// <remarks>
/// The design scaffold intentionally omits JavaScript interop. The shipping implementation batches
/// these operations and owns every listener and handle it creates. Specified by
/// <c>[RND-HOST-1]</c> and <c>[RND-HOST-3]</c>.
/// </remarks>
public sealed class BrowserVirtualNodeHost : IVirtualNodeHost<BrowserNodeHandle>
{
    /// <inheritdoc />
    public BrowserNodeHandle CreateElement(QualifiedName name) =>
        throw CreateDesignOnlyException();

    /// <inheritdoc />
    public BrowserNodeHandle CreateText(string text) =>
        throw CreateDesignOnlyException();

    /// <inheritdoc />
    public BrowserNodeHandle CreateComment(string text) =>
        throw CreateDesignOnlyException();

    /// <inheritdoc />
    public BrowserNodeHandle CreateDetachedContainer() =>
        throw CreateDesignOnlyException();

    /// <inheritdoc />
    public BrowserNodeHandle ResolveTarget(string targetIdentifier) =>
        throw CreateDesignOnlyException();

    /// <inheritdoc />
    public void Insert(
        BrowserNodeHandle child,
        BrowserNodeHandle parent,
        BrowserNodeHandle? anchor) =>
        throw CreateDesignOnlyException();

    /// <inheritdoc />
    public void Remove(BrowserNodeHandle node) =>
        throw CreateDesignOnlyException();

    /// <inheritdoc />
    public void SetText(BrowserNodeHandle node, string text) =>
        throw CreateDesignOnlyException();

    /// <inheritdoc />
    public void PatchBinding(
        BrowserNodeHandle element,
        ElementBinding? previous,
        ElementBinding? current) =>
        throw CreateDesignOnlyException();

    /// <inheritdoc />
    public BrowserNodeHandle? GetParent(BrowserNodeHandle node) =>
        throw CreateDesignOnlyException();

    /// <inheritdoc />
    public BrowserNodeHandle? GetNextSibling(BrowserNodeHandle node) =>
        throw CreateDesignOnlyException();

    private static NotSupportedException CreateDesignOnlyException() =>
        new("Browser JavaScript interop is outside the abstraction scaffold.");
}
