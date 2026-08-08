using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

using Assimalign.Viu;
using Assimalign.Viu.Components;

namespace Assimalign.Viu.Testing;

/// <summary>Queries and interacts with a virtual tree or one mounted authored component.</summary>
/// <remarks>
/// Authored queries use stable <see cref="MountedComponentView{TNode}"/> identity, public instance
/// and context values, and current first-to-last host ranges. Specified by <c>[RND-6]</c>, seam S5
/// in the component-model plan, and <c>[CONF-3]</c>.
/// </remarks>
public sealed class ComponentWrapper : IDisposable
{
    private readonly MountedComponentView<TestNode>? _mountedView;
    private readonly EmittedEvents _emitted;
    private readonly ScheduledFlush _flush;
    private readonly Renderer<TestNode> _renderer;
    private readonly TestElement _container;
    private readonly bool _ownsMount;
    private bool _isDisposed;
    private bool _isMounted = true;

    internal ComponentWrapper(
        VirtualNode node,
        MountedComponentView<TestNode>? mountedView,
        EmittedEvents emitted,
        ScheduledFlush flush,
        Renderer<TestNode> renderer,
        TestElement container,
        bool ownsMount)
    {
        Component = node;
        _mountedView = mountedView;
        Instance = mountedView?.Instance;
        Context = mountedView?.Context;
        _emitted = emitted;
        _flush = flush;
        _renderer = renderer;
        _container = container;
        _ownsMount = ownsMount;
    }

    /// <summary>Gets the immutable virtual node that established this wrapper.</summary>
    public VirtualNode Component { get; }

    /// <summary>Gets the mounted authored instance, or null for a primitive tree wrapper.</summary>
    public IComponent? Instance { get; }

    /// <summary>Gets the mounted authored context, or null for a primitive tree wrapper.</summary>
    public ComponentContext? Context { get; }

    /// <summary>Gets whether this wrapper's exact mount remains present.</summary>
    /// <returns>Whether stable view identity is still found in the renderer snapshot.</returns>
    public bool Exists()
    {
        if (!_isMounted)
        {
            return false;
        }

        return _mountedView is null || IsMounted(_mountedView);
    }

    /// <summary>Serializes every host node in this wrapper's current inclusive range.</summary>
    /// <returns>The diagnostic markup.</returns>
    public string Html()
    {
        StringBuilder builder = new();
        List<TestNode> nodes = HostNodes();
        for (int index = 0; index < nodes.Count; index++)
        {
            builder.Append(TestNodeSerializer.Serialize(nodes[index]));
        }

        return builder.ToString();
    }

    /// <summary>Gets concatenated text content in this wrapper's current host range.</summary>
    /// <returns>The text content.</returns>
    public string Text()
    {
        StringBuilder builder = new();
        List<TestNode> nodes = HostNodes();
        for (int index = 0; index < nodes.Count; index++)
        {
            TestQuery.AppendText(nodes[index], builder);
        }

        return builder.ToString();
    }

    /// <summary>Finds the first rendered element matching a supported selector.</summary>
    /// <param name="selector">A tag, identifier, class, or attribute selector.</param>
    /// <returns>The matching wrapper, or null.</returns>
    public ElementWrapper? Find(string selector)
    {
        ArgumentException.ThrowIfNullOrEmpty(selector);
        List<TestElement> candidates = TestQuery.DescendantElementsOf(HostNodes());
        for (int index = 0; index < candidates.Count; index++)
        {
            if (TestQuery.Matches(candidates[index], selector))
            {
                return new ElementWrapper(candidates[index], _flush);
            }
        }

        return null;
    }

    /// <summary>Gets the first rendered element matching a supported selector.</summary>
    /// <param name="selector">A tag, identifier, class, or attribute selector.</param>
    /// <returns>The matching wrapper.</returns>
    public ElementWrapper Get(string selector) =>
        Find(selector)
        ?? throw new InvalidOperationException(
            $"Unable to find an element matching selector '{selector}'.");

    /// <summary>Finds every rendered element matching a supported selector in host order.</summary>
    /// <param name="selector">A tag, identifier, class, or attribute selector.</param>
    /// <returns>The matching wrappers.</returns>
    public IReadOnlyList<ElementWrapper> FindAll(string selector)
    {
        ArgumentException.ThrowIfNullOrEmpty(selector);
        List<ElementWrapper> matches = [];
        List<TestElement> candidates = TestQuery.DescendantElementsOf(HostNodes());
        for (int index = 0; index < candidates.Count; index++)
        {
            if (TestQuery.Matches(candidates[index], selector))
            {
                matches.Add(new ElementWrapper(candidates[index], _flush));
            }
        }

        return matches;
    }

