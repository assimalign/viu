namespace Assimalign.Viu.Components;

/// <summary>
/// Identifies the closed structural variants understood by Viu's renderer.
/// </summary>
/// <remarks>The ten-value algebra is specified by <c>[CMP-3]</c>.</remarks>
public enum VirtualNodeKind
{
    /// <summary>An element with a qualified name, bindings, and child nodes.</summary>
    Element,

    /// <summary>A text node.</summary>
    Text,

    /// <summary>A comment node.</summary>
    Comment,

    /// <summary>A host-specific static markup payload.</summary>
    Static,

    /// <summary>A transparent ordered child collection.</summary>
    Fragment,

    /// <summary>An invocation of registered authored component behavior.</summary>
    Component,

    /// <summary>A subtree rendered into a host-resolved target.</summary>
    Teleport,

    /// <summary>A component subtree whose mounted state may be retained while inactive.</summary>
    KeepAlive,

    /// <summary>Content and fallback branches coordinated by asynchronous dependencies.</summary>
    Suspense,

    /// <summary>A child decorated with host-provided transition behavior.</summary>
    Transition
}
