using System;
using System.Threading.Tasks;

using Assimalign.Viu;
using Assimalign.Viu.Shared;

namespace Assimalign.Viu.ServerRenderer;

/// <summary>
/// Drives the platform-agnostic half of a component's lifecycle server-side — the C# port of the
/// <c>renderComponentVNode</c> / <c>renderComponentSubTree</c> setup path in
/// <c>@vue/server-renderer</c> (<c>packages/server-renderer/src/render.ts</c>) over the Core
/// component primitives upstream exposes as <c>ssrUtils</c>. For one component vnode it creates the
/// <see cref="ComponentInstance"/>, resolves props and slots, runs <c>Setup</c> once inside the
/// instance's effect scope, awaits every <see cref="Lifecycle.OnServerPrefetch"/> hook, and renders the
/// component root — returning the subtree vnode for the serializer to walk. No render effect is created
/// (there is no client-side reactivity server-side) and no lifecycle beyond <c>Setup</c>/
/// <c>ServerPrefetch</c> fires, matching upstream's server lifecycle. Not thread-safe (single-threaded
/// JS event-loop model).
/// </summary>
internal static class ServerComponentRenderer
{
    /// <summary>
    /// Sets up <paramref name="componentVirtualNode"/>'s instance and renders its root, awaiting async
    /// server dependencies first.
    /// </summary>
    /// <param name="componentVirtualNode">The component vnode (its <see cref="VirtualNode.Component"/> is set).</param>
    /// <param name="parent">The parent instance, or null at the tree root.</param>
    /// <returns>The rendered, normalized subtree vnode to serialize.</returns>
    public static async Task<VirtualNode> SetupAndRenderRootAsync(VirtualNode componentVirtualNode, ComponentInstance? parent)
    {
        var definition = (IComponent)componentVirtualNode.ComponentType!;
        var instance = new ComponentInstance(definition, componentVirtualNode, parent);
        componentVirtualNode.Component = instance;

        // Split the vnode props into declared props + fallthrough attrs and install parent slots, exactly
        // as the client renderer's MountComponent does (upstream initProps/initSlots).
        ComponentPropertyResolution.Resolve(instance, componentVirtualNode);
        if ((componentVirtualNode.ShapeFlag & ShapeFlags.SlotsChildren) != 0)
        {
            instance.Slots = componentVirtualNode.SlotChildren;
        }

        RunSetup(instance);
        await RunServerPrefetchAsync(instance).ConfigureAwait(false);

        var subtree = RenderComponentRoot(instance);
        instance.Subtree = subtree;
        return subtree;
    }

    private static void RunSetup(ComponentInstance instance)
    {
        // Setup runs exactly once, with the instance current and inside its effect scope, so refs and
        // computeds created in setup are owned by the instance — the C# port of setupComponent's stateful
        // branch (upstream setupStatefulComponent). Viu's Setup is synchronous by contract (the closure it
        // returns IS the proxy-free realization of upstream's state object), so there is no Promise to
        // await here; server-side async data loading is the OnServerPrefetch hook below.
        var context = new ComponentSetupContext(instance);
        instance.PushCurrent();
        try
        {
            instance.RenderFunction = instance.Scope.Run(() => instance.Definition.Setup(instance.Properties, context));
        }
        catch (Exception exception)
        {
            ComponentErrorHandling.Handle(exception, instance, "setup function");
        }
        finally
        {
            instance.PopCurrent();
        }
        instance.RenderFunction ??= static () => null;
    }

    private static async Task RunServerPrefetchAsync(ComponentInstance instance)
    {
        // Await every serverPrefetch hook before serializing this component's subtree (upstream
        // renderComponentSubTree: if (asyncSetupResult || instance.sp?.length) await ...). The hooks run
        // WITHOUT the instance on the current-instance stack — no synchronous vnode work happens across
        // the await, so the ambient Core machinery (ComponentInstance.Current, the block-tree
        // accumulator) stays balanced across suspension points.
        var hooks = instance.GetHooks(LifecycleHookKind.ServerPrefetch);
        if (hooks is null)
        {
            return;
        }
        foreach (var hook in hooks)
        {
            if (hook is not Func<Task> prefetch)
            {
                continue;
            }
            try
            {
                var task = prefetch();
                if (task is not null)
                {
                    await task.ConfigureAwait(false);
                }
            }
            catch (Exception exception)
            {
                // Route through the SSR error path: the OnErrorCaptured chain, then the app-level handler,
                // then rethrow (crash the render) if nothing consumed it — upstream's rejected-promise
                // behavior surfaced through the runtime's error machinery.
                ComponentErrorHandling.Handle(exception, instance, "serverPrefetch hook");
            }
        }
    }

    private static VirtualNode RenderComponentRoot(ComponentInstance instance)
    {
        // The platform-agnostic half of the client renderer's RenderComponentRoot (upstream
        // renderComponentRoot): run the render function with the instance current, normalize the result,
        // and merge single-element-root fallthrough attrs. DOM-side concerns (directive hooks, scopeIds,
        // transition) are not applied server-side in the vnode-walking fallback renderer.
        VirtualNode? root = null;
        instance.PushCurrent();
        try
        {
            root = instance.RenderFunction!();
        }
        catch (Exception exception)
        {
            // A render that threw mid-block must not leak its open block accumulator into later renders
            // when an error hook swallows the failure (upstream clears blockStack in its catch).
            BlockStack.ClearAfterRenderFailure();
            ComponentErrorHandling.Handle(exception, instance, "render function");
        }
        finally
        {
            instance.PopCurrent();
        }
        var normalized = VirtualNodeFactory.Normalize(root);
        if (instance.Definition.InheritAttributes
            && instance.Attributes.Count > 0
            && normalized.Type == VirtualNodeType.Element)
        {
            normalized = VirtualNodeFactory.Clone(normalized, instance.Attributes.ToProperties());
        }
        return normalized;
    }
}
