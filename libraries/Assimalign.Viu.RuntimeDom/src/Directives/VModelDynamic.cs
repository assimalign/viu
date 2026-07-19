using System;

using Assimalign.Viu.RuntimeCore;

namespace Assimalign.Viu.RuntimeDom;

/// <summary>
/// The <c>v-model</c> directive for elements whose kind is only known at runtime — the C# port of
/// upstream's <c>vModelDynamic</c>
/// (https://github.com/vuejs/core/blob/main/packages/runtime-dom/src/directives/vModel.ts). Each
/// hook resolves the concrete directive from the element's current tag and <c>type</c> and forwards
/// to it, so <c>&lt;input :type="t"&gt;</c> switches between text, checkbox, and radio behavior when
/// <c>t</c> changes at runtime; <c>&lt;select&gt;</c> and <c>&lt;textarea&gt;</c> resolve by tag.
/// Emitted by the compiler when the <c>v-model</c> element's type is dynamic
/// ([V01.01.05.03]). Stateless singleton (<see cref="Instance"/>).
/// </summary>
public sealed class VModelDynamic : IDirective
{
    /// <summary>The shared directive instance the compiler references.</summary>
    public static readonly VModelDynamic Instance = new();

    private VModelDynamic()
    {
    }

    /// <inheritdoc/>
    public DirectiveHook? Created => OnCreated;

    /// <inheritdoc/>
    public DirectiveHook? Mounted => OnMounted;

    /// <inheritdoc/>
    public DirectiveHook? BeforeUpdate => OnBeforeUpdate;

    /// <inheritdoc/>
    public DirectiveHook? Updated => OnUpdated;

    /// <inheritdoc/>
    public DirectiveHook? BeforeUnmount => OnBeforeUnmount;

    private static void OnCreated(object? element, DirectiveBinding binding, VirtualNode node, VirtualNode? previousNode)
        => Resolve(node).Created?.Invoke(element, binding, node, previousNode);

    private static void OnMounted(object? element, DirectiveBinding binding, VirtualNode node, VirtualNode? previousNode)
        => Resolve(node).Mounted?.Invoke(element, binding, node, previousNode);

    private static void OnBeforeUpdate(object? element, DirectiveBinding binding, VirtualNode node, VirtualNode? previousNode)
        => Resolve(node).BeforeUpdate?.Invoke(element, binding, node, previousNode);

    private static void OnUpdated(object? element, DirectiveBinding binding, VirtualNode node, VirtualNode? previousNode)
        => Resolve(node).Updated?.Invoke(element, binding, node, previousNode);

    private static void OnBeforeUnmount(object? element, DirectiveBinding binding, VirtualNode node, VirtualNode? previousNode)
        => Resolve(node).BeforeUnmount?.Invoke(element, binding, node, previousNode);

    // Upstream resolveDynamicModel: SELECT/TEXTAREA by tag, otherwise by input type.
    private static IDirective Resolve(VirtualNode node)
    {
        if (string.Equals(node.ElementTag, "select", StringComparison.Ordinal))
        {
            return VModelSelect.Instance;
        }
        if (string.Equals(node.ElementTag, "textarea", StringComparison.Ordinal))
        {
            return VModelText.Instance;
        }
        return BrowserModelDirective.FormatValue(BrowserModelDirective.Property(node, "type")) switch
        {
            "checkbox" => VModelCheckbox.Instance,
            "radio" => VModelRadio.Instance,
            _ => VModelText.Instance,
        };
    }
}
