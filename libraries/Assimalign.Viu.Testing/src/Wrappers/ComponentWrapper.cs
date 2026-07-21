using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

using Assimalign.Viu;

namespace Assimalign.Viu.Testing;

/// <summary>
/// A wrapper around a mounted component — the C# port of <c>@vue/test-utils</c>'s <c>VueWrapper</c>
/// (https://test-utils.vuejs.org/api/). Exposes the rendered tree via
/// <see cref="Find"/>/<see cref="FindComponent{TComponent}"/>, <see cref="Html"/>/<see cref="Text"/>,
/// the captured <see cref="Emitted()"/> events, and the underlying <see cref="Instance"/>; awaitable
/// <see cref="Trigger"/>/<see cref="SetValue"/>/<see cref="NextTickAsync"/> complete only after the
/// scheduler flush. Created by <see cref="ViuTest.Mount{TComponent}(TComponent, ComponentMountOptions?)"/>.
/// The root wrapper owns the mount lifecycle: dispose it (a <c>using</c>) to unmount and reset the
/// scheduler. Not thread-safe (single-threaded JS event-loop model).
/// </summary>
public sealed class ComponentWrapper : IDisposable
{
    private readonly ComponentInstance _instance;
    private readonly EmittedEvents _emitted;
    private readonly ScheduledFlush _flush;
    private readonly Renderer<TestNode> _renderer;
    private readonly TestElement _container;
    private readonly bool _isRoot;

    internal ComponentWrapper(
        ComponentInstance instance,
        EmittedEvents emitted,
        ScheduledFlush flush,
        Renderer<TestNode> renderer,
        TestElement container,
        bool isRoot)
    {
        _instance = instance;
        _emitted = emitted;
        _flush = flush;
        _renderer = renderer;
        _container = container;
        _isRoot = isRoot;
    }

    /// <summary>The mounted component instance (upstream: the wrapper's <c>vm</c>/component).</summary>
    public ComponentInstance Instance => _instance;

    /// <summary>Whether the component is still mounted (upstream: <c>exists()</c>).</summary>
    public bool Exists() => !_instance.IsUnmounted;

    /// <summary>The component's rendered HTML (upstream: <c>html()</c>).</summary>
    public string Html()
    {
        var nodes = TestQuery.HostNodes(_instance.Subtree);
        if (nodes.Count == 1)
        {
            return TestNodeSerializer.Serialize(nodes[0]);
        }
        var builder = new StringBuilder();
        foreach (var node in nodes)
        {
            builder.Append(TestNodeSerializer.Serialize(node));
        }
        return builder.ToString();
    }

    /// <summary>The component's rendered text content (upstream: <c>text()</c>).</summary>
    public string Text()
    {
        var builder = new StringBuilder();
        foreach (var node in TestQuery.HostNodes(_instance.Subtree))
        {
            TestQuery.AppendText(node, builder);
        }
        return builder.ToString();
    }

    /// <summary>The first rendered element matching <paramref name="selector"/>, or null (upstream: <c>find</c>).</summary>
    /// <param name="selector">A tag, <c>#id</c>, <c>.class</c>, or <c>[attr=value]</c> selector.</param>
    public ElementWrapper? Find(string selector)
    {
        ArgumentException.ThrowIfNullOrEmpty(selector);
        foreach (var candidate in TestQuery.DescendantElements(_instance.Subtree))
        {
            if (TestQuery.Matches(candidate, selector))
            {
                return new ElementWrapper(candidate, _flush);
            }
        }
        return null;
    }

    /// <summary>The first rendered element matching <paramref name="selector"/>; throws when none (upstream: <c>get</c>).</summary>
    /// <param name="selector">A tag, <c>#id</c>, <c>.class</c>, or <c>[attr=value]</c> selector.</param>
    /// <exception cref="InvalidOperationException">No element matches.</exception>
    public ElementWrapper Get(string selector)
        => Find(selector) ?? throw new InvalidOperationException($"Unable to find element matching selector: {selector}");

    /// <summary>Every rendered element matching <paramref name="selector"/> (upstream: <c>findAll</c>).</summary>
    /// <param name="selector">A tag, <c>#id</c>, <c>.class</c>, or <c>[attr=value]</c> selector.</param>
    public IReadOnlyList<ElementWrapper> FindAll(string selector)
    {
        ArgumentException.ThrowIfNullOrEmpty(selector);
        var matches = new List<ElementWrapper>();
        foreach (var candidate in TestQuery.DescendantElements(_instance.Subtree))
        {
            if (TestQuery.Matches(candidate, selector))
            {
                matches.Add(new ElementWrapper(candidate, _flush));
            }
        }
        return matches;
    }

