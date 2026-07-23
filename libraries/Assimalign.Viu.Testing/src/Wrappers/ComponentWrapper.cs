using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

using Assimalign.Viu;
using Assimalign.Viu.Components;

namespace Assimalign.Viu.Testing;

/// <summary>
/// Wraps a component tree rendered into the in-memory host.
/// </summary>
/// <remarks>
/// Root and child-template queries, host interactions, and per-template emitted-event capture use
/// Core's read-only mounted-template inspection seams.
/// </remarks>
public sealed class ComponentWrapper : IDisposable
{
    private readonly MountedTemplateNode<TestNode>? _mountedTemplate;
    private readonly EmittedEvents _emitted;
    private readonly ScheduledFlush _flush;
    private readonly Renderer<TestNode> _renderer;
    private readonly TestElement _container;
    private readonly bool _ownsMount;
    private bool _isMounted = true;

    internal ComponentWrapper(
        IComponent component,
        MountedTemplateNode<TestNode>? mountedTemplate,
        EmittedEvents emitted,
        ScheduledFlush flush,
        Renderer<TestNode> renderer,
        TestElement container,
        bool ownsMount)
    {
        Component = component;
        _mountedTemplate = mountedTemplate;
        Instance = mountedTemplate?.Instance.Template;
        Context = mountedTemplate?.Instance.Context;
        _emitted = emitted;
        _flush = flush;
        _renderer = renderer;
        _container = container;
        _ownsMount = ownsMount;
    }

    /// <summary>Gets the immutable component request wrapped by this instance.</summary>
    public IComponent Component { get; }

    /// <summary>Gets the mounted template instance, or null for a primitive tree wrapper.</summary>
    public IComponentTemplate? Instance { get; }

    /// <summary>Gets the mounted template context, or null for a primitive tree wrapper.</summary>
    public IComponentContext? Context { get; }

    /// <summary>Gets whether the wrapped tree remains mounted.</summary>
    public bool Exists()
    {
        return _ownsMount
            ? _isMounted
            : IsMounted(_mountedTemplate);
    }

    /// <summary>Serializes every top-level host node.</summary>
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

