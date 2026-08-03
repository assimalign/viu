using System;
using System.Collections.Generic;

using Assimalign.Viu.Components;

namespace Assimalign.Viu.Router;

/// <summary>
/// Builds the <see cref="RouteComponentArgumentsResolver"/>s for the two declarative ways a route
/// record supplies arguments to its component: <see cref="FromParameters"/> forwards the resolved
/// route parameters as same-named arguments, and <see cref="FromValues"/> supplies a fixed set of
/// arguments independent of the route. Anything else is a hand-written
/// <see cref="RouteComponentArgumentsResolver"/> that reads the <see cref="RouteLocation"/> directly.
/// </summary>
public static class RouteComponentArguments
{
    /// <summary>
    /// Each resolved route parameter is passed to the component as an argument of the same name
    /// (its string value; a repeatable parameter joins its segments with <c>/</c>). A route with no
    /// parameters resolves to no arguments.
    /// </summary>
    /// <returns>A resolver mapping <see cref="RouteLocation.Parameters"/> to an argument bag.</returns>
    public static RouteComponentArgumentsResolver FromParameters()
        => static route => ParametersToProperties(route.Parameters);

    /// <summary>
    /// The same fixed arguments are passed to the component on every resolution, independent of the
    /// route. The returned resolver hands back a single shared bag by reference, so a component with
    /// fixed arguments never re-renders because its arguments changed.
    /// </summary>
    /// <param name="entries">The fixed argument name/value pairs.</param>
    /// <returns>A resolver returning the fixed arguments.</returns>
    public static RouteComponentArgumentsResolver FromValues(params (string Name, object? Value)[] entries)
    {
        ArgumentNullException.ThrowIfNull(entries);
        List<KeyValuePair<string, object?>> values = new(entries.Length);
        foreach ((string name, object? value) in entries)
        {
            values.Add(new KeyValuePair<string, object?>(name, value));
        }

        ComponentArguments arguments = new(values);
        return _ => arguments;
    }

    private static IComponentArguments? ParametersToProperties(RouteParameters parameters)
    {
        if (parameters.Count == 0)
        {
            return null;
        }

        List<KeyValuePair<string, object?>> values = new(parameters.Count);
        foreach (var name in parameters.Names)
        {
            values.Add(new KeyValuePair<string, object?>(
                name,
                parameters.GetString(name)));
        }

        return new ComponentArguments(values);
    }
}
