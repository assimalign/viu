using System;
using System.Collections.Generic;
using System.Runtime.Versioning;

using Assimalign.Vue.RuntimeCore;

namespace Assimalign.Vue.RuntimeDom;

/// <summary>
/// The production <see cref="RendererOptions{TNode}"/> over the DOM bridge — the browser
/// implementation of Vue's <c>nodeOps</c> + <c>patchProp</c>
/// (https://github.com/vuejs/core/blob/main/packages/runtime-dom/src/nodeOps.ts). Handles are
/// ints (see the marshaling ADR in <c>libraries/Assimalign.Vue.RuntimeDom/docs/</c>); 0 is the
/// "no node" sentinel. Node removal releases the removed subtree's JS handles and DOM
/// listeners deterministically, and the returned handle list purges the C#-side listener
/// registry in the same call — no leaks on either side of the boundary ([V01.01.04.01]).
/// </summary>
[SupportedOSPlatform("browser")]
internal static class BrowserNodeOperations
{
    // Listener delegates keyed by (handle, lower-case event name); the JS side dispatches back
    // by the same pair. Multicast delegates (merged props) invoke every target.
    private static readonly Dictionary<(int NodeHandle, string EventName), Delegate> EventListeners = [];

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
        SetEventListener = static (element, eventName, listener) =>
        {
            if (listener is null)
            {
                EventListeners.Remove((element, eventName));
                BrowserDomBridge.RemoveEventListener(element, eventName);
            }
            else
            {
                EventListeners[(element, eventName)] = listener;
                BrowserDomBridge.AddEventListener(element, eventName);
            }
        },
    };

    internal static RendererOptions<int> Create() => new()
    {
        Insert = static (child, parent, anchor) => BrowserDomBridge.Insert(parent, child, anchor),
        Remove = static child => PurgeListeners(BrowserDomBridge.Remove(child)),
        CreateElement = static (tag, elementNamespace) => BrowserDomBridge.CreateElement(tag, elementNamespace),
        CreateText = static text => BrowserDomBridge.CreateText(text),
        CreateComment = static text => BrowserDomBridge.CreateComment(text),
        SetText = static (node, text) => BrowserDomBridge.SetText(node, text),
        SetElementText = static (node, text) => PurgeListeners(BrowserDomBridge.SetElementText(node, text)),
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

    internal static void DispatchEvent(int nodeHandle, string eventName)
    {
        if (!EventListeners.TryGetValue((nodeHandle, eventName), out var listener))
        {
            return;
        }
        // No DynamicInvoke — reflection-free dispatch over the supported delegate shapes; the
        // typed event contract lands with [V01.01.04.03].
        foreach (var target in listener.GetInvocationList())
        {
            switch (target)
            {
                case Action action:
                    action();
                    break;
                case Action<object?> payloadAction:
                    payloadAction(null);
                    break;
                default:
                    throw new NotSupportedException(
                        $"Event listener for '{eventName}' is a {target.GetType().Name}; the bridge dispatches "
                        + "Action or Action<object?> listeners until [V01.01.04.03] lands the typed event contract.");
            }
        }
    }

    /// <summary>Registry sizes for leak diagnostics: JS nodes, JS listener maps, C# listener entries.</summary>
    internal static (int JsNodes, int JsListenerMaps, int DotnetListeners) GetRegistryDiagnostics()
    {
        var sizes = BrowserDomBridge.GetRegistrySizes();
        return (sizes[0], sizes[1], EventListeners.Count);
    }

    private static void PurgeListeners(int[]? releasedHandles)
    {
        // The bridge reports every handle it released; drop their listener delegates so the
        // C# side cannot leak either.
        if (releasedHandles is null || releasedHandles.Length == 0 || EventListeners.Count == 0)
        {
            return;
        }
        List<(int, string)>? stale = null;
        foreach (var key in EventListeners.Keys)
        {
            if (Array.IndexOf(releasedHandles, key.NodeHandle) >= 0)
            {
                (stale ??= []).Add(key);
            }
        }
        if (stale is not null)
        {
            foreach (var key in stale)
            {
                EventListeners.Remove(key);
            }
        }
    }
}
