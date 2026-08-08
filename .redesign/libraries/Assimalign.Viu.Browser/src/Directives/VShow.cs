using System;
using System.Collections.Generic;

using Assimalign.Viu.Components;

namespace Assimalign.Viu.Browser;

/// <summary>Toggles Browser element visibility without destroying its mounted state.</summary>
/// <remarks>
/// The directive preserves the authored inline display value and coordinates a persisted
/// <see cref="ComponentTransition"/> so visibility changes own the enter and leave phases while
/// Core skips structural insertion and removal. Specified by <c>[SFC-CG-6]</c> and
/// <c>[BLT-10]</c>.
/// </remarks>
public sealed class VShow : IDirective
{
    /// <summary>Gets the reusable Browser visibility directive.</summary>
    public static VShow Instance { get; } = new();

    private VShow()
    {
    }

    /// <inheritdoc/>
    public DirectiveHook? BeforeMount => OnBeforeMount;

    /// <inheritdoc/>
    public DirectiveHook? Mounted => OnMounted;

    /// <inheritdoc/>
    public DirectiveHook? Updated => OnUpdated;

    /// <inheritdoc/>
    public DirectiveHook? BeforeUnmount => OnBeforeUnmount;

    private static void OnBeforeMount(
        object element,
        DirectiveBinding binding,
        ElementNode value,
        ElementNode? previousValue)
    {
        BrowserDirectiveOperations operations = BrowserDirectiveOperations.Require();
        int handle = BrowserModelDirective.Handle(element);
        string originalDisplay = OriginalDisplay(value);
        operations.GetState(handle).OriginalDisplay = originalDisplay;
        bool isVisible = StyleAndClassNormalization.IsTruthy(binding.Value);
        if (isVisible && PersistedTransition(binding) is { } transition)
        {
            transition.BeforeEnter(element);
            return;
        }

        SetDisplay(operations, handle, isVisible, originalDisplay);
    }

    private static void OnMounted(
        object element,
        DirectiveBinding binding,
        ElementNode value,
        ElementNode? previousValue)
    {
        if (StyleAndClassNormalization.IsTruthy(binding.Value)
            && PersistedTransition(binding) is { } transition)
        {
            transition.Enter(element);
        }
    }

    private static void OnUpdated(
        object element,
        DirectiveBinding binding,
        ElementNode value,
        ElementNode? previousValue)
    {
        bool isVisible = StyleAndClassNormalization.IsTruthy(binding.Value);
        BrowserDirectiveOperations operations = BrowserDirectiveOperations.Require();
        int handle = BrowserModelDirective.Handle(element);
        BrowserModelState state = operations.GetState(handle);
        string originalDisplay = OriginalDisplay(value);
        bool originalDisplayChanged = !string.Equals(
            originalDisplay,
            state.OriginalDisplay,
            StringComparison.Ordinal);
        state.OriginalDisplay = originalDisplay;
        bool visibilityChanged = isVisible
            != StyleAndClassNormalization.IsTruthy(binding.PreviousValue);
        if (!visibilityChanged)
        {
            if (originalDisplayChanged)
            {
                SetDisplay(operations, handle, isVisible, originalDisplay);
            }

            return;
        }

        if (PersistedTransition(binding) is { } transition)
        {
            if (isVisible)
            {
                transition.BeforeEnter(element);
                SetDisplay(operations, handle, isVisible: true, originalDisplay);
                transition.Enter(element);
            }
            else
            {
                transition.Leave(
                    element,
                    () => SetDisplay(
                        operations,
                        handle,
                        isVisible: false,
                        originalDisplay));
            }

            return;
        }

        SetDisplay(operations, handle, isVisible, originalDisplay);
    }

    private static void OnBeforeUnmount(
        object element,
        DirectiveBinding binding,
        ElementNode value,
        ElementNode? previousValue)
    {
        BrowserDirectiveOperations operations = BrowserDirectiveOperations.Require();
        int handle = BrowserModelDirective.Handle(element);
        SetDisplay(
            operations,
            handle,
            StyleAndClassNormalization.IsTruthy(binding.Value),
            operations.GetState(handle).OriginalDisplay ?? string.Empty);
        operations.ReleaseState(handle);
    }

    private static ComponentTransition? PersistedTransition(DirectiveBinding binding) =>
        binding.Transition is { IsPersisted: true } transition ? transition : null;

    private static void SetDisplay(
        BrowserDirectiveOperations operations,
        int handle,
        bool isVisible,
        string originalDisplay)
    {
        if (!isVisible)
        {
            operations.SetStyleProperty(handle, "display", "none", false);
        }
        else if (originalDisplay.Length == 0)
        {
            operations.RemoveStyleProperty(handle, "display");
        }
        else
        {
            operations.SetStyleProperty(handle, "display", originalDisplay, false);
        }
    }

    private static string OriginalDisplay(ElementNode value)
    {
        object? style = StyleAndClassNormalization.NormalizeStyle(
            BrowserModelDirective.Property(value, "style"));
        string? display = null;
        if (style is string css)
        {
            StyleAndClassNormalization.ParseStringStyle(css)
                .TryGetValue("display", out object? parsed);
            display = parsed as string;
        }
        else if (style is IReadOnlyDictionary<string, object?> readOnlyMap
            && readOnlyMap.TryGetValue("display", out object? readOnlyDisplay))
        {
            display = BrowserModelDirective.FormatValue(readOnlyDisplay);
        }
        else if (style is IDictionary<string, object?> map
            && map.TryGetValue("display", out object? mutableDisplay))
        {
            display = BrowserModelDirective.FormatValue(mutableDisplay);
        }

        display ??= string.Empty;
        return string.Equals(display, "none", StringComparison.OrdinalIgnoreCase)
            ? string.Empty
            : display;
    }
}
