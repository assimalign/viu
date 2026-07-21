using Assimalign.Viu;

namespace Assimalign.Viu.Router;

/// <summary>
/// The provide/inject keys wiring the router into the component tree — the C# counterparts of
/// vue-router's injection symbols (<c>packages/router/src/injectionSymbols.ts</c>). A host installs
/// the router by providing <see cref="Router"/> app-wide (<c>app.Provide(RouterInjectionKeys.Router,
/// router)</c> or the test harness's <c>global.provide</c>); <see cref="RouterView"/> and
/// <see cref="RouterLink"/> inject it. <see cref="ViewDepth"/> is provided by each
/// <see cref="RouterView"/> for its nested views and is an internal implementation detail.
/// </summary>
public static class RouterInjectionKeys
{
    /// <summary>
    /// The key the router instance is provided under (upstream: <c>routerKey</c>). Provide it app-wide
    /// so every <see cref="RouterView"/>/<see cref="RouterLink"/> resolves the same router.
    /// </summary>
    public static readonly InjectionKey<Router> Router = new("viu-router");

    /// <summary>
    /// The key carrying a <see cref="RouterView"/>'s nesting depth to its descendant views (upstream:
    /// <c>viewDepthKey</c>). Each view injects the depth (default 0), renders the matched record at
    /// that depth, and provides depth + 1 for the next view down.
    /// </summary>
    internal static readonly InjectionKey<int> ViewDepth = new("viu-router-view-depth");

    /// <summary>
    /// The key carrying the matched <see cref="RouteRecord"/> a <see cref="RouterView"/> renders to the
    /// component it renders (upstream: <c>matchedRouteKey</c>). The in-component guard composables
    /// (<see cref="RouterGuards.OnBeforeRouteLeave"/>/<see cref="RouterGuards.OnBeforeRouteUpdate"/>)
    /// inject it to bind a guard to its record; an implementation detail carried by a mutable holder.
    /// </summary>
    internal static readonly InjectionKey<MatchedRecordScope> MatchedRecord = new("viu-router-matched-record");
}
