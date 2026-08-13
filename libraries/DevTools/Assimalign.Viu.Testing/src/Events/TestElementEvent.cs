using System;
using System.Collections.Generic;

using Assimalign.Viu.Components;

namespace Assimalign.Viu.Testing;

/// <summary>Provides a constructed host-neutral element-event payload for DOM-free tests.</summary>
/// <remarks>
/// The in-memory host passes this exact instance to <see cref="IElementEvent"/> handlers. Its
/// values model the closed portable payload surface shared with the Browser host; no DOM object or
/// reflection-based conversion is involved. Specified by <c>[V01.01.11.06]</c>.
/// </remarks>
public sealed class TestElementEvent : IElementEvent
{
    /// <summary>Initializes an immutable test event payload.</summary>
    /// <param name="eventName">The normalized host event name.</param>
    /// <param name="targetValue">The captured target value, when one exists.</param>
    /// <param name="key">The keyboard key value, or an empty string.</param>
    /// <param name="targetChecked">The captured target checked state.</param>
    /// <param name="selectedValues">The captured multiple-selection values, when present.</param>
    public TestElementEvent(
        string eventName,
        string? targetValue = null,
        string key = "",
        bool targetChecked = false,
        IEnumerable<string>? selectedValues = null)
    {
        ArgumentException.ThrowIfNullOrEmpty(eventName);
        ArgumentNullException.ThrowIfNull(key);
        EventName = eventName;
        TargetValue = targetValue;
        Key = key;
        TargetChecked = targetChecked;
        SelectedValues = selectedValues is null
            ? null
            : new List<string>(selectedValues).AsReadOnly();
    }

    /// <inheritdoc />
    public string EventName { get; }

    /// <inheritdoc />
    public string Key { get; }

    /// <inheritdoc />
    public string? TargetValue { get; }

    /// <inheritdoc />
    public bool TargetChecked { get; }

    /// <inheritdoc />
    public IReadOnlyList<string>? SelectedValues { get; }
}
