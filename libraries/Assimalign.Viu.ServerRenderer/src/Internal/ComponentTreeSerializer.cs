using System;
using System.Collections.Generic;
using System.Threading.Tasks;

using Assimalign.Viu;
using Assimalign.Viu.Components;
using Assimalign.Viu.Shared;

namespace Assimalign.Viu.ServerRenderer;

/// <summary>
/// Serializes the unified component tree to HTML using Vue-compatible server-rendering markers.
/// </summary>
internal static class ComponentTreeSerializer
{
    internal static async Task RenderAsync(
        SsrRenderState state,
        IComponent component,
        ComponentContext? owner)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(component);
        state.CancellationToken.ThrowIfCancellationRequested();

        switch (component.Kind)
        {
            case ComponentKind.Element:
                await RenderElementAsync(
                    state,
                    Require<IElementComponent>(component, ComponentKind.Element),
                    owner).ConfigureAwait(false);
                break;
            case ComponentKind.Template:
                await ServerComponentRenderer.RenderAsync(
                    state,
                    Require<ITemplateComponent>(component, ComponentKind.Template),
                    owner).ConfigureAwait(false);
                break;
            case ComponentKind.Text:
                state.Push(
                    ServerRender.EscapeHtml(
                        Require<ITextComponent>(component, ComponentKind.Text).Text));
                break;
            case ComponentKind.Comment:
                state.Push(
                    ServerRender.SsrRenderComment(
                        Require<ICommentComponent>(component, ComponentKind.Comment).Text));
                break;
            case ComponentKind.Static:
                state.Push(Require<IStaticComponent>(component, ComponentKind.Static).Content);
                break;
            case ComponentKind.Fragment:
                await RenderFragmentAsync(
                    state,
                    Require<IFragmentComponent>(component, ComponentKind.Fragment),
                    owner).ConfigureAwait(false);
                break;
            case ComponentKind.Teleport:
                await RenderTeleportAsync(
                    state,
                    Require<ITeleportComponent>(component, ComponentKind.Teleport),
                    owner).ConfigureAwait(false);
                break;
            default:
                throw new InvalidOperationException(
                    $"Unknown component kind: {component.Kind}.");
        }
    }

    internal static async Task RenderChildrenAsync(
        SsrRenderState state,
        IReadOnlyList<IComponent> children,
        ComponentContext? owner)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(children);

        for (int index = 0; index < children.Count; index++)
        {
            await RenderAsync(state, children[index], owner).ConfigureAwait(false);
        }
    }

    private static async Task RenderElementAsync(
        SsrRenderState state,
        IElementComponent element,
        ComponentContext? owner)
    {
        string tag = element.Tag;
        IComponentAttributeCollection attributes = element.Attributes;

        state.Push("<" + tag);
        state.Push(ServerRender.SsrRenderAttrs(attributes, tag));
        if (owner?.ScopeIdentifier is { Length: > 0 } scopeIdentifier
            && !attributes.TryGetValue(scopeIdentifier, out _))
        {
            state.Push(ServerRender.SsrRenderDynamicAttr(scopeIdentifier, string.Empty, tag));
        }

        if (DomKnowledge.IsVoidTag(tag))
        {
            state.Push(">");
            return;
        }

        state.Push(">");

        if (attributes.TryGetValue("innerHTML", out object? innerHtml)
            && innerHtml is not null)
        {
            state.Push(DisplayStringFormatter.ToDisplayString(innerHtml));
        }
        else if (attributes.TryGetValue("textContent", out object? textContent)
            && textContent is not null)
        {
            state.Push(
                ServerRender.EscapeHtml(
                    DisplayStringFormatter.ToDisplayString(textContent)));
        }
        else if (string.Equals(tag, "textarea", StringComparison.Ordinal)
            && attributes.TryGetValue("value", out object? value)
            && value is not null)
        {
            state.Push(
                ServerRender.EscapeHtml(
                    DisplayStringFormatter.ToDisplayString(value)));
        }
        else
        {
            await RenderChildrenAsync(state, element.Children, owner).ConfigureAwait(false);
        }

        state.Push("</" + tag + ">");
    }

    private static async Task RenderFragmentAsync(
        SsrRenderState state,
        IFragmentComponent fragment,
        ComponentContext? owner)
    {
        state.Push(SsrMarkers.FragmentStart);
        await RenderChildrenAsync(state, fragment.Children, owner).ConfigureAwait(false);
        state.Push(SsrMarkers.FragmentEnd);
    }

    private static Task RenderTeleportAsync(
        SsrRenderState state,
        ITeleportComponent teleport,
        ComponentContext? owner)
    {
        string? target = teleport.Target as string;
        return ServerRender.SsrRenderTeleportAsync(
            state,
            contentState => RenderChildrenAsync(contentState, teleport.Children, owner),
            target,
            teleport.IsDisabled);
    }

    private static TComponent Require<TComponent>(
        IComponent component,
        ComponentKind expectedKind)
        where TComponent : class, IComponent
    {
        return component as TComponent
            ?? throw new InvalidOperationException(
                $"A component reporting kind {expectedKind} must implement {typeof(TComponent).Name}.");
    }
}
