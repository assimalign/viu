using Assimalign.Viu.Components;

namespace Assimalign.Viu.Router;

/// <summary>
/// Resolves the props passed to a matched route's component from the resolved location — the C# port
/// of the function form of vue-router's per-route <c>props</c> option
/// (<c>packages/router/src/RouterView.ts</c>, https://router.vuejs.org/guide/essentials/passing-props.html).
/// The boolean form (<c>props: true</c>, params as props) and the object form (static props) are the
/// resolvers produced by <see cref="RouteComponentArguments"/>; a hand-written delegate is the
/// function form, receiving the whole <see cref="RouteLocation"/>.
/// </summary>
/// <param name="route">The resolved location whose component is being rendered.</param>
/// <returns>The props for the component, or <see langword="null"/> for none.</returns>
public delegate IComponentArguments? RouteComponentArgumentsResolver(RouteLocation route);
