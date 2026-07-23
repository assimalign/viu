using System;
using System.Collections.Generic;

using Assimalign.Viu.Components;

namespace Assimalign.Viu;

/// <summary>
/// Preserves one dynamic template subtree across switches by moving inactive host nodes into
/// renderer-owned storage.
/// </summary>
/// <remarks>
/// This is Viu's host-generic port of Vue 3.5's <c>KeepAlive</c>:
/// https://github.com/vuejs/core/blob/v3.5.29/packages/runtime-core/src/components/KeepAlive.ts.
/// Each factory activation owns an independent cache. Component activation and application services
/// remain the responsibility of the application-supplied factory and service provider.
/// </remarks>
public sealed class KeepAlive : IComponentTemplate
{
    private static readonly IReadOnlyList<IComponentParameter> DeclaredParameters =
    [
        new ComponentParameter("include"),
        new ComponentParameter("exclude"),
        new ComponentParameter("max"),
    ];

    /// <inheritdoc/>
    public string? Name => "KeepAlive";

    /// <inheritdoc/>
    public ComponentFlags Flags => ComponentFlags.None;

    /// <inheritdoc/>
    public IReadOnlyList<IComponentParameter>? Parameters => DeclaredParameters;

    /// <summary>Gets the explicit AOT-safe registration for the built-in template.</summary>
    public static ComponentRegistration Registration =>
        new(
            typeof(KeepAlive),
            static () => new KeepAlive(),
            "KeepAlive");

    /// <summary>Creates a request for the built-in template.</summary>
    /// <param name="include">
    /// An optional comma-separated name, name sequence, or name predicate to cache.
    /// </param>
    /// <param name="exclude">
    /// An optional comma-separated name, name sequence, or name predicate not to cache.
    /// </param>
    /// <param name="maximum">The optional maximum number of cached templates.</param>
    /// <param name="child">The slot callback that produces the current child.</param>
    /// <param name="key">The optional identity of the KeepAlive wrapper itself.</param>
    /// <returns>The immutable KeepAlive template request.</returns>
    public static ITemplateComponent CreateComponent(
        object? include,
        object? exclude,
        object? maximum,
        ComponentSlot child,
        object? key = null)
    {
        ArgumentNullException.ThrowIfNull(child);
        List<KeyValuePair<string, object?>> arguments = [];
        if (include is not null)
        {
            arguments.Add(
                new KeyValuePair<string, object?>("include", include));
        }

        if (exclude is not null)
        {
            arguments.Add(
                new KeyValuePair<string, object?>("exclude", exclude));
        }

        if (maximum is not null)
        {
            arguments.Add(
                new KeyValuePair<string, object?>("max", maximum));
        }

        Dictionary<string, ComponentSlot> slots =
            new(StringComparer.Ordinal)
            {
                ["default"] = child,
            };
        return ComponentTree.Template<KeepAlive>(
            new ComponentArguments(arguments),
            slots,
            key);
    }

    /// <inheritdoc/>
    public ComponentRenderer Setup(IComponentContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        return () =>
        {
            if (!context.Slots.TryGetValue(
                "default",
                out ComponentSlot? slot))
            {
                return ComponentTree.Comment();
            }

            return slot(new ComponentArguments())
                ?? ComponentTree.Comment();
        };
    }

    internal bool ShouldCache(
        IComponentArguments arguments,
        string? componentName)
    {
        object? include = arguments["include"];
        object? exclude = arguments["exclude"];
        if (include is not null
            && (componentName is null || !Matches(include, componentName)))
        {
            return false;
        }

        return exclude is null
            || componentName is null
            || !Matches(exclude, componentName);
    }

    internal int Maximum(IComponentArguments arguments)
    {
        return arguments["max"] switch
        {
            int value => value,
            long value when value > int.MaxValue => int.MaxValue,
            long value when value < int.MinValue => int.MinValue,
            long value => (int)value,
            string text when int.TryParse(text, out int parsed) => parsed,
            _ => 0,
        };
    }

    private static bool Matches(object pattern, string name)
    {
        return pattern switch
        {
            string text => SplitContains(text, name),
            Func<string, bool> predicate => predicate(name),
            IEnumerable<string> names => AnyMatch(names, name),
            _ => false,
        };
    }

    private static bool SplitContains(string pattern, string name)
    {
        string[] segments = pattern.Split(',');
        for (int index = 0; index < segments.Length; index++)
        {
            if (string.Equals(
                segments[index],
                name,
                StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    private static bool AnyMatch(
        IEnumerable<string> patterns,
        string name)
    {
        foreach (string? pattern in patterns)
        {
            if (pattern is not null && SplitContains(pattern, name))
            {
                return true;
            }
        }

        return false;
    }
}
