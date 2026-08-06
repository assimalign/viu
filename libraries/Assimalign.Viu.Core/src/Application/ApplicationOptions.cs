using System;
using System.Collections.Generic;

using Assimalign.Viu.Components;

namespace Assimalign.Viu;

/// <summary>Configures diagnostics that are frozen into an application context at build time.</summary>
/// <remarks>
/// A builder snapshots these values for each built application; later option mutations do not alter
/// an existing context. Not thread-safe. Specified by <c>[APP-2]</c>.
/// </remarks>
public sealed class ApplicationOptions
{
    /// <summary>
    /// Gets or sets the terminal handler for render, lifecycle, watcher, and event errors that no
    /// component error-capture hook stopped.
    /// </summary>
    public Action<Exception, IComponentContext?, string>? ErrorHandler { get; set; }

    /// <summary>Gets or sets the application warning handler.</summary>
    public Action<string>? WarnHandler { get; set; }

    internal Action<IComponentContext, string, IReadOnlyList<object?>>? EventObserver { get; set; }
}
