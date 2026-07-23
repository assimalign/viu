using System;

namespace Assimalign.Viu;

/// <summary>The default platform-neutral application shell.</summary>
public sealed class Application : IApplication
{
    /// <summary>Creates an application over an immutable context.</summary>
    /// <param name="context">The application composition context.</param>
    public Application(IApplicationContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        Context = context;
    }

    /// <inheritdoc/>
    public IApplicationContext Context { get; }
}

