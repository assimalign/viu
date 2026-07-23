using System;
using System.Collections.Generic;

using Assimalign.Viu;
using Assimalign.Viu.Components;
using Assimalign.Viu.Testing;

namespace Assimalign.Viu.Browser.Tests;

// Drives the redesigned Core renderer and Browser directive pipeline over in-memory integer
// handles. Browser events still pass through the production invoker registry.
internal sealed class BrowserDirectiveTestHarness : IDisposable
{
    private readonly Dictionary<int, RecordingNode> _nodes = [];
    private readonly Dictionary<int, List<int>> _children = [];
    private readonly BrowserEventInvokerRegistry _registry;
    private readonly BrowserDirectiveOperations? _previousOperations;
    private readonly Renderer<int> _renderer;
    private readonly IApplicationContext _application;
    private readonly TestSchedulerPump _schedulerPump;
    private int _nextHandle = 1;
    private int _cssVariableCrossings;

    public BrowserDirectiveTestHarness(
        IComponentFactory? componentFactory = null)
    {
        _schedulerPump = TestSchedulerPump.Install();
        _registry = new BrowserEventInvokerRegistry(
            static (_, _, _, _, _) => { },
            static (_, _, _) => { });

        Leaf = new BrowserPropertyLeafOperations
        {
            SetAttribute = SetAttribute,
            RemoveAttribute =
                (element, name) => Element(element).Attributes.Remove(name),
            SetXlinkAttribute = SetAttribute,
            RemoveXlinkAttribute =
                (element, name) => Element(element).Attributes.Remove(name),
            SetClassName =
                (element, value) => Element(element).Attributes["class"] = value,
            SetStringProperty = SetAttribute,
            SetBooleanProperty = SetBooleanProperty,
            SetValueGuarded = SetValueGuarded,
            SetStyleText =
                (element, cssText) => Element(element).Attributes["style"] = cssText,
            SetStyleProperty =
                (element, name, value, _) => Element(element).Style[name] = value,
            RemoveStyleProperty =
                (element, name) => Element(element).Style.Remove(name),
            SetEventListener = (element, rawPropertyName, listener) =>
                _registry.SetListener(element, rawPropertyName, listener),
        };

        _previousOperations = BrowserDirectiveOperations.Current;
        BrowserDirectiveOperations.Current = new BrowserDirectiveOperations
        {
            SetModelListener = (element, rawPropertyName, handler) =>
                _registry.SetModelListener(element, rawPropertyName, handler),
            SetValueGuarded = SetValueGuarded,
            SetBooleanProperty = SetBooleanProperty,
            SetStyleProperty =
                (element, name, value, _) => Element(element).Style[name] = value,
            RemoveStyleProperty =
                (element, name) => Element(element).Style.Remove(name),
            SetCssVariables = (element, names, values) =>
            {
                _cssVariableCrossings++;
                Dictionary<string, string> style = Element(element).Style;
                for (int index = 0; index < names.Length; index++)
                {
                    style[names[index]] = values[index];
                }
            },
        };

        _renderer = RendererFactory.CreateRenderer(
            new RendererOptions<int>
            {
                Insert = Insert,
                Remove = Remove,
                CreateElement = (tag, _) =>
                    Create(new RecordingNode { Tag = tag }),
                CreateText = text =>
                    Create(new RecordingNode { Tag = "#text", Value = text }),
                CreateComment = text =>
                    Create(new RecordingNode
                    {
                        Tag = "#comment",
                        Value = text,
                    }),
                SetText = (node, text) => Element(node).Value = text,
                ParentNode = node => Element(node).Parent,
                NextSibling = NextSibling,
                PatchAttribute =
                    (
                        element,
                        elementTag,
                        propertyName,
                        previousValue,
                        nextValue,
                        elementNamespace) =>
                        BrowserPropertyPatcher.Patch(
                            Leaf,
                            element,
                            elementTag,
                            propertyName,
                            previousValue,
                            nextValue,
                            elementNamespace),
            });

        Container = Create(new RecordingNode { Tag = "#container" });
        _application = new ApplicationContext(
            ComponentTree.Comment("directive-test-root"),
            componentFactory
                ?? new ComponentFactory(
                    Array.Empty<ComponentRegistration>()),
            new EmptyServiceProvider(),
            directives: BrowserDirectiveResolver.Instance);
    }

    public BrowserPropertyLeafOperations Leaf { get; }

    public int Container { get; }

    public int InvokerCount => _registry.InvokerCount;

    public IComponentContext? Render(IComponent component)
        => _renderer.Render(component, Container, _application);

    public void Unmount() => _renderer.Render(null, Container, _application);

    public int RunUntilIdle()
    {
        return _schedulerPump.RunUntilIdle();
    }

    public int FindElement(string tag)
    {
        for (int handle = 1; handle < _nextHandle; handle++)
        {
            if (_nodes.TryGetValue(handle, out RecordingNode? node)
                && string.Equals(node.Tag, tag, StringComparison.Ordinal))
            {
                return handle;
            }
        }

        throw new InvalidOperationException($"No <{tag}> was recorded.");
    }

