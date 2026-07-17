using System;
using System.Runtime.Versioning;

using Assimalign.Vue.RuntimeCore;

namespace Assimalign.Vue.RuntimeDom;

/// <summary>
/// The production <see cref="RendererOptions{TNode}"/> over the DOM bridge — the browser
/// implementation of Vue's <c>nodeOps</c> + <c>patchProp</c>
/// (https://github.com/vuejs/core/blob/main/packages/runtime-dom/src/nodeOps.ts). Handles are
/// ints (see the marshaling ADR in <c>libraries/Assimalign.Vue.RuntimeDom/docs/</c>); 0 is the
/// "no node" sentinel. Node removal releases the removed subtree's JS handles and DOM
/// listeners deterministically, and the returned handle list purges the invoker registry in
/// the same call — no leaks on either side of the boundary ([V01.01.04.01]). Event handling
/// goes through the invoker registry ([V01.01.04.03]): handler changes between renders swap a
/// delegate with zero listener-management interop.
/// </summary>
[SupportedOSPlatform("browser")]
internal static class BrowserNodeOperations
{
    private static readonly BrowserEventInvokerRegistry Invokers = new(
        static (handle, eventName, once, capture, passive) =>
            BrowserDomBridge.AddEventListener(handle, eventName, once, capture, passive),
        static (handle, eventName, capture) =>
            BrowserDomBridge.RemoveEventListener(handle, eventName, capture));

    private static readonly BrowserPropertyLeafOperations LeafOperations = new()
    {
        SetAttribute = static (element, name, value) => BrowserDomBridge.SetAttribute(element, name, value),
        RemoveAttribute = static (element, name) => BrowserDomBridge.RemoveAttribute(element, name),
        SetXlinkAttribute = static (element, name, value) => BrowserDomBridge.SetXlinkAttribute(element, name, value),
        RemoveXlinkAttribute = static (element, name) => BrowserDomBridge.RemoveXlinkAttribute(element, name),
        SetClassName = static (element, value) => BrowserDomBridge.SetClassName(element, value),
        SetStringProperty = static (element, name, value) => BrowserDomBridge.SetStringProperty(element, name, value),
        SetBooleanProperty = static (element, name, value) => BrowserDomBridge.SetBooleanProperty(element, name, value),
        SetValueGuarded = static (element, value) => BrowserDomBridge.SetValueGuarded(element, value),
        SetStyleText = static (element, cssText) => BrowserDomBridge.SetStyleText(element, cssText),
        SetStyleProperty = static (element, name, value, important) => BrowserDomBridge.SetStyleProperty(element, name, value, important),
        RemoveStyleProperty = static (element, name) => BrowserDomBridge.RemoveStyleProperty(element, name),
        SetEventListener = static (element, rawPropertyName, listener) =>
            Invokers.SetListener(element, rawPropertyName, listener),
    };

    internal static RendererOptions<int> Create() => new()
    {
        Insert = static (child, parent, anchor) => BrowserDomBridge.Insert(parent, child, anchor),
        Remove = static child => Invokers.PurgeReleasedHandles(BrowserDomBridge.Remove(child)),
        CreateElement = static (tag, elementNamespace) => BrowserDomBridge.CreateElement(tag, elementNamespace),
        CreateText = static text => BrowserDomBridge.CreateText(text),
        CreateComment = static text => BrowserDomBridge.CreateComment(text),
        SetText = static (node, text) => BrowserDomBridge.SetText(node, text),
        SetElementText = static (node, text) => Invokers.PurgeReleasedHandles(BrowserDomBridge.SetElementText(node, text)),
        ParentNode = static node => BrowserDomBridge.ParentNode(node),
        NextSibling = static node => BrowserDomBridge.NextSibling(node),
        PatchProperty = static (element, elementTag, propertyName, previousValue, nextValue, elementNamespace) =>
            BrowserPropertyPatcher.Patch(LeafOperations, element, elementTag, propertyName, previousValue, nextValue, elementNamespace),
        QuerySelector = static selector => BrowserDomBridge.QuerySelector(selector),
        InsertStaticContent = static (content, parent, anchor, elementNamespace) =>
        {
            var span = BrowserDomBridge.InsertStaticContent(content, parent, anchor, elementNamespace);
            return (span[0], span[1]);
        },
    };

    internal static int DispatchEvent(int nodeHandle, bool capture, BrowserEvent browserEvent)
        => Invokers.Dispatch(nodeHandle, capture, browserEvent);

    /// <summary>Registry sizes for leak diagnostics: JS nodes, JS listener maps, .NET invokers.</summary>
    internal static (int JsNodes, int JsListenerMaps, int DotnetListeners) GetRegistryDiagnostics()
    {
        var sizes = BrowserDomBridge.GetRegistrySizes();
        return (sizes[0], sizes[1], Invokers.InvokerCount);
    }
}
