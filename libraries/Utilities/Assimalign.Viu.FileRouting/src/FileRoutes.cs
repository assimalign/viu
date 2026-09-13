using System;
using System.Collections.Generic;

using Assimalign.Viu.Components;
using Assimalign.Viu.Router;

namespace Assimalign.Viu.FileRouting;

/// <summary>
/// Converts declarative file-route descriptions into ordinary eager route records under
/// <c>[RTR-1]</c> and <c>[RTR-4]</c>, without introducing routing or activation semantics.
/// </summary>
public static class FileRoutes
{
    /// <summary>
    /// Builds an immutable list of fresh eager route records, preserving paths, names, order, and
    /// nesting. Components remain non-activating registered-name requests under <c>[CMP-7]</c>.
    /// </summary>
    /// <remarks>
    /// A true forwarding flag supplies <see cref="RouteComponentArguments.FromParameters"/>;
    /// otherwise no argument resolver is assigned. No lazy factory or assembly loader is created
    /// (<c>[RTR-8]</c>, <c>[RTR-11]</c>). Path validation and ranking remain the matcher's job.
    /// </remarks>
    /// <param name="descriptors">The ordered, non-null root descriptors.</param>
    /// <returns>Fresh route records with the same descriptor tree and no activated components.</returns>
    /// <exception cref="ArgumentNullException">The list or one of its descriptors is null.</exception>
    public static IReadOnlyList<RouteRecord> Create(IReadOnlyList<FileRouteDescriptor> descriptors)
    {
        ArgumentNullException.ThrowIfNull(descriptors);
        if (descriptors.Count == 0)
        {
            return Array.Empty<RouteRecord>();
        }

        RouteRecord[] records = new RouteRecord[descriptors.Count];
        for (int index = 0; index < descriptors.Count; index++)
        {
            FileRouteDescriptor descriptor = descriptors[index];
            ArgumentNullException.ThrowIfNull(descriptor, nameof(descriptors));
            records[index] = new RouteRecord(
                descriptor.Path,
                name: descriptor.Name,
                children: Create(descriptor.Children),
                component: new ComponentNode(ComponentReference.ForName(descriptor.ComponentName)),
                argumentsResolver: descriptor.ForwardParameters
                    ? RouteComponentArguments.FromParameters()
                    : null);
        }

        return Array.AsReadOnly(records);
    }
}
