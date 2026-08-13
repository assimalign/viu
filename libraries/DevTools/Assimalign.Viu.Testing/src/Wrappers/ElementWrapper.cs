using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Threading.Tasks;

using Assimalign.Viu.Components;

namespace Assimalign.Viu.Testing;

/// <summary>Queries and interacts with one rendered in-memory element.</summary>
/// <remarks>Specified by <c>[CONF-3]</c> and the scheduler contract <c>[SCH-9]</c>.</remarks>
public sealed class ElementWrapper
{
    private readonly TestElement _element;
    private readonly ScheduledFlush _flush;

    internal ElementWrapper(TestElement element, ScheduledFlush flush)
    {
        _element = element;
        _flush = flush;
    }

    /// <summary>Gets the underlying in-memory element.</summary>
    public TestElement Element => _element;

    /// <summary>Gets whether the element remains attached to its mounted host tree.</summary>
    public bool Exists() => _element.Parent is not null;

    /// <summary>Serializes this element and its descendants into diagnostic markup.</summary>
    /// <returns>The diagnostic markup.</returns>
    public string Html() => TestNodeSerializer.Serialize(_element);

    /// <summary>Gets concatenated descendant text content.</summary>
    /// <returns>The text content.</returns>
    public string Text()
    {
        StringBuilder builder = new();
        TestQuery.AppendText(_element, builder);
        return builder.ToString();
    }

    /// <summary>Gets an attribute or property value, or null when absent.</summary>
    /// <param name="name">The binding name.</param>
    /// <returns>The current value.</returns>
    public object? Attribute(string name)
    {
        ArgumentException.ThrowIfNullOrEmpty(name);
        return _element.Properties.TryGetValue(name, out object? value) ? value : null;
    }

    /// <summary>Finds the first descendant matching a supported selector.</summary>
    /// <param name="selector">A tag, identifier, class, or attribute selector.</param>
    /// <returns>The matching wrapper, or null.</returns>
    public ElementWrapper? Find(string selector)
    {
        ArgumentException.ThrowIfNullOrEmpty(selector);
        List<TestElement> candidates = TestQuery.DescendantElementsOf(_element);
        for (int index = 0; index < candidates.Count; index++)
        {
            if (TestQuery.Matches(candidates[index], selector))
            {
                return new ElementWrapper(candidates[index], _flush);
            }
        }

        return null;
    }

    /// <summary>Gets the first descendant matching a supported selector.</summary>
    /// <param name="selector">A tag, identifier, class, or attribute selector.</param>
    /// <returns>The matching wrapper.</returns>
    public ElementWrapper Get(string selector) =>
        Find(selector)
        ?? throw new InvalidOperationException(
            $"Unable to find an element matching selector '{selector}'.");

    /// <summary>Finds every descendant matching a supported selector in host order.</summary>
    /// <param name="selector">A tag, identifier, class, or attribute selector.</param>
    /// <returns>The matching wrappers.</returns>
    public IReadOnlyList<ElementWrapper> FindAll(string selector)
    {
        ArgumentException.ThrowIfNullOrEmpty(selector);
        List<ElementWrapper> matches = [];
        List<TestElement> candidates = TestQuery.DescendantElementsOf(_element);
        for (int index = 0; index < candidates.Count; index++)
        {
            if (TestQuery.Matches(candidates[index], selector))
            {
                matches.Add(new ElementWrapper(candidates[index], _flush));
            }
        }

        return matches;
    }

    /// <summary>Dispatches a host event without a payload and drains the deterministic scheduler.</summary>
    /// <param name="eventName">The event binding's local name.</param>
    /// <returns>A task completing after the flush chain.</returns>
    public Task TriggerAsync(string eventName)
    {
        ArgumentException.ThrowIfNullOrEmpty(eventName);
        return _flush.RunAsync(
            () => TestEventDispatcher.TriggerAsync(_element, eventName));
    }

    /// <summary>Dispatches an object payload and drains the deterministic scheduler.</summary>
    /// <param name="eventName">The event binding's local name.</param>
    /// <param name="payload">The event payload, which may be null.</param>
    /// <returns>A task completing after the flush chain.</returns>
    public Task TriggerAsync(string eventName, object? payload)
    {
        ArgumentException.ThrowIfNullOrEmpty(eventName);
        return _flush.RunAsync(
            () => TestEventDispatcher.TriggerAsync(_element, eventName, payload));
    }

    /// <summary>
    /// Dispatches an exact typed portable-event payload and drains deterministic asynchronous work.
    /// </summary>
    /// <typeparam name="TEvent">The concrete portable event type.</typeparam>
    /// <param name="eventName">The event binding's normalized local name.</param>
    /// <param name="payload">The exact payload delivered to the declared listener.</param>
    /// <returns>A task completing after the flush chain.</returns>
    public Task TriggerAsync<TEvent>(string eventName, TEvent payload)
        where TEvent : IElementEvent
    {
        ArgumentException.ThrowIfNullOrEmpty(eventName);
        ArgumentNullException.ThrowIfNull(payload);
        return _flush.RunAsync(
            () => TestEventDispatcher.TriggerAsync(_element, eventName, payload));
    }

    /// <summary>Sets the host value property, dispatches input, and drains the scheduler.</summary>
    /// <param name="value">The new host value.</param>
    /// <returns>A task completing after the flush chain.</returns>
    public Task SetValueAsync(object? value)
    {
        _element.SetProperty("value", value);
        TestElementEvent payload = new(
            "input",
            Convert.ToString(value, CultureInfo.InvariantCulture));
        return _flush.RunAsync(
            () => TestEventDispatcher.TriggerAsync(_element, "input", payload, value));
    }
}