    /// <summary>Gets the concatenated text content of the rendered host tree.</summary>
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
    /// <returns>The element wrapper, or null.</returns>
    public ElementWrapper? Find(string selector)
    {
        ArgumentException.ThrowIfNullOrEmpty(selector);
        List<TestElement> candidates =
            TestQuery.DescendantElementsOf(HostNodes());
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
    /// <returns>The element wrapper.</returns>
    public ElementWrapper Get(string selector)
    {
        return Find(selector)
            ?? throw new InvalidOperationException(
                $"Unable to find element matching selector: {selector}");
    }

    /// <summary>Finds every rendered element matching a supported selector.</summary>
    /// <param name="selector">A tag, identifier, class, or attribute selector.</param>
    /// <returns>The matching wrappers.</returns>
    public IReadOnlyList<ElementWrapper> FindAll(string selector)
    {
        ArgumentException.ThrowIfNullOrEmpty(selector);
        List<ElementWrapper> matches = [];
        List<TestElement> candidates =
            TestQuery.DescendantElementsOf(HostNodes());
        for (int index = 0; index < candidates.Count; index++)
        {
            if (TestQuery.Matches(candidates[index], selector))
            {
                matches.Add(new ElementWrapper(candidates[index], _flush));
            }
        }

        return matches;
    }

    /// <summary>Finds the first mounted descendant template of the requested type.</summary>
    /// <typeparam name="TComponent">The authored template type.</typeparam>
    /// <returns>The child wrapper, or null when no matching descendant is mounted.</returns>
    public ComponentWrapper? FindComponent<TComponent>()
        where TComponent : class, IComponentTemplate
    {
        IReadOnlyList<MountedTemplateNode<TestNode>> templates =
            _renderer.GetMountedTemplates(_container);
        for (int index = 0; index < templates.Count; index++)
        {
            MountedTemplateNode<TestNode> candidate = templates[index];
            if (!ReferenceEquals(candidate, _mountedTemplate)
                && candidate.Instance.Template is TComponent
                && IsDescendant(candidate))
            {
                return new ComponentWrapper(
                    candidate.Component,
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

    /// <summary>Gets the first mounted descendant template of the requested type.</summary>
    /// <typeparam name="TComponent">The authored template type.</typeparam>
    /// <returns>The matching child wrapper.</returns>
    /// <exception cref="InvalidOperationException">No matching descendant is mounted.</exception>
    public ComponentWrapper GetComponent<TComponent>()
        where TComponent : class, IComponentTemplate
    {
        return FindComponent<TComponent>()
            ?? throw new InvalidOperationException(
                $"Unable to find a mounted {typeof(TComponent).Name} component.");
    }

    /// <summary>Gets this template's captured event occurrences by name.</summary>
    /// <param name="eventName">The emitted event name.</param>
    /// <returns>The ordered occurrences.</returns>
    public IReadOnlyList<IReadOnlyList<object?>> Emitted(string eventName)
    {
        ArgumentException.ThrowIfNullOrEmpty(eventName);
        return _emitted.Occurrences(Context, eventName);
    }

    /// <summary>Gets every event captured from this template.</summary>
    public IReadOnlyDictionary<string, IReadOnlyList<IReadOnlyList<object?>>> Emitted()
    {
        return _emitted.All(Context);
    }

    /// <summary>Triggers an event on the first rendered element and awaits its task and scheduler flush.</summary>
    /// <param name="eventName">The event name.</param>
    /// <param name="payload">The optional event payload.</param>
    public async Task Trigger(string eventName, object? payload = null)
    {
        ArgumentException.ThrowIfNullOrEmpty(eventName);
        TestElement element = RootElement()
            ?? throw new InvalidOperationException(
                "The component tree has no root element to trigger.");
        await TestEventDispatcher.TriggerAsync(
            element,
            eventName,
            payload).ConfigureAwait(false);
        await _flush.RunAsync().ConfigureAwait(false);
    }

    /// <summary>Sets the first rendered element's value and dispatches an input event.</summary>
    /// <param name="value">The new value.</param>
    public async Task SetValue(object? value)
    {
        TestElement element = RootElement()
            ?? throw new InvalidOperationException(
                "The component tree has no root element to set.");
        element.Properties["value"] = value;
        await TestEventDispatcher.TriggerAsync(
            element,
            "input",
            value).ConfigureAwait(false);
        await _flush.RunAsync().ConfigureAwait(false);
    }

    /// <summary>Runs and awaits the deterministic scheduler until idle.</summary>
    public Task NextTickAsync()
    {
        return _flush.RunAsync();
    }

    /// <summary>Runs and awaits every currently pending scheduler flush.</summary>
    public Task FlushAsync()
    {
        return _flush.RunAsync();
    }

    /// <summary>
    /// Unmounts the application tree when this is the root wrapper. Child wrappers borrow the
    /// root lifecycle and leave it unchanged.
    /// </summary>
    public void Unmount()
    {
        if (!_ownsMount || !_isMounted)
        {
            return;
        }

        _renderer.Render(null, _container);
        _isMounted = false;
    }

    /// <summary>
    /// Releases the root mount and restores the prior scheduler dispatcher. Disposing a child
    /// wrapper is a no-op.
    /// </summary>
    public void Dispose()
    {
        if (!_ownsMount)
        {
            return;
        }

        Unmount();
        _flush.Dispose();
        Scheduler.Reset();
    }

    private TestElement? RootElement()
    {
        List<TestElement> elements =
            TestQuery.DescendantElementsOf(HostNodes());
        return elements.Count > 0 ? elements[0] : null;
    }

    private List<TestNode> HostNodes()
    {
        if (!Exists())
        {
            return [];
        }

        return _mountedTemplate is null
            ? TestQuery.HostNodes(_container)
            : TestQuery.HostNodes(_mountedTemplate);
    }

    private bool IsDescendant(MountedTemplateNode<TestNode> candidate)
    {
        if (Context is null)
        {
            return true;
        }

        ComponentContext? ancestor = candidate.Instance.Context.Parent;
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

    private bool IsMounted(MountedTemplateNode<TestNode>? template)
    {
        if (template is null)
        {
            return false;
        }

        IReadOnlyList<MountedTemplateNode<TestNode>> templates =
            _renderer.GetMountedTemplates(_container);
        for (int index = 0; index < templates.Count; index++)
        {
            if (ReferenceEquals(templates[index], template))
            {
                return true;
            }
        }

        return false;
    }
}
