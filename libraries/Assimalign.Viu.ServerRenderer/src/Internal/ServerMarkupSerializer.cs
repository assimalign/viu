using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading.Tasks;

using Assimalign.Viu;
using Assimalign.Viu.Components;

namespace Assimalign.Viu.ServerRenderer;

/// <summary>Serializes every value in the closed virtual-node algebra.</summary>
internal static class ServerMarkupSerializer
{
    private static readonly IReadOnlyDictionary<string, object?> EmptySlotArguments =
        new ReadOnlyDictionary<string, object?>(new Dictionary<string, object?>());

    internal static async Task RenderAsync(
        SsrRenderState state,
        VirtualNode? node,
        IComponentRenderScope? parent)
    {
        ArgumentNullException.ThrowIfNull(state);
        state.CancellationToken.ThrowIfCancellationRequested();

        if (node is null)
        {
            return;
        }

        switch (node.Kind)
        {
            case VirtualNodeKind.Element:
                await RenderElementAsync(state, (ElementNode)node, parent).ConfigureAwait(false);
                break;
            case VirtualNodeKind.Text:
                state.Push(ServerRender.EscapeHtml(((TextNode)node).Text));
                break;
            case VirtualNodeKind.Comment:
                state.Push(ServerRender.SsrRenderComment(((CommentNode)node).Text));
                break;
            case VirtualNodeKind.Static:
                RenderStatic(state, (StaticNode)node);
                break;
            case VirtualNodeKind.Fragment:
                await RenderFragmentAsync(state, (FragmentNode)node, parent).ConfigureAwait(false);
                break;
            case VirtualNodeKind.Component:
                await RenderComponentAsync(state, (ComponentNode)node, parent).ConfigureAwait(false);
                break;
            case VirtualNodeKind.Teleport:
                await RenderTeleportAsync(state, (TeleportNode)node, parent).ConfigureAwait(false);
                break;
            case VirtualNodeKind.KeepAlive:
                await RenderDefaultSlotAsync(
                    state,
                    ((KeepAliveNode)node).Invocation,
                    parent).ConfigureAwait(false);
                break;
            case VirtualNodeKind.Suspense:
                await RenderDefaultSlotAsync(
                    state,
                    ((SuspenseNode)node).Invocation,
                    parent).ConfigureAwait(false);
                break;
            case VirtualNodeKind.Transition:
                await RenderDefaultSlotAsync(
                    state,
                    ((TransitionNode)node).Invocation,
                    parent).ConfigureAwait(false);
                break;
            default:
                throw new InvalidOperationException(
                    $"Unknown virtual node kind: {node.Kind}.");
        }
    }

    internal static async Task RenderChildrenAsync(
        SsrRenderState state,
        IReadOnlyList<VirtualNode> children,
        IComponentRenderScope? parent)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(children);

