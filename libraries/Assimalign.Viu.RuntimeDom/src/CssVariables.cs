using System;
using System.Collections.Generic;

using Assimalign.Viu;

namespace Assimalign.Viu.RuntimeDom;

/// <summary>
/// The <c>UseCssVars</c> runtime — the C# port of Vue 3.5's <c>useCssVars</c>
/// (<c>@vue/runtime-dom</c> <c>helpers/useCssVars.ts</c>,
/// https://vuejs.org/api/sfc-css-features.html#v-bind-in-css), the runtime half of <c>v-bind()</c> in CSS.
/// It applies a component's evaluated <c>v-bind()</c> expressions as CSS custom properties on the
/// component's root element(s) and re-applies them reactively, on the post-flush phase, when a bound value
/// changes — without re-rendering the component.
/// <para>
/// It consumes only <em>generated metadata</em>: the getter the source generator emits
/// (<c>ApplyCssVariables</c>, [V01.01.06.06]) maps each hashed custom-property name to its evaluated value,
/// and this runtime never references the compiler. The hashed names match the <c>var(--&lt;hash&gt;)</c>
/// the CSS compilation produced, because both derive from the same component-scoped hash of the same
/// expression.
/// </para>
/// Not thread-safe (single-threaded JS event-loop model).
/// </summary>
public static class CssVariables
{
    /// <summary>
    /// Registers the component's <c>v-bind()</c> custom properties (upstream: <c>useCssVars(getter)</c>).
    /// Call during the component's <c>Setup</c>: <paramref name="getter"/> is evaluated once after mount
    /// (applying the initial values) and again in the post-flush phase whenever a value it reads changes,
    /// each pass batching every <c>style.setProperty</c> on a root element into one interop crossing. The
    /// watcher and its cleanup stop with the component. Called with no active component instance (outside
    /// <c>Setup</c>) it is a no-op, mirroring upstream's dev-time guard.
    /// </summary>
    /// <param name="getter">
    /// Produces the map from each hashed custom-property name (without the leading <c>--</c>) to its current
    /// value. Reading reactive state inside it is what makes the properties update reactively.
    /// </param>
    /// <exception cref="ArgumentNullException"><paramref name="getter"/> is <see langword="null"/>.</exception>
    public static void UseCssVars(Func<IReadOnlyDictionary<string, string>> getter)
    {
        if (getter is null)
        {
            throw new ArgumentNullException(nameof(getter));
        }

        var instance = ComponentInstance.Current;
        if (instance is null)
        {
            // Upstream warns and returns; without an instance there is no subtree to apply to.
            return;
        }

        void SetVars() => ApplyToVirtualNode(instance.Subtree, getter());

        // Upstream onBeforeUpdate -> queuePostFlushCb(setVars): a structural re-render can change which
        // element is the root, so re-apply after each update's patch. The reusable job deduplicates within a
        // flush, matching upstream's stable-function queuePostFlushCb.
        var updateJob = new SchedulerJob(SetVars);
        Lifecycle.OnBeforeUpdate(() => Scheduler.QueuePostFlushCallback(updateJob));

        Lifecycle.OnMounted(() =>
        {
            // Upstream: watch(setVars, NOOP, { flush: 'post' }). WatchEffect runs setVars immediately — here
            // that first run is inside onMounted, where the subtree's element handles exist, so it is the
            // initial application — and re-runs it in the post-flush phase whenever a value the getter reads
            // changes, tracking those values without touching the render effect (so no re-render).
            var handle = ViuWatch.WatchEffect(SetVars, new WatchOptions { Flush = WatchFlushMode.Post });
            Lifecycle.OnUnmounted(handle.Stop);
        });
    }

    // The C# port of upstream setVarsOnVNode: find the component's actual root element(s), drilling through
    // higher-order components and descending into fragments, and apply the vars to each. Suspense is not
    // implemented ([V01.01.03.19]), so its branch is intentionally absent.
    private static void ApplyToVirtualNode(VirtualNode? virtualNode, IReadOnlyDictionary<string, string> vars)
    {
        if (virtualNode is null)
        {
            return;
        }

        // Drill through higher-order components until a non-component vnode (upstream: while (vnode.component)
        // vnode = vnode.component.subTree).
        while (virtualNode.Component is ComponentInstance childInstance && childInstance.Subtree is { } childSubtree)
        {
            virtualNode = childSubtree;
        }

        switch (virtualNode.Type)
        {
            case VirtualNodeType.Element when virtualNode.El is { } element:
                ApplyToElement(element, vars);
                break;
            case VirtualNodeType.Fragment when virtualNode.ArrayChildren is { } children:
                foreach (var child in children)
                {
                    ApplyToVirtualNode(child, vars);
                }

                break;
            case VirtualNodeType.Static when virtualNode.El is { } staticElement:
                // A static chunk is inserted as one span; apply to its first element (upstream walks el..anchor).
                ApplyToElement(staticElement, vars);
                break;
        }
    }

    // Batches every custom-property write on one element into a single interop crossing (the acceptance
    // criterion: never one interop call per property). The element handle is the DOM renderer's boxed int.
    private static void ApplyToElement(object element, IReadOnlyDictionary<string, string> vars)
    {
        if (vars.Count == 0 || element is not int handle)
        {
            return;
        }

        var operations = BrowserDirectiveOperations.Current;
        if (operations is null)
        {
            return;
        }

        var names = new string[vars.Count];
        var values = new string[vars.Count];
        var index = 0;
        foreach (var pair in vars)
        {
            // The generated names are the bare hashes; the custom-property spelling adds the leading '--'
            // (upstream setVarsOnNode: style.setProperty(`--${key}`, ...)).
            names[index] = "--" + pair.Key;
            values[index] = pair.Value;
            index++;
        }

        operations.SetCssVariables(handle, names, values);
    }
}
