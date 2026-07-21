using System;
using System.Collections.Generic;

namespace Assimalign.Viu;

/// <summary>
/// Attaches and resolves runtime custom directives — the C# port of <c>withDirectives</c>
/// (<c>packages/runtime-core/src/directives.ts</c>) and <c>resolveDirective</c>
/// (<c>packages/runtime-core/src/helpers/resolveAssets.ts</c>,
/// https://vuejs.org/guide/reusability/custom-directives.html). <see cref="WithDirectives(VirtualNode, DirectiveArgument[])"/>
/// records directive bindings on a vnode; the renderer then invokes each directive's hooks at the
/// created/beforeMount/mounted/beforeUpdate/updated/beforeUnmount/unmounted points.
/// </summary>
public static class Directives
{
    /// <summary>
    /// Attaches one directive to <paramref name="node"/> (upstream: <c>withDirectives(vnode, [[dir,
    /// value, arg, modifiers]])</c> with a single entry). Must be called during a render, so the
    /// binding can capture the rendering instance; with no active instance it warns and returns the
    /// vnode unchanged (upstream parity). Applying to a component vnode transfers the binding to the
    /// component's root element when it renders.
    /// </summary>
    /// <param name="node">The element (or component) vnode to bind to.</param>
    /// <param name="directive">The directive to attach.</param>
    /// <param name="value">The bound value, or null.</param>
    /// <param name="argument">The directive argument (the <c>foo</c> in <c>v-x:foo</c>), or null.</param>
    /// <param name="modifiers">The modifiers, or null for none.</param>
    /// <returns><paramref name="node"/>, for chaining with vnode creation.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="node"/> or <paramref name="directive"/> is null.</exception>
    public static VirtualNode WithDirectives(
        VirtualNode node,
        IDirective directive,
        object? value = null,
        string? argument = null,
        IReadOnlyDictionary<string, bool>? modifiers = null)
    {
        ArgumentNullException.ThrowIfNull(directive);
        return WithDirectives(node, new DirectiveArgument(directive, value, argument, modifiers));
    }

    /// <summary>
    /// Attaches one or more directives to <paramref name="node"/> (upstream: <c>withDirectives(vnode,
    /// directives)</c>). Must be called during a render — the bindings capture
    /// <see cref="ComponentInstance.Current"/> as the owning instance; with no active instance it
    /// warns and returns the vnode unchanged.
    /// </summary>
    /// <param name="node">The element (or component) vnode to bind to.</param>
    /// <param name="directives">The directive bindings to attach.</param>
    /// <returns><paramref name="node"/>, for chaining with vnode creation.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="node"/> or <paramref name="directives"/> is null.</exception>
    public static VirtualNode WithDirectives(VirtualNode node, params DirectiveArgument[] directives)
    {
        ArgumentNullException.ThrowIfNull(node);
        ArgumentNullException.ThrowIfNull(directives);
        var instance = ComponentInstance.Current;
        if (instance is null)
        {
            RuntimeWarnings.Warn("WithDirectives can only be used inside a render function.");
            return node;
        }
        if (directives.Length == 0)
        {
            return node;
        }
        var bindings = node.Directives ??= new List<DirectiveBinding>(directives.Length);
        foreach (var argument in directives)
        {
            if (argument is null)
            {
                continue;
            }
            bindings.Add(new DirectiveBinding(
                argument.Directive,
                instance,
                argument.Value,
                argument.Argument,
                argument.Modifiers ?? DirectiveBinding.EmptyModifiers));
        }
        return node;
    }

    /// <summary>
    /// Resolves a directive by name against the current instance's app registry (upstream:
    /// <c>resolveDirective(name)</c>). The name resolves case-insensitively (raw, camelCase,
    /// PascalCase); an unresolved name warns in dev and returns null.
    /// </summary>
    /// <param name="name">The registered directive name.</param>
    /// <returns>The resolved directive, or null.</returns>
    /// <exception cref="ArgumentException"><paramref name="name"/> is null or empty.</exception>
    public static IDirective? ResolveDirective(string name)
    {
        ArgumentException.ThrowIfNullOrEmpty(name);
        var resolved = ComponentInstance.Current?.AppContext?.ResolveDirective(name);
        if (resolved is null)
        {
            RuntimeWarnings.Warn($"Failed to resolve directive: {name}");
        }
        return resolved;
    }
}
