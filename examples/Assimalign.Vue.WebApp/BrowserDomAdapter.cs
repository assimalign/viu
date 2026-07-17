using System;
using System.Collections.Generic;
using System.Globalization;
using System.Runtime.InteropServices.JavaScript;
using Assimalign.Vue.RuntimeCore;

internal sealed class BrowserDomAdapter : IVirtualDomAdapter<int>
{
    private readonly Dictionary<(int NodeHandle, string EventName), string> _eventBindings = new();
    private readonly Dictionary<string, VirtualEventHandler> _callbacks = new(StringComparer.Ordinal);

    public int CreateElement(string tagName) => BrowserDomInterop.CreateElement(tagName);

    public int CreateText(string textContent) => BrowserDomInterop.CreateText(textContent);

    public int CreateComment(string textContent) => BrowserDomInterop.CreateComment(textContent);

    public void SetText(int node, string textContent) => BrowserDomInterop.SetText(node, textContent);

    public void SetProperty(int node, string name, object? value)
    {
        if (value is null || value is false)
        {
            RemoveProperty(node, name);
            return;
        }

        if (TryGetEventProperty(name, value, out var eventName, out var handler))
        {
            SetEventListener(node, eventName, handler);
            return;
        }

        if (value is true)
        {
            BrowserDomInterop.SetBooleanProperty(node, NormalizePropertyName(name), true);
            return;
        }

        BrowserDomInterop.SetProperty(node, NormalizePropertyName(name), FormatValue(value));
    }

    public void RemoveProperty(int node, string name)
    {
        if (TryGetEventName(name, out var eventName))
        {
            RemoveEventListener(node, eventName);
            return;
        }

        BrowserDomInterop.RemoveProperty(node, NormalizePropertyName(name));
    }

    public void AppendChild(int parent, int child) => BrowserDomInterop.AppendChild(parent, child);

    public void InsertBefore(int parent, int child, int beforeChild) => BrowserDomInterop.InsertBefore(parent, child, beforeChild);

    public void RemoveChild(int parent, int child) => BrowserDomInterop.RemoveChild(parent, child);

    public void ClearChildren(int parent) => BrowserDomInterop.ClearChildren(parent);

    public void DestroyNode(int node)
    {
        CleanupEventBindings(node);
        BrowserDomInterop.DestroyNode(node);
    }

    public void Dispatch(string callbackId)
    {
        if (_callbacks.TryGetValue(callbackId, out var callback))
        {
            callback.Invoke();
        }
    }

    private static string FormatValue(object value)
    {
        return value switch
        {
            string text => text,
            IFormattable formattable => formattable.ToString(null, CultureInfo.InvariantCulture),
            _ => value.ToString() ?? string.Empty
        };
    }

    private static string NormalizePropertyName(string name)
    {
        return string.Equals(name, "className", StringComparison.Ordinal)
            ? "class"
            : name;
    }

    private static bool TryGetEventProperty(string name, object? value, out string eventName, out VirtualEventHandler handler)
    {
        if (value is VirtualEventHandler eventHandler && TryGetEventName(name, out eventName))
        {
            handler = eventHandler;
            return true;
        }

        eventName = string.Empty;
        handler = null!;
        return false;
    }

    private static bool TryGetEventName(string name, out string eventName)
    {
        if (name.StartsWith("on", StringComparison.Ordinal) && name.Length > 2)
        {
            var suffix = name.Substring(2);
            eventName = char.ToLowerInvariant(suffix[0]) + suffix.Substring(1);
            return true;
        }

        eventName = string.Empty;
        return false;
    }

    private void SetEventListener(int node, string eventName, VirtualEventHandler handler)
    {
        var key = (node, eventName);
        if (_eventBindings.TryGetValue(key, out var previousCallbackId))
        {
            _callbacks.Remove(previousCallbackId);
        }

        var callbackId = Guid.NewGuid().ToString("N");
        _callbacks[callbackId] = handler;
        _eventBindings[key] = callbackId;
        BrowserDomInterop.SetEventListener(node, eventName, callbackId);
    }

    private void RemoveEventListener(int node, string eventName)
    {
        var key = (node, eventName);
        if (_eventBindings.TryGetValue(key, out var callbackId))
        {
            _eventBindings.Remove(key);
            _callbacks.Remove(callbackId);
        }

        BrowserDomInterop.RemoveEventListener(node, eventName);
    }

    private void CleanupEventBindings(int node)
    {
        var keysToRemove = new List<(int NodeHandle, string EventName)>();

        foreach (var entry in _eventBindings)
        {
            if (entry.Key.NodeHandle == node)
            {
                BrowserDomInterop.RemoveEventListener(node, entry.Key.EventName);
                _callbacks.Remove(entry.Value);
                keysToRemove.Add(entry.Key);
            }
        }

        foreach (var key in keysToRemove)
        {
            _eventBindings.Remove(key);
        }
    }
}

internal static partial class BrowserDomInterop
{
    [JSImport("dom.querySelector", "main.js")]
    internal static partial int QuerySelector(string selector);

    [JSImport("dom.createElement", "main.js")]
    internal static partial int CreateElement(string tagName);

    [JSImport("dom.createText", "main.js")]
    internal static partial int CreateText(string textContent);

    [JSImport("dom.createComment", "main.js")]
    internal static partial int CreateComment(string textContent);

    [JSImport("dom.setText", "main.js")]
    internal static partial void SetText(int nodeHandle, string textContent);

    [JSImport("dom.setProperty", "main.js")]
    internal static partial void SetProperty(int nodeHandle, string name, string value);

    [JSImport("dom.setBooleanProperty", "main.js")]
    internal static partial void SetBooleanProperty(int nodeHandle, string name, bool value);

    [JSImport("dom.removeProperty", "main.js")]
    internal static partial void RemoveProperty(int nodeHandle, string name);

    [JSImport("dom.appendChild", "main.js")]
    internal static partial void AppendChild(int parentHandle, int childHandle);

    [JSImport("dom.insertBefore", "main.js")]
    internal static partial void InsertBefore(int parentHandle, int childHandle, int beforeChildHandle);

    [JSImport("dom.removeChild", "main.js")]
    internal static partial void RemoveChild(int parentHandle, int childHandle);

    [JSImport("dom.clearChildren", "main.js")]
    internal static partial void ClearChildren(int nodeHandle);

    [JSImport("dom.destroyNode", "main.js")]
    internal static partial void DestroyNode(int nodeHandle);

    [JSImport("dom.setEventListener", "main.js")]
    internal static partial void SetEventListener(int nodeHandle, string eventName, string callbackId);

    [JSImport("dom.removeEventListener", "main.js")]
    internal static partial void RemoveEventListener(int nodeHandle, string eventName);
}