        for (int index = 0; index < children.Count; index++)
        {
            await RenderAsync(state, children[index], parent).ConfigureAwait(false);
        }
    }

    private static async Task RenderElementAsync(
        SsrRenderState state,
        ElementNode element,
        IComponentRenderScope? parent)
    {
        string name = element.Name.ToString();
        state.Push("<");
        state.Push(name);
        state.Push(ServerRender.SsrRenderAttributes(element.Bindings, element.Name));

        if (HtmlSerializationRules.IsVoidElement(element.Name))
        {
            state.Push(">");
            return;
        }

        state.Push(">");
        if (TryGetChildOverride(element, "innerHTML", out object? innerHtml)
            && innerHtml is not null)
        {
            state.Push(DisplayStringFormatter.ToDisplayString(innerHtml));
        }
        else if (TryGetChildOverride(element, "textContent", out object? textContent)
            && textContent is not null)
        {
            state.Push(ServerRender.EscapeHtml(DisplayStringFormatter.ToDisplayString(textContent)));
        }
        else if (string.Equals(
                element.Name.LocalName,
                "textarea",
                StringComparison.OrdinalIgnoreCase)
            && TryGetChildOverride(element, "value", out object? value)
            && value is not null)
        {
            state.Push(ServerRender.EscapeHtml(DisplayStringFormatter.ToDisplayString(value)));
        }
        else
        {
            await RenderChildrenAsync(state, element.Children, parent).ConfigureAwait(false);
        }

        state.Push("</");
        state.Push(name);
        state.Push(">");
    }

    private static async Task RenderFragmentAsync(
        SsrRenderState state,
        FragmentNode fragment,
        IComponentRenderScope? parent)
    {
        state.Push(HydrationMarkers.FragmentStart);
        await RenderChildrenAsync(state, fragment.Children, parent).ConfigureAwait(false);
        state.Push(HydrationMarkers.FragmentEnd);
    }

    private static async Task RenderComponentAsync(
        SsrRenderState state,
        ComponentNode component,
        IComponentRenderScope? parent)
    {
        HydrationStrategyKind hydrationStrategy =
            component.Invocation.HydrationStrategy?.Kind
            ?? HydrationStrategyKind.Immediate;
        bool isDeferredHydration = hydrationStrategy != HydrationStrategyKind.Immediate;
        if (isDeferredHydration)
        {
            state.Push(HydrationMarkers.GetLazyHydrationStart(hydrationStrategy));
        }

        ComponentRegistration componentRegistration =
            state.Application.Components.Resolve(component.Component);
        if (state.ServerRenders is not null
            && state.ServerRenders.TryResolve(
                componentRegistration.Reference,
                out ServerRenderRegistration? serverRegistration))
        {
            ComponentActivation activation = await state.ComponentHost.ActivateForServerAsync(
                new ComponentRenderRequest(component, parent),
                state.CancellationToken).ConfigureAwait(false);
            await using ComponentRenderLease compiledScope =
                new(activation, tree: null);
            SsrWriter compiledWriter = new(recordFlushBoundaries: true);
            SsrContext compiledContext = state.Context.CreateRenderTransaction();
            SsrRenderState compiledState = state.CreateBuffer(
                compiledWriter,
                compiledContext);
            bool completed = false;
            try
            {
                await serverRegistration!.Render(
                    compiledState,
                    compiledScope.Instance,
                    compiledScope.Frame,
                    compiledScope).ConfigureAwait(false);
                completed = true;
            }
            catch (Exception exception)
            {
                activation.Context.RouteError(exception, "component render");
                state.Push(HydrationMarkers.EmptyComment);
            }

            if (completed)
            {
                state.Context.CommitRenderTransaction(compiledContext);
                foreach (string chunk in compiledWriter.RecordedFlushChunks)
                {
                    state.Push(chunk);
                    await state.FlushAsync().ConfigureAwait(false);
                }

                state.Push(compiledWriter.ToStringResult());
            }
        }
        else
        {
            await using IComponentRenderScope scope = await state.ComponentHost.RenderAsync(
                new ComponentRenderRequest(component, parent),
                state.CancellationToken).ConfigureAwait(false);

            if (scope.Tree is null)
            {
                state.Push(HydrationMarkers.EmptyComment);
            }
            else
            {
                await RenderAsync(state, scope.Tree, scope).ConfigureAwait(false);
            }
        }

        if (isDeferredHydration)
        {
            state.Push(HydrationMarkers.LazyHydrationEnd);
        }

        // A complete component subtree is the streaming boundary required by [SSR-1].
        await state.FlushAsync().ConfigureAwait(false);
    }

    private static Task RenderTeleportAsync(
        SsrRenderState state,
        TeleportNode teleport,
        IComponentRenderScope? parent) =>
        ServerRender.SsrRenderTeleportAsync(
            state,
            contentState => RenderChildrenAsync(contentState, teleport.Children, parent),
            teleport.TargetIdentifier,
            teleport.IsDisabled);

    private static Task RenderDefaultSlotAsync(
        SsrRenderState state,
        ComponentInvocation invocation,
        IComponentRenderScope? parent)
    {
        if (!invocation.Slots.TryGetValue("default", out ComponentSlot? slot))
        {
            return Task.CompletedTask;
        }

        return RenderAsync(state, slot(EmptySlotArguments), parent);
    }

    private static void RenderStatic(SsrRenderState state, StaticNode node)
    {
        if (node.Format != MarkupFormat.Html)
        {
            throw new NotSupportedException(
                "The HTML server renderer cannot consume a non-HTML static payload.");
        }

        state.Push(node.Content);
    }

    private static bool TryGetChildOverride(
        ElementNode element,
        string localName,
        out object? value)
    {
        for (int index = element.Bindings.Count - 1; index >= 0; index--)
        {
            ElementBinding binding = element.Bindings[index];
            if (binding.Kind != ElementBindingKind.Event
                && string.Equals(
                    binding.Name.LocalName,
                    localName,
                    StringComparison.Ordinal))
            {
                value = binding.Value;
                return true;
            }
        }

        value = null;
        return false;
    }
}
