using System;
using System.Collections.Generic;

using Assimalign.Viu;
using Assimalign.Viu.Components;
using Assimalign.Viu.State;

namespace Assimalign.Viu.Testing;

/// <summary>Options for mounting a component tree or authored template in the in-memory host.</summary>
/// <remarks>
/// Root arguments, slots, and listeners apply when mounting a supplied
/// <see cref="IComponentTemplate"/>. Application composition applies to both overloads.
/// </remarks>
public sealed class ComponentMountOptions
{
    /// <summary>Gets or sets the supplied root template's arguments.</summary>
    public IComponentArguments? Arguments { get; set; }

    /// <summary>Gets or sets the supplied root template's slots.</summary>
    public IReadOnlyDictionary<string, ComponentSlot>? Slots { get; set; }

    /// <summary>Gets or sets parent listeners supplied to the supplied root template request.</summary>
    public IReadOnlyDictionary<string, ComponentEventListener>? Listeners { get; set; }

    /// <summary>Gets or sets the application-selected component factory.</summary>
    public IComponentFactory? Components { get; set; }

    /// <summary>
    /// Gets child-template stubs keyed by requested template type. A null activator selects the
    /// generated placeholder stub.
    /// </summary>
    public Dictionary<Type, ComponentActivator?> Stubs { get; } = [];

    /// <summary>Gets or sets the standard application service provider.</summary>
    public IServiceProvider? Services { get; set; }

    /// <summary>Gets or sets the optional application state registry.</summary>
    public IStateStoreRegistry? State { get; set; }

    /// <summary>Gets or sets the optional application directive resolver.</summary>
    public IDirectiveResolver? Directives { get; set; }

    /// <summary>Gets or sets an optional application-context configuration callback.</summary>
    public Action<IApplicationContext>? ConfigureApplication { get; set; }

    /// <summary>
    /// Replaces child requests for <typeparamref name="TComponent"/> with an optional explicit stub
    /// activator.
    /// </summary>
    /// <typeparam name="TComponent">The child template type to replace.</typeparam>
    /// <param name="activator">
    /// A fresh-stub activator, or null to render an automatically named placeholder element.
    /// </param>
    /// <returns>These mount options.</returns>
    public ComponentMountOptions Stub<TComponent>(
        ComponentActivator? activator = null)
        where TComponent : class, IComponentTemplate
    {
        Stubs[typeof(TComponent)] = activator;
        return this;
    }
}
