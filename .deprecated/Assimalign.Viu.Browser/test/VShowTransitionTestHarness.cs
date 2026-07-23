using System;
using System.Collections.Generic;

using Assimalign.Viu;
using Assimalign.Viu.Testing;

namespace Assimalign.Viu.Browser.Tests;

// A DOM-free harness for the PERSISTED v-show transition path ([V01.01.04.07.01]): it drives the real
// Core renderer + the real DOM <Transition> + the real v-show directive over int node handles,
// installing BOTH a recording DomTransitionOperations (the CSS class choreography) AND a recording
// BrowserDirectiveOperations (the v-show display toggle), so a <Transition persisted> wrapping a v-show
// <div> exercises its whole production path with no browser. It is TransitionTestHarness merged with the
// v-show half of BrowserDirectiveTestHarness — the two seams write to one per-element record, so a test
// can pin the class sequence, the reflow count, and the display-toggle sequence together. Upstream
// contract: @vue/runtime-dom directives/vShow.ts persisted handling + components/Transition.ts
// (https://github.com/vuejs/core/blob/main/packages/runtime-dom/src/directives/vShow.ts).
internal sealed class VShowTransitionTestHarness : IDisposable
{
    // The sentinel a v-show reveal with no saved inline display logs (RemoveStyleProperty -> "no inline
    // display", i.e. visible). Distinct from "none" (hidden) and a concrete value like "flex".
    public const string DisplayRemoved = "";

    private readonly Dictionary<int, RecordingNode> _nodes = [];
    private readonly Dictionary<int, List<int>> _children = [];
    private readonly Renderer<int> _renderer;
    private readonly TestSchedulerPump _pump;
    private readonly DomTransitionOperations? _previousTransitionOperations;
    private readonly BrowserDirectiveOperations? _previousDirectiveOperations;
    private readonly List<Action> _nextFrameQueue = [];
    private readonly Dictionary<int, Action> _endResolvers = [];
    private int _nextHandle = 1;

    public VShowTransitionTestHarness()
    {
        Scheduler.Reset();
        _pump = TestSchedulerPump.Install();

        _previousTransitionOperations = DomTransitionOperations.Current;
        DomTransitionOperations.Current = new DomTransitionOperations
        {
            AddTransitionClass = (element, cssClass) =>
            {
                ClassLog.Add($"add:{cssClass}");
                Classes(element).Add(cssClass);
            },
            RemoveTransitionClass = (element, cssClass) =>
            {
                ClassLog.Add($"remove:{cssClass}");
                Classes(element).Remove(cssClass);
            },
            NextFrame = _nextFrameQueue.Add,
            ForceReflow = () => ReflowCount++,
            WhenTransitionEnds = (element, _, _, resolve) => _endResolvers[element] = resolve,
            // FLIP ops are unused by <Transition>; stub them (TransitionGroup has its own coverage).
            MeasurePositions = handles => new TransitionRectangle[handles.Length],
            SetMoveTransform = (_, _, _) => { },
            ClearMoveStyles = _ => { },
            HasCssTransform = (_, _, _) => false,
            WhenMoveEnds = (_, resolve) => resolve(),
        };

        _previousDirectiveOperations = BrowserDirectiveOperations.Current;
        BrowserDirectiveOperations.Current = new BrowserDirectiveOperations
        {
            // v-model channels are unused by v-show; stub them.
            SetModelListener = (_, _, _) => { },
            SetValueGuarded = (_, _) => { },
            SetBooleanProperty = (_, _, _) => { },
            SetCssVariables = (_, _, _) => { },
            SetStyleProperty = (element, name, value, _) =>
            {
                if (string.Equals(name, "display", StringComparison.Ordinal))
                {
                    DisplayLog.Add(value);
                    Node(element).Display = value;
                }
            },
            RemoveStyleProperty = (element, name) =>
            {
                if (string.Equals(name, "display", StringComparison.Ordinal))
                {
                    DisplayLog.Add(DisplayRemoved);
                    Node(element).Display = null;
                }
            },
        };

        _renderer = RendererFactory.CreateRenderer(new RendererOptions<int>
        {
            Insert = Insert,
            Remove = Remove,
            CreateElement = (tag, _) => Create(new RecordingNode { Tag = tag }),
            CreateText = text => Create(new RecordingNode { Tag = "#text", Value = text }),
            CreateComment = text => Create(new RecordingNode { Tag = "#comment", Value = text }),
            SetText = (node, text) => Node(node).Value = text,
            SetElementText = (node, text) => Node(node).Value = text,
            ParentNode = node => Node(node).Parent,
            NextSibling = NextSibling,
            // v-show writes display through BrowserDirectiveOperations, not patchProp; no other bound
            // prop matters to these tests, so the patch leaf is a no-op.
            PatchProperty = (_, _, _, _, _, _) => { },
        });

        Container = Create(new RecordingNode { Tag = "#container" });
    }

