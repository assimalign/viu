using Assimalign.Viu.Components;
using Assimalign.Viu.State;

namespace Assimalign.Viu;

/// <summary>
/// Composes a Viu application from a root component, a supplied component factory/service provider,
/// and an optional state registry.
/// </summary>
public interface IApplicationBuilder
{
    /// <summary>Sets the root value in the component tree.</summary>
    /// <param name="component">The root component.</param>
    /// <returns>This builder.</returns>
    IApplicationBuilder UseRootComponent(IComponent component);

    /// <summary>Sets the combined component activator and dependency resolver.</summary>
    /// <param name="components">The application component factory.</param>
    /// <returns>This builder.</returns>
    IApplicationBuilder UseComponentFactory(IComponentFactory components);

    /// <summary>Sets the optional application state registry.</summary>
    /// <param name="state">The application state registry.</param>
    /// <returns>This builder.</returns>
    IApplicationBuilder UseStateRegistry(IStateStoreRegistry state);

    /// <summary>Builds the configured application.</summary>
    /// <returns>The application.</returns>
    IApplication Build();
}

