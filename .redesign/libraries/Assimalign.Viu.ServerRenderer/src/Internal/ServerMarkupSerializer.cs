using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Net;
using System.Threading;
using System.Threading.Tasks;

using Assimalign.Viu;
using Assimalign.Viu.Components;

namespace Assimalign.Viu.ServerRenderer;

internal sealed class ServerMarkupSerializer
{
    private static readonly IReadOnlyDictionary<string, object?> EmptySlotArguments =
        new Dictionary<string, object?>();

    private readonly ComponentHost _componentHost;

    internal ServerMarkupSerializer(ComponentHost componentHost)
    {
        _componentHost = componentHost;
    }

    internal async ValueTask WriteAsync(
        VirtualNode? node,
        TextWriter writer,
        IComponentRenderScope? parent,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        switch (node)
        {
            case null:
                return;
            case TextNode text:
                await WriteTextAsync(
                    writer,
                    WebUtility.HtmlEncode(text.Text),
                    cancellationToken).ConfigureAwait(false);
                return;
            case CommentNode comment:
                await WriteTextAsync(writer, "<!--", cancellationToken).ConfigureAwait(false);
                await WriteTextAsync(
                    writer,
                    comment.Text.Replace("--", "- -", StringComparison.Ordinal),
                    cancellationToken).ConfigureAwait(false);
                await WriteTextAsync(writer, "-->", cancellationToken).ConfigureAwait(false);
                return;
            case StaticNode staticNode:
                if (staticNode.Format != MarkupFormat.Html)
                {
                    throw new NotSupportedException(
                        "The HTML server renderer cannot consume a non-HTML static payload.");
                }

                await WriteTextAsync(writer, staticNode.Content, cancellationToken).ConfigureAwait(false);
                return;
            case ElementNode element:
                await WriteElementAsync(element, writer, parent, cancellationToken).ConfigureAwait(false);
                return;
            case FragmentNode fragment:
                foreach (var child in fragment.Children)
                {
                    await WriteAsync(child, writer, parent, cancellationToken).ConfigureAwait(false);
                }

                return;
            case ComponentNode component:
                await using (var scope = await _componentHost.RenderAsync(
                    new ComponentRenderRequest(component, parent),
                    cancellationToken).ConfigureAwait(false))
                {
                    await WriteAsync(
                        scope.Tree,
                        writer,
                        scope,
                        cancellationToken).ConfigureAwait(false);
                }

                return;
            case TeleportNode teleport:
                foreach (var child in teleport.Children)
                {
                    await WriteAsync(child, writer, parent, cancellationToken).ConfigureAwait(false);
                }

                return;
            case KeepAliveNode keepAlive:
                await WriteDefaultSlotAsync(
                    keepAlive.Invocation, writer, parent, cancellationToken).ConfigureAwait(false);
                return;
            case SuspenseNode suspense:
                await WriteDefaultSlotAsync(
                    suspense.Invocation, writer, parent, cancellationToken).ConfigureAwait(false);
                return;
            case TransitionNode transition:
                await WriteDefaultSlotAsync(
                    transition.Invocation, writer, parent, cancellationToken).ConfigureAwait(false);
                return;
            default:
                throw new NotSupportedException(
                    $"The server renderer does not recognize virtual node kind '{node.Kind}'.");
        }
    }

    private async ValueTask WriteDefaultSlotAsync(
        ComponentInvocation invocation,
        TextWriter writer,
        IComponentRenderScope? parent,
        CancellationToken cancellationToken)
    {
        if (invocation.Slots.TryGetValue("default", out var slot))
        {
            await WriteAsync(
                slot(EmptySlotArguments),
                writer,
                parent,
                cancellationToken).ConfigureAwait(false);
        }
    }

    private async ValueTask WriteElementAsync(
        ElementNode element,
        TextWriter writer,
        IComponentRenderScope? parent,
        CancellationToken cancellationToken)
    {
        var name = element.Name.ToString();
        await WriteTextAsync(writer, "<", cancellationToken).ConfigureAwait(false);
        await WriteTextAsync(writer, name, cancellationToken).ConfigureAwait(false);

        foreach (var binding in element.Bindings)
        {
            if (binding.Kind != ElementBindingKind.Attribute)
            {
                continue;
            }

            await WriteTextAsync(writer, " ", cancellationToken).ConfigureAwait(false);
            await WriteTextAsync(writer, binding.Name.ToString(), cancellationToken).ConfigureAwait(false);
            if (binding.Value is not null)
            {
                await WriteTextAsync(writer, "=\"", cancellationToken).ConfigureAwait(false);
                await WriteTextAsync(
                    writer,
                    WebUtility.HtmlEncode(
                        Convert.ToString(binding.Value, CultureInfo.InvariantCulture)
                            ?? string.Empty)
                        ?? string.Empty,
                    cancellationToken).ConfigureAwait(false);
                await WriteTextAsync(writer, "\"", cancellationToken).ConfigureAwait(false);
            }
        }

        await WriteTextAsync(writer, ">", cancellationToken).ConfigureAwait(false);
        foreach (var child in element.Children)
        {
            await WriteAsync(child, writer, parent, cancellationToken).ConfigureAwait(false);
        }

        await WriteTextAsync(writer, "</", cancellationToken).ConfigureAwait(false);
        await WriteTextAsync(writer, name, cancellationToken).ConfigureAwait(false);
        await WriteTextAsync(writer, ">", cancellationToken).ConfigureAwait(false);
    }

    private static ValueTask WriteTextAsync(
        TextWriter writer,
        string text,
        CancellationToken cancellationToken) =>
        new(writer.WriteAsync(text.AsMemory(), cancellationToken));
}
