using System;
using System.Collections.Generic;

using Assimalign.Vue.RuntimeCore;
using Assimalign.Vue.Shared;


namespace Assimalign.Vue.RuntimeDom;

/// <summary>
/// The <c>v-show</c> directive — the C# port of upstream's <c>vShow</c>
/// (https://github.com/vuejs/core/blob/main/packages/runtime-dom/src/directives/vShow.ts,
/// https://vuejs.org/guide/essentials/conditional.html#v-show). Toggles an element's inline
/// <c>display</c> from a truthy/falsy binding while preserving its original value: the original
/// inline <c>display</c> is saved at mount, a falsy binding sets <c>display: none</c>, and a truthy
/// binding restores the saved value (removing the inline property when there was none, so a
/// stylesheet value wins). Because <see cref="IDirective.BeforeMount"/> runs before insertion, an
/// initially-falsy element is hidden from the first paint (no flash of visible content). Truthiness
/// follows JavaScript coercion (<see cref="StyleAndClassNormalization.IsTruthy(object?)"/>).
/// <para>
/// The saved display is derived from the vnode's <c>style</c> prop rather than an interop read of
/// <c>el.style.display</c> — equivalent for inline styles and one fewer boundary crossing. The
/// <c>&lt;Transition&gt;</c> coordination clause of the acceptance criteria remains a
/// <b>documented-inert seam</b>: the transition state machine now exists ([V01.01.04.07]) and the
/// renderer honors a <see cref="TransitionState"/>-persisted transition, but <c>v-show</c> does not yet
/// build that persisted transition object, so the hook shape is present and its coordination is a
/// follow-up.
/// </para>
/// Stateless singleton (<see cref="Instance"/>); the saved display lives in
/// <see cref="BrowserModelState.OriginalDisplay"/>.
/// </summary>
public sealed class VShow : IDirective
{
    /// <summary>The shared directive instance the compiler references.</summary>
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

    private static void OnBeforeMount(object? element, DirectiveBinding binding, VirtualNode node, VirtualNode? previousNode)
    {
        var operations = BrowserDirectiveOperations.Require();
        var handle = BrowserModelDirective.Handle(element);
        var original = OriginalDisplay(node);
        operations.GetState(handle).OriginalDisplay = original;
        // Transition seam (still inert): upstream would defer to transition.beforeEnter when a transition
        // and truthy value are present. The transition state machine now exists ([V01.01.04.07]) but
        // v-show does not yet mark itself a persisted transition, so this stays a plain toggle. beforeMount
        // timing means an initially-falsy element is hidden before its first paint.
        SetDisplay(operations, handle, StyleAndClassNormalization.IsTruthy(binding.Value), original);
    }

    private static void OnMounted(object? element, DirectiveBinding binding, VirtualNode node, VirtualNode? previousNode)
    {
        // Deferred-inert transition seam: upstream runs transition.enter here when a transition and
        // truthy value are present. The transition system exists ([V01.01.04.07]); wiring v-show to build a
        // persisted transition is a follow-up, so this is a no-op placeholder that keeps the hook shape.
    }

    private static void OnUpdated(object? element, DirectiveBinding binding, VirtualNode node, VirtualNode? previousNode)
    {
        var value = StyleAndClassNormalization.IsTruthy(binding.Value);
        // Upstream: if (!value === !oldValue) return — nothing to toggle when the truthiness is unchanged.
        if (value == StyleAndClassNormalization.IsTruthy(binding.OldValue))
        {
            return;
        }
        var operations = BrowserDirectiveOperations.Require();
        var handle = BrowserModelDirective.Handle(element);
        // Deferred-inert transition seam ([V01.01.04.07]); upstream coordinates enter/leave here.
        SetDisplay(operations, handle, value, operations.GetState(handle).OriginalDisplay ?? string.Empty);
    }

    private static void OnBeforeUnmount(object? element, DirectiveBinding binding, VirtualNode node, VirtualNode? previousNode)
    {
        var operations = BrowserDirectiveOperations.Require();
        var handle = BrowserModelDirective.Handle(element);
        // Upstream restores the binding's display before teardown (matters for a leave transition).
        SetDisplay(operations, handle, StyleAndClassNormalization.IsTruthy(binding.Value), operations.GetState(handle).OriginalDisplay ?? string.Empty);
        operations.ReleaseState(handle);
    }

    // Upstream setDisplay: el.style.display = value ? original : 'none'. An empty original removes
    // the inline property so a stylesheet-supplied display is not clobbered.
    private static void SetDisplay(BrowserDirectiveOperations operations, int handle, bool value, string original)
    {
        if (!value)
        {
            operations.SetStyleProperty(handle, "display", "none", false);
        }
        else if (original.Length == 0)
        {
            operations.RemoveStyleProperty(handle, "display");
        }
        else
        {
            operations.SetStyleProperty(handle, "display", original, false);
        }
    }

    // Derive the original inline display from the vnode's style prop (upstream reads el.style.display,
    // equivalent for inline styles); 'none' is normalized to empty so restoring reveals the element.
    private static string OriginalDisplay(VirtualNode node)
    {
        var style = BrowserModelDirective.Property(node, "style");
        if (style is null)
        {
            return string.Empty;
        }
        string? display = null;
        var normalized = StyleAndClassNormalization.NormalizeStyle(style);
        if (normalized is string css)
        {
            StyleAndClassNormalization.ParseStringStyle(css).TryGetValue("display", out var parsed);
            display = parsed as string;
        }
        else if (normalized is IReadOnlyDictionary<string, object?> readOnlyMap)
        {
            if (readOnlyMap.TryGetValue("display", out var mapped))
            {
                display = BrowserModelDirective.FormatValue(mapped);
            }
        }
        else if (normalized is IDictionary<string, object?> map)
        {
            if (map.TryGetValue("display", out var mapped))
            {
                display = BrowserModelDirective.FormatValue(mapped);
            }
        }
        display ??= string.Empty;
        return string.Equals(display, "none", StringComparison.OrdinalIgnoreCase) ? string.Empty : display;
    }
}