    /// <summary>
    /// The first mounted child component whose definition is <typeparamref name="TComponent"/>, or
    /// null (upstream: <c>findComponent</c>). Matching is by the definition's runtime type — no
    /// reflection-based activation.
    /// </summary>
    /// <typeparam name="TComponent">The child component definition type to locate.</typeparam>
    public ComponentWrapper? FindComponent<TComponent>()
        where TComponent : IComponentDefinition
    {
        foreach (var candidate in TestQuery.DescendantComponents(_instance.Subtree))
        {
            if (candidate.Definition is TComponent)
            {
                return new ComponentWrapper(candidate, _emitted, _flush, _renderer, _container, isRoot: false);
            }
        }
        return null;
    }

    /// <summary>The first mounted child component of type <typeparamref name="TComponent"/>; throws when none (upstream: <c>getComponent</c>).</summary>
    /// <typeparam name="TComponent">The child component definition type to locate.</typeparam>
    /// <exception cref="InvalidOperationException">No matching child component is mounted.</exception>
    public ComponentWrapper GetComponent<TComponent>()
        where TComponent : IComponentDefinition
        => FindComponent<TComponent>()
            ?? throw new InvalidOperationException($"Unable to find a mounted {typeof(TComponent).Name} component.");

    /// <summary>
    /// The ordered occurrences of <paramref name="eventName"/> emitted by this component, each the
    /// event's argument list (upstream: <c>emitted('name')</c>). Includes events emitted during
    /// mount.
    /// </summary>
    /// <param name="eventName">The event name.</param>
    public IReadOnlyList<IReadOnlyList<object?>> Emitted(string eventName)
    {
        ArgumentException.ThrowIfNullOrEmpty(eventName);
        return _emitted.Occurrences(_instance, eventName);
    }

    /// <summary>All events this component emitted, keyed by name (upstream: <c>emitted()</c>).</summary>
    public IReadOnlyDictionary<string, IReadOnlyList<IReadOnlyList<object?>>> Emitted()
        => _emitted.All(_instance);

    /// <summary>
    /// Dispatches <paramref name="eventName"/> on the component's root element and awaits the
    /// scheduler flush (upstream: <c>trigger</c>).
    /// </summary>
    /// <param name="eventName">The event name (e.g. <c>"click"</c>).</param>
    /// <param name="payload">The payload passed to payload-accepting listeners.</param>
    /// <exception cref="InvalidOperationException">The component has no root element.</exception>
    public Task Trigger(string eventName, object? payload = null)
    {
        ArgumentException.ThrowIfNullOrEmpty(eventName);
        var element = RootElement()
            ?? throw new InvalidOperationException("The component has no root element to trigger events on.");
        TestEventDispatcher.Trigger(element, eventName, payload);
        return _flush.RunAsync();
    }

    /// <summary>
    /// Sets the root element's <c>value</c>, dispatches an <c>input</c> event, and awaits the
    /// scheduler flush (upstream: <c>setValue</c>).
    /// </summary>
    /// <param name="value">The new value.</param>
    /// <exception cref="InvalidOperationException">The component has no root element.</exception>
    public Task SetValue(object? value)
    {
        var element = RootElement()
            ?? throw new InvalidOperationException("The component has no root element to set a value on.");
        element.Properties["value"] = value;
        TestEventDispatcher.Trigger(element, "input", value);
        return _flush.RunAsync();
    }

    /// <summary>Awaits the scheduler flush — the C# port of <c>nextTick()</c> for tests.</summary>
    public Task NextTickAsync() => _flush.RunAsync();

    /// <summary>Awaits every pending scheduler flush (the <c>flushPromises</c> counterpart).</summary>
    public Task FlushAsync() => _flush.RunAsync();

    /// <summary>Unmounts the component tree (root wrapper only; upstream: <c>unmount()</c>).</summary>
    public void Unmount()
    {
        if (_isRoot && !_instance.IsUnmounted)
        {
            _renderer.Render(null, _container);
        }
    }

    /// <summary>Unmounts and releases the scheduler pump — only the root wrapper owns this lifecycle.</summary>
    public void Dispose()
    {
        if (!_isRoot)
        {
            return;
        }
        Unmount();
        _flush.Dispose();
        Scheduler.Reset();
    }

    private TestElement? RootElement()
    {
        foreach (var node in TestQuery.HostNodes(_instance.Subtree))
        {
            if (node is TestElement element)
            {
                return element;
            }
        }
        return null;
    }
}
