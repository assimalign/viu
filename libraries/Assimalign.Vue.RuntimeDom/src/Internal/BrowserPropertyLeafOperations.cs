using System;

namespace Assimalign.Vue.RuntimeDom;

/// <summary>
/// The dumb leaf appliers the <see cref="BrowserPropertyPatcher"/> decision tree drives — each
/// resolution costs at most one leaf call, and every leaf maps 1:1 to a bridge interop op so
/// the whole engine serializes into the command buffer ([V01.01.04.05]). Production wires
/// these to <see cref="BrowserDomBridge"/>; tests wire recorders, keeping the [V01.01.04.02]
/// decision tree unit-testable with no DOM.
/// </summary>
internal sealed class BrowserPropertyLeafOperations
{
    /// <summary>Sets an attribute (<c>setAttribute</c>).</summary>
    public required Action<int, string, string> SetAttribute { get; init; }

    /// <summary>Removes an attribute (<c>removeAttribute</c>).</summary>
    public required Action<int, string> RemoveAttribute { get; init; }

    /// <summary>Sets an <c>xlink:</c>-prefixed attribute in the XLink namespace.</summary>
    public required Action<int, string, string> SetXlinkAttribute { get; init; }

    /// <summary>Removes an <c>xlink:</c>-prefixed attribute from the XLink namespace.</summary>
    public required Action<int, string> RemoveXlinkAttribute { get; init; }

    /// <summary>Writes <c>className</c> in one operation (the HTML class fast path).</summary>
    public required Action<int, string> SetClassName { get; init; }

    /// <summary>Sets a string DOM property (e.g. <c>innerHTML</c>, <c>value</c>-adjacent IDL).</summary>
    public required Action<int, string, string> SetStringProperty { get; init; }

    /// <summary>Sets a boolean DOM property (e.g. <c>checked</c>, <c>disabled</c>).</summary>
    public required Action<int, string, bool> SetBooleanProperty { get; init; }

    /// <summary>
    /// Sets <c>value</c> only when the live value differs (one compare-and-set interop call, so
    /// caret and IME state are never clobbered).
    /// </summary>
    public required Action<int, string> SetValueGuarded { get; init; }

    /// <summary>Replaces the whole inline style (<c>style.cssText</c>).</summary>
    public required Action<int, string> SetStyleText { get; init; }

    /// <summary>Sets one style property (name, value, important) via <c>style.setProperty</c>.</summary>
    public required Action<int, string, string, bool> SetStyleProperty { get; init; }

    /// <summary>Removes one style property via <c>style.removeProperty</c>.</summary>
    public required Action<int, string> RemoveStyleProperty { get; init; }

    /// <summary>
    /// Registers (non-null) or removes (null) the listener delegate for a lower-case event
    /// name. The invoker pattern and modifiers land with [V01.01.04.03].
    /// </summary>
    public required Action<int, string, Delegate?> SetEventListener { get; init; }
}
