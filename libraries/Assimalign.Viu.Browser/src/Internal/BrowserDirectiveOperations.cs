using System;
using System.Collections.Generic;

namespace Assimalign.Viu.Browser;

/// <summary>
/// The small operation surface the DOM <c>v-model</c>/<c>v-show</c> directives write through, plus
/// the per-element <see cref="BrowserModelState"/> store they keep between renders. The directive
/// types (<see cref="VModelText"/>, <see cref="VShow"/>, …) are stateless singletons the compiler
/// references, so they resolve this ambient instance at hook time rather than capturing operations
/// at construction: <see cref="BrowserNodeOperations"/> installs the bridge-backed instance for a
/// real app, and tests install a recording instance to exercise the whole directive pipeline with
/// no browser.
/// <para>
/// Every write maps 1:1 to a <see cref="BrowserPropertyLeafOperations"/> leaf and, for listeners,
/// the model channel of <see cref="BrowserEventInvokerRegistry"/> — the same batched, cleanup-safe
/// paths the patch engine uses. Single-threaded ambient state (browser main thread only).
/// </para>
/// </summary>
internal sealed class BrowserDirectiveOperations
{
    private readonly Dictionary<int, BrowserModelState> _states = [];

    /// <summary>
    /// The ambient instance the directives resolve. Installed by <see cref="BrowserNodeOperations"/>
    /// (bridge-backed) or a test harness (recording); never resolved off the main thread.
    /// </summary>
    public static BrowserDirectiveOperations? Current { get; set; }

    /// <summary>Registers, swaps, or removes (null) a model-channel event listener (upstream: v-model's raw <c>addEventListener</c>).</summary>
    public required Action<int, string, Delegate?> SetModelListener { get; init; }

    /// <summary>Writes <c>el.value</c> through the guarded compare-and-set leaf (caret/IME safe).</summary>
    public required Action<int, string> SetValueGuarded { get; init; }

    /// <summary>Writes a boolean DOM property — <c>checked</c> on inputs, <c>selected</c> on options.</summary>
    public required Action<int, string, bool> SetBooleanProperty { get; init; }

    /// <summary>Sets one inline style property (name, value, important) — <c>v-show</c> toggles <c>display</c>.</summary>
    public required Action<int, string, string, bool> SetStyleProperty { get; init; }

    /// <summary>Removes one inline style property — <c>v-show</c> uses it to restore an empty original <c>display</c>.</summary>
    public required Action<int, string> RemoveStyleProperty { get; init; }

    /// <summary>
    /// Batches setting several CSS custom properties (index-aligned name/value arrays, the names including
    /// the leading <c>--</c>) on an element into a single interop crossing — the <c>UseCssVariables</c>
    /// application path ([V01.01.06.06]), which must never issue one interop call per property.
    /// </summary>
    public required Action<int, string[], string[]> SetCssVariables { get; init; }

    /// <summary>The ambient instance, or a thrown <see cref="InvalidOperationException"/> when none is installed.</summary>
    public static BrowserDirectiveOperations Require()
        => Current ?? throw new InvalidOperationException(
            "No BrowserDirectiveOperations installed. Building a BrowserApplication installs the "
            + "browser-backed operations; a v-model/v-show directive ran before it.");

    /// <summary>Gets (creating on first use) the model state for <paramref name="handle"/>.</summary>
    /// <param name="handle">The element handle.</param>
    public BrowserModelState GetState(int handle)
        => _states.TryGetValue(handle, out var state) ? state : _states[handle] = new BrowserModelState();

    /// <summary>Releases the model state for <paramref name="handle"/> when its element unmounts.</summary>
    /// <param name="handle">The element handle.</param>
    public void ReleaseState(int handle) => _states.Remove(handle);
}
