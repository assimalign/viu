using System;
using System.Collections.Generic;
using System.Globalization;
using System.Runtime.InteropServices.JavaScript;

using Assimalign.Vue.RuntimeCore;

// The example-level browser node-ops: RendererOptions<int> over integer DOM handles
// (0 = the "no node" sentinel). The production RuntimeDom bridge — deterministic handle
// disposal, typed interop errors, the full patchProp decision tree — lands with
// [V01.01.04.01] and [V01.01.04.02]; this adapter keeps the demo honest until then.
internal static class BrowserRendererOptions
{
    private static readonly Dictionary<(int NodeHandle, string EventName), Action> EventCallbacks = [];

    public static RendererOptions<int> Create() => new()
    {
        Insert = static (child, parent, anchor) => BrowserDomInterop.Insert(parent, child, anchor),
        Remove = static child =>
        {
            BrowserDomInterop.Remove(child);
            // JS swept the DOM-side listeners; drop this handle's callback entries too.
            RemoveCallbacksForNode(child);
        },
        CreateElement = static (tag, elementNamespace) => BrowserDomInterop.CreateElement(tag, elementNamespace),
        CreateText = static text => BrowserDomInterop.CreateText(text),
        CreateComment = static text => BrowserDomInterop.CreateComment(text),
        SetText = static (node, text) => BrowserDomInterop.SetText(node, text),
        SetElementText = static (node, text) => BrowserDomInterop.SetElementText(node, text),
        ParentNode = static node => BrowserDomInterop.ParentNode(node),
        NextSibling = static node => BrowserDomInterop.NextSibling(node),
        PatchProperty = static (element, name, _, nextValue, _) => PatchProperty(element, name, nextValue),
        QuerySelector = static selector => BrowserDomInterop.QuerySelector(selector),
    };

    public static void Dispatch(int nodeHandle, string eventName)
    {
        if (EventCallbacks.TryGetValue((nodeHandle, eventName), out var callback))
        {
            callback();
        }
    }

    private static void PatchProperty(int element, string name, object? nextValue)
    {
        if (VirtualNodeFactory.IsEventListenerName(name))
        {
            // onClick -> "click"; the JS listener dispatches back by (handle, event name).
            var eventName = name[2..].ToLowerInvariant();
            if (nextValue is Action callback)
            {
                EventCallbacks[(element, eventName)] = callback;
                BrowserDomInterop.SetEventListener(element, eventName);
            }
            else
            {
                EventCallbacks.Remove((element, eventName));
                BrowserDomInterop.RemoveEventListener(element, eventName);
            }
            return;
        }
        switch (nextValue)
        {
            case null or false:
                BrowserDomInterop.RemoveProperty(element, name);
                break;
            case true:
                BrowserDomInterop.SetBooleanProperty(element, name, true);
                break;
            default:
                BrowserDomInterop.SetProperty(element, name, FormatValue(nextValue));
                break;
        }
    }

    private static void RemoveCallbacksForNode(int nodeHandle)
    {
        List<(int, string)>? stale = null;
        foreach (var entry in EventCallbacks.Keys)
        {
            if (entry.NodeHandle == nodeHandle)
            {
                (stale ??= []).Add(entry);
            }
        }
        if (stale is not null)
        {
            foreach (var entry in stale)
            {
                EventCallbacks.Remove(entry);
            }
        }
    }

    private static string FormatValue(object value) => value switch
    {
        string text => text,
        IFormattable formattable => formattable.ToString(null, CultureInfo.InvariantCulture),
        _ => value.ToString() ?? string.Empty,
    };
}

internal static partial class BrowserDomInterop
{
    [JSImport("dom.querySelector", "main.js")]
    internal static partial int QuerySelector(string selector);

    [JSImport("dom.createElement", "main.js")]
    internal static partial int CreateElement(string tagName, string? namespaceName);

    [JSImport("dom.createText", "main.js")]
    internal static partial int CreateText(string textContent);

    [JSImport("dom.createComment", "main.js")]
    internal static partial int CreateComment(string textContent);

    [JSImport("dom.setText", "main.js")]
    internal static partial void SetText(int nodeHandle, string textContent);

    [JSImport("dom.setElementText", "main.js")]
    internal static partial void SetElementText(int nodeHandle, string textContent);

    [JSImport("dom.insert", "main.js")]
    internal static partial void Insert(int parentHandle, int childHandle, int anchorHandle);

    [JSImport("dom.remove", "main.js")]
    internal static partial void Remove(int childHandle);

    [JSImport("dom.parentNode", "main.js")]
    internal static partial int ParentNode(int nodeHandle);

    [JSImport("dom.nextSibling", "main.js")]
    internal static partial int NextSibling(int nodeHandle);

    [JSImport("dom.setProperty", "main.js")]
    internal static partial void SetProperty(int nodeHandle, string name, string value);

    [JSImport("dom.setBooleanProperty", "main.js")]
    internal static partial void SetBooleanProperty(int nodeHandle, string name, bool value);

    [JSImport("dom.removeProperty", "main.js")]
    internal static partial void RemoveProperty(int nodeHandle, string name);

    [JSImport("dom.setEventListener", "main.js")]
    internal static partial void SetEventListener(int nodeHandle, string eventName);

    [JSImport("dom.removeEventListener", "main.js")]
    internal static partial void RemoveEventListener(int nodeHandle, string eventName);
}
