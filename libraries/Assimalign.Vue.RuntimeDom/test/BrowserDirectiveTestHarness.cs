using System;
using System.Collections.Generic;

using Assimalign.Vue.RuntimeCore;
using Assimalign.Vue.Testing;

namespace Assimalign.Vue.RuntimeDom.Tests;

// A DOM-free harness that drives the real RuntimeCore renderer + directive pipeline over int node
// handles (as the browser does), recording element state in memory and routing events through the
// real BrowserEventInvokerRegistry. It installs a recording BrowserDirectiveOperations so v-model /
// v-show exercise their whole production path with no browser, WASM toolchain, or JS interop. The
// browser-only remainder (real IME composition, real focus, actual selectedOptions reads) is
// deferred to the e2e harness ([V01.01.11.03]).
internal sealed class BrowserDirectiveTestHarness : IDisposable
{
    private readonly Dictionary<int, RecordingNode> _nodes = [];
    private readonly Dictionary<int, List<int>> _children = [];
    private readonly BrowserEventInvokerRegistry _registry;
    private readonly BrowserDirectiveOperations? _previousOperations;
    private readonly TestSchedulerPump _pump;
    private readonly Renderer<int> _renderer;
    private int _nextHandle = 1;

    public BrowserDirectiveTestHarness()
    {
        Scheduler.Reset();
        _pump = TestSchedulerPump.Install();
        _registry = new BrowserEventInvokerRegistry(
            static (_, _, _, _, _) => { },
            static (_, _, _) => { });

        Leaf = new BrowserPropertyLeafOperations
        {
            SetAttribute = SetAttribute,
            RemoveAttribute = (element, name) => Element(element).Attributes.Remove(name),
            SetXlinkAttribute = SetAttribute,
            RemoveXlinkAttribute = (element, name) => Element(element).Attributes.Remove(name),
            SetClassName = (element, value) => Element(element).Attributes["class"] = value,
            SetStringProperty = SetAttribute,
            SetBooleanProperty = SetBooleanProperty,
            SetValueGuarded = SetValueGuarded,
            SetStyleText = (element, cssText) => Element(element).Attributes["style"] = cssText,
            SetStyleProperty = (element, name, value, _) => Element(element).Style[name] = value,
            RemoveStyleProperty = (element, name) => Element(element).Style.Remove(name),
            SetEventListener = (element, rawPropertyName, listener) => _registry.SetListener(element, rawPropertyName, listener),
        };

        _previousOperations = BrowserDirectiveOperations.Current;
        BrowserDirectiveOperations.Current = new BrowserDirectiveOperations
        {
            SetModelListener = (element, rawPropertyName, handler) => _registry.SetModelListener(element, rawPropertyName, handler),
            SetValueGuarded = SetValueGuarded,
            SetBooleanProperty = SetBooleanProperty,
            SetStyleProperty = (element, name, value, _) => Element(element).Style[name] = value,
            RemoveStyleProperty = (element, name) => Element(element).Style.Remove(name),
        };

        _renderer = RendererFactory.CreateRenderer(new RendererOptions<int>
        {
            Insert = Insert,
            Remove = Remove,
            CreateElement = (tag, _) => Create(new RecordingNode { Tag = tag }),
            CreateText = text => Create(new RecordingNode { Tag = "#text", Value = text }),
            CreateComment = text => Create(new RecordingNode { Tag = "#comment", Value = text }),
            SetText = (node, text) => Element(node).Value = text,
            SetElementText = (node, text) => Element(node).Value = text,
            ParentNode = node => Element(node).Parent,
            NextSibling = _ => 0,
            PatchProperty = (element, elementTag, propertyName, previousValue, nextValue, elementNamespace) =>
                BrowserPropertyPatcher.Patch(Leaf, element, elementTag, propertyName, previousValue, nextValue, elementNamespace),
        });

        Container = Create(new RecordingNode { Tag = "#container" });
    }

    /// <summary>The leaf operations the renderer's patchProp records through.</summary>
    public BrowserPropertyLeafOperations Leaf { get; }

    /// <summary>The container handle to render into.</summary>
    public int Container { get; }

    /// <summary>The live invoker count (for listener-cleanup assertions).</summary>
    public int InvokerCount => _registry.InvokerCount;

    /// <summary>Renders a component's tree into the container.</summary>
    public void Render(IComponentDefinition component)
        => _renderer.Render(VirtualNodeFactory.Component(component), Container);

    /// <summary>Unmounts everything from the container.</summary>
    public void Unmount() => _renderer.Render(null, Container);

    /// <summary>Runs captured scheduler flushes until idle (post-flush mounted/updated hooks).</summary>
    public int RunUntilIdle() => _pump.RunUntilIdle();

    /// <summary>The handle of the first recorded element with <paramref name="tag"/>, in creation order.</summary>
    public int FindElement(string tag)
    {
        for (var handle = 1; handle < _nextHandle; handle++)
        {
            if (_nodes.TryGetValue(handle, out var node) && string.Equals(node.Tag, tag, StringComparison.Ordinal))
            {
                return handle;
            }
        }
        throw new InvalidOperationException($"No <{tag}> was recorded.");
    }

    /// <summary>Every recorded element handle with <paramref name="tag"/>, in creation order.</summary>
    public IReadOnlyList<int> FindElements(string tag)
    {
        var matches = new List<int>();
        for (var handle = 1; handle < _nextHandle; handle++)
        {
            if (_nodes.TryGetValue(handle, out var node) && string.Equals(node.Tag, tag, StringComparison.Ordinal))
            {
                matches.Add(handle);
            }
        }
        return matches;
    }

