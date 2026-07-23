namespace Assimalign.Viu.ServerRenderer;

/// <summary>
/// Composes a host-neutral server application from a root component tree and independently supplied
/// component, service, and state resolvers.
/// </summary>
public sealed class ServerApplicationBuilder : ApplicationBuilder
{
    /// <summary>Builds the configured server application.</summary>
    /// <returns>The configured application.</returns>
    public override ServerApplication Build()
    {
        ServerApplication application = new(CreateContext());
        ApplyConfiguration(application);
        return application;
    }
}
