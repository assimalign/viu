using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace Assimalign.Viu.Router;

/// <summary>
/// Builds the <see cref="RouteComponentArgumentsResolver"/>s for the two declarative ways a route
/// record supplies arguments to its component: <see cref="FromParameters"/> forwards the resolved
/// route parameters as same-named arguments, and <see cref="FromValues"/> supplies a fixed set of
/// arguments independent of the route. Anything else is a hand-written
/// <see cref="RouteComponentArgumentsResolver"/> that reads the <see cref="RouteLocation"/> directly.
/// </summary>
/// <remarks>Specified by <c>[RTR-4]</c>.</remarks>
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
    /// route. The returned resolver exposes one immutable shared dictionary; each component
    /// invocation snapshots that dictionary before mount or update, so callers cannot mutate the
    /// route definition's fixed values.
    /// </summary>
    /// <param name="entries">The fixed argument name/value pairs.</param>
    /// <returns>A resolver returning the fixed arguments.</returns>
    public static RouteComponentArgumentsResolver FromValues(params (string Name, object? Value)[] entries)
    {
        ArgumentNullException.ThrowIfNull(entries);
        Dictionary<string, object?> values = new(entries.Length, StringComparer.Ordinal);
        foreach ((string name, object? value) in entries)
        {
            ArgumentException.ThrowIfNullOrEmpty(name);
            values.Add(name, value);
        }

        IReadOnlyDictionary<string, object?> arguments =
            new ReadOnlyDictionary<string, object?>(values);
        return _ => arguments;
    }

    private static IReadOnlyDictionary<string, object?>? ParametersToProperties(
        RouteParameters parameters)
    {
        if (parameters.Count == 0)
        {
            return null;
        }

        Dictionary<string, object?> values = new(parameters.Count, StringComparer.Ordinal);
        foreach (string name in parameters.Names)
        {
            values.Add(name, parameters.GetString(name));
        }

        return new ReadOnlyDictionary<string, object?>(values);
    }
}
