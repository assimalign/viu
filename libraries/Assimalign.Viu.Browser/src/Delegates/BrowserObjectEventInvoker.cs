using System;

namespace Assimalign.Viu.Browser;

/// <summary>
/// Bridges a dispatched <see cref="BrowserEvent"/> into a renderer-agnostic
/// <see cref="Action{T}"/> of <see cref="object"/> event handler — the shape a component that
/// renders through the node-ops abstraction (rather than the DOM directly) attaches, so its
/// handler receives a platform-free payload instead of the <see cref="BrowserEvent"/>. The
/// in-memory Testing renderer invokes these handlers with a synthesized payload directly; in the
/// browser a host installs an invoker on <see cref="BrowserObjectEvents"/> that converts the
/// <paramref name="browserEvent"/> into that payload, calls <paramref name="handler"/>, and
/// applies the handler's prevent/stop decision back to the <paramref name="browserEvent"/> (whose
/// response flags re-cross the interop boundary in the single dispatch return).
/// <para>
/// The DOM runtime stays agnostic of any concrete payload type — the canonical consumer is the
/// Router's DOM bridge, which maps the click metadata to vue-router's <c>guardEvent</c> contract.
/// </para>
/// </summary>
/// <param name="handler">The component's object-payload handler (from an <c>onX</c> prop).</param>
/// <param name="browserEvent">The dispatched browser event carrying the native metadata.</param>
public delegate void BrowserObjectEventInvoker(Action<object?> handler, BrowserEvent browserEvent);