    public IReadOnlyList<int> FindElements(string tag)
    {
        List<int> matches = [];
        for (int handle = 1; handle < _nextHandle; handle++)
        {
            if (_nodes.TryGetValue(handle, out RecordingNode? node)
                && string.Equals(node.Tag, tag, StringComparison.Ordinal))
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

    public string? Display(int handle)
        => Element(handle).Style.TryGetValue(
            "display",
            out string? display)
                ? display
                : null;

    public string? CssVariable(int handle, string name)
        => Element(handle).Style.TryGetValue(name, out string? value)
            ? value
            : null;

    public int CssVariableCrossings => _cssVariableCrossings;

    public int FireInput(int handle, string value)
    {
        SimulateUserValue(handle, value);
        return _registry.Dispatch(
            handle,
            capture: false,
            Event("input", targetValue: value));
    }

    public int FireChange(int handle, string value)
    {
        SimulateUserValue(handle, value);
        return _registry.Dispatch(
            handle,
            capture: false,
            Event("change", targetValue: value));
    }

    public int FireCheckboxChange(int handle, bool isChecked)
    {
        Element(handle).Checked = isChecked;
        return _registry.Dispatch(
            handle,
            capture: false,
            Event("change", targetChecked: isChecked));
    }

    public int FireSelectChange(
        int handle,
        string singleValue,
        string[]? selectedValues = null)
    {
        SimulateUserValue(handle, singleValue);
        return _registry.Dispatch(
            handle,
            capture: false,
            Event(
                "change",
                targetValue: singleValue,
                selectedValues: selectedValues));
    }

    public int FireClick(int handle)
        => _registry.Dispatch(
            handle,
            capture: false,
            Event("click"));

    public int FireCompositionStart(int handle)
        => _registry.Dispatch(
            handle,
            capture: false,
            Event("compositionstart"));

    public int FireCompositionEnd(int handle, string value)
    {
        SimulateUserValue(handle, value);
        return _registry.Dispatch(
            handle,
            capture: false,
            Event("compositionend", targetValue: value));
    }

    public void FireFocus(int handle)
        => _registry.Dispatch(
            handle,
            capture: false,
            Event("focus"));

    public void FireBlur(int handle)
        => _registry.Dispatch(
            handle,
            capture: false,
            Event("blur"));

    public void Dispose()
    {
        Unmount();
        _schedulerPump.RunUntilIdle();
        BrowserDirectiveOperations.Current = _previousOperations;
        _schedulerPump.Dispose();
    }

    private static BrowserEvent Event(
        string eventName,
        string? targetValue = null,
        bool targetChecked = false,
        string[]? selectedValues = null)
        => new(
            eventName,
            1000,
            string.Empty,
            string.Empty,
            BrowserEventModifiers.None,
            -1,
            0,
            0,
            0,
            0,
            true,
            targetValue,
            targetChecked,
            selectedValues);

    private void SimulateUserValue(int handle, string value)
        => Element(handle).Value = value;

    private int Create(RecordingNode node)
    {
        int handle = _nextHandle++;
        _nodes[handle] = node;
        return handle;
    }

    private RecordingNode Element(int handle) => _nodes[handle];

    private void Insert(int child, int parent, int anchor)
    {
        RecordingNode childNode = Element(child);
        if (childNode.Parent != 0
            && _children.TryGetValue(
                childNode.Parent,
                out List<int>? previousSiblings))
        {
            previousSiblings.Remove(child);
        }

        childNode.Parent = parent;
        List<int> siblings = _children.TryGetValue(
            parent,
            out List<int>? existing)
                ? existing
                : _children[parent] = [];
        int anchorIndex = anchor == 0 ? -1 : siblings.IndexOf(anchor);
        if (anchorIndex < 0)
        {
            siblings.Add(child);
        }
        else
        {
            siblings.Insert(anchorIndex, child);
        }
    }

    private void Remove(int child)
    {
        int parent = Element(child).Parent;
        List<int> released = [];
        CollectSubtree(child, released);
        _registry.PurgeReleasedHandles(released.ToArray());
        foreach (int handle in released)
        {
            _nodes.Remove(handle);
            _children.Remove(handle);
        }

        if (_children.TryGetValue(parent, out List<int>? siblings))
        {
            siblings.Remove(child);
        }
    }

    private void CollectSubtree(int handle, List<int> released)
    {
        released.Add(handle);
        if (!_children.TryGetValue(handle, out List<int>? childHandles))
        {
            return;
        }

        foreach (int child in childHandles)
        {
            CollectSubtree(child, released);
        }
    }

    private int NextSibling(int node)
    {
        int parent = Element(node).Parent;
        if (!_children.TryGetValue(parent, out List<int>? siblings))
        {
            return 0;
        }

        int index = siblings.IndexOf(node);
        return index >= 0 && index + 1 < siblings.Count
            ? siblings[index + 1]
            : 0;
    }

    private void SetAttribute(int handle, string name, string value)
        => Element(handle).Attributes[name] = value;

    private void SetBooleanProperty(int handle, string name, bool value)
    {
        RecordingNode node = Element(handle);
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
        RecordingNode node = Element(handle);
        if (string.Equals(node.Value, value, StringComparison.Ordinal))
        {
            return;
        }

        node.Value = value;
        node.ValueWriteCount++;
    }

    private sealed class EmptyServiceProvider : IServiceProvider
    {
        public object? GetService(Type serviceType)
        {
            ArgumentNullException.ThrowIfNull(serviceType);
            return null;
        }
    }

    private sealed class RecordingNode
    {
        public string Tag = string.Empty;
        public string Value = string.Empty;
        public bool Checked;
        public bool Selected;
        public int Parent;
        public int ValueWriteCount;
        public readonly Dictionary<string, string> Attributes =
            new(StringComparer.Ordinal);
        public readonly Dictionary<string, string> Style =
            new(StringComparer.Ordinal);
    }
}
