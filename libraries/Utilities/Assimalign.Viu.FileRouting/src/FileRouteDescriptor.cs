using System;
using System.Collections.Generic;

namespace Assimalign.Viu.FileRouting;

/// <summary>
/// Describes an eager route produced by the standalone folder-routing generator, without activating
/// its registered component. Child paths retain the ordinary joining rules of <c>[RTR-1]</c>.
/// </summary>
/// <remarks>
/// Instances snapshot their children and contain only immutable declarative data. The component name
/// identifies an explicit registration under <c>[CMP-6]</c>, never a runtime type lookup.
/// </remarks>
public sealed class FileRouteDescriptor
{
    /// <summary>
    /// Creates an immutable description whose values are passed unchanged to an eager route record.
    /// Parameter forwarding uses the argument contract of <c>[RTR-4]</c>.
    /// </summary>
    /// <param name="path">An absolute root path, relative child path, or empty default-child path.</param>
    /// <param name="name">The nonempty, ordinal route name chosen by the generator or caller.</param>
    /// <param name="componentName">The nonempty name of an explicitly registered component.</param>
    /// <param name="forwardParameters">Whether all resolved route parameters become same-named arguments.</param>
    /// <param name="children">Child descriptors, snapshotted in their declared order, or null for none.</param>
    /// <exception cref="ArgumentNullException">A required value or a child descriptor is null.</exception>
    /// <exception cref="ArgumentException">The route name or component name is empty.</exception>
    public FileRouteDescriptor(
        string path,
        string name,
        string componentName,
        bool forwardParameters = false,
        IReadOnlyList<FileRouteDescriptor>? children = null)
    {
        ArgumentNullException.ThrowIfNull(path);
        ArgumentException.ThrowIfNullOrEmpty(name);
        ArgumentException.ThrowIfNullOrEmpty(componentName);

        Path = path;
        Name = name;
        ComponentName = componentName;
        ForwardParameters = forwardParameters;

        if (children is null || children.Count == 0)
        {
            Children = Array.Empty<FileRouteDescriptor>();
            return;
        }

        FileRouteDescriptor[] snapshot = new FileRouteDescriptor[children.Count];
        for (int index = 0; index < children.Count; index++)
        {
            FileRouteDescriptor child = children[index];
            ArgumentNullException.ThrowIfNull(child, nameof(children));
            snapshot[index] = child;
        }

        Children = Array.AsReadOnly(snapshot);
    }

    /// <summary>Gets the declared path, before parent joining under <c>[RTR-1]</c>.</summary>
    public string Path { get; }

    /// <summary>Gets the ordinal name used for ordinary named resolution under <c>[RTR-1]</c>.</summary>
    public string Name { get; }

    /// <summary>Gets the explicit registration name used without activation under <c>[CMP-6]</c>.</summary>
    public string ComponentName { get; }

    /// <summary>Gets whether the route forwards all resolved parameters under <c>[RTR-4]</c>.</summary>
    public bool ForwardParameters { get; }

    /// <summary>Gets the immutable, ordered child snapshot preserving nesting under <c>[RTR-1]</c>.</summary>
    public IReadOnlyList<FileRouteDescriptor> Children { get; }
}
