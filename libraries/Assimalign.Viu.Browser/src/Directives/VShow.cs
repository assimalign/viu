using System;
using System.Collections.Generic;

using Assimalign.Viu;
using Assimalign.Viu.Shared;


namespace Assimalign.Viu.Browser;

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
/// <c>el.style.display</c> — equivalent for inline styles and one fewer boundary crossing.
/// </para>
/// <para>
/// <b>Persisted transitions</b> — a <c>&lt;Transition&gt;</c> wrapping a <c>v-show</c> element
/// (upstream <c>vShow.ts</c> persisted handling + <c>components/Transition.ts</c>,
/// https://github.com/vuejs/core/blob/main/packages/runtime-dom/src/components/Transition.ts). When
/// the vnode carries a <see cref="VirtualNode.Transition"/> resolved as
/// <see cref="BaseTransitionProperties.Persisted"/>, the element stays mounted and the enter/leave
/// choreography runs on <em>show/hide</em> instead of mount/unmount: the renderer skips its own
/// enter/leave for a persisted transition, so this directive drives <c>beforeEnter</c>/<c>enter</c>
/// on reveal and <c>leave</c> on hide (upstream's <c>persisted</c> flag changes <em>who</em> calls
/// the hooks, not the hook contract). On reveal, <c>beforeEnter</c> and <see cref="SetDisplay"/> make
/// the element visible before the enter frame; on hide, the element is hidden only once the leave
/// transition completes — the leave <c>done</c> callback runs <see cref="SetDisplay"/>. The
/// transition's own enter/leave cancellation (upstream <c>el[enterCbKey]</c>/<c>el[leaveCbKey]</c>)
/// converges an interrupted toggle to the final visibility with the saved display value intact and no
/// orphaned transition classes. Keying on the resolved <c>Persisted</c> flag — the exact complement
/// of the renderer's persisted skip — is Viu's explicit form of upstream's coupling, which relies on
/// the compiler injecting <c>persisted</c> whenever a <c>&lt;Transition&gt;</c> wraps a <c>v-show</c>
/// child.
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
        // Captured once, here, and preserved across every later toggle (upstream vShowOriginalDisplay).
        operations.GetState(handle).OriginalDisplay = original;
        var value = StyleAndClassNormalization.IsTruthy(binding.Value);
        // Upstream vShow.beforeMount: with a persisted transition present and a truthy value, defer the
        // reveal to transition.beforeEnter (the enter choreography drives display); otherwise toggle
        // display directly. beforeMount timing hides an initially-falsy element before its first paint.
        if (value && PersistedTransition(node) is { } transition)
        {
            transition.BeforeEnter(element!);
        }
        else
        {
            SetDisplay(operations, handle, value, original);
        }
    }

    private static void OnMounted(object? element, DirectiveBinding binding, VirtualNode node, VirtualNode? previousNode)
    {
        // Upstream vShow.mounted: run transition.enter for a truthy persisted-transition element. On the
        // first mount the transition state is not yet mounted, so enter is a no-op unless `appear` is set.
        if (StyleAndClassNormalization.IsTruthy(binding.Value) && PersistedTransition(node) is { } transition)
        {
            transition.Enter(element!);
        }
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
        var original = operations.GetState(handle).OriginalDisplay ?? string.Empty;
        if (PersistedTransition(node) is { } transition)
        {
            // Upstream vShow.updated: show -> beforeEnter, setDisplay(true), enter; hide -> leave, then
            // setDisplay(false) once the leave completes. beforeEnter/leave cancel any in-flight leave/enter
            // on the same element first, so an interrupted toggle converges to the final visibility with the
            // saved display intact and no orphaned transition classes.
            if (value)
            {
                transition.BeforeEnter(element!);
                SetDisplay(operations, handle, true, original);
                transition.Enter(element!);
            }
            else
            {
                transition.Leave(element!, () => SetDisplay(operations, handle, false, original));
            }
        }
        else
        {
            SetDisplay(operations, handle, value, original);
        }
    }

    private static void OnBeforeUnmount(object? element, DirectiveBinding binding, VirtualNode node, VirtualNode? previousNode)
    {
        var operations = BrowserDirectiveOperations.Require();
        var handle = BrowserModelDirective.Handle(element);
        // Upstream restores the binding's display before teardown (matters for a leave transition).
        SetDisplay(operations, handle, StyleAndClassNormalization.IsTruthy(binding.Value), operations.GetState(handle).OriginalDisplay ?? string.Empty);
        operations.ReleaseState(handle);
    }

    // The resolved persisted transition hooks stamped onto the vnode by an enclosing <Transition>
    // (upstream: vnode.transition), or null when the element is not a persisted-transition child. Keying
    // on Persisted — the exact complement of the renderer's persisted skip (VirtualNode.Transition is
    // { Persisted: false }) — means v-show drives the enter/leave choreography exactly when the renderer
    // does not, so the two never both fire (upstream couples them through the compiler's guaranteed
    // `persisted` injection when a <Transition> wraps a v-show child).
    private static TransitionHooks? PersistedTransition(VirtualNode node)
        => node.Transition is { Persisted: true } transition ? transition : null;

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
