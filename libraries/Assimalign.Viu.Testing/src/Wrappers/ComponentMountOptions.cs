using System;
using System.Collections.Generic;

using Assimalign.Viu;

namespace Assimalign.Viu.Testing;

/// <summary>
/// Options for <see cref="ViuTest.Mount{TComponent}(TComponent, ComponentMountOptions?)"/> — the
/// C# port of <c>@vue/test-utils</c>'s <c>mount(component, options)</c> options
/// (https://test-utils.vuejs.org/api/#mount): the props and slots to render the component under
/// test with, and the global configuration (app-level provides, name-registered components, and
/// child-component stubs). All fields are optional.
/// </summary>
public sealed class ComponentMountOptions
{
    /// <summary>The props passed to the mounted component (upstream: <c>props</c>).</summary>
    public VirtualNodeProperties? Properties { get; set; }

    /// <summary>The slot content passed to the mounted component (upstream: <c>slots</c>).</summary>
    public ComponentSlots? Slots { get; set; }

    /// <summary>
    /// App-level provides available to the whole tree (upstream: <c>global.provide</c>). Keyed by
    /// the same <see cref="InjectionKey{T}"/>/string identities <c>Inject</c> uses.
    /// </summary>
    public Dictionary<object, object?> Provides { get; } = [];

    /// <summary>
    /// Components registered by name for dynamic/name resolution (upstream: <c>global.components</c>).
    /// </summary>
    public Dictionary<string, IComponentDefinition> Components { get; } = new(StringComparer.Ordinal);

    /// <summary>
    /// Child-component stubs (upstream: <c>global.stubs</c>): each real definition maps to the stub
    /// mounted in its place, or null to use an auto-generated placeholder stub.
    /// </summary>
    public Dictionary<IComponentDefinition, IComponentDefinition?> Stubs { get; } = [];

    /// <summary>
    /// An optional hook to set app-level configuration (upstream: <c>global.config</c>) — for
    /// example an <see cref="ApplicationConfiguration.ErrorHandler"/> or
    /// <see cref="ApplicationConfiguration.WarnHandler"/>.
    /// </summary>
    public Action<ApplicationConfiguration>? ConfigureApplication { get; set; }

    /// <summary>Adds an app-level provide under a typed key (fluent).</summary>
    /// <typeparam name="T">The provided value type.</typeparam>
    /// <param name="key">The injection key.</param>
    /// <param name="value">The provided value.</param>
    /// <returns>These options, for chaining.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="key"/> is null.</exception>
    public ComponentMountOptions Provide<T>(InjectionKey<T> key, T value)
    {
        ArgumentNullException.ThrowIfNull(key);
        Provides[key] = value;
        return this;
    }

    /// <summary>Adds an app-level provide under a string key (fluent).</summary>
    /// <param name="key">The string key.</param>
    /// <param name="value">The provided value.</param>
    /// <returns>These options, for chaining.</returns>
    /// <exception cref="ArgumentException"><paramref name="key"/> is null or empty.</exception>
    public ComponentMountOptions Provide(string key, object? value)
    {
        ArgumentException.ThrowIfNullOrEmpty(key);
        Provides[key] = value;
        return this;
    }

    /// <summary>
    /// Stubs a child component (fluent): <paramref name="real"/> is replaced with
    /// <paramref name="stub"/>, or with an auto-generated placeholder when <paramref name="stub"/>
    /// is null.
    /// </summary>
    /// <param name="real">The real component to replace.</param>
    /// <param name="stub">The stub to use, or null for an auto placeholder.</param>
    /// <returns>These options, for chaining.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="real"/> is null.</exception>
    public ComponentMountOptions Stub(IComponentDefinition real, IComponentDefinition? stub = null)
    {
        ArgumentNullException.ThrowIfNull(real);
        Stubs[real] = stub;
        return this;
    }
}
