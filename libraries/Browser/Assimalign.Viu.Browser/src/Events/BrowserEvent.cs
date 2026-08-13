using System.Collections.Generic;

using Assimalign.Viu.Components;

namespace Assimalign.Viu.Browser;

/// <summary>
/// Carries the browser event fields needed by generated handlers, event guards, and native-control
/// model directives in one host dispatch value.
/// </summary>
/// <remarks>
/// The Browser host extracts this closed field set before crossing the JavaScript interop boundary.
/// The same instance implements <see cref="IElementEvent"/> for portable authored handlers; no
/// test or host adapter converts it to another payload.
/// <see cref="StopPropagation"/> and <see cref="PreventDefault"/> record response intents that the
/// host applies to the live event after synchronous dispatch. Specified by <c>[SFC-CG-2]</c> and
/// <c>[V01.01.04.03]</c> and <c>[V01.01.11.06]</c>.
/// </remarks>
public sealed class BrowserEvent : IElementEvent
{
    private readonly bool _defaultPreventedOnArrival;
    private bool _preventDefaultRequested;

    /// <summary>Initializes one browser event payload with mutable response intents.</summary>
    /// <param name="eventName">The DOM event type.</param>
    /// <param name="timeStamp">The page-relative timestamp in milliseconds.</param>
    /// <param name="key">The keyboard key value, or an empty string.</param>
    /// <param name="code">The keyboard code value, or an empty string.</param>
    /// <param name="modifiers">The captured system-modifier flags.</param>
    /// <param name="button">The mouse button, or negative one.</param>
    /// <param name="buttons">The pressed mouse-button bitmask.</param>
    /// <param name="clientX">The pointer viewport X coordinate.</param>
    /// <param name="clientY">The pointer viewport Y coordinate.</param>
    /// <param name="detail">The user-interface event detail.</param>
    /// <param name="isSelfTarget">Whether target and current target are identical.</param>
    /// <param name="targetValue">The captured control value.</param>
    /// <param name="targetChecked">The captured checked state.</param>
    /// <param name="selectedValues">The captured multiple-selection values.</param>
    /// <param name="defaultPrevented">Whether default handling was already prevented.</param>
    public BrowserEvent(
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
        string[]? selectedValues = null,
        bool defaultPrevented = false)
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
        _defaultPreventedOnArrival = defaultPrevented;
    }

    /// <summary>Gets the DOM event type, such as <c>click</c> or <c>keydown</c>.</summary>
    public string EventName { get; }

    /// <summary>Gets the event timestamp in milliseconds relative to the page time origin.</summary>
    public double TimeStamp { get; }

    /// <summary>Gets the keyboard key value, or an empty string for a non-keyboard event.</summary>
    public string Key { get; }

    /// <summary>Gets the keyboard code value, or an empty string when it is unavailable.</summary>
    public string Code { get; }

    /// <summary>Gets the system-modifier state captured at dispatch.</summary>
    public BrowserEventModifiers Modifiers { get; }

    /// <summary>Gets the mouse button, or <c>-1</c> for a non-mouse event.</summary>
    public int Button { get; }

    /// <summary>Gets the pressed mouse-button bitmask captured at dispatch.</summary>
    public int Buttons { get; }

    /// <summary>Gets the pointer viewport X coordinate, or zero when it is unavailable.</summary>
    public double ClientX { get; }

    /// <summary>Gets the pointer viewport Y coordinate, or zero when it is unavailable.</summary>
    public double ClientY { get; }

    /// <summary>Gets the user-interface event detail value, such as a click count.</summary>
    public int Detail { get; }

    /// <summary>
    /// Gets whether the event target and current target were the same at dispatch.
    /// </summary>
    public bool IsSelfTarget { get; }

    /// <summary>Gets the target control value captured at dispatch, when one exists.</summary>
    public string? TargetValue { get; }

    /// <summary>Gets the target control checked state captured at dispatch.</summary>
    public bool TargetChecked { get; }

    /// <summary>
    /// Gets the selected option values for a multiple-selection control, or null for other events.
    /// </summary>
    public IReadOnlyList<string>? SelectedValues { get; }

    /// <summary>Gets whether a handler requested propagation to stop.</summary>
    public bool PropagationStopped { get; private set; }

    /// <summary>
    /// Gets whether the browser default had already been prevented or a handler requested
    /// prevention during this dispatch.
    /// </summary>
    public bool DefaultPrevented => _defaultPreventedOnArrival || _preventDefaultRequested;

    /// <summary>Requests that the host stop propagation on the live browser event.</summary>
    public void StopPropagation() => PropagationStopped = true;

    /// <summary>Requests that the host prevent the default action on the live browser event.</summary>
    public void PreventDefault() => _preventDefaultRequested = true;

    internal int ToResponseFlags() =>
        (PropagationStopped ? 1 : 0) | (_preventDefaultRequested ? 2 : 0);
}