    /// <summary>Finds the first mounted descendant authored component of a requested type.</summary>
    /// <typeparam name="TComponent">The authored instance type.</typeparam>
    /// <returns>The descendant wrapper, or null.</returns>
    public ComponentWrapper? FindComponent<TComponent>()
        where TComponent : class, IComponent
    {
        IReadOnlyList<MountedComponentView<TestNode>> views =
            _renderer.GetMountedComponentViews(_container);
        for (int index = 0; index < views.Count; index++)
        {
            MountedComponentView<TestNode> candidate = views[index];
            if (!ReferenceEquals(candidate, _mountedView)
                && candidate.Instance is TComponent
                && IsDescendant(candidate))
            {
                return new ComponentWrapper(
                    candidate.Request,
                    candidate,
                    _emitted,
                    _flush,
                    _renderer,
                    _container,
                    ownsMount: false);
            }
        }

        return null;
    }

    /// <summary>Gets the first mounted descendant authored component of a requested type.</summary>
    /// <typeparam name="TComponent">The authored instance type.</typeparam>
    /// <returns>The matching descendant wrapper.</returns>
    public ComponentWrapper GetComponent<TComponent>()
        where TComponent : class, IComponent =>
        FindComponent<TComponent>()
        ?? throw new InvalidOperationException(
            $"Unable to find a mounted '{typeof(TComponent).Name}' component.");

    /// <summary>Gets captured event occurrences from this exact component context.</summary>
    /// <param name="eventName">The emitted event name.</param>
    /// <returns>Ordered immutable argument snapshots.</returns>
    public IReadOnlyList<IReadOnlyList<object?>> Emitted(string eventName)
    {
        ArgumentException.ThrowIfNullOrEmpty(eventName);
        return _emitted.Occurrences(Context, eventName);
    }

    /// <summary>Gets every captured event from this exact component context.</summary>
    /// <returns>Events keyed by name.</returns>
    public IReadOnlyDictionary<string, IReadOnlyList<IReadOnlyList<object?>>> Emitted() =>
        _emitted.All(Context);

    /// <summary>Triggers an event on the first rendered element and drains the scheduler.</summary>
    /// <param name="eventName">The event binding's local name.</param>
    /// <param name="payload">The optional event payload.</param>
    /// <returns>A task completing after the flush chain.</returns>
    public async Task TriggerAsync(string eventName, object? payload = null)
    {
        ArgumentException.ThrowIfNullOrEmpty(eventName);
        TestElement element = RootElement()
            ?? throw new InvalidOperationException(
                "The wrapped host range has no element to trigger.");
        await TestEventDispatcher.TriggerAsync(element, eventName, payload).ConfigureAwait(false);
        await _flush.RunAsync().ConfigureAwait(false);
    }

    /// <summary>Sets the first rendered element's value, dispatches input, and drains the scheduler.</summary>
    /// <param name="value">The new host value.</param>
    /// <returns>A task completing after the flush chain.</returns>
    public async Task SetValueAsync(object? value)
    {
        TestElement element = RootElement()
            ?? throw new InvalidOperationException(
                "The wrapped host range has no element to update.");
        element.SetProperty("value", value);
        await TestEventDispatcher.TriggerAsync(element, "input", value).ConfigureAwait(false);
        await _flush.RunAsync().ConfigureAwait(false);
    }

    /// <summary>Drains the deterministic scheduler through the current next-tick boundary.</summary>
    /// <returns>A task completing after the flush chain.</returns>
    public Task NextTickAsync() => _flush.RunAsync();

    /// <summary>Drains every currently captured scheduler continuation.</summary>
    /// <returns>A task completing after the flush chain.</returns>
    public Task FlushAsync() => _flush.RunAsync();

    /// <summary>Unmounts the root tree; descendant wrappers borrow and cannot end that lifetime.</summary>
    public void Unmount()
    {
        if (!_ownsMount || !_isMounted)
        {
            return;
        }

        _renderer.Render(null, _container);
        _isMounted = false;
    }

    /// <summary>Unmounts an owned root and restores the preceding scheduler dispatcher.</summary>
    public void Dispose()
    {
        if (!_ownsMount || _isDisposed)
        {
            return;
        }

        try
        {
            Unmount();
        }
        finally
        {
            _flush.Dispose();
            Scheduler.Reset();
            _isDisposed = true;
        }
    }

    private TestElement? RootElement()
    {
        List<TestElement> elements = TestQuery.DescendantElementsOf(HostNodes());
        return elements.Count > 0 ? elements[0] : null;
    }

    private List<TestNode> HostNodes()
    {
        if (!Exists())
        {
            return [];
        }

        return _mountedView is null
            ? TestQuery.HostNodes(_container)
            : TestQuery.HostNodes(_mountedView);
    }

    private bool IsDescendant(MountedComponentView<TestNode> candidate)
    {
        if (Context is null)
        {
            return true;
        }

        ComponentContext? ancestor = candidate.Context.Parent;
        while (ancestor is not null)
        {
            if (ReferenceEquals(ancestor, Context))
            {
                return true;
            }

            ancestor = ancestor.Parent;
        }

        return false;
    }

    private bool IsMounted(MountedComponentView<TestNode> view)
    {
        IReadOnlyList<MountedComponentView<TestNode>> views =
            _renderer.GetMountedComponentViews(_container);
        for (int index = 0; index < views.Count; index++)
        {
            if (ReferenceEquals(views[index], view))
            {
                return true;
            }
        }

        return false;
    }
}
