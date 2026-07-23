using System;

using Assimalign.Viu;

namespace Assimalign.Viu.Browser.Tests;

/// <summary>Provides direct in-memory DOM operations for command-buffer differential tests.</summary>
internal sealed class DirectHandleDomWorld : IDisposable
{
    internal DirectHandleDomWorld(InMemoryHandleDom dom)
    {
        ArgumentNullException.ThrowIfNull(dom);
        BrowserEventInvokerRegistry invokers = new(
            (handle, eventName, once, capture, passive) =>
                dom.AddEventListener(handle, eventName, once, capture, passive),
            (handle, eventName, capture) =>
                dom.RemoveEventListener(handle, eventName, capture));
        BrowserPropertyLeafOperations leaf = new()
        {
            SetAttribute = dom.SetAttribute,
            RemoveAttribute = dom.RemoveAttribute,
            SetXlinkAttribute = dom.SetXlinkAttribute,
            RemoveXlinkAttribute = dom.RemoveXlinkAttribute,
            SetClassName = dom.SetClassName,
            SetStringProperty = dom.SetStringProperty,
            SetBooleanProperty = dom.SetBooleanProperty,
            SetValueGuarded = dom.SetValueGuarded,
            SetStyleText = dom.SetStyleText,
            SetStyleProperty = dom.SetStyleProperty,
            RemoveStyleProperty = dom.RemoveStyleProperty,
            SetEventListener = invokers.SetListener,
        };
        Options = new RendererOptions<int>
        {
            Insert = (child, parent, anchor) =>
                dom.Insert(parent, child, anchor),
            Remove = child =>
                invokers.PurgeReleasedHandles(dom.Remove(child)),
            CreateElement = dom.CreateElement,
            CreateText = dom.CreateText,
            CreateComment = dom.CreateComment,
            SetText = dom.SetText,
            ParentNode = dom.ParentNode,
            NextSibling = dom.NextSibling,
            PatchAttribute =
                (element, elementTag, attributeName, previousValue, nextValue, elementNamespace) =>
                    BrowserPropertyPatcher.Patch(
                        leaf,
                        element,
                        elementTag,
                        attributeName,
                        previousValue,
                        nextValue,
                        elementNamespace),
            SetScopeIdentifier = (element, scopeIdentifier) =>
                dom.SetAttribute(element, scopeIdentifier, string.Empty),
            ResolveTeleportTarget = static _ => default,
            InsertStaticContent = dom.InsertStaticContent,
        };
    }

    internal RendererOptions<int> Options { get; }

    public void Dispose()
    {
    }
}
