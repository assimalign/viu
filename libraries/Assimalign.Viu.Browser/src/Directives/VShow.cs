using System;
using System.Collections.Generic;

using Assimalign.Viu;
using Assimalign.Viu.Components;
using Assimalign.Viu.Shared;

namespace Assimalign.Viu.Browser;

/// <summary>
/// The browser <c>v-show</c> directive.
/// </summary>
/// <remarks>
/// This is Viu's C# port of Vue's
/// <c>vShow</c>
/// (https://github.com/vuejs/core/blob/main/packages/runtime-dom/src/directives/vShow.ts).
/// It preserves the author-supplied inline display value, hides an initially falsy element before
/// insertion, and restores the original value when the binding becomes truthy. Transition
/// coordination uses <see cref="DirectiveBinding.Transition"/>: a persisted transition drives
/// enter hooks when the element becomes visible and defers <c>display: none</c> until leave
/// completes. This mirrors Vue 3.5's browser <c>vShow</c> implementation:
/// https://github.com/vuejs/core/blob/v3.5.29/packages/runtime-dom/src/directives/vShow.ts.
/// </remarks>
public sealed class VShow : IDirective
{
    /// <summary>Gets the shared browser directive instance.</summary>
    public static readonly VShow Instance = new();

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
        IElementComponent component,
        IElementComponent? previousComponent)
    {
        BrowserDirectiveOperations operations =
            BrowserDirectiveOperations.Require();
        int handle = BrowserModelDirective.Handle(element);
        string originalDisplay = OriginalDisplay(component);
        operations.GetState(handle).OriginalDisplay = originalDisplay;
        bool isVisible =
            StyleAndClassNormalization.IsTruthy(binding.Value);
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
        IElementComponent component,
        IElementComponent? previousComponent)
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
        IElementComponent component,
        IElementComponent? previousComponent)
    {
        bool isVisible =
            StyleAndClassNormalization.IsTruthy(binding.Value);
        BrowserDirectiveOperations operations =
            BrowserDirectiveOperations.Require();
        int handle = BrowserModelDirective.Handle(element);
        BrowserModelState state = operations.GetState(handle);
        string originalDisplay = OriginalDisplay(component);
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
                SetDisplay(
                    operations,
                    handle,
                    isVisible,
                    originalDisplay);
            }
            return;
        }

        if (PersistedTransition(binding) is { } transition)
        {
            if (isVisible)
            {
                transition.BeforeEnter(element);
                SetDisplay(operations, handle, true, originalDisplay);
                transition.Enter(element);
            }
            else
            {
                transition.Leave(
                    element,
                    () => SetDisplay(
                        operations,
                        handle,
                        false,
                        originalDisplay));
            }
            return;
        }

        SetDisplay(
            operations,
            handle,
            isVisible,
            originalDisplay);
    }

    private static void OnBeforeUnmount(
        object element,
        DirectiveBinding binding,
        IElementComponent component,
        IElementComponent? previousComponent)
    {
        BrowserDirectiveOperations operations =
            BrowserDirectiveOperations.Require();
        int handle = BrowserModelDirective.Handle(element);
        SetDisplay(
            operations,
            handle,
            StyleAndClassNormalization.IsTruthy(binding.Value),
            operations.GetState(handle).OriginalDisplay ?? string.Empty);
        operations.ReleaseState(handle);
    }

    private static ComponentTransition? PersistedTransition(
        DirectiveBinding binding)
    {
        ComponentTransition? transition = binding.Transition;
        return transition is { IsPersisted: true }
            ? transition
            : null;
    }

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
            operations.SetStyleProperty(
                handle,
                "display",
                originalDisplay,
                false);
        }
    }

    private static string OriginalDisplay(IElementComponent component)
    {
        object? style = BrowserModelDirective.Property(component, "style");
        if (style is null)
        {
            return string.Empty;
        }

        string? display = null;
        object? normalized = StyleAndClassNormalization.NormalizeStyle(style);
        if (normalized is string css)
        {
            StyleAndClassNormalization.ParseStringStyle(css)
                .TryGetValue("display", out object? parsed);
            display = parsed as string;
        }
        else if (normalized
            is IReadOnlyDictionary<string, object?> readOnlyMap
            && readOnlyMap.TryGetValue(
                "display",
                out object? readOnlyDisplay))
        {
            display = BrowserModelDirective.FormatValue(readOnlyDisplay);
        }
        else if (normalized
            is IDictionary<string, object?> map
            && map.TryGetValue("display", out object? mutableDisplay))
        {
            display = BrowserModelDirective.FormatValue(mutableDisplay);
        }

        display ??= string.Empty;
        return string.Equals(
            display,
            "none",
            StringComparison.OrdinalIgnoreCase)
                ? string.Empty
                : display;
    }
}
