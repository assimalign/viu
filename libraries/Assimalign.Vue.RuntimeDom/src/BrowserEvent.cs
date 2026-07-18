using System;
using System.Collections.Generic;

namespace Assimalign.Vue.RuntimeDom;

/// <summary>
/// The typed event arguments a Vuecs handler receives — the fields of the underlying DOM event
/// (W3C UI Events, https://www.w3.org/TR/uievents/) that Vue's modifier and key guards need,
/// extracted JS-side and marshaled in the single dispatch call ([V01.01.04.03]): no per-field
/// <c>JSObject</c> property reads and no proxy retained per event.
/// <see cref="StopPropagation"/>/<see cref="PreventDefault"/> record intents the bridge applies
/// to the live JS event when the synchronous dispatch returns.
/// <para>
/// <see cref="TargetValue"/>, <see cref="TargetChecked"/>, and <see cref="SelectedValues"/> carry
/// the form-control state <c>v-model</c> ([V01.01.04.06]) reads, so the directive never issues a
/// follow-up interop read per event (the issue's interop-boundary requirement).
/// </para>
/// </summary>
public sealed class BrowserEvent
{
    internal BrowserEvent(
        string eventName,
        double timeStamp,
        string key,
        string code,
        BrowserEventModifiers modifiers,
        int button,
        int buttons,
        double clientX,
        double clientY,
        int detail,
        bool isSelfTarget,
        string? targetValue,
        bool targetChecked,
        string[]? selectedValues = null)
    {
        EventName = eventName;
        TimeStamp = timeStamp;
        Key = key;
        Code = code;
        Modifiers = modifiers;
        Button = button;
        Buttons = buttons;
        ClientX = clientX;
        ClientY = clientY;
        Detail = detail;
        IsSelfTarget = isSelfTarget;
        TargetValue = targetValue;
        TargetChecked = targetChecked;
        SelectedValues = selectedValues;
    }

    /// <summary>The DOM event type (e.g. <c>"click"</c>, <c>"keydown"</c>).</summary>
    public string EventName { get; }

    /// <summary>The event's <c>timeStamp</c> (milliseconds relative to the page's time origin).</summary>
    public double TimeStamp { get; }

    /// <summary>The keyboard <c>key</c> value, or empty for non-keyboard events.</summary>
    public string Key { get; }

    /// <summary>The keyboard <c>code</c> value, or empty for non-keyboard events.</summary>
    public string Code { get; }

    /// <summary>The system-modifier state at dispatch.</summary>
    public BrowserEventModifiers Modifiers { get; }

    /// <summary>The mouse <c>button</c> (0 left, 1 middle, 2 right), or -1 for non-mouse events.</summary>
    public int Button { get; }

    /// <summary>The mouse <c>buttons</c> bitmask at dispatch.</summary>
    public int Buttons { get; }

    /// <summary>The pointer's viewport X coordinate, or 0 for non-pointer events.</summary>
    public double ClientX { get; }

    /// <summary>The pointer's viewport Y coordinate, or 0 for non-pointer events.</summary>
    public double ClientY { get; }

    /// <summary>The UI event <c>detail</c> value (e.g. click count).</summary>
    public int Detail { get; }

    /// <summary>
    /// Whether <c>event.target === event.currentTarget</c> (evaluated JS-side) — the
    /// <c>.self</c> modifier's guard.
    /// </summary>
    public bool IsSelfTarget { get; }

    /// <summary>The target element's <c>value</c> at dispatch, when it has one (inputs, selects).</summary>
    public string? TargetValue { get; }

    /// <summary>The target element's <c>checked</c> state at dispatch, when it has one.</summary>
    public bool TargetChecked { get; }

    /// <summary>
    /// The values of the selected <c>&lt;option&gt;</c>s when the target is a <c>&lt;select
    /// multiple&gt;</c> (upstream reads <c>el.selectedOptions</c>), or null for every other event.
    /// Carried so <c>VModelSelect</c> maps a multi-select change to its bound list or set without a
    /// follow-up interop read.
    /// </summary>
    public IReadOnlyList<string>? SelectedValues { get; }

    /// <summary>Whether <see cref="StopPropagation"/> was requested.</summary>
    public bool PropagationStopped { get; private set; }

    /// <summary>Whether <see cref="PreventDefault"/> was requested.</summary>
    public bool DefaultPrevented { get; private set; }

    /// <summary>
    /// Requests <c>stopPropagation()</c> on the live event when this synchronous dispatch
    /// returns (Vue's <c>.stop</c> modifier calls this).
    /// </summary>
    public void StopPropagation() => PropagationStopped = true;

    /// <summary>
    /// Requests <c>preventDefault()</c> on the live event when this synchronous dispatch
    /// returns (Vue's <c>.prevent</c> modifier calls this).
    /// </summary>
    public void PreventDefault() => DefaultPrevented = true;

    internal int ToResponseFlags()
        => (PropagationStopped ? 1 : 0) | (DefaultPrevented ? 2 : 0);
}