    public string Value(int handle) => Element(handle).Value;

    public bool Checked(int handle) => Element(handle).Checked;

    public bool Selected(int handle) => Element(handle).Selected;

    public int ValueWriteCount(int handle) => Element(handle).ValueWriteCount;

    /// <summary>The element's inline <c>display</c>, or null when there is no inline display.</summary>
    public string? Display(int handle)
        => Element(handle).Style.TryGetValue("display", out var display) ? display : null;

    // --- event firing (routes through the real registry: property + model channels) ------------

    public int FireInput(int handle, string value)
    {
        SimulateUserValue(handle, value); // the browser sets el.value before firing 'input'
        return _registry.Dispatch(handle, capture: false, Event("input", targetValue: value));
    }

    public int FireChange(int handle, string value)
    {
        SimulateUserValue(handle, value);
        return _registry.Dispatch(handle, capture: false, Event("change", targetValue: value));
    }

    public int FireCheckboxChange(int handle, bool isChecked)
    {
        Element(handle).Checked = isChecked; // the browser toggles el.checked before firing 'change'
        return _registry.Dispatch(handle, capture: false, Event("change", targetChecked: isChecked));
    }

    public int FireSelectChange(int handle, string singleValue, string[]? selectedValues = null)
    {
        SimulateUserValue(handle, singleValue);
        return _registry.Dispatch(handle, capture: false, Event("change", targetValue: singleValue, selectedValues: selectedValues));
    }

    /// <summary>
    /// Dispatches a plain <c>click</c> to the element's property-channel handlers, returning the response
    /// flags the bridge would apply to the live event (bit 0 <c>stopPropagation</c>, bit 1
    /// <c>preventDefault</c>) — the seam a <c>@click.stop</c>/<c>.prevent</c> modifier handler records intents on.
    /// </summary>
    public int FireClick(int handle)
        => _registry.Dispatch(handle, capture: false, Event("click"));

    public int FireCompositionStart(int handle)
        => _registry.Dispatch(handle, capture: false, Event("compositionstart"));

    public int FireCompositionEnd(int handle, string value)
    {
        SimulateUserValue(handle, value);
        return _registry.Dispatch(handle, capture: false, Event("compositionend", targetValue: value));
    }

    // Simulate a user edit landing in the DOM (does not count as a directive write).
    private void SimulateUserValue(int handle, string value) => Element(handle).Value = value;

    public void FireFocus(int handle) => _registry.Dispatch(handle, capture: false, Event("focus"));

    public void FireBlur(int handle) => _registry.Dispatch(handle, capture: false, Event("blur"));

    private static BrowserEvent Event(string eventName, string? targetValue = null, bool targetChecked = false, string[]? selectedValues = null)
        => new(eventName, 1000, string.Empty, string.Empty, BrowserEventModifiers.None, -1, 0, 0, 0, 0, true, targetValue, targetChecked, selectedValues);

    // --- recording node-ops --------------------------------------------------------------------

    private int Create(RecordingNode node)
    {
        var handle = _nextHandle++;
        _nodes[handle] = node;
        return handle;
    }

    private RecordingNode Element(int handle) => _nodes[handle];

    private void Insert(int child, int parent, int anchor)
    {
        Element(child).Parent = parent;
        (_children.TryGetValue(parent, out var list) ? list : _children[parent] = []).Add(child);
    }

    private void Remove(int child)
    {
        var parent = Element(child).Parent;
        var released = new List<int>();
        CollectSubtree(child, released);
        _registry.PurgeReleasedHandles(released.ToArray());
        foreach (var handle in released)
        {
            _nodes.Remove(handle);
            _children.Remove(handle);
        }
        if (_children.TryGetValue(parent, out var siblings))
        {
            siblings.Remove(child);
        }
    }

    private void CollectSubtree(int handle, List<int> released)
    {
        released.Add(handle);
        if (_children.TryGetValue(handle, out var childHandles))
        {
            foreach (var child in childHandles)
            {
                CollectSubtree(child, released);
            }
        }
    }

    private void SetAttribute(int handle, string name, string value) => Element(handle).Attributes[name] = value;

    private void SetBooleanProperty(int handle, string name, bool value)
    {
        var node = Element(handle);
        if (string.Equals(name, "checked", StringComparison.Ordinal))
        {
            node.Checked = value;
        }
        else if (string.Equals(name, "selected", StringComparison.Ordinal))
        {
            node.Selected = value;
        }
        else
        {
            node.Attributes[name] = value ? "true" : "false";
        }
    }

    private void SetValueGuarded(int handle, string value)
    {
        // Mirror the JS compare-and-set so tests can assert caret-safe no-writes.
        var node = Element(handle);
        if (!string.Equals(node.Value, value, StringComparison.Ordinal))
        {
            node.Value = value;
            node.ValueWriteCount++;
        }
    }

    public void Dispose()
    {
        BrowserDirectiveOperations.Current = _previousOperations;
        _pump.Dispose();
        Scheduler.Reset();
    }

    private sealed class RecordingNode
    {
        public string Tag = string.Empty;
        public string Value = string.Empty;
        public bool Checked;
        public bool Selected;
        public int Parent;
        public int ValueWriteCount;
        public readonly Dictionary<string, string> Attributes = new(StringComparer.Ordinal);
        public readonly Dictionary<string, string> Style = new(StringComparer.Ordinal);
    }
}
