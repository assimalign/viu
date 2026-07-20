using Assimalign.Viu.RuntimeCore;

namespace Assimalign.Viu.Router;

/// <summary>
/// Builds the <see cref="RouteComponentPropertiesResolver"/>s for the two declarative forms of
/// vue-router's per-route <c>props</c> option (https://router.vuejs.org/guide/essentials/passing-props.html):
/// <see cref="FromParameters"/> is the <c>props: true</c> form (the resolved params become props) and
/// <see cref="FromValues"/> is the object form (static props independent of the route). The function
/// form is a hand-written <see cref="RouteComponentPropertiesResolver"/> that reads the
/// <see cref="RouteLocation"/> directly.
/// </summary>
public static class RouteComponentProperties
{
    /// <summary>
    /// The <c>props: true</c> form — each resolved route parameter is passed to the component as a
    /// prop of the same name (its string value; a repeatable parameter joins with <c>/</c>). A route
    /// with no parameters resolves to no props.
    /// </summary>
    /// <returns>A resolver mapping <see cref="RouteLocation.Parameters"/> to a props bag.</returns>
    public static RouteComponentPropertiesResolver FromParameters()
        => static route => ParametersToProperties(route.Parameters);

    /// <summary>
    /// The object form — the same static props are passed to the component on every resolution,
    /// independent of the route (upstream: <c>props: { … }</c>). The returned resolver hands back a
    /// single shared bag, so a component with static props is never re-rendered for a prop change.
    /// </summary>
    /// <param name="entries">The static prop name/value pairs.</param>
    /// <returns>A resolver returning the static props.</returns>
    public static RouteComponentPropertiesResolver FromValues(params (string Name, object? Value)[] entries)
    {
        var properties = VirtualNodeFactory.Properties(entries);
        return _ => properties;
    }

    private static VirtualNodeProperties? ParametersToProperties(RouteParameters parameters)
    {
        if (parameters.Count == 0)
        {
            return null;
        }
        var properties = new VirtualNodeProperties(parameters.Count);
        foreach (var name in parameters.Names)
        {
            properties.Set(name, parameters.GetString(name));
        }
        return properties;
    }
}
