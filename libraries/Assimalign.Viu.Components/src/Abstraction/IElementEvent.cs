using System.Collections.Generic;

namespace Assimalign.Viu.Components;

/// <summary>
/// Exposes the host-neutral element-event values that authored handlers can read on every host.
/// </summary>
/// <remarks>
/// Hosts preserve their concrete event object while presenting this common read-only payload, so
/// one handler shape can read input, keyboard, checkbox, and multiple-selection state without a
/// host-specific dependency. Specified by <c>[V01.01.11.06]</c>.
/// </remarks>
public interface IElementEvent
{
    /// <summary>Gets the normalized host event name.</summary>
    string EventName { get; }

    /// <summary>Gets the keyboard key value, or an empty string for a non-keyboard event.</summary>
    string Key { get; }

    /// <summary>Gets the target control value captured at dispatch, when one exists.</summary>
    string? TargetValue { get; }

    /// <summary>Gets the target control checked state captured at dispatch.</summary>
    bool TargetChecked { get; }

    /// <summary>
    /// Gets the selected option values for a multiple-selection control, or null for other events.
    /// </summary>
    IReadOnlyList<string>? SelectedValues { get; }
}
