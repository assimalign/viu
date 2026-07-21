using System;
using System.Collections.Generic;

using Assimalign.Viu;
using Assimalign.Viu.Testing;

namespace Assimalign.Viu.Browser.Tests;

// A DOM-free harness that drives the real Core renderer + the real DOM <Transition>/
// <TransitionGroup> components over int node handles, with a recording DomTransitionOperations that
// the test advances deterministically. It mirrors how upstream tests transitions without a browser
// (packages/runtime-dom/__tests__/components/Transition* use jsdom + synthetic transitionend); here
// the class add/remove, next-frame schedule, and end-detection are all recorded/driven in memory.
internal sealed class TransitionTestHarness : IDisposable
{
    private readonly Dictionary<int, RecordingNode> _nodes = [];
    private readonly Dictionary<int, List<int>> _children = [];
    private readonly Renderer<int> _renderer;
    private readonly TestSchedulerPump _pump;
    private readonly DomTransitionOperations? _previousOperations;
    private readonly List<Action> _nextFrameQueue = [];
    private readonly Dictionary<int, Action> _endResolvers = [];
    private readonly Dictionary<int, Action> _moveResolvers = [];
    private readonly Dictionary<int, Queue<TransitionRectangle>> _positions = [];
    private int _nextHandle = 1;

    public TransitionTestHarness()
    {
        Scheduler.Reset();
        _pump = TestSchedulerPump.Install();

        _previousOperations = DomTransitionOperations.Current;
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
            NextFrame = callback => _nextFrameQueue.Add(callback),
            ForceReflow = () => ReflowCount++,
            WhenTransitionEnds = (element, _, _, resolve) => _endResolvers[element] = resolve,
            MeasurePositions = handles =>
            {
                // One batched read crossing per FLIP pass ([V01.01.04.07.03]); the interop-call-count
                // criterion counts these — a reorder of N children must not cost N crossings.
                MeasurePositionsCallCount++;
                MeasuredBatchSizes.Add(handles.Length);
                var result = new TransitionRectangle[handles.Length];
                for (var index = 0; index < handles.Length; index++)
                {
                    var element = handles[index];
                    MeasureLog.Add(element);
                    result[index] = _positions.TryGetValue(element, out var queue) && queue.Count > 0
                        ? (queue.Count > 1 ? queue.Dequeue() : queue.Peek())
                        : default;
                }
                return result;
            },
            SetMoveTransform = (element, deltaX, deltaY) =>
            {
                MoveLog.Add($"transform:{element}:{deltaX},{deltaY}");
                MoveTransforms[element] = (deltaX, deltaY);
            },
            ClearMoveStyles = element =>
            {
                MoveLog.Add($"clear:{element}");
                MoveTransforms.Remove(element);
            },
            HasCssTransform = (_, _, _) => HasCssTransform,
            WhenMoveEnds = (element, resolve) => _moveResolvers[element] = resolve,
        };

        _renderer = RendererFactory.CreateRenderer(new RendererOptions<int>
        {
            Insert = Insert,
            Remove = Remove,
            CreateElement = (tag, _) => Create(new RecordingNode { Tag = tag }),
            CreateText = text => Create(new RecordingNode { Tag = "#text", Value = text }),
            CreateComment = text => Create(new RecordingNode { Tag = "#comment", Value = text }),
            SetText = (node, text) => _nodes[node].Value = text,
            SetElementText = (node, text) => _nodes[node].Value = text,
            ParentNode = node => _nodes[node].Parent,
            NextSibling = NextSibling,
            PatchProperty = (element, _, propertyName, _, nextValue, _) =>
            {
                // Record every patched vnode prop (class/style/arbitrary attrs) so the fallthrough tests
                // can read what landed on the wrapper element. This is the ELEMENT-prop channel — distinct
                // from the transition-class channel (AddTransitionClass -> TransitionClasses), which proves
                // the wrapper's fallthrough class never mixes with the children's choreography classes.
                var properties = _nodes[element].BoundProperties;
                if (nextValue is null)
                {
                    properties.Remove(propertyName);
                }
                else
                {
                    properties[propertyName] = nextValue;
                }
            },
        });

