using System;
using System.Collections.Generic;

namespace Assimalign.Vue.RuntimeCore;

/// <summary>
/// The shared per-application context — the C# port of upstream's <c>AppContext</c>
/// (<c>packages/runtime-core/src/apiCreateApp.ts</c>). Carries the registries and app-level
/// provides that every component in the app resolves against: it is attached to the root vnode at
/// mount and inherited by every <see cref="ComponentInstance"/> down the tree
/// (<see cref="ComponentInstance.AppContext"/>), exactly as upstream inherits
/// <c>instance.appContext</c> from the parent (or the root vnode). Internal — the public surface is
/// <see cref="VueApplication{TNode}"/>. Not thread-safe (single-threaded JS event-loop model).
/// </summary>
internal sealed class ApplicationContext
{
    /// <summary>
    /// App-level provides (upstream: <c>appContext.provides</c>), the final fallback in the
    /// inject lookup chain (see <see cref="DependencyInjection"/>). Keyed by the same
    /// <see cref="InjectionKey{T}"/>/string identities component-level provides use.
    /// </summary>
    public Dictionary<object, object?> Provides { get; } = [];

    /// <summary>The name → definition registry (upstream: <c>appContext.components</c>).</summary>
    public Dictionary<string, IComponentDefinition> Components { get; } = new(StringComparer.Ordinal);

    /// <summary>The app-level configuration (upstream: <c>appContext.config</c>).</summary>
    public ApplicationConfiguration Config { get; } = new();

    /// <summary>
    /// Resolves a registered component by name (upstream: <c>resolveAsset(COMPONENTS, name)</c> in
    /// <c>helpers/resolveAssets.ts</c>): the raw name, then its camelCase form, then its
    /// PascalCase form, so <c>my-widget</c> resolves a <c>MyWidget</c> registration and vice versa.
    /// </summary>
    /// <param name="name">The component name as used at the call site.</param>
    /// <returns>The registered definition, or null when none matches.</returns>
    public IComponentDefinition? ResolveComponent(string name)
    {
        if (Components.TryGetValue(name, out var direct))
        {
            return direct;
        }
        var camel = ComponentInstance.Camelize(name);
        if (!string.Equals(camel, name, StringComparison.Ordinal) && Components.TryGetValue(camel, out var camelized))
        {
            return camelized;
        }
        var pascal = Capitalize(camel);
        return !string.Equals(pascal, camel, StringComparison.Ordinal) && Components.TryGetValue(pascal, out var capitalized)
            ? capitalized
            : null;
    }

    private static string Capitalize(string name)
        => name.Length == 0 ? name : char.ToUpperInvariant(name[0]) + name[1..];
}
