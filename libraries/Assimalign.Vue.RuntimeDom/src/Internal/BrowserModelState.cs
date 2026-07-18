using System;
using System.Collections.Generic;

namespace Assimalign.Vue.RuntimeDom;

/// <summary>
/// The per-element mutable state a <c>v-model</c> (or <c>v-show</c>) directive keeps between renders
/// and events — the C# stand-in for the ad-hoc fields upstream stashes on the DOM element itself
/// (<c>el._assign</c>, <c>el._modelValue</c>, <c>el._value</c>, <c>el.composing</c>,
/// <c>el._assigning</c>, <c>el[vShowOriginalDisplay]</c>;
/// https://github.com/vuejs/core/blob/main/packages/runtime-dom/src/directives/vModel.ts and
/// <c>vShow.ts</c>). A handle-based bridge cannot hang arbitrary .NET objects off a JS node, so this
/// state lives on the .NET side keyed by the element's int handle
/// (<see cref="BrowserDirectiveOperations.GetState(int)"/>) and is released when the element
/// unmounts. Not thread-safe (single-threaded browser main thread).
/// </summary>
internal sealed class BrowserModelState
{
    /// <summary>
    /// The model setter to invoke on element input (upstream: <c>el[assignKey]</c>), refreshed each
    /// render from the binding's <see cref="VueModelBinding.Setter"/>.
    /// </summary>
    public Action<object?>? Assign { get; set; }

    /// <summary>Whether an IME composition is in progress (upstream: <c>el.composing</c>) — input updates are suppressed while true.</summary>
    public bool Composing { get; set; }

    /// <summary>Whether the element currently holds focus (tracked from focus/blur), gating the lazy/trim write guards.</summary>
    public bool Focused { get; set; }

    /// <summary>The last-known DOM string value (from input events and directive writes), for the "already equal" write guard.</summary>
    public string CurrentValue { get; set; } = string.Empty;

    /// <summary>The element's bound <c>:value</c> (checkbox/radio), refreshed each render — the raw object, so identity round-trips (upstream: <c>el._value</c>).</summary>
    public object? ElementValue { get; set; }

    /// <summary>The checkbox <c>true-value</c>, refreshed each render (upstream: <c>el._trueValue</c>).</summary>
    public object? TrueValue { get; set; }

    /// <summary>The checkbox <c>false-value</c>, refreshed each render (upstream: <c>el._falseValue</c>).</summary>
    public object? FalseValue { get; set; }

    /// <summary>The bound model collection/value last reflected onto the checkbox (upstream: <c>el._modelValue</c>), so the change handler knows whether it is a list, set, or scalar.</summary>
    public object? ModelValue { get; set; }

    /// <summary>
    /// Set true by a select change while it writes the model back, so the directive's <c>updated</c>
    /// pass skips re-reflecting the value it just read (upstream: <c>el._assigning</c>).
    /// </summary>
    public bool Assigning { get; set; }

    /// <summary>
    /// The current <c>&lt;option&gt;</c> values in document order (formatted string paired with the
    /// raw object), refreshed each render — lets a select change map DOM string selections back to
    /// their raw (possibly object) values, so object option values round-trip.
    /// </summary>
    public List<KeyValuePair<string, object?>>? OptionValues { get; set; }

    /// <summary>
    /// The element's original inline <c>display</c>, saved by <c>v-show</c> at mount and restored
    /// when the binding becomes truthy (upstream: <c>el[vShowOriginalDisplay]</c>). Empty means "no
    /// inline display" — restoring removes the inline property so a stylesheet value wins.
    /// </summary>
    public string? OriginalDisplay { get; set; }
}
