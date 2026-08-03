using Assimalign.Viu.Components;

namespace Assimalign.Viu.Router;

/// <summary>
/// Resolves the arguments passed to a matched route's component from the resolved location. The two
/// declarative forms — route parameters as arguments, and a fixed argument set — are produced by
/// <see cref="RouteComponentArguments"/>; a hand-written delegate is the general form, receiving the
/// whole <see cref="RouteLocation"/> and returning whatever the component needs.
/// </summary>
/// <param name="route">The resolved location whose component is being rendered.</param>
/// <returns>The arguments for the component, or <see langword="null"/> for none.</returns>
public delegate IComponentArguments? RouteComponentArgumentsResolver(RouteLocation route);
