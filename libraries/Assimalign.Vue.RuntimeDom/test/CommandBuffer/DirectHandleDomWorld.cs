using System;

using Assimalign.Vue.RuntimeCore;

namespace Assimalign.Vue.RuntimeDom.Tests;

// The DIRECT-mode world for the differential test ([V01.01.04.05]): a RendererOptions<int> wired to
// an InMemoryHandleDom exactly the way production BrowserNodeOperations wires the JS bridge — an
// invoker registry for events, BrowserPropertyPatcher over leaf ops, per-op released-handle purge on
// remove/setElementText — plus the ambient BrowserDirectiveOperations (v-model/v-show) pointed at the
// same DOM for the scope's lifetime. The buffered world runs the identical scenario through the
// command buffer; byte-identical Serialize() output proves batching is behaviorally invisible.
internal sealed class DirectHandleDomWorld : IDisposable
{
    private readonly BrowserDirectiveOperations? _previousDirectiveOperations;

    internal DirectHandleDomWorld(InMemoryHandleDom dom)
    {
        var invokers = new BrowserEventInvokerRegistry(
            (handle, eventName, once, capture, passive) => dom.AddEventListener(handle, eventName, once, capture, passive),
            (handle, eventName, capture) => dom.RemoveEventListener(handle, eventName, capture));
        var leaf = new BrowserPropertyLeafOperations
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
            Insert = (child, parent, anchor) => dom.Insert(parent, child, anchor),
            Remove = child => invokers.PurgeReleasedHandles(dom.Remove(child)),
            CreateElement = dom.CreateElement,
            CreateText = dom.CreateText,
            CreateComment = dom.CreateComment,
            SetText = dom.SetText,
            SetElementText = (node, text) => invokers.PurgeReleasedHandles(dom.SetElementText(node, text)),
            ParentNode = dom.ParentNode,
            NextSibling = dom.NextSibling,
            PatchProperty = (element, elementTag, propertyName, previousValue, nextValue, elementNamespace) =>
                BrowserPropertyPatcher.Patch(leaf, element, elementTag, propertyName, previousValue, nextValue, elementNamespace),
            QuerySelector = _ => 0,
            InsertStaticContent = dom.InsertStaticContent,
        };
        _previousDirectiveOperations = BrowserDirectiveOperations.Current;
        BrowserDirectiveOperations.Current = new BrowserDirectiveOperations
        {
            SetModelListener = (element, rawPropertyName, handler) => invokers.SetModelListener(element, rawPropertyName, handler),
            SetValueGuarded = leaf.SetValueGuarded,
            SetBooleanProperty = leaf.SetBooleanProperty,
            SetStyleProperty = leaf.SetStyleProperty,
            RemoveStyleProperty = leaf.RemoveStyleProperty,
            SetCssVariables = (element, names, values) =>
            {
                for (var index = 0; index < names.Length; index++)
                {
                    leaf.SetStyleProperty(element, names[index], values[index], false);
                }
            },
        };
    }

    internal RendererOptions<int> Options { get; }

    public void Dispose() => BrowserDirectiveOperations.Current = _previousDirectiveOperations;
}
