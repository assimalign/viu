using System;
using System.Collections.Generic;

using Assimalign.Viu;
using Assimalign.Viu.Components;
using Assimalign.Viu.State;

namespace Assimalign.Viu.Testing;

/// <summary>Configures one DOM-free component or virtual-tree mount.</summary>
/// <remarks>
/// All composed dependencies remain caller-owned. Specified by <c>[APP-2]</c>, <c>[APP-6]</c>,
/// <c>[CMP-9]</c>, and <c>[CONF-3]</c>.
/// </remarks>
public sealed class ComponentMountOptions
{
    /// <summary>Gets or sets raw arguments for an authored root component invocation.</summary>
    public IReadOnlyDictionary<string, object?>? Arguments { get; set; }

    /// <summary>Gets or sets raw slots for an authored root component invocation.</summary>
    public IReadOnlyDictionary<string, ComponentSlot>? Slots { get; set; }

    /// <summary>Gets or sets parent listeners for an authored root component invocation.</summary>
    public IReadOnlyDictionary<string, ComponentEventListener>? Listeners { get; set; }

    /// <summary>Gets or sets directives attached to an authored root component invocation.</summary>
    public IReadOnlyList<DirectiveInvocation>? RootDirectives { get; set; }

    /// <summary>Gets or sets the root slot-set stability classification.</summary>
    public SlotStability SlotStability { get; set; } = SlotStability.Stable;

    /// <summary>Gets or sets the static contract used when mounting a supplied component instance.</summary>
    public ComponentContract? RootContract { get; set; }

    /// <summary>Gets or sets the application-selected component factory for descendant requests.</summary>
    public IComponentFactory? Components { get; set; }

    /// <summary>Gets child-component stubs keyed by requested authored type.</summary>
    public Dictionary<Type, ComponentActivator?> Stubs { get; } = [];

    /// <summary>Gets or sets the caller-owned application service provider.</summary>
    public IServiceProvider? Services { get; set; }

    /// <summary>Gets or sets the caller-owned state registry.</summary>
    public IStateStoreRegistry? State { get; set; }

    /// <summary>Gets or sets the caller-owned directive resolver.</summary>
    public IDirectiveResolver? Directives { get; set; }

    /// <summary>Gets or sets optional application configuration applied before the mount is frozen.</summary>
    public Action<ApplicationOptions>? ConfigureApplication { get; set; }

    /// <summary>Replaces descendant requests for a component type with a test stub.</summary>
    /// <typeparam name="TComponent">The descendant authored type.</typeparam>
    /// <param name="activator">A fresh-stub activator, or null for a generated placeholder.</param>
    /// <returns>These options for fluent composition.</returns>
    public ComponentMountOptions Stub<TComponent>(ComponentActivator? activator = null)
        where TComponent : class, IComponent
    {
        Stubs[typeof(TComponent)] = activator;
        return this;
    }
}
