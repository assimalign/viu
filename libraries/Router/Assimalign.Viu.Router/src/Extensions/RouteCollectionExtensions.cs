using System;
using System.Collections.Generic;

using Assimalign.Viu.Components;

namespace Assimalign.Viu.Router;

/// <summary>Creates ordinary eager route records from component identities ([RTR-31]).</summary>
public static class RouteCollectionExtensions
{
    extension(ICollection<RouteRecord> routes)
    {
        /// <summary>
        /// Adds an eager named-component route. The component name is also the default route name;
        /// a supplied title replaces meta["title"] in a new metadata dictionary. Specified by <c>[RTR-31]</c>.
        /// </summary>
        /// <param name="path">The route path.</param>
        /// <param name="componentName">The registered component name.</param>
        /// <param name="title">Optional document title metadata.</param>
        /// <param name="children">Optional nested routes.</param>
        /// <param name="meta">Optional metadata copied without mutating the caller's dictionary.</param>
        /// <param name="argumentsResolver">Optional route argument resolver.</param>
        /// <param name="name">An explicit route name, defaulting to the component name.</param>
        /// <returns>The added record.</returns>
        public RouteRecord Add(string path, string componentName, string? title = null,
            IReadOnlyList<RouteRecord>? children = null, IReadOnlyDictionary<string, object?>? meta = null,
            RouteComponentArgumentsResolver? argumentsResolver = null, string? name = null)
        {
            ArgumentNullException.ThrowIfNull(routes);
            ArgumentException.ThrowIfNullOrEmpty(componentName);
            Dictionary<string, object?> metadata = new(StringComparer.Ordinal);
            if (meta is not null)
            {
                foreach (KeyValuePair<string, object?> entry in meta)
                {
                    metadata.Add(entry.Key, entry.Value);
                }
            }
            if (title is not null)
            {
                metadata["title"] = title;
            }
            RouteRecord record = new(path, name ?? componentName, children, metadata,
                new ComponentNode(ComponentReference.ForName(componentName)), argumentsResolver);
            routes.Add(record);
            return record;
        }

        /// <summary>
        /// Adds a route using a compile-time component definition without reflection. Its registered
        /// name is the default route name. Specified by <c>[RTR-31]</c>.
        /// </summary>
        /// <typeparam name="TComponent">The component definition with a registered-name reference.</typeparam>
        /// <param name="path">The route path.</param>
        /// <param name="title">Optional title metadata.</param>
        /// <param name="children">Optional nested routes.</param>
        /// <param name="meta">Optional copied metadata.</param>
        /// <param name="argumentsResolver">Optional route argument resolver.</param>
        /// <param name="name">Optional explicit route name.</param>
        /// <returns>The added record.</returns>
        public RouteRecord Add<TComponent>(string path, string? title = null,
            IReadOnlyList<RouteRecord>? children = null, IReadOnlyDictionary<string, object?>? meta = null,
            RouteComponentArgumentsResolver? argumentsResolver = null, string? name = null)
            where TComponent : IComponentDefinition
            => routes.Add(path, TComponent.Registration.Reference.RegisteredName
                ?? throw new ArgumentException("The component definition must use a registered name."),
                title, children, meta, argumentsResolver, name);
    }
}
