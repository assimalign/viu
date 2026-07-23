using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace Assimalign.Viu.Testing;

/// <summary>
/// A wrapper around one rendered element — the C# port of <c>@vue/test-utils</c>'s
/// <c>DOMWrapper</c> (https://test-utils.vuejs.org/api/#element). Queries the element's subtree and
/// dispatches events through the in-memory test adapter; the awaitable
/// <see cref="Trigger"/>/<see cref="SetValue"/> complete only after the scheduler flush, so
/// assertions observe post-update state without a manual next-tick.
/// </summary>
public sealed class ElementWrapper
{
    private readonly TestElement _element;
    private readonly ScheduledFlush _flush;

    internal ElementWrapper(TestElement element, ScheduledFlush flush)
    {
        _element = element;
        _flush = flush;
    }

    /// <summary>The underlying in-memory element.</summary>
    public TestElement Element => _element;

    /// <summary>Whether the wrapped element exists (always true for a found wrapper).</summary>
    public bool Exists() => true;

    /// <summary>The element's serialized HTML (upstream: <c>html()</c>).</summary>
    public string Html() => TestNodeSerializer.Serialize(_element);

    /// <summary>The element's text content (upstream: <c>text()</c>).</summary>
    public string Text()
    {
        var builder = new StringBuilder();
        TestQuery.AppendText(_element, builder);
        return builder.ToString();
    }

    /// <summary>The value of an attribute/property, or null (upstream: <c>attributes(name)</c>).</summary>
    /// <param name="name">The attribute name.</param>
    public object? Attribute(string name)
    {
        ArgumentException.ThrowIfNullOrEmpty(name);
        return _element.Properties.TryGetValue(name, out var value) ? value : null;
    }

    /// <summary>The first descendant matching <paramref name="selector"/>, or null (upstream: <c>find</c>).</summary>
    /// <param name="selector">A tag, <c>#id</c>, <c>.class</c>, or <c>[attr=value]</c> selector.</param>
    public ElementWrapper? Find(string selector)
    {
        ArgumentException.ThrowIfNullOrEmpty(selector);
        foreach (var candidate in TestQuery.DescendantElementsOf(_element))
        {
            if (TestQuery.Matches(candidate, selector))
            {
                return new ElementWrapper(candidate, _flush);
            }
        }
        return null;
    }

    /// <summary>The first descendant matching <paramref name="selector"/>; throws when none (upstream: <c>get</c>).</summary>
    /// <param name="selector">A tag, <c>#id</c>, <c>.class</c>, or <c>[attr=value]</c> selector.</param>
    /// <exception cref="InvalidOperationException">No descendant matches.</exception>
    public ElementWrapper Get(string selector)
        => Find(selector) ?? throw new InvalidOperationException($"Unable to find element matching selector: {selector}");

    /// <summary>Every descendant matching <paramref name="selector"/> (upstream: <c>findAll</c>).</summary>
    /// <param name="selector">A tag, <c>#id</c>, <c>.class</c>, or <c>[attr=value]</c> selector.</param>
    public IReadOnlyList<ElementWrapper> FindAll(string selector)
    {
        ArgumentException.ThrowIfNullOrEmpty(selector);
        var matches = new List<ElementWrapper>();
        foreach (var candidate in TestQuery.DescendantElementsOf(_element))
        {
            if (TestQuery.Matches(candidate, selector))
            {
                matches.Add(new ElementWrapper(candidate, _flush));
            }
        }
        return matches;
    }

    /// <summary>
    /// Dispatches <paramref name="eventName"/> on the element and awaits the scheduler flush
    /// (upstream: <c>trigger</c>). Assertions after the returned task observe post-update state.
    /// </summary>
    /// <param name="eventName">The event name (e.g. <c>"click"</c>).</param>
    /// <param name="payload">The payload passed to payload-accepting listeners.</param>
    public async Task Trigger(string eventName, object? payload = null)
    {
        ArgumentException.ThrowIfNullOrEmpty(eventName);
        await TestEventDispatcher.TriggerAsync(
            _element,
            eventName,
            payload).ConfigureAwait(false);
        await _flush.RunAsync().ConfigureAwait(false);
    }

    /// <summary>
    /// Sets the element's <c>value</c> and dispatches an <c>input</c> event, then awaits the
    /// scheduler flush (upstream: <c>setValue</c> for input-like elements and v-model bindings).
    /// </summary>
    /// <param name="value">The new value.</param>
    public async Task SetValue(object? value)
    {
        _element.Properties["value"] = value;
        await TestEventDispatcher.TriggerAsync(
            _element,
            "input",
            value).ConfigureAwait(false);
        await _flush.RunAsync().ConfigureAwait(false);
    }
}