        Container = Create(new RecordingNode { Tag = "#container" });
    }

    /// <summary>The ordered log of transition class add/remove operations (the choreography sequence).</summary>
    public List<string> ClassLog { get; } = [];

    /// <summary>The ordered log of FLIP transform/clear operations.</summary>
    public List<string> MoveLog { get; } = [];

    /// <summary>The FLIP transforms currently applied, by element handle.</summary>
    public Dictionary<int, (double DeltaX, double DeltaY)> MoveTransforms { get; } = [];

    /// <summary>The number of forced reflows recorded.</summary>
    public int ReflowCount { get; private set; }

    /// <summary>The element handles measured, in order (diagnostics).</summary>
    public List<int> MeasureLog { get; } = [];

    /// <summary>
    /// The number of batched <see cref="DomTransitionOperations.MeasurePositions"/> crossings — one per
    /// FLIP pass (pre-patch snapshot + post-patch read), independent of child count ([V01.01.04.07.03]).
    /// </summary>
    public int MeasurePositionsCallCount { get; private set; }

    /// <summary>The child count of each batched read crossing, in order — proves the whole pass rode one call.</summary>
    public List<int> MeasuredBatchSizes { get; } = [];

    /// <summary>Whether the fake reports that the move class adds a CSS transform (drives the FLIP gate).</summary>
    public bool HasCssTransform { get; set; } = true;

    /// <summary>The container handle to render into.</summary>
    public int Container { get; }

    public void Dispose()
    {
        DomTransitionOperations.Current = _previousOperations;
        _pump.Dispose();
        Scheduler.Reset();
    }

    /// <summary>Renders a component's tree into the container and drains scheduled flushes.</summary>
    public void Render(IComponentDefinition component)
    {
        _renderer.Render(VirtualNodeFactory.Component(component), Container);
        _pump.RunUntilIdle();
    }

    /// <summary>Runs captured scheduler flushes until idle.</summary>
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

    /// <summary>Fires the FLIP move transition-end for an element, removing its move class.</summary>
    public void FireMoveEnd(int element)
    {
        if (_moveResolvers.Remove(element, out var resolve))
        {
            resolve();
        }
    }

    /// <summary>Whether an element currently has a pending FLIP move end registered.</summary>
    public bool HasPendingMove(int element) => _moveResolvers.ContainsKey(element);

    /// <summary>
    /// Enqueues a position the fake reports for an element (FLIP snapshots). The FLIP measures each
    /// element twice — old (in the render) then new (in onUpdated) — so enqueue the old position, then
    /// the new; the last enqueued value is reported for any further reads.
    /// </summary>
    public void EnqueuePosition(int element, double left, double top)
    {
        if (!_positions.TryGetValue(element, out var queue))
        {
            queue = _positions[element] = new Queue<TransitionRectangle>();
        }
        queue.Enqueue(new TransitionRectangle(left, top));
    }

    /// <summary>The transition classes currently applied to an element.</summary>
    public HashSet<string> Classes(int element)
    {
        if (!_nodes.TryGetValue(element, out var node))
        {
            return [];
        }
        return node.TransitionClasses;
    }

    /// <summary>
    /// The current value of a patched vnode prop on an element (class/style/arbitrary attribute), or null
    /// when unset — the fallthrough-attrs channel, distinct from the transition-class choreography.
    /// </summary>
    public object? BoundProperty(int element, string name)
        => _nodes.TryGetValue(element, out var node) && node.BoundProperties.TryGetValue(name, out var value)
            ? value
            : null;

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

    /// <summary>Every live element handle with <paramref name="tag"/>, in creation order.</summary>
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

    /// <summary>Whether the element handle is still registered (not yet removed from the host).</summary>
    public bool IsMounted(int element) => _nodes.ContainsKey(element);

    // --- recording node-ops --------------------------------------------------------------------

    private int Create(RecordingNode node)
    {
        var handle = _nextHandle++;
        _nodes[handle] = node;
        return handle;
    }

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
        public readonly Dictionary<string, object?> BoundProperties = new(StringComparer.Ordinal);
        public readonly HashSet<string> TransitionClasses = new(StringComparer.Ordinal);
    }
}