    /// <summary>The ordered transition class add/remove operations (the choreography sequence).</summary>
    public List<string> ClassLog { get; } = [];

    /// <summary>
    /// The ordered v-show display writes: a concrete value (<c>"none"</c>/<c>"flex"</c>…) for a
    /// <c>SetStyleProperty</c>, or <see cref="DisplayRemoved"/> for a <c>RemoveStyleProperty</c> (reveal
    /// with no saved inline display). Pins the run count of display toggles across an interrupted transition.
    /// </summary>
    public List<string> DisplayLog { get; } = [];

    /// <summary>The number of forced reflows recorded (the leave barrier).</summary>
    public int ReflowCount { get; private set; }

    /// <summary>The container handle to render into.</summary>
    public int Container { get; }

    public void Dispose()
    {
        DomTransitionOperations.Current = _previousTransitionOperations;
        BrowserDirectiveOperations.Current = _previousDirectiveOperations;
        _pump.Dispose();
        Scheduler.Reset();
    }

    /// <summary>Renders a component's tree into the container and drains scheduled flushes.</summary>
    public void Render(IComponent component)
    {
        _renderer.Render(VirtualNodeFactory.Component(component), Container);
        _pump.RunUntilIdle();
    }

    /// <summary>Runs captured scheduler flushes until idle (post-flush mounted/updated hooks).</summary>
    public void RunUntilIdle() => _pump.RunUntilIdle();

    /// <summary>Runs every queued next-frame callback (the deterministic stand-in for the double rAF).</summary>
    public void AdvanceFrame()
    {
        var pending = new List<Action>(_nextFrameQueue);
        _nextFrameQueue.Clear();
        foreach (var callback in pending)
        {
            callback();
        }
    }

    /// <summary>Fires the transition-end for an element, completing its in-flight enter/leave.</summary>
    public void FireTransitionEnd(int element)
    {
        if (_endResolvers.Remove(element, out var resolve))
        {
            resolve();
        }
    }

    /// <summary>The transition classes currently applied to an element.</summary>
    public HashSet<string> Classes(int element)
        => _nodes.TryGetValue(element, out var node) ? node.TransitionClasses : [];

    /// <summary>The element's inline <c>display</c>, or null when there is no inline display (visible).</summary>
    public string? Display(int element) => _nodes.TryGetValue(element, out var node) ? node.Display : null;

    /// <summary>Whether an element currently has a pending in-flight transition end (enter or leave).</summary>
    public bool HasPendingEnd(int element) => _endResolvers.ContainsKey(element);

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

    /// <summary>Whether the element handle is still registered (not yet removed from the host).</summary>
    public bool IsMounted(int element) => _nodes.ContainsKey(element);

    // --- recording node-ops --------------------------------------------------------------------

    private int Create(RecordingNode node)
    {
        var handle = _nextHandle++;
        _nodes[handle] = node;
        return handle;
    }

    private RecordingNode Node(int handle) => _nodes[handle];

    private void Insert(int child, int parent, int anchor)
    {
        _nodes[child].Parent = parent;
        var siblings = _children.TryGetValue(parent, out var list) ? list : _children[parent] = [];
        siblings.Remove(child);
        if (anchor != 0 && siblings.IndexOf(anchor) is var index and >= 0)
        {
            siblings.Insert(index, child);
        }
        else
        {
            siblings.Add(child);
        }
    }

    private void Remove(int child)
    {
        var parent = _nodes.TryGetValue(child, out var node) ? node.Parent : 0;
        var released = new List<int>();
        CollectSubtree(child, released);
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

    private int NextSibling(int node)
    {
        var parent = _nodes.TryGetValue(node, out var recording) ? recording.Parent : 0;
        if (parent == 0 || !_children.TryGetValue(parent, out var siblings))
        {
            return 0;
        }
        var index = siblings.IndexOf(node);
        return index >= 0 && index + 1 < siblings.Count ? siblings[index + 1] : 0;
    }

    private void CollectSubtree(int handle, List<int> released)
    {
        released.Add(handle);
        if (_children.TryGetValue(handle, out var childHandles))
        {
            foreach (var child in new List<int>(childHandles))
            {
                CollectSubtree(child, released);
            }
        }
    }

    private sealed class RecordingNode
    {
        public string Tag = string.Empty;
        public string Value = string.Empty;
        public int Parent;
        // The recorded inline display: null when there is no inline display (visible), else the last
        // value written ("none"/"flex"/…). Written by the v-show BrowserDirectiveOperations seam.
        public string? Display;
        public readonly HashSet<string> TransitionClasses = new(StringComparer.Ordinal);
    }
}
